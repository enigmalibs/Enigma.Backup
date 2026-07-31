using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Archiving;
using Enigma.Backup.Core.Encryption;

namespace Enigma.Backup.Core.Verification;

/// <summary>
/// The <see cref="IVerificationService"/>: re-reads the archive, recomputes its SHA-256 and compares
/// it with what the backup recorded — and, for an encrypted archive, opens every chunk to prove the
/// payload authenticates as well.
/// </summary>
/// <remarks>
/// <para>
/// The digest catches what a backup is actually at risk of — bit rot, a truncated copy, a bad transfer
/// to another disk — because it was taken from the bytes that were written, so any difference in the
/// bytes that are read shows up.
/// </para>
/// <para>
/// For an encrypted archive both halves come out of a single read of the file: the container is
/// decrypted <em>through</em> the hashing stream, so the disk is not read twice. The two halves answer
/// different questions, and the messages say which — a file whose digest matches but whose chunks do
/// not open has not been damaged, the password is simply wrong.
/// </para>
/// <para>
/// A missing or unreadable sidecar is reported as an outcome rather than thrown: an archive whose
/// sidecar was lost is a normal situation the user has to be told about, not a program error.
/// </para>
/// </remarks>
public sealed class VerificationService : IVerificationService
{
    private const int BufferSize = 1024 * 1024;

    /// <summary>How much has to be read before another progress report is worth making.</summary>
    private const long ReportInterval = 1024 * 1024;

    private readonly TimeProvider _timeProvider;

    /// <summary>Creates a verification service.</summary>
    /// <param name="timeProvider">Measures how long a verification took.</param>
    public VerificationService(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<VerificationResult> VerifyAsync(
        string archivePath,
        string? password = null,
        IProgress<VerificationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);

        long startedAt = _timeProvider.GetTimestamp();
        string sidecarPath = Sha256Sidecar.GetPath(archivePath);
        string name = Path.GetFileName(archivePath);

        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException($"The archive '{archivePath}' does not exist.", archivePath);
        }

        if (!File.Exists(sidecarPath))
        {
            return Build(
                archivePath,
                sidecarPath,
                VerificationOutcome.SidecarMissing,
                expected: null,
                actual: null,
                bytesRead: 0,
                _timeProvider.GetElapsedTime(startedAt),
                $"There is no checksum file beside '{name}', so it cannot be verified.");
        }

        string contents = await File.ReadAllTextAsync(sidecarPath, cancellationToken).ConfigureAwait(false);
        if (!Sha256Sidecar.TryParse(contents, out string expected))
        {
            return Build(
                archivePath,
                sidecarPath,
                VerificationOutcome.SidecarUnreadable,
                expected: null,
                actual: null,
                bytesRead: 0,
                _timeProvider.GetElapsedTime(startedAt),
                $"The checksum file '{Path.GetFileName(sidecarPath)}' does not contain a SHA-256 digest.");
        }

        ArchiveReading reading = await ReadArchiveAsync(archivePath, password, progress, cancellationToken).ConfigureAwait(false);
        bool matches = string.Equals(expected, reading.HashHex, StringComparison.OrdinalIgnoreCase);

        (VerificationOutcome outcome, string message) = Judge(name, matches, reading);

        return Build(
            archivePath,
            sidecarPath,
            outcome,
            expected,
            reading.HashHex,
            reading.BytesRead,
            _timeProvider.GetElapsedTime(startedAt),
            message,
            reading.IsEncrypted,
            reading.Authenticated);
    }

    /// <summary>Turns what the read found into an outcome and the sentence that explains it.</summary>
    private static (VerificationOutcome Outcome, string Message) Judge(string name, bool matches, ArchiveReading reading)
    {
        if (!matches)
        {
            return (
                VerificationOutcome.HashMismatch,
                $"'{name}' does not match its checksum: the file has changed since it was written.");
        }

        if (reading.IsEncrypted && reading.Authenticated is null)
        {
            return (
                VerificationOutcome.PasswordRequired,
                $"'{name}' matches its checksum, so the file itself is unchanged — but it is encrypted, and its contents cannot be checked without the password.");
        }

        if (reading.Authenticated == false)
        {
            // The digest already proved the file is byte-for-byte what the backup wrote, so the
            // container's own "wrong password or damaged" collapses to just the one possibility.
            string detail = reading.Problem?.Problem == EncryptedArchiveProblem.WrongPasswordOrCorrupted
                ? "which means the password is wrong."
                : $"but its contents could not be read: {reading.Problem?.Message}";

            return (
                VerificationOutcome.AuthenticationFailed,
                $"'{name}' matches its checksum, so the file has not changed since it was written, {detail}");
        }

        return (
            VerificationOutcome.Succeeded,
            reading.IsEncrypted
                ? $"'{name}' matches its checksum and every encrypted block passed its authentication check: the archive is intact and opens with this password."
                : $"'{name}' matches its checksum: the archive is intact.");
    }

    /// <summary>
    /// Reads the archive once: the digest always, and — for an encrypted archive with a password —
    /// every chunk opened and authenticated on the way past.
    /// </summary>
    private static async Task<ArchiveReading> ReadArchiveAsync(
        string archivePath,
        string? password,
        IProgress<VerificationProgress>? progress,
        CancellationToken cancellationToken)
    {
        FileStreamOptions options = new()
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            BufferSize = BufferSize,
        };

        await using FileStream archive = new(archivePath, options);

        long total = archive.Length;
        long lastReported = 0;
        progress?.Report(new VerificationProgress(0, total));

        bool encrypted = await IsEncryptedAsync(archive, cancellationToken).ConfigureAwait(false);

        await using HashingReadStream hashing = new(
            archive,
            read =>
            {
                if (progress is not null && read - lastReported >= ReportInterval)
                {
                    lastReported = read;
                    progress.Report(new VerificationProgress(read, total));
                }
            });

        bool? authenticated = null;
        EncryptedArchiveException? problem = null;

        if (encrypted && !string.IsNullOrEmpty(password))
        {
            try
            {
                await using Ebk1DecryptStream decryptor =
                    await Ebk1DecryptStream.CreateAsync(hashing, password, leaveOpen: true, cancellationToken).ConfigureAwait(false);

                await decryptor.CopyToAsync(Stream.Null, cancellationToken).ConfigureAwait(false);
                authenticated = true;
            }
            catch (EncryptedArchiveException exception)
            {
                authenticated = false;
                problem = exception;
            }
        }

        // Whatever the container did or did not consume, the rest of the file still has to go through
        // the digest — the checksum is about the file, not about the payload inside it.
        await hashing.CopyToAsync(Stream.Null, BufferSize, cancellationToken).ConfigureAwait(false);
        progress?.Report(new VerificationProgress(hashing.BytesRead, total));

        return new ArchiveReading(hashing.GetHashHex(), hashing.BytesRead, encrypted, authenticated, problem);
    }

    /// <summary>Tells whether the file starts with the <c>EBK1</c> marker, and rewinds it.</summary>
    /// <remarks>
    /// Only that one question is asked. A verification is about whether a file still equals its
    /// checksum, so a file this application would refuse to restore is still hashed and reported on
    /// rather than rejected out of hand.
    /// </remarks>
    private static async Task<bool> IsEncryptedAsync(Stream archive, CancellationToken cancellationToken)
    {
        byte[] signature = new byte[Ebk1Header.Magic.Length];
        int read = await archive.ReadAtLeastAsync(signature, signature.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
        archive.Seek(0, SeekOrigin.Begin);

        return read == signature.Length && signature.AsSpan().SequenceEqual(Ebk1Header.Magic);
    }

    private static VerificationResult Build(
        string archivePath,
        string sidecarPath,
        VerificationOutcome outcome,
        string? expected,
        string? actual,
        long bytesRead,
        TimeSpan duration,
        string message,
        bool isEncrypted = false,
        bool? payloadAuthenticated = null) =>
        new()
        {
            ArchivePath = archivePath,
            SidecarPath = sidecarPath,
            Outcome = outcome,
            ExpectedSha256 = expected,
            ActualSha256 = actual,
            BytesRead = bytesRead,
            Duration = duration,
            Message = message,
            IsEncrypted = isEncrypted,
            PayloadAuthenticated = payloadAuthenticated,
        };

    /// <summary>What one pass over the archive found.</summary>
    /// <param name="HashHex">The digest of the whole file.</param>
    /// <param name="BytesRead">How many bytes went through the digest.</param>
    /// <param name="IsEncrypted">Whether the file is an <c>EBK1</c> container.</param>
    /// <param name="Authenticated">Whether every chunk opened; <see langword="null"/> when not attempted.</param>
    /// <param name="Problem">Why the container would not open, when it would not.</param>
    private readonly record struct ArchiveReading(
        string HashHex,
        long BytesRead,
        bool IsEncrypted,
        bool? Authenticated,
        EncryptedArchiveException? Problem);
}
