namespace Enigma.Backup.Core.Errors;

/// <summary>What the caller decided to do about a <see cref="BackupError"/>.</summary>
public enum BackupErrorAction
{
    /// <summary>Leave the offending entry out of the run and carry on.</summary>
    Skip,

    /// <summary>Attempt the failed operation once more.</summary>
    Retry,

    /// <summary>Stop the whole run; any partial output is discarded.</summary>
    Abort,
}
