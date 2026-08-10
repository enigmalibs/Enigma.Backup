using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Archiving;
using Enigma.Backup.Core.Encryption;
using Enigma.Backup.Core.Errors;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Core.Scanning;

namespace Enigma.Backup.Core.Backups;

/// <summary>
/// The streaming <see cref="IBackupService"/>: <c>scan → tar → compress → file</c> in a single pass, or
/// <c>scan → zip → file</c> when the profile asks for a plain <c>.zip</c>.
/// </summary>
/// <remarks>
/// <para>
/// Nothing is staged on disk and nothing is buffered whole, so a 2 GB backup and a 2 TB backup use the
/// same amount of memory. The SHA-256 sidecar comes for free: the digest is computed on the bytes as
/// they are written, so the finished archive never has to be read back.
/// </para>
/// <para>
/// The layers are built inside-out — file, digest, encryption, compression, tar — and disposed in
/// that same order, because each layer's epilogue has to reach the file before the one below it
/// closes. The digest sits closest to the file, so it covers the bytes that are actually on disk,
/// encrypted ones included: a <c>.sha256</c> sidecar can be checked without the password.
/// </para>
/// <para>
/// Which archive the entries go into is the only thing the format changes — an
/// <c>IArchiveEntryWriter</c> either side of the same scan, the same error handler and the same
/// progress. A zip is never encrypted, and a profile asking for both is refused here before the output
/// file is even named.
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
    private readonly Ebk1Options _encryption;

    /// <summary>Creates a backup service.</summary>
    /// <param name="scanner">Turns the profile's inputs into the entries to write.</param>
    /// <param name="timeProvider">Supplies the run's timestamp and measures its duration.</param>
    /// <param name="encryption">The parameters new encrypted containers are written with.</param>
    public BackupService(IInputScanner scanner, TimeProvider timeProvider, Ebk1Options encryption)
    {
        ArgumentNullException.ThrowIfNull(scanner);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(encryption);

        _scanner = scanner;
        _timeProvider = timeProvider;
        _encryption = encryption;
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

        if (profile.Encrypt && profile.Compression == CompressionAlgorithm.Zip)
        {
            // Checked before the password, so a hand-edited profile is told what is actually wrong with
            // it rather than that a password is missing. The window never offers the combination.
            throw new ProfileFormatException(
                $"Profile '{profile.Name}' asks for an encrypted zip archive, which cannot be written: a zip is never " +
                "encrypted. Choose Zstandard or Gzip to encrypt the archive, or turn encryption off to keep the zip.");
        }

        if (profile.Encrypt && string.IsNullOrEmpty(password))
        {
            // Empty rather than whitespace: a password of spaces is a password, and refusing it here
            // would make an archive nobody could restore.
            throw new ArgumentException("The profile encrypts its archives, but no password was supplied.", nameof(password));
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
            hashHex = await WriteArchiveAsync(scan, profile, password, outputPath, reporter, policy, skipped, warnings, cancellationToken)
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
    private async Task<string> WriteArchiveAsync(
        ScanResult scan,
        BackupProfile profile,
        string? password,
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

        // Null when the profile does not encrypt, in which case the compressor writes straight to the
        // digest. `await using` on a null is a no-op, so the two cases need no separate code path.
        Ebk1EncryptStream? encryptor = profile.Encrypt
            ? await Ebk1EncryptStream.CreateAsync(hashing, password!, _encryption, leaveOpen: true, cancellationToken).ConfigureAwait(false)
            : null;

        await using (encryptor)
        {
            await using (IArchiveEntryWriter writer = CreateEntryWriter(profile.Compression, encryptor ?? (Stream)hashing))
            {
                foreach (ScanEntry entry in scan.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (entry.Kind == ScanEntryKind.Directory)
                    {
                        await writer.WriteDirectoryAsync(entry, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await WriteFileEntryAsync(writer, entry, reporter, policy, skipped, warnings, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        // Every layer has flushed its epilogue by now — the compressor's, and the container's
        // end-of-stream marker — so the digest covers the complete file.
        await hashing.FlushAsync(cancellationToken).ConfigureAwait(false);

        return hashing.GetHashHex();
    }

    /// <summary>Chooses what the entries are written into: a compressed tar stream, or a zip container.</summary>
    /// <param name="compression">What the profile asked for.</param>
    /// <param name="destination">The stream the archive is written to; the writer does not own it.</param>
    private static IArchiveEntryWriter CreateEntryWriter(CompressionAlgorithm compression, Stream destination) =>
        compression == CompressionAlgorithm.Zip
            ? new ZipEntryWriter(destination)
            : new TarEntryWriter(destination, compression);

    private static async Task WriteFileEntryAsync(
        IArchiveEntryWriter writer,
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

            try
            {
                await writer.WriteFileAsync(entry, data, cancellationToken).ConfigureAwait(false);
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
