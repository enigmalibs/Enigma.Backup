namespace Enigma.Backup.Core.Scanning;

/// <summary>Why the scan reported a <see cref="ScanWarning"/>.</summary>
public enum ScanWarningKind
{
    /// <summary>The same input path was listed more than once; the copies were dropped.</summary>
    DuplicateInput,

    /// <summary>An input path sits inside another input path and was merged into it.</summary>
    NestedInputMerged,

    /// <summary>A directory symbolic link (or junction) was not followed.</summary>
    SymbolicLinkSkipped,

    /// <summary>The output directory, or something inside it, was left out so a backup cannot contain itself.</summary>
    OutputDirectoryExcluded,

    /// <summary>Two inputs shared a top-level name, so the later one was given a numeric suffix.</summary>
    RootNameDisambiguated,

    /// <summary>An input's top-level name contained characters that are unusable on Windows and was adjusted.</summary>
    RootNameSanitized,

    /// <summary>
    /// A pattern input matched nothing under its base folder. A legitimate outcome — there may be no
    /// log files today — but one worth saying out loud, because the alternative reading is a pattern
    /// that does not mean what its author thought.
    /// </summary>
    PatternMatchedNothing,
}
