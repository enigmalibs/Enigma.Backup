using System;

namespace Enigma.Backup.Core.Restoring;

/// <summary>
/// One progress report from a restore.
/// </summary>
/// <remarks>
/// The percentage is measured on the archive being consumed, not on the files coming out of it: how
/// much a compressed archive expands to is not known until it has been read, and a bar that has to
/// guess is worse than one that counts.
/// </remarks>
/// <param name="CurrentPath">The entry being written, or <see langword="null"/> before the first one.</param>
/// <param name="FilesRestored">How many files have been written so far.</param>
/// <param name="BytesRestored">How many bytes of file content have been written so far.</param>
/// <param name="ArchiveBytesRead">How much of the archive has been consumed.</param>
/// <param name="ArchiveBytes">The archive's total size on disk.</param>
public readonly record struct RestoreProgress(
    string? CurrentPath,
    int FilesRestored,
    long BytesRestored,
    long ArchiveBytesRead,
    long ArchiveBytes)
{
    /// <summary>How far the restore has got, from 0 to 1.</summary>
    public double Fraction => ArchiveBytes > 0 ? Math.Min(1, (double)ArchiveBytesRead / ArchiveBytes) : 0;

    /// <summary>The same value as <see cref="Fraction"/>, expressed as a percentage from 0 to 100.</summary>
    public double PercentComplete => Fraction * 100;
}
