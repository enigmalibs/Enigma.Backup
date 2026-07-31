using System.Threading;
using System.Threading.Tasks;

namespace Enigma.Backup.Core.Restoring;

/// <summary>
/// Decides what happens when a restore would write over a file that is already there.
/// </summary>
/// <remarks>
/// The counterpart of <see cref="Errors.IBackupErrorHandler"/> on the restore side, and the same
/// CLI-readiness seam: the desktop app implements it with a dialog, tests implement it
/// programmatically, and the core never knows the difference.
/// </remarks>
public interface IRestoreConflictHandler
{
    /// <summary>Asks what to do about one conflict.</summary>
    /// <param name="conflict">The entry and the file it would replace.</param>
    /// <param name="cancellationToken">Cancels waiting for the answer.</param>
    /// <returns>The action to take, and whether it applies to every further conflict.</returns>
    Task<RestoreConflictDecision> HandleAsync(RestoreConflict conflict, CancellationToken cancellationToken = default);
}
