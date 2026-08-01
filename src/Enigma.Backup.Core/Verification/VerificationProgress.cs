using System;

namespace Enigma.Backup.Core.Verification;

/// <summary>One progress report from a verification.</summary>
/// <param name="BytesProcessed">How much of the archive has been read.</param>
/// <param name="TotalBytes">The archive's size on disk.</param>
public readonly record struct VerificationProgress(long BytesProcessed, long TotalBytes)
{
    /// <summary>How far the verification has got, from 0 to 1.</summary>
    public double Fraction => TotalBytes > 0 ? Math.Min(1, (double)BytesProcessed / TotalBytes) : 0;

    /// <summary>The same value as <see cref="Fraction"/>, expressed as a percentage from 0 to 100.</summary>
    public double PercentComplete => Fraction * 100;
}
