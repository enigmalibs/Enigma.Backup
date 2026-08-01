namespace Enigma.Backup.Core.Restoring;

/// <summary>What to do about a file the restore would write over.</summary>
public enum RestoreConflictAction
{
    /// <summary>Leave the file on disk as it is and move on to the next entry.</summary>
    Skip,

    /// <summary>Replace the file on disk with the one from the archive.</summary>
    Overwrite,

    /// <summary>Stop the restore; everything already written stays.</summary>
    Abort,
}
