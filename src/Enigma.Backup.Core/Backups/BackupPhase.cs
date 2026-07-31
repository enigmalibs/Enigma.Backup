namespace Enigma.Backup.Core.Backups;

/// <summary>Which part of a run the progress report belongs to.</summary>
public enum BackupPhase
{
    /// <summary>The inputs are being enumerated; the totals are not known yet.</summary>
    Scanning,

    /// <summary>Entries are being written to the archive.</summary>
    Archiving,

    /// <summary>The archive is closed and its sidecar written.</summary>
    Completed,
}
