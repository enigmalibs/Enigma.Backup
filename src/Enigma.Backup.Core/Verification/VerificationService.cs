using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Archiving;

namespace Enigma.Backup.Core.Verification;

/// <summary>
/// The sidecar-comparing <see cref="IVerificationService"/>: re-reads the archive, recomputes its
/// SHA-256, and compares it with what the backup recorded.
/// </summary>
/// <remarks>
/// <para>
/// This catches what a backup is actually at risk of — bit rot, a truncated copy, a bad transfer to
/// another disk — because the digest was taken from the bytes that were written, so any difference in
/// the bytes that are read shows up.
/// </para>
/// <para>
/// A missing or unreadable sidecar is reported as an outcome rather than thrown: an archive whose
/// sidecar was lost is a normal situation the user has to be told about, not a program error. PHASE04
/// adds the second half of the check for encrypted archives — validating every GCM tag, which proves
/// the payload decrypts as well as that the file is unchanged.
/// </para>
/// </remarks>
public sealed class VerificationService : IVerificationService
{
    private const int BufferSize = 1024 * 1024;

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
                $"There is no checksum file beside '{Path.GetFileName(archivePath)}', so it cannot be verified.");
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

        (string actual, long bytesRead) = await ComputeHashAsync(archivePath, progress, cancellationToken).ConfigureAwait(false);
        bool matches = string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);

        return Build(
            archivePath,
            sidecarPath,
            matches ? VerificationOutcome.Succeeded : VerificationOutcome.HashMismatch,
            expected,
            actual,
            bytesRead,
            _timeProvider.GetElapsedTime(startedAt),
            matches
                ? $"'{Path.GetFileName(archivePath)}' matches its checksum: the archive is intact."
                : $"'{Path.GetFileName(archivePath)}' does not match its checksum: the file has changed since it was written.");
    }

    private static async Task<(string HashHex, long BytesRead)> ComputeHashAsync(
        string archivePath,
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

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[BufferSize];
        long bytesRead = 0;

        await using (FileStream archive = new(archivePath, options))
        {
            long total = archive.Length;
            progress?.Report(new VerificationProgress(0, total));

            while (true)
            {
                int read = await archive.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                hash.AppendData(buffer.AsSpan(0, read));
                bytesRead += read;
                progress?.Report(new VerificationProgress(bytesRead, total));
            }
        }

        return (Convert.ToHexStringLower(hash.GetHashAndReset()), bytesRead);
    }

    private static VerificationResult Build(
        string archivePath,
        string sidecarPath,
        VerificationOutcome outcome,
        string? expected,
        string? actual,
        long bytesRead,
        TimeSpan duration,
        string message) =>
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
        };
}
