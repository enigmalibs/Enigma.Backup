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

/// <summary>Backing up, restoring and verifying: what reaches the core, and what comes back.</summary>
public sealed class MainWindowViewModelRunTests
{
    [Fact]
    public void Backup_IsDisabledUntilThereIsSomethingToBackUpAndSomewhereToPutIt()
    {
        Harness harness = new();

        Assert.False(harness.ViewModel.BackupCommand.CanExecute(null));

        harness.Editor.AddInputs(["/home/jo/Documents"]);
        Assert.False(harness.ViewModel.BackupCommand.CanExecute(null));

        harness.Editor.OutputDirectory = "/backups";
        Assert.True(harness.ViewModel.BackupCommand.CanExecute(null));

        harness.Editor.ClearInputsCommand.Execute(null);
        Assert.False(harness.ViewModel.BackupCommand.CanExecute(null));
    }

    [Fact]
    public async Task Backup_HandsTheCoreExactlyWhatTheEditorShows()
    {
        Harness harness = new();
        harness.Profiles.Seed(Harness.Profile("Documents", inputs: ["/docs"]));
        await harness.ViewModel.RefreshProfilesCommand.ExecuteAsync(null);

        harness.Editor.SelectedCompression = CompressionOption.For(CompressionAlgorithm.Gzip);
        await harness.ViewModel.BackupCommand.ExecuteAsync(null);

        BackupProfile sent = Assert.IsType<BackupProfile>(harness.Backups.LastProfile);

        Assert.Equal("Documents", sent.Name);
        Assert.Equal(CompressionAlgorithm.Gzip, sent.Compression);
        Assert.Equal("/docs", Assert.Single(sent.Inputs));
        Assert.Null(harness.Backups.LastPassword);
    }

    [Fact]
    public async Task Backup_NamesTheArchiveAfterNothingInParticularWhenTheProfileIsUnsaved()
    {
        Harness harness = new();
        harness.FillEditor();

        await harness.ViewModel.BackupCommand.ExecuteAsync(null);

        Assert.Equal("Backup", harness.Backups.LastProfile?.Name);
    }

    [Fact]
    public async Task Backup_AsksForAPasswordTwiceOverWhenTheProfileEncrypts()
    {
        Harness harness = new();
        harness.Profiles.Seed(Harness.Profile("Documents", encrypt: true));
        await harness.ViewModel.RefreshProfilesCommand.ExecuteAsync(null);
        harness.Dialogs.Passwords.Enqueue("s3cret");

        await harness.ViewModel.BackupCommand.ExecuteAsync(null);

        (PasswordPurpose purpose, string subject) = Assert.Single(harness.Dialogs.PasswordRequests);

        Assert.Equal(PasswordPurpose.ProtectNewArchive, purpose);
        Assert.Equal("Documents", subject);
        Assert.Equal("s3cret", harness.Backups.LastPassword);
    }

    [Fact]
    public async Task Backup_DoesNotStartWhenThePasswordPromptIsCancelled()
    {
        Harness harness = new();
        harness.FillEditor(encrypt: true);

        await harness.ViewModel.BackupCommand.ExecuteAsync(null);

        Assert.Equal(0, harness.Backups.Runs);
        Assert.Null(harness.ViewModel.Summary);
        Assert.Equal("The backup was not started — no password was given.", harness.ViewModel.StatusText);
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

        await harness.ViewModel.BackupCommand.ExecuteAsync(null);

        Assert.Same(raised, Assert.Single(harness.Dialogs.ErrorRequests));
        Assert.Equal(BackupErrorAction.Skip, answered?.Action);
        Assert.True(answered?.ApplyToAll);

        RunSummary summary = Assert.IsType<RunSummary>(harness.ViewModel.Summary);
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

        await harness.ViewModel.BackupCommand.ExecuteAsync(null);

        RunSummary summary = Assert.IsType<RunSummary>(harness.ViewModel.Summary);

        Assert.True(summary.IsSuccess);
        Assert.Equal("Backed up 12 files to 'Documents_2026-08-01_120000.tar.zst'.", summary.Headline);
        Assert.Equal(summary.Headline, harness.ViewModel.StatusText);
        Assert.Contains(summary.Details, detail => detail is { Label: "Archive", Value: "/backups/Documents_2026-08-01_120000.tar.zst" });
        Assert.False(summary.HasSkipped);
    }

    [Fact]
    public async Task Backup_ShowsProgressWhileItRunsAndClearsItAfterwards()
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
            progress?.Report(new BackupProgress(BackupPhase.Scanning, null, 0, 0, 0, 0));
            indeterminateWhileScanning = harness.ViewModel.IsProgressIndeterminate;

            progress?.Report(new BackupProgress(BackupPhase.Archiving, "/docs/a.txt", 1, 4, 512, 2048));
            percentWhileArchiving = harness.ViewModel.ProgressPercent;
            itemWhileArchiving = harness.ViewModel.CurrentItem;
            busyWhileRunning = harness.ViewModel.IsBusy;
            backupBlockedWhileRunning = !harness.ViewModel.BackupCommand.CanExecute(null);

            return Task.FromResult(Results.Backup());
        };

        await harness.ViewModel.BackupCommand.ExecuteAsync(null);

        Assert.True(indeterminateWhileScanning);
        Assert.True(busyWhileRunning);
        Assert.True(backupBlockedWhileRunning);
        Assert.Equal(25, percentWhileArchiving);
        Assert.Equal("/docs/a.txt", itemWhileArchiving);
        Assert.True(harness.Dispatcher.PostCount >= 2);

        Assert.False(harness.ViewModel.IsBusy);
        Assert.False(harness.ViewModel.IsProgressIndeterminate);
        Assert.Null(harness.ViewModel.CurrentItem);
        Assert.True(harness.ViewModel.BackupCommand.CanExecute(null));
    }

    [Fact]
    public async Task Backup_CancelStopsTheRunAndSaysThePartialArchiveWasDeleted()
    {
        Harness harness = new();
        harness.FillEditor();

        harness.Backups.Behaviour = (_, _, _, _, token) =>
        {
            harness.ViewModel.CancelCommand.Execute(null);
            token.ThrowIfCancellationRequested();

            return Task.FromResult(Results.Backup());
        };

        await harness.ViewModel.BackupCommand.ExecuteAsync(null);

        RunSummary summary = Assert.IsType<RunSummary>(harness.ViewModel.Summary);

        Assert.True(summary.IsWarning);
        Assert.Equal("The backup was cancelled; the partial archive was deleted.", summary.Headline);
        Assert.False(harness.ViewModel.IsBusy);
    }

    [Fact]
    public async Task Backup_AnAbortReportsTheFailureItStoppedOn()
    {
        Harness harness = new();
        harness.FillEditor();

        BackupError raised = new(BackupErrorKind.ReadFailed, "/docs/a.txt", "'/docs/a.txt' could not be read: it is in use.");
        harness.Backups.Behaviour = (_, _, _, _, _) => throw new BackupAbortedException(raised);

        await harness.ViewModel.BackupCommand.ExecuteAsync(null);

        RunSummary summary = Assert.IsType<RunSummary>(harness.ViewModel.Summary);

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

        await harness.ViewModel.BackupCommand.ExecuteAsync(null);

        RunSummary summary = Assert.IsType<RunSummary>(harness.ViewModel.Summary);

        Assert.True(summary.IsFailure);
        Assert.Equal("The backup failed: The output drive vanished.", summary.Headline);
        Assert.Equal(0, harness.ViewModel.ProgressPercent);
        Assert.False(harness.ViewModel.IsBusy);
    }

    [Fact]
    public async Task Restore_NeedsBothAnArchiveAndAFolder()
    {
        Harness harness = new();

        await harness.ViewModel.RestoreCommand.ExecuteAsync(null);
        Assert.Equal(0, harness.Restores.Attempts);

        harness.FilePicker.Archives.Enqueue("/backups/x.tar.zst");
        await harness.ViewModel.RestoreCommand.ExecuteAsync(null);
        Assert.Equal(0, harness.Restores.Attempts);
    }

    [Fact]
    public async Task Restore_RoutesAConflictThroughTheDialog()
    {
        Harness harness = new();
        harness.FilePicker.Archives.Enqueue("/backups/x.tar.zst");
        harness.FilePicker.Folders.Enqueue("/restored");
        harness.Dialogs.ConflictDecisions.Enqueue(RestoreConflictDecision.Overwrite(applyToAll: true));

        RestoreConflictDecision? answered = null;
        RestoreConflict conflict = new("docs/a.txt", "/restored/docs/a.txt", 10, DateTimeOffset.UnixEpoch, 20, DateTimeOffset.UnixEpoch);

        harness.Restores.Behaviour = async (archive, destination, _, conflicts, _, token) =>
        {
            answered = await conflicts.HandleAsync(conflict, token);

            return Results.Restore(archive, destination);
        };

        await harness.ViewModel.RestoreCommand.ExecuteAsync(null);

        Assert.Same(conflict, Assert.Single(harness.Dialogs.ConflictRequests));
        Assert.Equal(RestoreConflictAction.Overwrite, answered?.Action);
        Assert.True(answered?.ApplyToAll);
    }

    [Fact]
    public async Task Restore_AsksForThePasswordOnlyWhenTheArchiveTurnsOutToBeEncrypted()
    {
        Harness harness = new();
        harness.FilePicker.Archives.Enqueue("/backups/x.tar.zst.enc");
        harness.FilePicker.Folders.Enqueue("/restored");
        harness.Dialogs.Passwords.Enqueue("s3cret");

        harness.Restores.Behaviour = (archive, destination, password, _, _, _) => password is null
            ? throw new EncryptedArchiveException(
                EncryptedArchiveProblem.PasswordRequired,
                "'x.tar.zst.enc' is encrypted, so its password is needed to restore it.")
            : Task.FromResult(Results.Restore(archive, destination));

        await harness.ViewModel.RestoreCommand.ExecuteAsync(null);

        (PasswordPurpose purpose, string subject) = Assert.Single(harness.Dialogs.PasswordRequests);

        Assert.Equal(PasswordPurpose.OpenArchive, purpose);
        Assert.Equal("x.tar.zst.enc", subject);
        Assert.Equal(2, harness.Restores.Attempts);
        Assert.Equal("s3cret", harness.Restores.LastPassword);
        Assert.True(harness.ViewModel.Summary?.IsSuccess);
    }

    [Fact]
    public async Task Restore_StopsWhenThePasswordPromptIsCancelled()
    {
        Harness harness = new();
        harness.FilePicker.Archives.Enqueue("/backups/x.tar.zst.enc");
        harness.FilePicker.Folders.Enqueue("/restored");

        harness.Restores.Behaviour = (_, _, _, _, _, _) => throw new EncryptedArchiveException(
            EncryptedArchiveProblem.PasswordRequired,
            "'x.tar.zst.enc' is encrypted, so its password is needed to restore it.");

        await harness.ViewModel.RestoreCommand.ExecuteAsync(null);

        RunSummary summary = Assert.IsType<RunSummary>(harness.ViewModel.Summary);

        Assert.Equal(1, harness.Restores.Attempts);
        Assert.Equal("The restore was cancelled.", summary.Headline);
    }

    [Fact]
    public async Task Restore_AWrongPasswordIsReportedRatherThanRetriedForever()
    {
        Harness harness = new();
        harness.FilePicker.Archives.Enqueue("/backups/x.tar.zst.enc");
        harness.FilePicker.Folders.Enqueue("/restored");
        harness.Dialogs.Passwords.Enqueue("wrong");

        harness.Restores.Behaviour = (_, _, password, _, _, _) => password is null
            ? throw new EncryptedArchiveException(EncryptedArchiveProblem.PasswordRequired, "A password is needed.")
            : throw new EncryptedArchiveException(
                EncryptedArchiveProblem.WrongPasswordOrCorrupted,
                "'x.tar.zst.enc' could not be opened: the password is wrong, or the file has been altered.");

        await harness.ViewModel.RestoreCommand.ExecuteAsync(null);

        RunSummary summary = Assert.IsType<RunSummary>(harness.ViewModel.Summary);

        Assert.Equal(2, harness.Restores.Attempts);
        Assert.True(summary.IsFailure);
        Assert.Contains("the password is wrong", summary.Headline, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Restore_AnAbortedRestoreSaysWhatWasKept()
    {
        Harness harness = new();
        harness.FilePicker.Archives.Enqueue("/backups/x.tar.zst");
        harness.FilePicker.Folders.Enqueue("/restored");
        harness.Restores.Behaviour = (archive, destination, _, _, _, _) =>
            Task.FromResult(Results.Restore(archive, destination, RestoreOutcome.Aborted, fileCount: 4));

        await harness.ViewModel.RestoreCommand.ExecuteAsync(null);

        RunSummary summary = Assert.IsType<RunSummary>(harness.ViewModel.Summary);

        Assert.True(summary.IsWarning);
        Assert.Equal("The restore was stopped after 4 files; what had already been written was kept.", summary.Headline);
    }

    [Fact]
    public async Task Verify_ChecksWithoutAPasswordFirst()
    {
        Harness harness = new();
        harness.FilePicker.Archives.Enqueue("/backups/x.tar.zst");

        await harness.ViewModel.VerifyCommand.ExecuteAsync(null);

        Assert.Equal(1, harness.Verifications.Attempts);
        Assert.Null(harness.Verifications.LastPassword);
        Assert.True(harness.ViewModel.Summary?.IsSuccess);
    }

    [Fact]
    public async Task Verify_AsksForThePasswordWhenOnlyHalfTheCheckCouldBeDone()
    {
        Harness harness = new();
        harness.FilePicker.Archives.Enqueue("/backups/x.tar.zst.enc");
        harness.Dialogs.Passwords.Enqueue("s3cret");

        harness.Verifications.Behaviour = (archive, password, _, _) => Task.FromResult(password is null
            ? Results.Verification(archive, VerificationOutcome.PasswordRequired, "It is encrypted; the password is needed.", isEncrypted: true)
            : Results.Verification(archive, VerificationOutcome.Succeeded, "Everything checks out.", isEncrypted: true, payloadAuthenticated: true));

        await harness.ViewModel.VerifyCommand.ExecuteAsync(null);

        RunSummary summary = Assert.IsType<RunSummary>(harness.ViewModel.Summary);

        Assert.Equal(2, harness.Verifications.Attempts);
        Assert.Equal("s3cret", harness.Verifications.LastPassword);
        Assert.True(summary.IsSuccess);
        Assert.Contains(summary.Details, detail => detail.Label == "Encrypted contents");
    }

    [Fact]
    public async Task Verify_ReportsThePartialAnswerWhenNoPasswordIsGiven()
    {
        Harness harness = new();
        harness.FilePicker.Archives.Enqueue("/backups/x.tar.zst.enc");

        harness.Verifications.Behaviour = (archive, _, _, _) => Task.FromResult(
            Results.Verification(archive, VerificationOutcome.PasswordRequired, "It is encrypted; the password is needed.", isEncrypted: true));

        await harness.ViewModel.VerifyCommand.ExecuteAsync(null);

        RunSummary summary = Assert.IsType<RunSummary>(harness.ViewModel.Summary);

        Assert.Equal(1, harness.Verifications.Attempts);
        Assert.True(summary.IsWarning);
        Assert.Equal("It is encrypted; the password is needed.", summary.Headline);
    }

    [Fact]
    public async Task Verify_AMismatchIsAFailure()
    {
        Harness harness = new();
        harness.FilePicker.Archives.Enqueue("/backups/x.tar.zst");
        harness.Verifications.Behaviour = (archive, _, _, _) => Task.FromResult(
            Results.Verification(archive, VerificationOutcome.HashMismatch, "It does not match its checksum."));

        await harness.ViewModel.VerifyCommand.ExecuteAsync(null);

        Assert.True(harness.ViewModel.Summary?.IsFailure);
    }
}
