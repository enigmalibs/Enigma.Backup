using System;
using Enigma.Backup.Core.Errors;
using Enigma.Backup.Core.Restoring;
using Enigma.Backup.Desktop.Services;
using Enigma.Backup.Desktop.ViewModels;
using Xunit;

namespace Enigma.Backup.Desktop.Tests.ViewModels;

/// <summary>The three dialogs that answer with a decision rather than a value.</summary>
public sealed class DecisionDialogViewModelTests
{
    [Fact]
    public void AnErrorDialogNobodyAnsweredAborts_RatherThanQuietlyDroppingTheFile()
    {
        ErrorDecisionDialogViewModel dialog = new(Error());

        Assert.Equal(BackupErrorAction.Abort, dialog.Decision.Action);
    }

    [Theory]
    [InlineData(BackupErrorKind.MissingInput, "A path is missing")]
    [InlineData(BackupErrorKind.AccessDenied, "Access was denied")]
    [InlineData(BackupErrorKind.ReadFailed, "A file could not be read")]
    public void AnErrorDialogSaysWhichKindOfFailureItIs(BackupErrorKind kind, string expectedTitle)
    {
        ErrorDecisionDialogViewModel dialog = new(Error(kind));

        Assert.Equal(expectedTitle, dialog.Title);
        Assert.Equal("/secret", dialog.Path);
    }

    [Fact]
    public void SkipCanBeMadeToApplyToEveryFurtherError()
    {
        ErrorDecisionDialogViewModel dialog = new(Error()) { ApplyToAll = true };

        int closes = 0;
        dialog.CloseRequested += (_, _) => closes++;

        dialog.SkipCommand.Execute(null);

        Assert.Equal(BackupErrorAction.Skip, dialog.Decision.Action);
        Assert.True(dialog.Decision.ApplyToAll);
        Assert.Equal(1, closes);
    }

    [Fact]
    public void RetryIsNeverMadeSticky_BecauseAPermanentFailureWouldLoop()
    {
        ErrorDecisionDialogViewModel dialog = new(Error()) { ApplyToAll = true };

        dialog.RetryCommand.Execute(null);

        Assert.Equal(BackupErrorAction.Retry, dialog.Decision.Action);
        Assert.False(dialog.Decision.ApplyToAll);
    }

    [Fact]
    public void AbortCarriesTheTickedBoxLikeAnyOtherAnswer()
    {
        ErrorDecisionDialogViewModel dialog = new(Error()) { ApplyToAll = true };

        dialog.AbortCommand.Execute(null);

        Assert.Equal(BackupErrorAction.Abort, dialog.Decision.Action);
        Assert.True(dialog.Decision.ApplyToAll);
    }

    [Fact]
    public void AConflictDialogNobodyAnsweredAborts_RatherThanOverwritingOrSkipping()
    {
        RestoreConflictDialogViewModel dialog = new(Conflict());

        Assert.Equal(RestoreConflictAction.Abort, dialog.Decision.Action);
    }

    [Fact]
    public void AConflictDialogDescribesBothCopies()
    {
        RestoreConflictDialogViewModel dialog = new(Conflict());

        Assert.Equal("2 KiB · 2026-01-01 12:00", dialog.ArchiveDescription);
        Assert.Equal("1 KiB · 2026-02-02 08:30", dialog.ExistingDescription);
        Assert.Equal("docs/a.txt", dialog.ArchivePath);
        Assert.Equal("/restored/docs/a.txt", dialog.DestinationPath);
    }

    [Fact]
    public void OverwriteCanBeMadeToApplyToEveryFurtherFile()
    {
        RestoreConflictDialogViewModel dialog = new(Conflict()) { ApplyToAll = true };

        dialog.OverwriteCommand.Execute(null);

        Assert.Equal(RestoreConflictAction.Overwrite, dialog.Decision.Action);
        Assert.True(dialog.Decision.ApplyToAll);
    }

    [Fact]
    public void SkippingAConflictKeepsTheFileOnDisk()
    {
        RestoreConflictDialogViewModel dialog = new(Conflict());

        dialog.SkipCommand.Execute(null);

        Assert.Equal(RestoreConflictAction.Skip, dialog.Decision.Action);
        Assert.False(dialog.Decision.ApplyToAll);
    }

    [Fact]
    public void APlainMessageHasOneButtonAndNothingToDecide()
    {
        MessageDialogViewModel dialog = new("Profiles", "The folder could not be read.", MessageSeverity.Error);

        Assert.False(dialog.IsConfirmation);
        Assert.Equal("OK", dialog.ConfirmLabel);
        Assert.True(dialog.IsError);
        Assert.False(dialog.IsWarning);
        Assert.False(dialog.Confirmed);
    }

    [Fact]
    public void AConfirmationNobodyAnsweredDoesNotGoAhead()
    {
        MessageDialogViewModel dialog = new("Delete the profile", "Delete 'Documents'?", MessageSeverity.Warning, "Delete");

        Assert.True(dialog.IsConfirmation);
        Assert.Equal("Delete", dialog.ConfirmLabel);
        Assert.True(dialog.IsWarning);
        Assert.False(dialog.Confirmed);

        dialog.CancelCommand.Execute(null);
        Assert.False(dialog.Confirmed);
    }

    [Fact]
    public void ConfirmingSaysSo()
    {
        MessageDialogViewModel dialog = new("Delete the profile", "Delete 'Documents'?", MessageSeverity.Warning, "Delete");

        int closes = 0;
        dialog.CloseRequested += (_, _) => closes++;

        dialog.ConfirmCommand.Execute(null);

        Assert.True(dialog.Confirmed);
        Assert.Equal(1, closes);
    }

    private static BackupError Error(BackupErrorKind kind = BackupErrorKind.AccessDenied) =>
        new(kind, "/secret", "'/secret' could not be read: access denied.");

    // Built in local time, so the expected strings hold in whatever time zone the tests run in.
    private static RestoreConflict Conflict() =>
        new(
            "docs/a.txt",
            "/restored/docs/a.txt",
            2048,
            new DateTimeOffset(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Local)),
            1024,
            new DateTimeOffset(new DateTime(2026, 2, 2, 8, 30, 0, DateTimeKind.Local)));
}
