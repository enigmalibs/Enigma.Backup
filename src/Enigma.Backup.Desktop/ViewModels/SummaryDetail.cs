namespace Enigma.Backup.Desktop.ViewModels;

/// <summary>One labelled line of a <see cref="RunSummary"/>.</summary>
/// <param name="Label">What the value is, e.g. "Archive".</param>
/// <param name="Value">The value itself, already formatted for display.</param>
public sealed record SummaryDetail(string Label, string Value);
