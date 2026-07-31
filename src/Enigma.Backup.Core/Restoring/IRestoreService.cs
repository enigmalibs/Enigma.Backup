using System;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Archiving;

namespace Enigma.Backup.Core.Restoring;

/// <summary>
/// Streams an archive produced by <see cref="Backups.IBackupService"/> back out into a directory.
/// </summary>
public interface IRestoreService
{
    /// <summary>Restores <paramref name="archivePath"/> into <paramref name="destinationDirectory"/>.</summary>
    /// <param name="archivePath">The archive to read; its format is taken from its contents, not its name.</param>
    /// <param name="destinationDirectory">The directory to write into; created when it does not exist.</param>
    /// <param name="password">
    /// The password for an encrypted archive, prompted for this restore only. Never stored, logged or
    /// cached; ignored for an unencrypted archive.
    /// </param>
    /// <param name="conflictHandler">
    /// Asked about every file that already exists (Skip / Overwrite / Abort). An "apply to all" answer
    /// holds for the rest of the restore.
    /// </param>
    /// <param name="progress">Receives progress reports; may be <see langword="null"/>.</param>
    /// <param name="cancellationToken">
    /// Cancels the restore. Unlike a backup, what has already been written stays: the destination is
    /// the user's own directory, not a file this restore owns.
    /// </param>
    /// <returns>The end-of-run summary.</returns>
    /// <exception cref="ArchiveFormatException">The file is not a readable archive, or contains an entry that would be written outside the destination.</exception>
    /// <exception cref="OperationCanceledException">The restore was cancelled.</exception>
    Task<RestoreResult> RestoreAsync(
        string archivePath,
        string destinationDirectory,
        string? password,
        IRestoreConflictHandler conflictHandler,
        IProgress<RestoreProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
