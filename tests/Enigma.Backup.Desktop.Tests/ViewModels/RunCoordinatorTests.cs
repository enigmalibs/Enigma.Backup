using System;
using System.Threading.Tasks;
using Enigma.Backup.Core.Backups;
using Enigma.Backup.Core.Errors;
using Enigma.Backup.Core.Verification;
using Enigma.Backup.Desktop.Tests.TestSupport;
using Enigma.Backup.Desktop.ViewModels;
using Xunit;

namespace Enigma.Backup.Desktop.Tests.ViewModels;

/// <summary>
/// The seam a run reports itself through: the run dialog, from its first report to its outcome.
/// </summary>
/// <remarks>
/// The dialog is the whole progress readout, the only place a run can be cancelled from, and where its
/// outcome is read — so "it went up, it was told what was happening, it was finished" is not cosmetic: a
/// run that is never finished is a dialog nobody can close.
/// </remarks>
public sealed class RunCoordinatorTests
{
    [Fact]
    public async Task ARun_ShowsOneDialogAndFinishesIt()
    {
        Harness harness = new();
        harness.FillEditor();

        await harness.BackupPage.BackupCommand.ExecuteAsync(null);

        RunViewModel run = Assert.Single(harness.RunDialog.Runs);

        Assert.Equal("Backing up", run.Title);
        Assert.False(run.IsRunning);
        Assert.True(run.CloseCommand.CanExecute(null));
        Assert.Equal("Done", run.DialogTitle);
    }

    [Fact]
    public async Task ARun_ThatFailsUnexpectedlyIsStillFinished()
    {
        Harness harness = new();
        harness.FillEditor();
        harness.Backups.Behaviour = (_, _, _, _, _) => throw new InvalidOperationException("The output drive vanished.");

        await harness.BackupPage.BackupCommand.ExecuteAsync(null);

        RunViewModel run = Assert.Single(harness.RunDialog.Runs);

        Assert.False(run.IsRunning);
        Assert.True(run.Summary?.IsFailure);
        Assert.Equal("Something went wrong", run.DialogTitle);
    }

    [Fact]
    public async Task ARun_ThatNeverStartsShowsNothingAtAll()
    {
        Harness harness = new();
        harness.FillEditor(encrypt: true);

        // No password is queued, so the prompt is cancelled and the backup does not begin.
        await harness.BackupPage.BackupCommand.ExecuteAsync(null);

        Assert.Empty(harness.RunDialog.Runs);
    }

    [Fact]
    public async Task TheDialog_IsToldEverythingTheCoreReported()
    {
        Harness harness = new();
        harness.FillEditor();

        string? first = null;
        bool firstIndeterminate = false;
        bool sawScan = false;
        bool sawArchiving = false;

        harness.Backups.Behaviour = (_, _, _, progress, _) =>
        {
            RunViewModel run = harness.RunDialog.Last!;

            // The view model's own "Starting the backup…", reported as the dialog went up.
            first = run.Message;
            firstIndeterminate = run.IsIndeterminate;

            progress?.Report(new BackupProgress(BackupPhase.Scanning, null, 0, 0, 0, 0));
            sawScan = run is { Message: "Scanning the inputs…", IsIndeterminate: true };

            progress?.Report(new BackupProgress(BackupPhase.Archiving, "/docs/a.txt", 1, 4, 512, 2048));
            sawArchiving = run is { CurrentItem: "/docs/a.txt", Percent: 25, IsIndeterminate: false }
                && run.Message.StartsWith("Archiving —", StringComparison.Ordinal);

            return Task.FromResult(Results.Backup());
        };

        await harness.BackupPage.BackupCommand.ExecuteAsync(null);

        Assert.Equal("Starting the backup…", first);
        Assert.True(firstIndeterminate);
        Assert.True(sawScan);
        Assert.True(sawArchiving);
    }

    [Fact]
    public async Task TheDialog_CancelStopsTheRunAndTheDialogShowsTheCancelledOutcome()
    {
        Harness harness = new();
        harness.FillEditor();

        string? messageAfterCancel = null;

        harness.Backups.Behaviour = (_, _, _, _, token) =>
        {
            RunViewModel run = harness.RunDialog.Last!;

            // Exactly what a click on the dialog's Cancel button does.
            run.CancelCommand.Execute(null);
            messageAfterCancel = run.Message;
            token.ThrowIfCancellationRequested();

            return Task.FromResult(Results.Backup());
        };

        await harness.BackupPage.BackupCommand.ExecuteAsync(null);

        RunViewModel run = Assert.Single(harness.RunDialog.Runs);

        Assert.Equal("Cancelling…", messageAfterCancel);
        Assert.False(run.IsRunning);
        Assert.Equal("The backup was cancelled; the partial archive was deleted.", run.Summary?.Headline);
        Assert.Equal("Finished, with something to report", run.DialogTitle);
    }

    [Fact]
    public async Task TheDialog_ReadsALeftOutEntryAsAWarning()
    {
        Harness harness = new();
        harness.FillEditor();
        harness.Backups.Behaviour = (_, _, _, _, _) => Task.FromResult(Results.Backup(
            skipped: [new SkippedEntry("/secret", "'/secret' could not be read: access denied.")]));

        await harness.BackupPage.BackupCommand.ExecuteAsync(null);

        RunViewModel run = Assert.Single(harness.RunDialog.Runs);

        Assert.True(run.Summary?.IsWarning);
        Assert.Equal("Finished, with something to report", run.DialogTitle);
    }

    [Fact]
    public async Task TheDialog_ReadsAMismatchAsAFailure()
    {
        Harness harness = new();
        harness.FilePicker.Archives.Enqueue("/backups/x.tar.zst");
        harness.Verifications.Behaviour = (archive, _, _, _) => Task.FromResult(
            Results.Verification(archive, VerificationOutcome.HashMismatch, "It does not match its checksum."));

        await harness.VerifyRestorePage.VerifyCommand.ExecuteAsync(null);

        RunViewModel run = Assert.Single(harness.RunDialog.Runs);

        Assert.Equal("Checking the archive", run.Title);
        Assert.Equal("Something went wrong", run.DialogTitle);
        Assert.Equal("It does not match its checksum.", run.Summary?.Headline);
    }

    [Fact]
    public async Task EachRun_GetsADialogOfItsOwn()
    {
        Harness harness = new();
        harness.FillEditor();

        await harness.BackupPage.BackupCommand.ExecuteAsync(null);
        await harness.BackupPage.BackupCommand.ExecuteAsync(null);

        Assert.Equal(2, harness.RunDialog.Runs.Count);
        Assert.NotSame(harness.RunDialog.Runs[0], harness.RunDialog.Runs[1]);
        Assert.All(harness.RunDialog.Runs, static run => Assert.False(run.IsRunning));
    }

    [Fact]
    public async Task ARestore_GetsItsOwnDialogHeading()
    {
        Harness harness = new();
        harness.FilePicker.Archives.Enqueue("/backups/x.tar.zst");
        harness.FilePicker.Folders.Enqueue("/restored");

        await harness.VerifyRestorePage.RestoreCommand.ExecuteAsync(null);

        RunViewModel run = Assert.Single(harness.RunDialog.Runs);

        Assert.Equal("Restoring", run.Title);
        Assert.True(run.Summary?.IsSuccess);
    }

    [Fact]
    public async Task AnAbort_IsFinishedWithTheFailureItStoppedOn()
    {
        Harness harness = new();
        harness.FillEditor();

        BackupError raised = new(BackupErrorKind.ReadFailed, "/docs/a.txt", "'/docs/a.txt' could not be read: it is in use.");
        harness.Backups.Behaviour = (_, _, _, _, _) => throw new BackupAbortedException(raised);

        await harness.BackupPage.BackupCommand.ExecuteAsync(null);

        RunViewModel run = Assert.Single(harness.RunDialog.Runs);

        Assert.False(run.IsRunning);
        Assert.Contains(raised.Message, run.Summary?.Headline, StringComparison.Ordinal);
    }
}
