using System;
using System.Threading.Tasks;
using Enigma.Backup.Core.Backups;
using Enigma.Backup.Core.Errors;
using Enigma.Backup.Core.Verification;
using Enigma.Backup.Desktop.Services;
using Enigma.Backup.Desktop.Tests.TestSupport;
using Xunit;

namespace Enigma.Backup.Desktop.Tests.ViewModels;

/// <summary>
/// The two seams a run reports itself through: the card on the overlay while it runs, and the banner
/// afterwards.
/// </summary>
/// <remarks>
/// The card is the whole progress readout now, and it is also the only place a run can be cancelled
/// from — so "it went up, it was told what was happening, it came down" is not cosmetic: a card that
/// is never taken down leaves a scrim over an app nobody can click.
/// </remarks>
public sealed class MainWindowViewModelOverlayTests
{
    [Fact]
    public async Task ARun_RaisesTheCardOnceAndTakesItDownOnce()
    {
        Harness harness = new();
        harness.FillEditor();

        await harness.ViewModel.BackupCommand.ExecuteAsync(null);

        Assert.Equal(1, harness.Overlay.Shows);
        Assert.Equal(1, harness.Overlay.Hides);
        Assert.False(harness.Overlay.IsShowing);
        Assert.Equal("Backing up", Assert.Single(harness.Overlay.Titles));
    }

    [Fact]
    public async Task ARun_ThatFailsUnexpectedlyStillTakesTheCardDown()
    {
        Harness harness = new();
        harness.FillEditor();
        harness.Backups.Behaviour = (_, _, _, _, _) => throw new InvalidOperationException("The output drive vanished.");

        await harness.ViewModel.BackupCommand.ExecuteAsync(null);

        Assert.Equal(1, harness.Overlay.Shows);
        Assert.Equal(1, harness.Overlay.Hides);
        Assert.False(harness.Overlay.IsShowing);
    }

    [Fact]
    public async Task ARun_ThatNeverStartsShowsNothingAtAll()
    {
        Harness harness = new();
        harness.FillEditor(encrypt: true);

        // No password is queued, so the prompt is cancelled and the backup does not begin.
        await harness.ViewModel.BackupCommand.ExecuteAsync(null);

        Assert.Equal(0, harness.Overlay.Shows);
        Assert.Empty(harness.Notifications.Shown);
    }

    [Fact]
    public async Task TheCard_IsToldEverythingTheCoreReported()
    {
        Harness harness = new();
        harness.FillEditor();

        harness.Backups.Behaviour = (_, _, _, progress, _) =>
        {
            progress?.Report(new BackupProgress(BackupPhase.Scanning, null, 0, 0, 0, 0));
            progress?.Report(new BackupProgress(BackupPhase.Archiving, "/docs/a.txt", 1, 4, 512, 2048));

            return Task.FromResult(Results.Backup());
        };

        await harness.ViewModel.BackupCommand.ExecuteAsync(null);

        // The first report is the view model's own "Starting the backup…", raised with the card.
        Assert.Equal(
            ("Starting the backup…", null, 0d, true),
            harness.Overlay.Reports[0]);

        Assert.Contains(harness.Overlay.Reports, report => report is { Message: "Scanning the inputs…", IsIndeterminate: true });
        Assert.Contains(
            harness.Overlay.Reports,
            report => report is { CurrentItem: "/docs/a.txt", Percent: 25, IsIndeterminate: false }
                && report.Message.StartsWith("Archiving —", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheCard_CarriesTheCancelCommandThatStopsTheRun()
    {
        Harness harness = new();
        harness.FillEditor();

        harness.Backups.Behaviour = (_, _, _, _, token) =>
        {
            // Exactly what a click on the card's Cancel button does.
            harness.Overlay.CancelCommand?.Execute(null);
            token.ThrowIfCancellationRequested();

            return Task.FromResult(Results.Backup());
        };

        await harness.ViewModel.BackupCommand.ExecuteAsync(null);

        Assert.Same(harness.ViewModel.CancelCommand, harness.Overlay.CancelCommand);
        Assert.Contains(harness.Overlay.Reports, report => report.Message == "Cancelling…");
        Assert.Equal("The backup was cancelled; the partial archive was deleted.", harness.ViewModel.Summary?.Headline);
    }

    [Fact]
    public async Task TheBanner_ReportsACleanRunAsASuccess()
    {
        Harness harness = new();
        harness.FillEditor();

        await harness.ViewModel.BackupCommand.ExecuteAsync(null);

        (string title, string message, NotificationSeverity severity) = Assert.Single(harness.Notifications.Shown);

        Assert.Equal("Done", title);
        Assert.Equal(harness.ViewModel.Summary?.Headline, message);
        Assert.Equal(NotificationSeverity.Success, severity);
    }

    [Fact]
    public async Task TheBanner_ReportsALeftOutEntryAsAWarning()
    {
        Harness harness = new();
        harness.FillEditor();
        harness.Backups.Behaviour = (_, _, _, _, _) => Task.FromResult(Results.Backup(
            skipped: [new SkippedEntry("/secret", "'/secret' could not be read: access denied.")]));

        await harness.ViewModel.BackupCommand.ExecuteAsync(null);

        Assert.Equal(
            ("Finished, with something to report", NotificationSeverity.Warning),
            (Assert.Single(harness.Notifications.Shown).Title, harness.Notifications.Shown[0].Severity));
    }

    [Fact]
    public async Task TheBanner_ReportsAFailureAsAnError()
    {
        Harness harness = new();
        harness.FilePicker.Archives.Enqueue("/backups/x.tar.zst");
        harness.Verifications.Behaviour = (archive, _, _, _) => Task.FromResult(
            Results.Verification(archive, VerificationOutcome.HashMismatch, "It does not match its checksum."));

        await harness.ViewModel.VerifyCommand.ExecuteAsync(null);

        (string title, string message, NotificationSeverity severity) = Assert.Single(harness.Notifications.Shown);

        Assert.Equal("Something went wrong", title);
        Assert.Equal("It does not match its checksum.", message);
        Assert.Equal(NotificationSeverity.Error, severity);
        Assert.Equal("Checking the archive", Assert.Single(harness.Overlay.Titles));
    }

    [Fact]
    public async Task TheBanner_OfThePreviousRunIsTakenAwayBeforeTheNextOneStarts()
    {
        Harness harness = new();
        harness.FillEditor();

        await harness.ViewModel.BackupCommand.ExecuteAsync(null);
        await harness.ViewModel.BackupCommand.ExecuteAsync(null);

        Assert.Equal(2, harness.Notifications.Hides);
        Assert.Equal(2, harness.Notifications.Shown.Count);
        Assert.Equal(2, harness.Overlay.Shows);
        Assert.Equal(2, harness.Overlay.Hides);
    }

    [Fact]
    public async Task ARestore_GetsItsOwnCardHeading()
    {
        Harness harness = new();
        harness.FilePicker.Archives.Enqueue("/backups/x.tar.zst");
        harness.FilePicker.Folders.Enqueue("/restored");

        await harness.ViewModel.RestoreCommand.ExecuteAsync(null);

        Assert.Equal("Restoring", Assert.Single(harness.Overlay.Titles));
        Assert.Equal(NotificationSeverity.Success, Assert.Single(harness.Notifications.Shown).Severity);
    }
}
