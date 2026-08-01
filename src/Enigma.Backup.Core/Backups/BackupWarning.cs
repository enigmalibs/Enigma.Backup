namespace Enigma.Backup.Core.Backups;

/// <summary>
/// Something a run had to work around while writing the archive. Unlike a skip, the entry is in the
/// archive — it just may not be exactly what is on disk now.
/// </summary>
/// <param name="Kind">What sort of adjustment this is.</param>
/// <param name="Path">The file it concerns.</param>
/// <param name="Message">A ready-to-display explanation.</param>
public sealed record BackupWarning(BackupWarningKind Kind, string Path, string Message);
