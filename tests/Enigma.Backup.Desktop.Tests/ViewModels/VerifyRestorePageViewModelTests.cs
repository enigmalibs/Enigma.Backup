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

/// <summary>Restoring and verifying: what reaches the core, and what comes back.</summary>
public sealed class VerifyRestorePageViewModelTests
{
    [Fact]
    public async Task Restore_NeedsBothAnArchiveAndAFolder()
    {
        Harness harness = new();

        await harness.VerifyRestorePage.RestoreCommand.ExecuteAsync(null);
        Assert.Equal(0, harness.Restores.Attempts);

        harness.FilePicker.Archives.Enqueue("/backups/x.tar.zst");
        await harness.VerifyRestorePage.RestoreCommand.ExecuteAsync(null);
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

        await harness.VerifyRestorePage.RestoreCommand.ExecuteAsync(null);

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

        await harness.VerifyRestorePage.RestoreCommand.ExecuteAsync(null);

        (PasswordPurpose purpose, string subject) = Assert.Single(harness.Dialogs.PasswordRequests);

        Assert.Equal(PasswordPurpose.OpenArchive, purpose);
        Assert.Equal("x.tar.zst.enc", subject);
        Assert.Equal(2, harness.Restores.Attempts);
        Assert.Equal("s3cret", harness.Restores.LastPassword);
        Assert.True(harness.RunDialog.LastSummary?.IsSuccess);
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

        await harness.VerifyRestorePage.RestoreCommand.ExecuteAsync(null);

        RunSummary summary = Assert.IsType<RunSummary>(harness.RunDialog.LastSummary);

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

        await harness.VerifyRestorePage.RestoreCommand.ExecuteAsync(null);

        RunSummary summary = Assert.IsType<RunSummary>(harness.RunDialog.LastSummary);

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

        await harness.VerifyRestorePage.RestoreCommand.ExecuteAsync(null);

        RunSummary summary = Assert.IsType<RunSummary>(harness.RunDialog.LastSummary);

        Assert.True(summary.IsWarning);
        Assert.Equal("The restore was stopped after 4 files; what had already been written was kept.", summary.Headline);
    }

    [Fact]
    public async Task Verify_ChecksWithoutAPasswordFirst()
    {
        Harness harness = new();
        harness.FilePicker.Archives.Enqueue("/backups/x.tar.zst");

        await harness.VerifyRestorePage.VerifyCommand.ExecuteAsync(null);

        Assert.Equal(1, harness.Verifications.Attempts);
        Assert.Null(harness.Verifications.LastPassword);
        Assert.True(harness.RunDialog.LastSummary?.IsSuccess);
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

        await harness.VerifyRestorePage.VerifyCommand.ExecuteAsync(null);

        RunSummary summary = Assert.IsType<RunSummary>(harness.RunDialog.LastSummary);

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

        await harness.VerifyRestorePage.VerifyCommand.ExecuteAsync(null);

        RunSummary summary = Assert.IsType<RunSummary>(harness.RunDialog.LastSummary);

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

        await harness.VerifyRestorePage.VerifyCommand.ExecuteAsync(null);

        Assert.True(harness.RunDialog.LastSummary?.IsFailure);
    }
}
