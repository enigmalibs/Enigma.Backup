using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Errors;
using Enigma.Backup.Core.Profiles;

namespace Enigma.Backup.Core.Scanning;

/// <summary>
/// The file-system <see cref="IInputScanner"/>.
/// </summary>
/// <remarks>
/// <para>The rules it applies, in the order they take effect:</para>
/// <list type="bullet">
/// <item><description>Every input is canonicalized; blank entries are ignored.</description></item>
/// <item><description>
/// Overlapping inputs are merged: an input that is another input, or lies inside one, is dropped —
/// whichever of the two is the broader path wins, so an input is never archived twice.
/// </description></item>
/// <item><description>The output directory and everything inside it is left out, so a backup can never contain itself.</description></item>
/// <item><description>An input that no longer exists goes to the error handler (Skip / Retry / Abort), as does a directory that cannot be read.</description></item>
/// <item><description>Directory symbolic links and junctions are never followed, wherever they appear; each is reported as a warning.</description></item>
/// <item><description>Exclude globs are matched against each entry's path relative to the input it was found under.</description></item>
/// <item><description>Top-level names are made portable and given a numeric suffix when two inputs would collide.</description></item>
/// </list>
/// <para>Hidden files are included: leaving out a dotfile or a hidden folder would quietly lose data.</para>
/// </remarks>
public sealed class InputScanner : IInputScanner
{
    /// <inheritdoc />
    public async Task<ScanResult> ScanAsync(BackupProfile profile, IBackupErrorHandler errorHandler, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(errorHandler);

        ScanState state = new()
        {
            Policy = new ErrorDecisionPolicy(errorHandler),
            Excluder = GlobExcluder.Create(profile.ExcludeGlobs),
            OutputDirectory = NormalizeOutputDirectory(profile.OutputDirectory),
        };

        foreach (string root in ResolveRoots(profile.Inputs, state.Warnings))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ScanRootAsync(root, state, cancellationToken).ConfigureAwait(false);
        }

        return new ScanResult(state.Entries, state.Warnings, state.Skipped);
    }

    private static string? NormalizeOutputDirectory(string outputDirectory) =>
        !string.IsNullOrWhiteSpace(outputDirectory) && PathUtilities.TryNormalize(outputDirectory, out string normalized)
            ? normalized
            : null;

    /// <summary>
    /// Canonicalizes the profile's inputs and collapses the ones that overlap, keeping the order the
    /// user put them in so top-level names stay predictable.
    /// </summary>
    private static List<string> ResolveRoots(IList<string> inputs, List<ScanWarning> warnings)
    {
        List<string> roots = [];

        foreach (string input in inputs)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            // A path that cannot be canonicalized here (malformed for this platform) is kept as-is
            // and surfaces as a missing input, which routes it through the error handler.
            _ = PathUtilities.TryNormalize(input, out string candidate);

            if (TryFindContainer(roots, candidate, out string container))
            {
                warnings.Add(PathUtilities.AreEqual(candidate, container)
                    ? new ScanWarning(ScanWarningKind.DuplicateInput, candidate, $"'{candidate}' was listed more than once; the extra entry was ignored.")
                    : new ScanWarning(ScanWarningKind.NestedInputMerged, candidate, $"'{candidate}' lies inside '{container}' and was merged into it."));
                continue;
            }

            // The new input is the broader path: whatever it already contains folds into it.
            for (int index = roots.Count - 1; index >= 0; index--)
            {
                if (PathUtilities.IsSameOrUnder(roots[index], candidate))
                {
                    warnings.Add(new ScanWarning(
                        ScanWarningKind.NestedInputMerged,
                        roots[index],
                        $"'{roots[index]}' lies inside '{candidate}' and was merged into it."));
                    roots.RemoveAt(index);
                }
            }

            roots.Add(candidate);
        }

        return roots;
    }

    private static bool TryFindContainer(List<string> roots, string candidate, out string container)
    {
        foreach (string root in roots)
        {
            if (PathUtilities.IsSameOrUnder(candidate, root))
            {
                container = root;
                return true;
            }
        }

        container = string.Empty;
        return false;
    }

    private static async Task ScanRootAsync(string root, ScanState state, CancellationToken cancellationToken)
    {
        FileSystemInfo? info = await ResolveRootAsync(root, state, cancellationToken).ConfigureAwait(false);
        if (info is null)
        {
            return;
        }

        if (state.OutputDirectory is not null && PathUtilities.IsSameOrUnder(root, state.OutputDirectory))
        {
            state.Warnings.Add(new ScanWarning(
                ScanWarningKind.OutputDirectoryExcluded,
                root,
                $"'{root}' is inside the output directory and was left out, so the backup cannot contain itself."));
            return;
        }

        if (info is DirectoryInfo && info.LinkTarget is not null)
        {
            state.Warnings.Add(SymbolicLinkWarning(info));
            return;
        }

        string relativePath = info is DirectoryInfo ? string.Empty : info.Name;
        if (relativePath.Length > 0 && state.Excluder.IsExcluded(relativePath))
        {
            return;
        }

        string name = AllocateRootName(root, state);

        if (info is DirectoryInfo directory)
        {
            await ScanDirectoryAsync(directory, name, state, cancellationToken).ConfigureAwait(false);
        }
        else if (info is FileInfo file)
        {
            state.Entries.Add(new ScanEntry(ScanEntryKind.File, file.FullName, name, file.Length, file.LastWriteTimeUtc));
        }
    }

    /// <summary>Waits until the input exists, the user skips it, or the run is aborted.</summary>
    private static async Task<FileSystemInfo?> ResolveRootAsync(string root, ScanState state, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Directory.Exists(root))
            {
                return new DirectoryInfo(root);
            }

            if (File.Exists(root))
            {
                return new FileInfo(root);
            }

            BackupError error = new(BackupErrorKind.MissingInput, root, $"The input path '{root}' does not exist.");
            BackupErrorDecision decision = await state.Policy.HandleAsync(error, cancellationToken).ConfigureAwait(false);

            switch (decision.Action)
            {
                case BackupErrorAction.Retry:
                    continue;
                case BackupErrorAction.Abort:
                    throw new BackupAbortedException(error);
                default:
                    state.Skipped.Add(new SkippedEntry(root, error.Message));
                    return null;
            }
        }
    }

    /// <summary>
    /// Walks a directory input depth-first, emitting a directory before everything found inside it
    /// so the archive can be written straight through.
    /// </summary>
    private static async Task ScanDirectoryAsync(DirectoryInfo root, string rootName, ScanState state, CancellationToken cancellationToken)
    {
        Stack<PendingDirectory> pending = new();
        pending.Push(new PendingDirectory(root, rootName, string.Empty));

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PendingDirectory current = pending.Pop();
            state.Entries.Add(new ScanEntry(
                ScanEntryKind.Directory,
                current.Info.FullName,
                current.ArchivePath,
                0,
                current.Info.LastWriteTimeUtc));

            FileSystemInfo[]? children = await GetChildrenAsync(current.Info, state, cancellationToken).ConfigureAwait(false);
            if (children is null)
            {
                continue;
            }

            Array.Sort(children, static (left, right) => string.CompareOrdinal(left.Name, right.Name));

            List<PendingDirectory> subdirectories = [];
            foreach (FileSystemInfo child in children)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string relativePath = current.RelativePath.Length == 0 ? child.Name : current.RelativePath + '/' + child.Name;
                if (state.Excluder.IsExcluded(relativePath))
                {
                    continue;
                }

                string archivePath = current.ArchivePath + '/' + child.Name;

                if (child is DirectoryInfo directory)
                {
                    if (directory.LinkTarget is not null)
                    {
                        state.Warnings.Add(SymbolicLinkWarning(directory));
                        continue;
                    }

                    if (state.OutputDirectory is not null && PathUtilities.IsSameOrUnder(directory.FullName, state.OutputDirectory))
                    {
                        state.Warnings.Add(new ScanWarning(
                            ScanWarningKind.OutputDirectoryExcluded,
                            directory.FullName,
                            $"'{directory.FullName}' is the backup output directory and was left out, so the backup cannot contain itself."));
                        continue;
                    }

                    subdirectories.Add(new PendingDirectory(directory, archivePath, relativePath));
                }
                else if (child is FileInfo file)
                {
                    state.Entries.Add(new ScanEntry(ScanEntryKind.File, file.FullName, archivePath, file.Length, file.LastWriteTimeUtc));
                }
            }

            // Pushed in reverse so the stack pops them back in name order.
            for (int index = subdirectories.Count - 1; index >= 0; index--)
            {
                pending.Push(subdirectories[index]);
            }
        }
    }

    /// <summary>Reads one directory's children, routing a refusal through the error handler.</summary>
    private static async Task<FileSystemInfo[]?> GetChildrenAsync(DirectoryInfo directory, ScanState state, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return directory.GetFileSystemInfos();
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                BackupErrorKind kind = exception is UnauthorizedAccessException ? BackupErrorKind.AccessDenied : BackupErrorKind.ReadFailed;
                BackupError error = new(
                    kind,
                    directory.FullName,
                    $"The directory '{directory.FullName}' could not be read: {exception.Message}",
                    exception);

                BackupErrorDecision decision = await state.Policy.HandleAsync(error, cancellationToken).ConfigureAwait(false);

                switch (decision.Action)
                {
                    case BackupErrorAction.Retry:
                        directory.Refresh();
                        continue;
                    case BackupErrorAction.Abort:
                        throw new BackupAbortedException(error);
                    default:
                        state.Skipped.Add(new SkippedEntry(directory.FullName, error.Message));
                        return null;
                }
            }
        }
    }

    /// <summary>Gives an input its top-level archive name, portable and unique within the archive.</summary>
    private static string AllocateRootName(string root, ScanState state)
    {
        string baseName = PathUtilities.DeriveRootName(root, out bool sanitized);
        if (sanitized)
        {
            state.Warnings.Add(new ScanWarning(
                ScanWarningKind.RootNameSanitized,
                root,
                $"'{root}' is stored in the archive as '{baseName}' so it can be restored on Windows too."));
        }

        // The number goes before the extension, so two "config.json" inputs become "config.json"
        // and "config_2.json" rather than "config.json_2".
        string stem = Path.GetFileNameWithoutExtension(baseName);
        string extension = Path.GetExtension(baseName);

        string name = baseName;
        for (int suffix = 2; !state.RootNames.Add(name); suffix++)
        {
            name = $"{stem}_{suffix}{extension}";

            if (suffix == 2)
            {
                state.Warnings.Add(new ScanWarning(
                    ScanWarningKind.RootNameDisambiguated,
                    root,
                    $"Another input is already stored as '{baseName}', so '{root}' is stored alongside it under a numbered name."));
            }
        }

        return name;
    }

    private static ScanWarning SymbolicLinkWarning(FileSystemInfo directory) =>
        new(ScanWarningKind.SymbolicLinkSkipped,
            directory.FullName,
            $"'{directory.FullName}' is a link to '{directory.LinkTarget}' and was not followed.");

    /// <summary>A directory queued for the walk, with the paths it contributes to entries below it.</summary>
    private readonly record struct PendingDirectory(DirectoryInfo Info, string ArchivePath, string RelativePath);

    /// <summary>Everything one scan accumulates; the scanner itself stays stateless and shareable.</summary>
    private sealed class ScanState
    {
        public required ErrorDecisionPolicy Policy { get; init; }

        public required GlobExcluder Excluder { get; init; }

        public required string? OutputDirectory { get; init; }

        public List<ScanEntry> Entries { get; } = [];

        public List<ScanWarning> Warnings { get; } = [];

        public List<SkippedEntry> Skipped { get; } = [];

        public HashSet<string> RootNames { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
