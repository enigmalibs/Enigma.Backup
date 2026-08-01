namespace Enigma.Backup.Core.Errors;

/// <summary>
/// A file or directory that was left out of a run after its failure was answered with
/// <see cref="BackupErrorAction.Skip"/>. Every one of these is listed in the end-of-run summary.
/// </summary>
/// <param name="Path">The file or directory that was left out.</param>
/// <param name="Reason">Why it was left out, ready to display.</param>
public sealed record SkippedEntry(string Path, string Reason);
