using System;
using System.Collections.Generic;
using Enigma.Backup.Core.Backups;
using Enigma.Backup.Core.Errors;
using Enigma.Backup.Core.Restoring;
using Enigma.Backup.Core.Scanning;
using Enigma.Backup.Core.Verification;

namespace Enigma.Backup.Desktop.Tests.TestSupport;

/// <summary>Ready-made core results, so a test only spells out the part it is about.</summary>
internal static class Results
{
    /// <summary>A completed backup.</summary>
    public static BackupResult Backup(
        string outputPath = "/backups/Documents_2026-08-01_120000.tar.zst",
        int fileCount = 12,
        int directoryCount = 3,
        long bytesProcessed = 3_145_728,
        long archiveBytes = 1_048_576,
        TimeSpan? duration = null,
        IReadOnlyList<SkippedEntry>? skipped = null,
        IReadOnlyList<ScanWarning>? scanWarnings = null,
        IReadOnlyList<BackupWarning>? warnings = null) =>
        new()
        {
            OutputPath = outputPath,
            SidecarPath = outputPath + ".sha256",
            Sha256 = "3b1f8c0e5d2a47b69f0c1d8e2a5b7c9d0e3f4a6b8c1d2e3f4a5b6c7d8e9f0a1b",
            FileCount = fileCount,
            DirectoryCount = directoryCount,
            BytesProcessed = bytesProcessed,
            ArchiveBytes = archiveBytes,
            Duration = duration ?? TimeSpan.FromSeconds(1.5),
            Skipped = skipped ?? [],
            ScanWarnings = scanWarnings ?? [],
            Warnings = warnings ?? [],
        };

    /// <summary>A completed restore.</summary>
    public static RestoreResult Restore(
        string archivePath = "/backups/Documents_2026-08-01_120000.tar.zst",
        string destinationDirectory = "/restored",
        RestoreOutcome outcome = RestoreOutcome.Completed,
        int fileCount = 12,
        int directoryCount = 3,
        long bytesRestored = 3_145_728,
        IReadOnlyList<SkippedEntry>? skipped = null) =>
        new()
        {
            ArchivePath = archivePath,
            DestinationDirectory = destinationDirectory,
            Outcome = outcome,
            FileCount = fileCount,
            DirectoryCount = directoryCount,
            BytesRestored = bytesRestored,
            Duration = TimeSpan.FromSeconds(0.9),
            Skipped = skipped ?? [],
        };

    /// <summary>A verification with whatever outcome the test is about.</summary>
    public static VerificationResult Verification(
        string archivePath = "/backups/Documents_2026-08-01_120000.tar.zst",
        VerificationOutcome outcome = VerificationOutcome.Succeeded,
        string message = "'Documents_2026-08-01_120000.tar.zst' matches its checksum: the archive is intact.",
        bool isEncrypted = false,
        bool? payloadAuthenticated = null) =>
        new()
        {
            ArchivePath = archivePath,
            SidecarPath = archivePath + ".sha256",
            Outcome = outcome,
            ExpectedSha256 = "3b1f8c0e5d2a47b69f0c1d8e2a5b7c9d0e3f4a6b8c1d2e3f4a5b6c7d8e9f0a1b",
            ActualSha256 = "3b1f8c0e5d2a47b69f0c1d8e2a5b7c9d0e3f4a6b8c1d2e3f4a5b6c7d8e9f0a1b",
            BytesRead = 1_048_576,
            Duration = TimeSpan.FromSeconds(0.4),
            Message = message,
            IsEncrypted = isEncrypted,
            PayloadAuthenticated = payloadAuthenticated,
        };
}
