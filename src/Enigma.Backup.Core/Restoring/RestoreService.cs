using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Archiving;
using Enigma.Backup.Core.Encryption;
using Enigma.Backup.Core.Errors;

namespace Enigma.Backup.Core.Restoring;

/// <summary>
/// The streaming <see cref="IRestoreService"/>: <c>file → [decrypt] → decompress → untar →
/// destination</c>, one entry at a time, in the order they were written.
/// </summary>
/// <remarks>
/// <para>
/// What the archive is, is decided by its leading bytes rather than its name, so a renamed archive
/// still restores and a file that only looks like one is refused before anything is written. An
/// encrypted archive says which compression it holds in the same way, once it has been opened.
/// </para>
/// <para>
/// Nothing is written to disk before it has been authenticated: a chunk of an encrypted archive is
/// only handed to the decompressor after its GCM tag has verified, so a tampered archive fails before
/// its contents reach the destination.
/// </para>
/// <para>
/// Entry names are checked before they are used: an absolute path, a <c>..</c> segment or a hidden
/// separator would let an archive write outside the directory the user picked, so such an archive is
/// rejected outright.
/// </para>
/// <para>
/// Directory timestamps and modes are applied last, deepest first — writing a file into a directory
/// updates that directory's timestamp, and a read-only directory cannot be written into at all.
/// </para>
/// </remarks>
public sealed class RestoreService : IRestoreService
{
    private const int BufferSize = 128 * 1024;
    private const long ReportInterval = 1024 * 1024;

    private readonly TimeProvider _timeProvider;

    /// <summary>Creates a restore service.</summary>
    /// <param name="timeProvider">Measures how long a restore took.</param>
    public RestoreService(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<RestoreResult> RestoreAsync(
        string archivePath,
        string destinationDirectory,
        string? password,
        IRestoreConflictHandler conflictHandler,
        IProgress<RestoreProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        ArgumentNullException.ThrowIfNull(conflictHandler);

        long startedAt = _timeProvider.GetTimestamp();

        // Checked up front so a missing archive reads the same whether or not its folder is still
        // there, instead of surfacing as a DirectoryNotFoundException the user cannot act on.
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException($"The archive '{archivePath}' does not exist.", archivePath);
        }

        string destinationRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destinationDirectory));
        Directory.CreateDirectory(destinationRoot);

        RestoreState state = new()
        {
            DestinationRoot = destinationRoot,
            Policy = new RestoreConflictPolicy(conflictHandler),
            Progress = progress,
        };

        FileStreamOptions options = new()
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            BufferSize = BufferSize,
        };

        await using (FileStream archive = new(archivePath, options))
        {
            state.ArchiveBytes = archive.Length;

            ArchiveLayer layer = await ArchiveFormatDetector.DetectAsync(archive, cancellationToken).ConfigureAwait(false);

            await using CountingReadStream counting = new(archive);
            state.Counting = counting;

            Ebk1DecryptStream? decryptor = null;
            Stream? payload = null;

            try
            {
                if (layer == ArchiveLayer.Ebk1)
                {
                    if (string.IsNullOrEmpty(password))
                    {
                        throw new EncryptedArchiveException(
                            EncryptedArchiveProblem.PasswordRequired,
                            $"'{Path.GetFileName(archivePath)}' is encrypted, so its password is needed to restore it.");
                    }

                    decryptor = await Ebk1DecryptStream.CreateAsync(counting, password, leaveOpen: true, cancellationToken).ConfigureAwait(false);
                    (layer, payload) = await ArchiveFormatDetector.DetectForwardOnlyAsync(decryptor, cancellationToken).ConfigureAwait(false);

                    if (layer == ArchiveLayer.Ebk1)
                    {
                        throw new ArchiveFormatException(
                            $"'{Path.GetFileName(archivePath)}' holds another encrypted container instead of a compressed archive, which this application does not produce.");
                    }
                }

                await using Stream decompressor = CompressionStreams.CreateDecompressor(payload ?? (Stream)counting, layer);

                try
                {
                    await ReadEntriesAsync(decompressor, state, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (CompressionStreams.IsCorruptData(exception))
                {
                    // A file that was cut short, or whose bytes rotted, fails somewhere inside the
                    // decompressor or the tar reader. Whatever had already been restored stays.
                    throw new ArchiveFormatException(
                        $"'{Path.GetFileName(archivePath)}' is damaged or incomplete and could not be read to the end: {exception.Message}",
                        exception);
                }
            }
            finally
            {
                if (payload is not null)
                {
                    await payload.DisposeAsync().ConfigureAwait(false);
                }

                if (decryptor is not null)
                {
                    await decryptor.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        ApplyDirectoryMetadata(state.Directories);
        state.CurrentPath = null;
        state.Report();

        return new RestoreResult
        {
            ArchivePath = archivePath,
            DestinationDirectory = destinationRoot,
            Outcome = state.Outcome,
            FileCount = state.FileCount,
            DirectoryCount = state.Directories.Count,
            BytesRestored = state.BytesRestored,
            Duration = _timeProvider.GetElapsedTime(startedAt),
            Skipped = state.Skipped,
        };
    }

    private static async Task ReadEntriesAsync(Stream decompressed, RestoreState state, CancellationToken cancellationToken)
    {
        await using TarReader reader = new(decompressed, leaveOpen: true);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TarEntry? entry = await reader.GetNextEntryAsync(copyData: false, cancellationToken).ConfigureAwait(false);

            if (entry is null)
            {
                return;
            }

            if (!ArchiveEntryPath.TryResolve(state.DestinationRoot, entry.Name, out string destinationPath))
            {
                throw new ArchiveFormatException(
                    $"The archive contains the entry '{entry.Name}', which does not resolve to a path inside the destination directory. Nothing further was restored.");
            }

            switch (entry.EntryType)
            {
                case TarEntryType.Directory:
                    RestoreDirectory(entry, destinationPath, state);
                    break;

                case TarEntryType.RegularFile:
                case TarEntryType.V7RegularFile:
                case TarEntryType.ContiguousFile:
                    await RestoreFileAsync(entry, destinationPath, state, cancellationToken).ConfigureAwait(false);
                    break;

                default:
                    // Only files and directories are ever written by this application; a link or a
                    // device node comes from a foreign archive and is reported rather than guessed at.
                    state.Skipped.Add(new SkippedEntry(
                        entry.Name,
                        $"'{entry.Name}' is a {entry.EntryType} entry, which is not restored."));
                    break;
            }

            if (state.Outcome == RestoreOutcome.Aborted)
            {
                return;
            }
        }
    }

    private static void RestoreDirectory(TarEntry entry, string destinationPath, RestoreState state)
    {
        Directory.CreateDirectory(destinationPath);
        state.Directories.Add(new PendingDirectory(destinationPath, entry.ModificationTime, entry.Mode));
    }

    private static async Task RestoreFileAsync(TarEntry entry, string destinationPath, RestoreState state, CancellationToken cancellationToken)
    {
        if (Directory.Exists(destinationPath))
        {
            state.Skipped.Add(new SkippedEntry(
                entry.Name,
                $"'{destinationPath}' is a directory, so the file '{entry.Name}' was not restored over it."));
            return;
        }

        if (File.Exists(destinationPath) && !await ResolveConflictAsync(entry, destinationPath, state, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        string? parent = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        state.CurrentPath = entry.Name;
        state.Report();

        FileStreamOptions options = new()
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous,
            BufferSize = BufferSize,
        };

        await using (FileStream target = new(destinationPath, options))
        {
            if (entry.DataStream is Stream data)
            {
                await CopyAsync(data, target, state, cancellationToken).ConfigureAwait(false);
            }
        }

        File.SetLastWriteTimeUtc(destinationPath, entry.ModificationTime.UtcDateTime);
        UnixPermissions.Apply(destinationPath, entry.Mode);

        state.FileCount++;
        state.Report();
    }

    /// <summary>Asks about a file that is already there.</summary>
    /// <returns><see langword="true"/> when the entry should be written anyway.</returns>
    private static async Task<bool> ResolveConflictAsync(TarEntry entry, string destinationPath, RestoreState state, CancellationToken cancellationToken)
    {
        FileInfo existing = new(destinationPath);
        RestoreConflict conflict = new(
            entry.Name,
            destinationPath,
            entry.Length,
            entry.ModificationTime,
            existing.Length,
            existing.LastWriteTimeUtc);

        RestoreConflictDecision decision = await state.Policy.HandleAsync(conflict, cancellationToken).ConfigureAwait(false);

        switch (decision.Action)
        {
            case RestoreConflictAction.Overwrite:
                return true;

            case RestoreConflictAction.Abort:
                state.Outcome = RestoreOutcome.Aborted;
                return false;

            default:
                state.Skipped.Add(new SkippedEntry(destinationPath, $"'{destinationPath}' already exists and was left as it is."));
                return false;
        }
    }

    /// <summary>Copies one entry's data across, reporting progress as it goes.</summary>
    private static async Task CopyAsync(Stream source, Stream target, RestoreState state, CancellationToken cancellationToken)
    {
        byte[] buffer = state.Buffer;
        long sinceReport = 0;

        while (true)
        {
            int read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return;
            }

            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);

            state.BytesRestored += read;
            sinceReport += read;

            if (sinceReport >= ReportInterval)
            {
                sinceReport = 0;
                state.Report();
            }
        }
    }

    /// <summary>
    /// Stamps the restored directories with their archived timestamp and mode, deepest first.
    /// </summary>
    private static void ApplyDirectoryMetadata(List<PendingDirectory> directories)
    {
        directories.Sort(static (left, right) => right.Path.Length.CompareTo(left.Path.Length));

        foreach (PendingDirectory directory in directories)
        {
            if (!Directory.Exists(directory.Path))
            {
                continue;
            }

            try
            {
                Directory.SetLastWriteTimeUtc(directory.Path, directory.ModificationTime.UtcDateTime);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A restored file is worth more than a restored timestamp.
            }

            // After the timestamp: a mode without write permission would block the call above.
            UnixPermissions.Apply(directory.Path, directory.Mode);
        }
    }

    /// <summary>A directory whose metadata is applied once everything inside it has been written.</summary>
    private readonly record struct PendingDirectory(string Path, DateTimeOffset ModificationTime, UnixFileMode Mode);

    /// <summary>Everything one restore accumulates; the service itself stays stateless and shareable.</summary>
    private sealed class RestoreState
    {
        public required string DestinationRoot { get; init; }

        public required RestoreConflictPolicy Policy { get; init; }

        public required IProgress<RestoreProgress>? Progress { get; init; }

        public CountingReadStream? Counting { get; set; }

        public long ArchiveBytes { get; set; }

        public RestoreOutcome Outcome { get; set; } = RestoreOutcome.Completed;

        public int FileCount { get; set; }

        public long BytesRestored { get; set; }

        public string? CurrentPath { get; set; }

        public byte[] Buffer { get; } = new byte[BufferSize];

        public List<PendingDirectory> Directories { get; } = [];

        public List<SkippedEntry> Skipped { get; } = [];

        public void Report()
        {
            Progress?.Report(new RestoreProgress(CurrentPath, FileCount, BytesRestored, Counting?.BytesRead ?? 0, ArchiveBytes));
        }
    }
}
