namespace Enigma.Backup.Core.Scanning;

/// <summary>
/// Something the scan decided on its own that the user should know about. Unlike a
/// <see cref="Errors.SkippedEntry"/> it needs no decision — it is reported in the run summary.
/// </summary>
/// <param name="Kind">What sort of adjustment this is.</param>
/// <param name="Path">The file or directory it concerns.</param>
/// <param name="Message">A ready-to-display explanation.</param>
public sealed record ScanWarning(ScanWarningKind Kind, string Path, string Message);
