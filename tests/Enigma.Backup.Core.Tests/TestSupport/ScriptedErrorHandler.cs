using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Errors;

namespace Enigma.Backup.Core.Tests.TestSupport;

/// <summary>
/// An <see cref="IBackupErrorHandler"/> that answers from a script and records what it was asked —
/// the programmatic stand-in for the desktop app's Skip / Retry / Abort dialog.
/// </summary>
public sealed class ScriptedErrorHandler : IBackupErrorHandler
{
    private readonly Queue<BackupErrorDecision> _scripted;
    private readonly BackupErrorDecision _fallback;

    /// <summary>Creates a handler.</summary>
    /// <param name="fallback">The answer given once the script runs out.</param>
    /// <param name="scripted">The answers to give, in order.</param>
    public ScriptedErrorHandler(BackupErrorDecision fallback, params BackupErrorDecision[] scripted)
    {
        _fallback = fallback;
        _scripted = new Queue<BackupErrorDecision>(scripted);
    }

    /// <summary>Every error the handler was asked about, in order.</summary>
    public List<BackupError> Errors { get; } = [];

    /// <inheritdoc />
    public Task<BackupErrorDecision> HandleAsync(BackupError error, CancellationToken cancellationToken = default)
    {
        Errors.Add(error);

        return Task.FromResult(_scripted.Count > 0 ? _scripted.Dequeue() : _fallback);
    }
}
