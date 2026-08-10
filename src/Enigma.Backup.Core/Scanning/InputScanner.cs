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
/// <item><description>
/// Pattern inputs are resolved after the path inputs, each on its own: the base folder is walked like
/// a folder input and its glob decides what comes along. They never merge — with each other or with a
/// path input — because a pattern is a rule, not a place, so a base folder that is also a path input
/// is simply a second top-level name.
/// </description></item>
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

        foreach (PatternInput patternInput in profile.PatternInputs ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ScanPatternInputAsync(patternInput, state, cancellationToken).ConfigureAwait(false);
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
                    if (!CanDescendInto(directory, state))
                    {
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

    /// <summary>
    /// Resolves one pattern input: the base folder is walked like a folder input, and the glob decides
    /// what of it is archived.
    /// </summary>
    private static async Task ScanPatternInputAsync(PatternInput patternInput, ScanState state, CancellationToken cancellationToken)
    {
        if (patternInput is null)
        {
            return;
        }

        string pattern = patternInput.Pattern ?? string.Empty;

        // As for a path input, a base folder that cannot be canonicalized here is kept as-is and
        // surfaces as a missing one, which routes it through the error handler.
        _ = PathUtilities.TryNormalize(patternInput.BaseDirectory ?? string.Empty, out string baseDirectory);

        DirectoryInfo? root = await ResolvePatternBaseAsync(baseDirectory, pattern, state, cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        if (state.OutputDirectory is not null && PathUtilities.IsSameOrUnder(baseDirectory, state.OutputDirectory))
        {
            state.Warnings.Add(new ScanWarning(
                ScanWarningKind.OutputDirectoryExcluded,
                baseDirectory,
                $"'{baseDirectory}' is inside the output directory and was left out, so the backup cannot contain itself."));
            return;
        }

        if (root.LinkTarget is not null)
        {
            state.Warnings.Add(SymbolicLinkWarning(root));
            return;
        }

        string rootName = AllocateRootName(baseDirectory, state);
        int matched = await ScanPatternTreeAsync(root, rootName, PatternMatcher.Create(pattern), state, cancellationToken).ConfigureAwait(false);

        if (matched == 0)
        {
            state.Warnings.Add(new ScanWarning(
                ScanWarningKind.PatternMatchedNothing,
                baseDirectory,
                $"Nothing under '{baseDirectory}' matched the pattern '{pattern}'."));
        }
    }

    /// <summary>Waits until a pattern's base folder is there, the user skips it, or the run is aborted.</summary>
    private static async Task<DirectoryInfo?> ResolvePatternBaseAsync(
        string baseDirectory,
        string pattern,
        ScanState state,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Directory.Exists(baseDirectory))
            {
                return new DirectoryInfo(baseDirectory);
            }

            // A pattern is resolved against a folder, so a base that is blank, is a file, or is simply
            // not there is the same kind of failure as a missing input — and gets the same answer.
            string message = baseDirectory.Length == 0
                ? $"The pattern '{pattern}' has no base folder."
                : File.Exists(baseDirectory)
                    ? $"The base folder '{baseDirectory}' for the pattern '{pattern}' is a file, not a folder."
                    : $"The base folder '{baseDirectory}' for the pattern '{pattern}' does not exist.";

            BackupError error = new(BackupErrorKind.MissingInput, baseDirectory, message);
            BackupErrorDecision decision = await state.Policy.HandleAsync(error, cancellationToken).ConfigureAwait(false);

            switch (decision.Action)
            {
                case BackupErrorAction.Retry:
                    continue;
                case BackupErrorAction.Abort:
                    throw new BackupAbortedException(error);
                default:
                    state.Skipped.Add(new SkippedEntry(baseDirectory, error.Message));
                    return null;
            }
        }
    }

    /// <summary>
    /// Walks a pattern's base folder and adds what its glob selects: every matching file, every
    /// matching directory with its whole subtree, and the directories on the way to a match so the
    /// archive keeps its shape. A directory with nothing matching below it never appears.
    /// </summary>
    /// <returns>How many entries the pattern selected, not counting the base folder itself.</returns>
    /// <remarks>
    /// Whether a directory is on the way to a match is only known once its subtree has been walked, so
    /// the walk records candidates and then works the selection back up the parent links — the entries
    /// are still handed on in walk order, a directory before everything inside it.
    /// </remarks>
    private static async Task<int> ScanPatternTreeAsync(
        DirectoryInfo root,
        string rootName,
        PatternMatcher matcher,
        ScanState state,
        CancellationToken cancellationToken)
    {
        // The base folder itself is always recorded, so the archive says where the pattern looked even
        // when it found nothing.
        state.Entries.Add(new ScanEntry(ScanEntryKind.Directory, root.FullName, rootName, 0, root.LastWriteTimeUtc));

        if (!matcher.MatchesAnything)
        {
            return 0;
        }

        List<PatternCandidate> candidates = [];
        Stack<PendingPatternDirectory> pending = new();
        pending.Push(new PendingPatternDirectory(root, rootName, string.Empty, Parent: -1, IncludesEverything: false));

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PendingPatternDirectory current = pending.Pop();
            int parent = current.Parent;

            // The base folder is already an entry; every directory below it becomes a candidate, kept
            // only if it matched itself or something under it did.
            if (current.RelativePath.Length > 0)
            {
                candidates.Add(new PatternCandidate(
                    new ScanEntry(ScanEntryKind.Directory, current.Info.FullName, current.ArchivePath, 0, current.Info.LastWriteTimeUtc),
                    current.Parent)
                {
                    IsSelected = current.IncludesEverything,
                });

                parent = candidates.Count - 1;
            }

            FileSystemInfo[]? children = await GetChildrenAsync(current.Info, state, cancellationToken).ConfigureAwait(false);
            if (children is null)
            {
                continue;
            }

            Array.Sort(children, static (left, right) => string.CompareOrdinal(left.Name, right.Name));

            List<PendingPatternDirectory> subdirectories = [];
            foreach (FileSystemInfo child in children)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string relativePath = current.RelativePath.Length == 0 ? child.Name : current.RelativePath + '/' + child.Name;

                // Excludes win over a pattern match: they are the last word for a path input too.
                if (state.Excluder.IsExcluded(relativePath))
                {
                    continue;
                }

                string archivePath = current.ArchivePath + '/' + child.Name;
                bool selected = current.IncludesEverything || matcher.Matches(relativePath);

                if (child is DirectoryInfo directory)
                {
                    if (!CanDescendInto(directory, state))
                    {
                        continue;
                    }

                    subdirectories.Add(new PendingPatternDirectory(directory, archivePath, relativePath, parent, selected));
                }
                else if (child is FileInfo file && selected)
                {
                    candidates.Add(new PatternCandidate(
                        new ScanEntry(ScanEntryKind.File, file.FullName, archivePath, file.Length, file.LastWriteTimeUtc),
                        parent)
                    {
                        IsSelected = true,
                    });
                }
            }

            // Pushed in reverse so the stack pops them back in name order.
            for (int index = subdirectories.Count - 1; index >= 0; index--)
            {
                pending.Push(subdirectories[index]);
            }
        }

        // A parent is always recorded before its children, so one pass from the end carries every
        // selection up to the base folder.
        for (int index = candidates.Count - 1; index >= 0; index--)
        {
            if (candidates[index] is { IsSelected: true, Parent: >= 0 } selected)
            {
                candidates[selected.Parent].IsSelected = true;
            }
        }

        int count = 0;
        foreach (PatternCandidate candidate in candidates)
        {
            if (candidate.IsSelected)
            {
                state.Entries.Add(candidate.Entry);
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Whether a child directory may be walked into: a link is never followed, and the output
    /// directory is never archived. Both say so rather than disappearing quietly.
    /// </summary>
    private static bool CanDescendInto(DirectoryInfo directory, ScanState state)
    {
        if (directory.LinkTarget is not null)
        {
            state.Warnings.Add(SymbolicLinkWarning(directory));

            return false;
        }

        if (state.OutputDirectory is not null && PathUtilities.IsSameOrUnder(directory.FullName, state.OutputDirectory))
        {
            state.Warnings.Add(new ScanWarning(
                ScanWarningKind.OutputDirectoryExcluded,
                directory.FullName,
                $"'{directory.FullName}' is the backup output directory and was left out, so the backup cannot contain itself."));

            return false;
        }

        return true;
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

    /// <summary>A directory queued for a pattern's walk.</summary>
    /// <param name="Info">The directory itself.</param>
    /// <param name="ArchivePath">The path it would take in the archive.</param>
    /// <param name="RelativePath">Its path relative to the base folder, which the glob is matched against.</param>
    /// <param name="Parent">The index of the candidate holding it, or <c>-1</c> under the base folder.</param>
    /// <param name="IncludesEverything">
    /// Whether it is inside something the pattern already matched, in which case everything in it comes
    /// along without being matched itself.
    /// </param>
    private readonly record struct PendingPatternDirectory(
        DirectoryInfo Info,
        string ArchivePath,
        string RelativePath,
        int Parent,
        bool IncludesEverything);

    /// <summary>One entry a pattern's walk considered, with the link back to the directory holding it.</summary>
    /// <param name="Entry">The entry as it would be archived.</param>
    /// <param name="Parent">The index of the candidate holding it, or <c>-1</c> under the base folder.</param>
    private sealed record PatternCandidate(ScanEntry Entry, int Parent)
    {
        /// <summary>Whether the entry is archived: it matched the pattern, or something below it did.</summary>
        public bool IsSelected { get; set; }
    }

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
