using System;
using System.Threading.Tasks;
using Enigma.Backup.Core.Backups;
using Enigma.Backup.Core.Encryption;
using Enigma.Backup.Core.Errors;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Core.Restoring;
using Enigma.Backup.Core.Verification;
using Enigma.Backup.Desktop.Services;
using Enigma.Backup.Desktop.Tests.TestSupport;
using Enigma.Backup.Desktop.ViewModels;
using Xunit;

namespace Enigma.Backup.Desktop.Tests.ViewModels;

/// <summary>Backing up: what reaches the core, and what comes back.</summary>
public sealed class BackupPageViewModelRunTests
{
    [Fact]
    public void Backup_IsDisabledUntilThereIsSomethingToBackUpAndSomewhereToPutIt()
    {
        Harness harness = new();

        Assert.False(harness.BackupPage.BackupCommand.CanExecute(null));

        harness.BackupEditor.AddInputs(["/home/jo/Documents"]);
        Assert.False(harness.BackupPage.BackupCommand.CanExecute(null));

        harness.BackupEditor.OutputDirectory = "/backups";
        Assert.True(harness.BackupPage.BackupCommand.CanExecute(null));

        harness.BackupEditor.ClearInputsCommand.Execute(null);
        Assert.False(harness.BackupPage.BackupCommand.CanExecute(null));
    }

    [Fact]
    public async Task Backup_HandsTheCoreExactlyWhatThePageShows()
    {
        Harness harness = new();
        harness.BackupEditor.AddInputs(["/docs"]);
        harness.BackupEditor.OutputDirectory = "/backups";
        harness.BackupEditor.SelectedCompression = CompressionOption.For(CompressionAlgorithm.Gzip);

        await harness.BackupPage.BackupCommand.ExecuteAsync(null);

        BackupProfile sent = Assert.IsType<BackupProfile>(harness.Backups.LastProfile);

        Assert.Equal(BackupPageViewModel.OneOffName, sent.Name);
        Assert.Equal(CompressionAlgorithm.Gzip, sent.Compression);
        Assert.Equal("/docs", Assert.Single(sent.Inputs));
        Assert.Null(harness.Backups.LastPassword);
    }

    [Fact]
    public async Task Backup_NamesAOneOffArchiveBackup()
    {
        Harness harness = new();
        harness.FillEditor();

        await harness.BackupPage.BackupCommand.ExecuteAsync(null);

        Assert.Equal("Backup", harness.Backups.LastProfile?.Name);
    }

    [Fact]
    public async Task Backup_AsksForAPasswordTwiceOverWhenItEncrypts()
    {
        Harness harness = new();
        harness.FillEditor(encrypt: true);
        harness.Dialogs.Passwords.Enqueue("s3cret");

        await harness.BackupPage.BackupCommand.ExecuteAsync(null);

        (PasswordPurpose purpose, string subject) = Assert.Single(harness.Dialogs.PasswordRequests);

        Assert.Equal(PasswordPurpose.ProtectNewArchive, purpose);
        Assert.Equal(BackupPageViewModel.OneOffName, subject);
        Assert.Equal("s3cret", harness.Backups.LastPassword);
    }

    [Fact]
    public async Task Backup_DoesNotStartWhenThePasswordPromptIsCancelled()
    {
        Harness harness = new();
        harness.FillEditor(encrypt: true);

        await harness.BackupPage.BackupCommand.ExecuteAsync(null);

        Assert.Equal(0, harness.Backups.Runs);
        Assert.Empty(harness.RunDialog.Runs);
        Assert.Equal("The backup was not started — no password was given.", harness.BackupPage.StatusText);
    }

    [Fact]
    public async Task Backup_RoutesAFailureThroughTheErrorDialogAndCarriesTheAnswerBack()
    {
        Harness harness = new();
        harness.FillEditor();
        harness.Dialogs.ErrorDecisions.Enqueue(BackupErrorDecision.Skip(applyToAll: true));

        BackupErrorDecision? answered = null;
        BackupError raised = new(BackupErrorKind.AccessDenied, "/secret", "'/secret' could not be read: access denied.");

        harness.Backups.Behaviour = async (_, _, errors, _, token) =>
        {
            answered = await errors.HandleAsync(raised, token);

            return Results.Backup(skipped: [new SkippedEntry("/secret", raised.Message)]);
        };

        await harness.BackupPage.BackupCommand.ExecuteAsync(null);

        Assert.Same(raised, Assert.Single(harness.Dialogs.ErrorRequests));
        Assert.Equal(BackupErrorAction.Skip, answered?.Action);
        Assert.True(answered?.ApplyToAll);

        RunSummary summary = Assert.IsType<RunSummary>(harness.RunDialog.LastSummary);
        Assert.True(summary.IsWarning);
        Assert.Equal(raised.Message, Assert.Single(summary.Skipped));
    }

    [Fact]
    public async Task Backup_FillsTheSummaryFromWhatTheCoreReported()
    {
        Harness harness = new();
        harness.FillEditor();
        harness.Backups.Behaviour = (_, _, _, _, _) => Task.FromResult(Results.Backup(
            outputPath: "/backups/Documents_2026-08-01_120000.tar.zst",
            fileCount: 12,
            directoryCount: 3));

        await harness.BackupPage.BackupCommand.ExecuteAsync(null);

        RunSummary summary = Assert.IsType<RunSummary>(harness.RunDialog.LastSummary);

        Assert.True(summary.IsSuccess);
        Assert.Equal("Backed up 12 files to 'Documents_2026-08-01_120000.tar.zst'.", summary.Headline);
        Assert.Contains(summary.Details, detail => detail is { Label: "Archive", Value: "/backups/Documents_2026-08-01_120000.tar.zst" });
        Assert.False(summary.HasSkipped);
    }

    [Fact]
    public async Task Backup_ShowsProgressWhileItRunsAndFinishesTheRunAfterwards()
    {
        Harness harness = new();
        harness.FillEditor();

        bool indeterminateWhileScanning = false;
        bool busyWhileRunning = false;
        bool backupBlockedWhileRunning = true;
        double percentWhileArchiving = 0;
        string? itemWhileArchiving = null;

        harness.Backups.Behaviour = (_, _, _, progress, _) =>
        {
            RunViewModel run = harness.RunDialog.Last!;

            progress?.Report(new BackupProgress(BackupPhase.Scanning, null, 0, 0, 0, 0));
            indeterminateWhileScanning = run.IsIndeterminate;

            progress?.Report(new BackupProgress(BackupPhase.Archiving, "/docs/a.txt", 1, 4, 512, 2048));
            percentWhileArchiving = run.Percent;
            itemWhileArchiving = run.CurrentItem;
            busyWhileRunning = harness.BackupPage.IsBusy && run.IsRunning;
            backupBlockedWhileRunning = !harness.BackupPage.BackupCommand.CanExecute(null);

            return Task.FromResult(Results.Backup());
        };

        await harness.BackupPage.BackupCommand.ExecuteAsync(null);

        Assert.True(indeterminateWhileScanning);
        Assert.True(busyWhileRunning);
        Assert.True(backupBlockedWhileRunning);
        Assert.Equal(25, percentWhileArchiving);
        Assert.Equal("/docs/a.txt", itemWhileArchiving);
        Assert.True(harness.Dispatcher.PostCount >= 2);

        RunViewModel finished = Assert.Single(harness.RunDialog.Runs);

        Assert.False(harness.BackupPage.IsBusy);
        Assert.False(finished.IsRunning);
        Assert.False(finished.IsIndeterminate);
        Assert.Null(finished.CurrentItem);
        Assert.True(harness.BackupPage.BackupCommand.CanExecute(null));
    }

    [Fact]
    public async Task Backup_CancelStopsTheRunAndSaysThePartialArchiveWasDeleted()
    {
        Harness harness = new();
        harness.FillEditor();

        harness.Backups.Behaviour = (_, _, _, _, token) =>
        {
            harness.RunDialog.Last!.CancelCommand.Execute(null);
            token.ThrowIfCancellationRequested();

            return Task.FromResult(Results.Backup());
        };

        await harness.BackupPage.BackupCommand.ExecuteAsync(null);

        RunSummary summary = Assert.IsType<RunSummary>(harness.RunDialog.LastSummary);

        Assert.True(summary.IsWarning);
        Assert.Equal("The backup was cancelled; the partial archive was deleted.", summary.Headline);
        Assert.False(harness.BackupPage.IsBusy);
    }

    [Fact]
    public async Task Backup_AnAbortReportsTheFailureItStoppedOn()
    {
        Harness harness = new();
        harness.FillEditor();

        BackupError raised = new(BackupErrorKind.ReadFailed, "/docs/a.txt", "'/docs/a.txt' could not be read: it is in use.");
        harness.Backups.Behaviour = (_, _, _, _, _) => throw new BackupAbortedException(raised);

        await harness.BackupPage.BackupCommand.ExecuteAsync(null);

        RunSummary summary = Assert.IsType<RunSummary>(harness.RunDialog.LastSummary);

        Assert.True(summary.IsWarning);
        Assert.Contains(raised.Message, summary.Headline, StringComparison.Ordinal);
        Assert.DoesNotContain("The run was aborted:", summary.Headline, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Backup_AnUnexpectedFailureBecomesTheSummaryRatherThanCrashingTheWindow()
    {
        Harness harness = new();
        harness.FillEditor();
        harness.Backups.Behaviour = (_, _, _, _, _) => throw new InvalidOperationException("The output drive vanished.");

        await harness.BackupPage.BackupCommand.ExecuteAsync(null);

        RunSummary summary = Assert.IsType<RunSummary>(harness.RunDialog.LastSummary);

        Assert.True(summary.IsFailure);
        Assert.Equal("The backup failed: The output drive vanished.", summary.Headline);
        Assert.False(harness.BackupPage.IsBusy);
    }

    [Fact]
    public void Backup_SaysWhatItStillNeeds()
    {
        Harness harness = new();

        Assert.Equal("Add something to back up, and choose where to save the archive.", harness.BackupPage.MissingForBackup);

        harness.BackupEditor.AddInputs(["/home/jo/Documents"]);
        Assert.Equal("Choose where to save the archive.", harness.BackupPage.MissingForBackup);

        harness.BackupEditor.OutputDirectory = "/backups";
        Assert.Null(harness.BackupPage.MissingForBackup);

        harness.BackupEditor.ClearInputsCommand.Execute(null);
        Assert.Equal("Add a file, a folder or a pattern to back up.", harness.BackupPage.MissingForBackup);
    }

    [Fact]
    public void DroppedPaths_LandInTheOneOffInputsAndAreCounted()
    {
        Harness harness = new();

        harness.BackupPage.AddDroppedInputs(["/a", "/b"]);

        Assert.Equal(2, harness.BackupEditor.Inputs.Count);
        Assert.Equal("Added 2 inputs.", harness.BackupPage.StatusText);

        harness.BackupPage.AddDroppedInputs(["/a"]);

        Assert.Equal(2, harness.BackupEditor.Inputs.Count);
        Assert.Equal("Nothing was added — those paths are already in the list.", harness.BackupPage.StatusText);
    }
}
