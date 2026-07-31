namespace Enigma.Backup.Core.Backups;

/// <summary>Why a run reported a <see cref="BackupWarning"/> while writing the archive.</summary>
public enum BackupWarningKind
{
    /// <summary>
    /// The file grew while it was being archived; the archive holds the part that existed when the
    /// entry started.
    /// </summary>
    SourceGrewWhileReading,

    /// <summary>
    /// The file shrank while it was being archived; the entry was padded with zeros to the size its
    /// header already declared.
    /// </summary>
    SourceShrankWhileReading,
}
