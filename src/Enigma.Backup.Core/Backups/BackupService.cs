using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Archiving;
using Enigma.Backup.Core.Errors;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Core.Scanning;

namespace Enigma.Backup.Core.Backups;

/// <summary>
/// The streaming <see cref="IBackupService"/>: <c>scan → tar → compress → file</c> in a single pass.
/// </summary>
/// <remarks>
/// <para>
/// Nothing is staged on disk and nothing is buffered whole, so a 2 GB backup and a 2 TB backup use the
/// same amount of memory. The SHA-256 sidecar comes for free: the digest is computed on the bytes as
/// they are written, so the finished archive never has to be read back.
/// </para>
/// <para>
/// The layers are built inside-out — file, digest, compression, tar — and disposed in that same
/// order, because the compressor's epilogue has to reach the file before it closes. PHASE04's
/// encrypting stream slots in between the digest and the compression, which leaves the digest
/// covering the bytes that are actually on disk.
/// </para>
/// <para>
/// If anything goes wrong — a failure, an Abort, a cancellation — the partial archive is deleted:
/// a half-written backup that looks like a real one is worse than no backup at all.
/// </para>
/// </remarks>
public sealed class BackupService : IBackupService
{
    private const int BufferSize = 128 * 1024;

    private readonly IInputScanner _scanner;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates a backup service.</summary>
    /// <param name="scanner">Turns the profile's inputs into the entries to write.</param>
    /// <param name="timeProvider">Supplies the run's timestamp and measures its duration.</param>
    public BackupService(IInputScanner scanner, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(scanner);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _scanner = scanner;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<BackupResult> RunAsync(
        BackupProfile profile,
        string? password,
        IBackupErrorHandler errorHandler,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(errorHandler);

        if (string.IsNullOrWhiteSpace(profile.OutputDirectory))
        {
            throw new ArgumentException("The profile has no output directory.", nameof(profile));
        }

        if (profile.Encrypt)
        {
            throw new NotSupportedException(
                "Encrypted archives are not supported by this build yet: the EBK1 container arrives with PHASE04.");
        }

        long startedAt = _timeProvider.GetTimestamp();
        BackupProgressReporter reporter = new(progress);

        // One policy for the whole run, shared by the scan and the write, so "apply to all" answered
        // during the scan still holds while the archive is being written.
        ErrorDecisionPolicy policy = new(errorHandler);

        reporter.ReportScanning();
        ScanResult scan = await _scanner.ScanAsync(profile, policy, cancellationToken).ConfigureAwait(false);
        reporter.SetTotals(scan.FileCount, scan.TotalBytes);

        string outputDirectory = Path.GetFullPath(profile.OutputDirectory);
        Directory.CreateDirectory(outputDirectory);

        string outputPath = ArchiveNaming.ResolveFreePath(
            outputDirectory,
            ArchiveNaming.BuildStem(profile.Name, _timeProvider.GetLocalNow()),
            ArchiveNaming.BuildExtension(profile.Compression, profile.Encrypt));

        List<SkippedEntry> skipped = [.. scan.Skipped];
        List<BackupWarning> warnings = [];
        string hashHex;
        string sidecarPath;

        try
        {
            hashHex = await WriteArchiveAsync(scan, profile, outputPath, reporter, policy, skipped, warnings, cancellationToken)
                .ConfigureAwait(false);
            sidecarPath = await Sha256Sidecar.WriteAsync(outputPath, hashHex, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // The streams are already disposed by the time this runs, so the file can be removed on
            // Windows too.
            TryDelete(outputPath);
            TryDelete(Sha256Sidecar.GetPath(outputPath));
            throw;
        }

        reporter.ReportCompleted();

        return new BackupResult
        {
            OutputPath = outputPath,
            SidecarPath = sidecarPath,
            Sha256 = hashHex,
            FileCount = reporter.FilesProcessed,
            DirectoryCount = scan.DirectoryCount,
            BytesProcessed = reporter.BytesProcessed,
            ArchiveBytes = new FileInfo(outputPath).Length,
            Duration = _timeProvider.GetElapsedTime(startedAt),
            Skipped = skipped,
            ScanWarnings = scan.Warnings,
            Warnings = warnings,
        };
    }

    /// <summary>Writes the whole archive and returns its SHA-256 digest as lowercase hex.</summary>
    private static async Task<string> WriteArchiveAsync(
        ScanResult scan,
        BackupProfile profile,
        string outputPath,
        BackupProgressReporter reporter,
        ErrorDecisionPolicy policy,
        List<SkippedEntry> skipped,
        List<BackupWarning> warnings,
        CancellationToken cancellationToken)
    {
        FileStreamOptions output = new()
        {
            // CreateNew rather than Create: the name was resolved to a free one, and refusing to
            // overwrite is the safer failure if that ever stops being true.
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous,
            BufferSize = BufferSize,
        };

        await using FileStream file = new(outputPath, output);
        await using HashingWriteStream hashing = new(file);

        await using (Stream compressor = CompressionStreams.CreateCompressor(hashing, profile.Compression))
        {
            await using TarWriter tar = new(compressor, TarEntryFormat.Pax, leaveOpen: true);

            foreach (ScanEntry entry in scan.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (entry.Kind == ScanEntryKind.Directory)
                {
                    await WriteDirectoryEntryAsync(tar, entry, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await WriteFileEntryAsync(tar, entry, reporter, policy, skipped, warnings, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        // The compressor has flushed its epilogue by now, so the digest covers the complete file.
        await hashing.FlushAsync(cancellationToken).ConfigureAwait(false);

        return hashing.GetHashHex();
    }

    private static async Task WriteDirectoryEntryAsync(TarWriter tar, ScanEntry entry, CancellationToken cancellationToken)
    {
        // The trailing slash is what every other tar implementation writes for a directory.
        PaxTarEntry tarEntry = new(TarEntryType.Directory, entry.ArchivePath + '/')
        {
            ModificationTime = entry.LastWriteTime,
        };

        if (UnixPermissions.Read(entry.SourcePath) is UnixFileMode mode)
        {
            tarEntry.Mode = mode;
        }

        await tar.WriteEntryAsync(tarEntry, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteFileEntryAsync(
        TarWriter tar,
        ScanEntry entry,
        BackupProgressReporter reporter,
        ErrorDecisionPolicy policy,
        List<SkippedEntry> skipped,
        List<BackupWarning> warnings,
        CancellationToken cancellationToken)
    {
        FileStream? source = await OpenSourceAsync(entry, policy, skipped, cancellationToken).ConfigureAwait(false);
        if (source is null)
        {
            return;
        }

        await using (source)
        {
            // The length is taken now, not from the scan, so a file that changed in between is still
            // archived correctly.
            long length = source.Length;
            reporter.StartFile(entry.SourcePath);

            using FixedLengthReadStream data = new(source, length, reporter.AddBytes);

            PaxTarEntry tarEntry = new(TarEntryType.RegularFile, entry.ArchivePath)
            {
                ModificationTime = entry.LastWriteTime,
                DataStream = data,
            };

            if (UnixPermissions.Read(entry.SourcePath) is UnixFileMode mode)
            {
                tarEntry.Mode = mode;
            }

            try
            {
                await tar.WriteEntryAsync(tarEntry, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // The entry's header is already in the stream, so this one cannot be skipped: the
                // archive is compromised and the run has to end. The partial file is deleted.
                throw new IOException($"'{entry.SourcePath}' became unreadable while it was being archived: {exception.Message}", exception);
            }

            if (data.SourcePadded)
            {
                warnings.Add(new BackupWarning(
                    BackupWarningKind.SourceShrankWhileReading,
                    entry.SourcePath,
                    $"'{entry.SourcePath}' shrank while it was being archived; the entry was padded to the size it declared."));
            }
            else if (source.Length > length)
            {
                warnings.Add(new BackupWarning(
                    BackupWarningKind.SourceGrewWhileReading,
                    entry.SourcePath,
                    $"'{entry.SourcePath}' grew while it was being archived; the archive holds the first {length} bytes."));
            }

            reporter.CompleteFile();
        }
    }

    /// <summary>Opens a source file, routing a refusal through the error handler until it is answered.</summary>
    private static async Task<FileStream?> OpenSourceAsync(
        ScanEntry entry,
        ErrorDecisionPolicy policy,
        List<SkippedEntry> skipped,
        CancellationToken cancellationToken)
    {
        FileStreamOptions options = new()
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            // A file another program is writing to must still be backed up, so its own lock is not
            // demanded here; the entry is size-stable either way.
            Share = FileShare.ReadWrite | FileShare.Delete,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            BufferSize = BufferSize,
        };

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return new FileStream(entry.SourcePath, options);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                BackupErrorKind kind = exception switch
                {
                    FileNotFoundException or DirectoryNotFoundException => BackupErrorKind.MissingInput,
                    UnauthorizedAccessException => BackupErrorKind.AccessDenied,
                    _ => BackupErrorKind.ReadFailed,
                };

                BackupError error = new(
                    kind,
                    entry.SourcePath,
                    $"'{entry.SourcePath}' could not be read: {exception.Message}",
                    exception);

                BackupErrorDecision decision = await policy.HandleAsync(error, cancellationToken).ConfigureAwait(false);

                switch (decision.Action)
                {
                    case BackupErrorAction.Retry:
                        continue;
                    case BackupErrorAction.Abort:
                        throw new BackupAbortedException(error);
                    default:
                        skipped.Add(new SkippedEntry(entry.SourcePath, error.Message));
                        return null;
                }
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Nothing more can be done about it here, and the original failure matters more.
        }
    }
}
