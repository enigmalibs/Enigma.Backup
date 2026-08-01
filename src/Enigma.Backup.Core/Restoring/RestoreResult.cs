using System;
using System.Collections.Generic;
using Enigma.Backup.Core.Errors;

namespace Enigma.Backup.Core.Restoring;

/// <summary>What one restore produced — the end-of-run summary, ready to display.</summary>
public sealed class RestoreResult
{
    /// <summary>The archive that was read.</summary>
    public required string ArchivePath { get; init; }

    /// <summary>The directory everything was written into.</summary>
    public required string DestinationDirectory { get; init; }

    /// <summary>How it ended: <see cref="RestoreOutcome.Aborted"/> when a conflict stopped it early.</summary>
    public required RestoreOutcome Outcome { get; init; }

    /// <summary>How many files were written.</summary>
    public required int FileCount { get; init; }

    /// <summary>How many directories were created or updated.</summary>
    public required int DirectoryCount { get; init; }

    /// <summary>How many bytes of file content were written.</summary>
    public required long BytesRestored { get; init; }

    /// <summary>How long the restore took.</summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Everything left on disk untouched, with the reason — files whose conflict was answered with
    /// Skip, and entry types this restore does not write.
    /// </summary>
    public required IReadOnlyList<SkippedEntry> Skipped { get; init; }
}
