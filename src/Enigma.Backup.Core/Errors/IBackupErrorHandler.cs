using System.Threading;
using System.Threading.Tasks;

namespace Enigma.Backup.Core.Errors;

/// <summary>
/// The seam through which a recoverable failure reaches whoever is driving the run. The desktop app
/// implements it with a Skip / Retry / Abort dialog, tests implement it programmatically, and a CLI
/// could implement it on the console — which is what keeps the core free of UI dependencies.
/// </summary>
public interface IBackupErrorHandler
{
    /// <summary>Asks what to do about <paramref name="error"/>.</summary>
    /// <param name="error">The failure that was encountered.</param>
    /// <param name="cancellationToken">Cancels waiting for the decision.</param>
    /// <returns>The action to take, and whether it applies to all further errors.</returns>
    Task<BackupErrorDecision> HandleAsync(BackupError error, CancellationToken cancellationToken = default);
}
