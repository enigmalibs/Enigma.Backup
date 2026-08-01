using System;

namespace Enigma.Backup.Core.Backups;

/// <summary>
/// One progress report from a run: what it is doing now, and how far along it is.
/// </summary>
/// <param name="Phase">Which part of the run this report belongs to.</param>
/// <param name="CurrentPath">The file being written, or <see langword="null"/> outside the archiving phase.</param>
/// <param name="FilesProcessed">How many files have been written so far.</param>
/// <param name="FileCount">How many files the scan found; zero while scanning.</param>
/// <param name="BytesProcessed">How many bytes of file content have been written so far.</param>
/// <param name="TotalBytes">How many bytes the scan found; zero while scanning.</param>
public readonly record struct BackupProgress(
    BackupPhase Phase,
    string? CurrentPath,
    int FilesProcessed,
    int FileCount,
    long BytesProcessed,
    long TotalBytes)
{
    /// <summary>
    /// How far the run has got, from 0 to 1, measured on bytes where there are any and on files
    /// otherwise. Returns 0 while the totals are still unknown and 1 once the run is complete.
    /// </summary>
    public double Fraction => Phase switch
    {
        BackupPhase.Completed => 1,
        _ when TotalBytes > 0 => Math.Min(1, (double)BytesProcessed / TotalBytes),
        _ when FileCount > 0 => Math.Min(1, (double)FilesProcessed / FileCount),
        _ => 0,
    };

    /// <summary>The same value as <see cref="Fraction"/>, expressed as a percentage from 0 to 100.</summary>
    public double PercentComplete => Fraction * 100;
}
