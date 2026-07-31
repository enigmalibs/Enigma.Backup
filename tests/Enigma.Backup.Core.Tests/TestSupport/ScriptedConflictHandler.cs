using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Restoring;

namespace Enigma.Backup.Core.Tests.TestSupport;

/// <summary>
/// An <see cref="IRestoreConflictHandler"/> that answers from a script and records what it was asked —
/// the programmatic stand-in for the desktop app's Skip / Overwrite / Abort dialog.
/// </summary>
public sealed class ScriptedConflictHandler : IRestoreConflictHandler
{
    private readonly Queue<RestoreConflictDecision> _scripted;
    private readonly RestoreConflictDecision _fallback;

    /// <summary>Creates a handler.</summary>
    /// <param name="fallback">The answer given once the script runs out.</param>
    /// <param name="scripted">The answers to give, in order.</param>
    public ScriptedConflictHandler(RestoreConflictDecision fallback, params RestoreConflictDecision[] scripted)
    {
        _fallback = fallback;
        _scripted = new Queue<RestoreConflictDecision>(scripted);
    }

    /// <summary>Every conflict the handler was asked about, in order.</summary>
    public List<RestoreConflict> Conflicts { get; } = [];

    /// <inheritdoc />
    public Task<RestoreConflictDecision> HandleAsync(RestoreConflict conflict, CancellationToken cancellationToken = default)
    {
        Conflicts.Add(conflict);

        return Task.FromResult(_scripted.Count > 0 ? _scripted.Dequeue() : _fallback);
    }
}
