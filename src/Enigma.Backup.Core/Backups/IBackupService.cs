using System;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Errors;
using Enigma.Backup.Core.Profiles;

namespace Enigma.Backup.Core.Backups;

/// <summary>
/// Runs a backup: scans a profile's inputs and streams them into one compressed — optionally
/// encrypted — archive, with a <c>.sha256</c> sidecar beside it.
/// </summary>
public interface IBackupService
{
    /// <summary>Runs <paramref name="profile"/> once.</summary>
    /// <param name="profile">The profile to back up.</param>
    /// <param name="password">
    /// The password for an encrypting profile, prompted for this run only. Never stored, logged or
    /// cached; ignored when <see cref="BackupProfile.Encrypt"/> is <see langword="false"/>.
    /// </param>
    /// <param name="errorHandler">
    /// Asked about every entry that cannot be read (Skip / Retry / Abort). An "apply to all" answer
    /// holds for the rest of the run, scan and write alike.
    /// </param>
    /// <param name="progress">Receives progress reports; may be <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the run and deletes the partial archive.</param>
    /// <returns>The end-of-run summary.</returns>
    /// <exception cref="ArgumentException">The profile has no output directory, or encrypts without a password.</exception>
    /// <exception cref="BackupAbortedException">An error was answered with Abort; no archive remains.</exception>
    /// <exception cref="OperationCanceledException">The run was cancelled; no archive remains.</exception>
    Task<BackupResult> RunAsync(
        BackupProfile profile,
        string? password,
        IBackupErrorHandler errorHandler,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
