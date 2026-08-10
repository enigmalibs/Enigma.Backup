using Enigma.Backup.Core.Profiles;

namespace Enigma.Backup.Desktop.ViewModels;

/// <summary>
/// One row of the "Files and folders to back up" list: either a path, or a pattern to work out again
/// on every run.
/// </summary>
/// <remarks>
/// A profile keeps the two apart (<see cref="BackupProfile.Inputs"/> and
/// <see cref="BackupProfile.PatternInputs"/>), but the window shows one list — so the list needs one
/// item type. It is immutable and compares by value, which is what makes "is this already in the
/// profile?" a plain comparison and lets the <c>ListBox</c> hold a selection.
/// </remarks>
public sealed record InputItem
{
    private InputItem(string path, string? pattern)
    {
        Path = path;
        Pattern = pattern;
    }

    /// <summary>The path this row backs up: the file or folder itself, or a pattern's base folder.</summary>
    public string Path { get; }

    /// <summary>The glob matched inside <see cref="Path"/>, or <see langword="null"/> for a plain path.</summary>
    public string? Pattern { get; }

    /// <summary>Whether this row is a pattern rather than a path.</summary>
    public bool IsPattern => Pattern is not null;

    /// <summary>What the list shows: the path, or the pair as <c>folder ▸ pattern</c>.</summary>
    public string Display => Pattern is null ? Path : $"{Path} ▸ {Pattern}";

    /// <summary>The tooltip, which spells out what a pattern row means.</summary>
    public string Detail => Pattern is null ? Path : $"Everything matching '{Pattern}' under '{Path}'";

    /// <summary>A row for a file or folder.</summary>
    /// <param name="path">The path to back up.</param>
    public static InputItem ForPath(string path) => new(path, pattern: null);

    /// <summary>A row for a pattern.</summary>
    /// <param name="baseDirectory">The folder the pattern applies to.</param>
    /// <param name="pattern">The glob matched inside it.</param>
    public static InputItem ForPattern(string baseDirectory, string pattern) => new(baseDirectory, pattern);
}
