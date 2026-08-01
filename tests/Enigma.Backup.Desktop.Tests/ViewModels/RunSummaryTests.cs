using System;
using Enigma.Backup.Core.Backups;
using Enigma.Backup.Core.Errors;
using Enigma.Backup.Core.Restoring;
using Enigma.Backup.Core.Scanning;
using Enigma.Backup.Core.Verification;
using Enigma.Backup.Desktop.Tests.TestSupport;
using Enigma.Backup.Desktop.ViewModels;
using Xunit;

namespace Enigma.Backup.Desktop.Tests.ViewModels;

public sealed class RunSummaryTests
{
    [Fact]
    public void ACleanBackupSucceedsAndShowsWhatItWrote()
    {
        RunSummary summary = RunSummary.FromBackup(Results.Backup(
            outputPath: "/backups/Documents_2026-08-01_120000.tar.zst",
            fileCount: 12,
            directoryCount: 3,
            bytesProcessed: 3_145_728,
            archiveBytes: 1_048_576,
            duration: TimeSpan.FromSeconds(1.5)));

        Assert.True(summary.IsSuccess);
        Assert.Equal("Backed up 12 files to 'Documents_2026-08-01_120000.tar.zst'.", summary.Headline);
        Assert.False(summary.HasSkipped);
        Assert.False(summary.HasWarnings);

        Assert.Contains(summary.Details, detail => detail is { Label: "Contents", Value: "12 files · 3 folders" });
        Assert.Contains(summary.Details, detail => detail is { Label: "Read", Value: "3 MiB" });
        Assert.Contains(summary.Details, detail => detail is { Label: "Written", Value: "1 MiB" });
        Assert.Contains(summary.Details, detail => detail is { Label: "Took", Value: "1.5 s" });
        Assert.Contains(summary.Details, detail => detail.Label == "SHA-256");
    }

    [Fact]
    public void ABackupThatLeftSomethingOutIsAWarning_AndSaysWhatAndWhy()
    {
        RunSummary summary = RunSummary.FromBackup(Results.Backup(
            fileCount: 11,
            skipped: [new SkippedEntry("/secret", "'/secret' could not be read: access denied.")],
            scanWarnings: [new ScanWarning(ScanWarningKind.SymbolicLinkSkipped, "/link", "'/link' is a symbolic link and was not followed.")],
            warnings: [new BackupWarning(BackupWarningKind.SourceGrewWhileReading, "/log", "'/log' grew while it was being archived.")]));

        Assert.True(summary.IsWarning);
        Assert.Contains("leaving out 1 entry", summary.Headline, StringComparison.Ordinal);
        Assert.Equal("Left out — 1 entry", summary.SkippedHeader);
        Assert.Equal("'/secret' could not be read: access denied.", Assert.Single(summary.Skipped));

        // Scan warnings and write warnings are one list to the user; both are "worth knowing".
        Assert.Equal(2, summary.Warnings.Count);
        Assert.Equal("Warnings — 2 notes", summary.WarningsHeader);
    }

    [Fact]
    public void ACleanRestoreSucceeds()
    {
        RunSummary summary = RunSummary.FromRestore(Results.Restore(destinationDirectory: "/restored", fileCount: 12));

        Assert.True(summary.IsSuccess);
        Assert.Equal("Restored 12 files into '/restored'.", summary.Headline);
        Assert.Contains(summary.Details, detail => detail is { Label: "Restored into", Value: "/restored" });
    }

    [Fact]
    public void ARestoreThatSkippedFilesIsAWarning()
    {
        RunSummary summary = RunSummary.FromRestore(Results.Restore(
            fileCount: 10,
            skipped: [new SkippedEntry("/restored/a.txt", "'/restored/a.txt' already exists and was left as it is.")]));

        Assert.True(summary.IsWarning);
        Assert.Contains("leaving 1 entry untouched", summary.Headline, StringComparison.Ordinal);
        Assert.Single(summary.Skipped);
    }

    [Fact]
    public void AnAbortedRestoreSaysWhatWasKept()
    {
        RunSummary summary = RunSummary.FromRestore(Results.Restore(outcome: RestoreOutcome.Aborted, fileCount: 4));

        Assert.True(summary.IsWarning);
        Assert.Equal("The restore was stopped after 4 files; what had already been written was kept.", summary.Headline);
    }

    [Theory]
    [InlineData(VerificationOutcome.Succeeded, RunSummaryKind.Success)]
    [InlineData(VerificationOutcome.HashMismatch, RunSummaryKind.Failure)]
    [InlineData(VerificationOutcome.AuthenticationFailed, RunSummaryKind.Failure)]
    [InlineData(VerificationOutcome.PasswordRequired, RunSummaryKind.Warning)]
    [InlineData(VerificationOutcome.SidecarMissing, RunSummaryKind.Warning)]
    [InlineData(VerificationOutcome.SidecarUnreadable, RunSummaryKind.Warning)]
    public void AVerificationsOutcomeDecidesHowItReads(VerificationOutcome outcome, RunSummaryKind expected)
    {
        RunSummary summary = RunSummary.FromVerification(Results.Verification(outcome: outcome, message: "What it found."));

        Assert.Equal(expected, summary.Kind);

        // The core already writes the sentence for display; the summary must not paraphrase it.
        Assert.Equal("What it found.", summary.Headline);
    }

    [Theory]
    [InlineData(true, "every block passed its authentication check")]
    [InlineData(false, "a block failed its authentication check")]
    [InlineData(null, "not checked — no password was given")]
    public void AnEncryptedArchiveSaysWhetherItsContentsWereOpened(bool? authenticated, string expected)
    {
        RunSummary summary = RunSummary.FromVerification(Results.Verification(
            isEncrypted: true,
            payloadAuthenticated: authenticated));

        SummaryDetail detail = Assert.Single(summary.Details, candidate => candidate.Label == "Encrypted contents");
        Assert.Equal(expected, detail.Value);
    }

    [Fact]
    public void AnUnencryptedArchiveSaysNothingAboutEncryption()
    {
        RunSummary summary = RunSummary.FromVerification(Results.Verification());

        Assert.DoesNotContain(summary.Details, detail => detail.Label == "Encrypted contents");
    }

    [Fact]
    public void ACancelledBackupSaysThePartialArchiveWentWithIt()
    {
        Assert.Equal(
            "The backup was cancelled; the partial archive was deleted.",
            RunSummary.Cancelled("backup", discarded: true).Headline);

        Assert.Equal(
            "The restore was cancelled.",
            RunSummary.Cancelled("restore", discarded: false).Headline);
    }

    [Fact]
    public void AnAbortAndAFailureReadDifferently()
    {
        RunSummary aborted = RunSummary.Aborted("'/secret' could not be read: access denied.");
        RunSummary failed = RunSummary.Failed("backup", "The output drive vanished.");

        Assert.True(aborted.IsWarning);
        Assert.Contains("the partial archive deleted", aborted.Headline, StringComparison.Ordinal);

        Assert.True(failed.IsFailure);
        Assert.Equal("The backup failed: The output drive vanished.", failed.Headline);
    }
}
