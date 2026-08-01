namespace Enigma.Backup.Core.Errors;

/// <summary>
/// The answer to a <see cref="BackupError"/>: what to do, and whether the same answer should be
/// applied automatically to every further error of the run without asking again.
/// </summary>
/// <param name="Action">The action to take for this error.</param>
/// <param name="ApplyToAll">
/// <see langword="true"/> when the user ticked "apply to all further errors".
/// <see cref="BackupErrorAction.Retry"/> is never made sticky — see <see cref="ErrorDecisionPolicy"/>.
/// </param>
public readonly record struct BackupErrorDecision(BackupErrorAction Action, bool ApplyToAll)
{
    /// <summary>Creates a "skip this entry" decision.</summary>
    /// <param name="applyToAll">Whether every further error should be skipped too.</param>
    public static BackupErrorDecision Skip(bool applyToAll = false) => new(BackupErrorAction.Skip, applyToAll);

    /// <summary>Creates a "try again" decision.</summary>
    public static BackupErrorDecision Retry() => new(BackupErrorAction.Retry, false);

    /// <summary>Creates an "abort the run" decision.</summary>
    /// <param name="applyToAll">Kept for symmetry; aborting ends the run either way.</param>
    public static BackupErrorDecision Abort(bool applyToAll = false) => new(BackupErrorAction.Abort, applyToAll);
}
