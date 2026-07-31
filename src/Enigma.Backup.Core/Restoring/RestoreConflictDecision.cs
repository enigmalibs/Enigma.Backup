namespace Enigma.Backup.Core.Restoring;

/// <summary>
/// The answer to a <see cref="RestoreConflict"/>: what to do, and whether the same answer should be
/// applied automatically to every further conflict without asking again.
/// </summary>
/// <param name="Action">The action to take for this conflict.</param>
/// <param name="ApplyToAll"><see langword="true"/> when the user ticked "apply to all further conflicts".</param>
public readonly record struct RestoreConflictDecision(RestoreConflictAction Action, bool ApplyToAll)
{
    /// <summary>Creates a "keep the file on disk" decision.</summary>
    /// <param name="applyToAll">Whether every further conflict should be skipped too.</param>
    public static RestoreConflictDecision Skip(bool applyToAll = false) => new(RestoreConflictAction.Skip, applyToAll);

    /// <summary>Creates a "replace the file on disk" decision.</summary>
    /// <param name="applyToAll">Whether every further conflict should be overwritten too.</param>
    public static RestoreConflictDecision Overwrite(bool applyToAll = false) => new(RestoreConflictAction.Overwrite, applyToAll);

    /// <summary>Creates a "stop the restore" decision.</summary>
    /// <param name="applyToAll">Kept for symmetry; aborting ends the restore either way.</param>
    public static RestoreConflictDecision Abort(bool applyToAll = false) => new(RestoreConflictAction.Abort, applyToAll);
}
