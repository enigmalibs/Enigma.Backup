using System;
using System.Collections.Generic;
using Enigma.Backup.Core.Errors;
using Enigma.Backup.Core.Scanning;

namespace Enigma.Backup.Core.Backups;

/// <summary>
/// What one completed run produced — the end-of-run summary, ready to display.
/// </summary>
/// <remarks>
/// A run that failed or was cancelled never returns one of these: it throws, and leaves no output
/// file behind.
/// </remarks>
public sealed class BackupResult
{
    /// <summary>The archive that was written.</summary>
    public required string OutputPath { get; init; }

    /// <summary>The <c>.sha256</c> sidecar written beside the archive.</summary>
    public required string SidecarPath { get; init; }

    /// <summary>The archive's SHA-256 digest, lowercase hex, as recorded in the sidecar.</summary>
    public required string Sha256 { get; init; }

    /// <summary>How many files were written into the archive.</summary>
    public required int FileCount { get; init; }

    /// <summary>How many directories were written into the archive.</summary>
    public required int DirectoryCount { get; init; }

    /// <summary>How many bytes of file content were read and written.</summary>
    public required long BytesProcessed { get; init; }

    /// <summary>The archive's own size on disk, after compression and encryption.</summary>
    public required long ArchiveBytes { get; init; }

    /// <summary>How long the run took, scan included.</summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>Everything left out of the archive, with the reason — the prominent part of the summary.</summary>
    public required IReadOnlyList<SkippedEntry> Skipped { get; init; }

    /// <summary>Adjustments the scan made on its own, such as merged inputs or skipped links.</summary>
    public required IReadOnlyList<ScanWarning> ScanWarnings { get; init; }

    /// <summary>Adjustments made while writing, such as a file that changed during the run.</summary>
    public required IReadOnlyList<BackupWarning> Warnings { get; init; }
}
