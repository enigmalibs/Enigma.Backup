using System;

namespace Enigma.Backup.Core.Verification;

/// <summary>What one verification found, ready to display.</summary>
public sealed class VerificationResult
{
    /// <summary>The archive that was checked.</summary>
    public required string ArchivePath { get; init; }

    /// <summary>The sidecar it was checked against, whether or not that file exists.</summary>
    public required string SidecarPath { get; init; }

    /// <summary>What the verification found.</summary>
    public required VerificationOutcome Outcome { get; init; }

    /// <summary>The digest recorded in the sidecar, or <see langword="null"/> when none could be read.</summary>
    public required string? ExpectedSha256 { get; init; }

    /// <summary>The digest computed from the archive, or <see langword="null"/> when it was not computed.</summary>
    public required string? ActualSha256 { get; init; }

    /// <summary>How many bytes of the archive were read.</summary>
    public required long BytesRead { get; init; }

    /// <summary>How long the verification took.</summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>A ready-to-display explanation of the outcome.</summary>
    public required string Message { get; init; }

    /// <summary>Whether the archive passed.</summary>
    public bool Succeeded => Outcome == VerificationOutcome.Succeeded;
}
