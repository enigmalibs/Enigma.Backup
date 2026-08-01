using System;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.Backup.Core.Restoring;

/// <summary>
/// Wraps an <see cref="IRestoreConflictHandler"/> and remembers an "apply to all further conflicts"
/// decision for the rest of the restore, so the user is asked once instead of once per file.
/// </summary>
/// <remarks>
/// <para>
/// The mirror of <see cref="Errors.ErrorDecisionPolicy"/>, and itself an
/// <see cref="IRestoreConflictHandler"/>, so wrapping twice composes: a sticky decision is
/// re-reported with <see cref="RestoreConflictDecision.ApplyToAll"/> set.
/// </para>
/// <para>
/// Every action can be made sticky here — unlike the error handler's Retry, none of Skip, Overwrite
/// or Abort can loop.
/// </para>
/// <para>One instance per restore; not thread-safe.</para>
/// </remarks>
public sealed class RestoreConflictPolicy : IRestoreConflictHandler
{
    private readonly IRestoreConflictHandler _handler;
    private RestoreConflictAction? _sticky;

    /// <summary>Creates a policy in front of <paramref name="handler"/>.</summary>
    /// <param name="handler">The handler that is asked whenever no sticky decision is in force.</param>
    public RestoreConflictPolicy(IRestoreConflictHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        _handler = handler;
    }

    /// <summary>The decision in force for every further conflict, or <see langword="null"/> while conflicts are still asked about.</summary>
    public RestoreConflictAction? StickyAction => _sticky;

    /// <inheritdoc />
    public async Task<RestoreConflictDecision> HandleAsync(RestoreConflict conflict, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conflict);

        if (_sticky is RestoreConflictAction sticky)
        {
            return new RestoreConflictDecision(sticky, true);
        }

        RestoreConflictDecision decision = await _handler.HandleAsync(conflict, cancellationToken).ConfigureAwait(false);

        if (decision.ApplyToAll)
        {
            _sticky = decision.Action;
        }

        return decision;
    }
}
