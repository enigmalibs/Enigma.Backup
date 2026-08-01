using System;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.Backup.Core.Errors;

/// <summary>
/// Wraps an <see cref="IBackupErrorHandler"/> and remembers an "apply to all further errors"
/// decision for the rest of the run, so the user is asked once instead of once per failure.
/// </summary>
/// <remarks>
/// <para>
/// The policy is itself an <see cref="IBackupErrorHandler"/>: a run made of several stages can wrap
/// once and pass the policy down, and a stage that wraps again composes correctly, because a sticky
/// decision is re-reported with <see cref="BackupErrorDecision.ApplyToAll"/> set.
/// </para>
/// <para>
/// <see cref="BackupErrorAction.Retry"/> is deliberately never made sticky: a permanent failure
/// answered with "retry, apply to all" would loop forever.
/// </para>
/// <para>One instance per run; not thread-safe.</para>
/// </remarks>
public sealed class ErrorDecisionPolicy : IBackupErrorHandler
{
    private readonly IBackupErrorHandler _handler;
    private BackupErrorAction? _sticky;

    /// <summary>Creates a policy in front of <paramref name="handler"/>.</summary>
    /// <param name="handler">The handler that is asked whenever no sticky decision is in force.</param>
    public ErrorDecisionPolicy(IBackupErrorHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        _handler = handler;
    }

    /// <summary>The decision in force for every further error, or <see langword="null"/> while errors are still asked about.</summary>
    public BackupErrorAction? StickyAction => _sticky;

    /// <inheritdoc />
    public async Task<BackupErrorDecision> HandleAsync(BackupError error, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (_sticky is BackupErrorAction sticky)
        {
            return new BackupErrorDecision(sticky, true);
        }

        BackupErrorDecision decision = await _handler.HandleAsync(error, cancellationToken).ConfigureAwait(false);

        if (decision.ApplyToAll && decision.Action != BackupErrorAction.Retry)
        {
            _sticky = decision.Action;
        }

        return decision;
    }
}
