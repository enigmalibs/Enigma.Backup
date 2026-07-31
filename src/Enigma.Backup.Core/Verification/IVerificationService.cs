using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.Backup.Core.Verification;

/// <summary>
/// Checks an archive against the <c>.sha256</c> sidecar written beside it, on demand, long after the
/// backup ran.
/// </summary>
public interface IVerificationService
{
    /// <summary>Verifies <paramref name="archivePath"/>.</summary>
    /// <param name="archivePath">The archive to check.</param>
    /// <param name="password">
    /// The password for an encrypted archive, prompted for this check only. Not needed for the
    /// sidecar comparison, which reads the file exactly as it is on disk.
    /// </param>
    /// <param name="progress">Receives progress reports; may be <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the verification.</param>
    /// <returns>What the verification found; a mismatch is a result, not an exception.</returns>
    /// <exception cref="FileNotFoundException">The archive does not exist.</exception>
    /// <exception cref="OperationCanceledException">The verification was cancelled.</exception>
    Task<VerificationResult> VerifyAsync(
        string archivePath,
        string? password = null,
        IProgress<VerificationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
