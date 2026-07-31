using System;
using System.Collections.Generic;
using Enigma.Backup.Core.Errors;

namespace Enigma.Backup.Core.Scanning;

/// <summary>
/// Everything a scan produced: the entries to archive, the adjustments it made along the way, and
/// what the user chose to leave out. The totals let a run report overall progress before it starts
/// writing.
/// </summary>
public sealed class ScanResult
{
    /// <summary>Creates a scan result and computes its totals.</summary>
    /// <param name="entries">The entries to archive, in the order they should be written.</param>
    /// <param name="warnings">Adjustments the scan made on its own.</param>
    /// <param name="skipped">Entries left out after an error was answered with Skip.</param>
    public ScanResult(IReadOnlyList<ScanEntry> entries, IReadOnlyList<ScanWarning> warnings, IReadOnlyList<SkippedEntry> skipped)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(warnings);
        ArgumentNullException.ThrowIfNull(skipped);

        Entries = entries;
        Warnings = warnings;
        Skipped = skipped;

        foreach (ScanEntry entry in entries)
        {
            if (entry.Kind == ScanEntryKind.File)
            {
                FileCount++;
                TotalBytes += entry.Length;
            }
            else
            {
                DirectoryCount++;
            }
        }
    }

    /// <summary>The entries to archive: a directory always precedes everything found inside it.</summary>
    public IReadOnlyList<ScanEntry> Entries { get; }

    /// <summary>Adjustments the scan made without asking, such as merged or renamed inputs.</summary>
    public IReadOnlyList<ScanWarning> Warnings { get; }

    /// <summary>Entries left out because their failure was answered with Skip.</summary>
    public IReadOnlyList<SkippedEntry> Skipped { get; }

    /// <summary>How many file entries the scan found.</summary>
    public int FileCount { get; }

    /// <summary>How many directory entries the scan found.</summary>
    public int DirectoryCount { get; }

    /// <summary>The combined size of every file entry, in bytes.</summary>
    public long TotalBytes { get; }
}
