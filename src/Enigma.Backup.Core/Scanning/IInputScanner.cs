using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Errors;
using Enigma.Backup.Core.Profiles;

namespace Enigma.Backup.Core.Scanning;

/// <summary>
/// Turns a profile's input paths into the flat list of entries a run archives, resolving everything
/// that has to be decided before a single byte is written: canonical paths, overlapping inputs,
/// exclusions, and inputs that are no longer there.
/// </summary>
public interface IInputScanner
{
    /// <summary>Scans <paramref name="profile"/>'s inputs.</summary>
    /// <param name="profile">The profile whose inputs, excludes and output directory drive the scan.</param>
    /// <param name="errorHandler">
    /// Asked about every missing or unreadable path. Pass an <see cref="ErrorDecisionPolicy"/> to
    /// carry an "apply to all" decision across the rest of the run.
    /// </param>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns>The entries to archive, plus the warnings and skips to report.</returns>
    /// <exception cref="BackupAbortedException">An error was answered with Abort.</exception>
    Task<ScanResult> ScanAsync(BackupProfile profile, IBackupErrorHandler errorHandler, CancellationToken cancellationToken = default);
}
