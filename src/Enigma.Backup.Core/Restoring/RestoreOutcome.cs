namespace Enigma.Backup.Core.Restoring;

/// <summary>How a restore ended.</summary>
public enum RestoreOutcome
{
    /// <summary>Every entry in the archive was handled.</summary>
    Completed,

    /// <summary>A conflict was answered with Abort; the entries already written stay on disk.</summary>
    Aborted,
}
