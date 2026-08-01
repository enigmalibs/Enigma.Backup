using Enigma.Backup.Desktop.Services;
using Enigma.Backup.Desktop.ViewModels;
using Xunit;

namespace Enigma.Backup.Desktop.Tests.ViewModels;

public sealed class PasswordDialogViewModelTests
{
    [Fact]
    public void ANewArchivesPasswordIsTypedTwice()
    {
        PasswordDialogViewModel dialog = new(PasswordPurpose.ProtectNewArchive, "Documents");

        Assert.True(dialog.RequiresConfirmation);
        Assert.Equal("Choose a password", dialog.Title);
        Assert.Contains("Documents", dialog.Prompt, System.StringComparison.Ordinal);
    }

    [Fact]
    public void AnExistingArchivesPasswordIsTypedOnce()
    {
        PasswordDialogViewModel dialog = new(PasswordPurpose.OpenArchive, "Documents_2026-08-01.tar.zst.enc");

        Assert.False(dialog.RequiresConfirmation);
        Assert.Equal("Password required", dialog.Title);
    }

    [Fact]
    public void AnEmptyPasswordCannotBeAccepted_AndIsNotCalledAnError()
    {
        PasswordDialogViewModel dialog = new(PasswordPurpose.OpenArchive, "archive");

        Assert.False(dialog.CanAccept);
        Assert.Null(dialog.ValidationMessage);
        Assert.False(dialog.AcceptCommand.CanExecute(null));
    }

    [Fact]
    public void OpeningAnArchiveNeedsNothingBeyondAPassword()
    {
        PasswordDialogViewModel dialog = new(PasswordPurpose.OpenArchive, "archive")
        {
            Password = "s3cret",
        };

        Assert.True(dialog.CanAccept);
        Assert.Null(dialog.ValidationMessage);
    }

    [Fact]
    public void TwoDifferentPasswordsAreRefused_ButOnlyOnceTheSecondHasBeenStarted()
    {
        PasswordDialogViewModel dialog = new(PasswordPurpose.ProtectNewArchive, "Documents")
        {
            Password = "s3cret",
        };

        Assert.False(dialog.CanAccept);
        Assert.Null(dialog.ValidationMessage);

        dialog.Confirmation = "s3";

        Assert.False(dialog.CanAccept);
        Assert.Equal("The two passwords do not match.", dialog.ValidationMessage);

        dialog.Confirmation = "s3cret";

        Assert.True(dialog.CanAccept);
        Assert.Null(dialog.ValidationMessage);
    }

    [Fact]
    public void APasswordOfSpacesIsAPassword_MatchingWhatTheCoreAccepts()
    {
        PasswordDialogViewModel dialog = new(PasswordPurpose.ProtectNewArchive, "Documents")
        {
            Password = "   ",
            Confirmation = "   ",
        };

        Assert.True(dialog.CanAccept);
    }

    [Fact]
    public void Accepting_HandsBackThePasswordAndClosesTheDialog()
    {
        PasswordDialogViewModel dialog = new(PasswordPurpose.OpenArchive, "archive")
        {
            Password = "s3cret",
        };

        int closes = 0;
        dialog.CloseRequested += (_, _) => closes++;

        dialog.AcceptCommand.Execute(null);

        Assert.Equal("s3cret", dialog.Result);
        Assert.Equal(1, closes);
    }

    [Fact]
    public void Cancelling_HandsBackNothing()
    {
        PasswordDialogViewModel dialog = new(PasswordPurpose.OpenArchive, "archive")
        {
            Password = "s3cret",
        };

        int closes = 0;
        dialog.CloseRequested += (_, _) => closes++;

        dialog.CancelCommand.Execute(null);

        Assert.Null(dialog.Result);
        Assert.Equal(1, closes);
    }

    [Fact]
    public void ADialogNobodyAnsweredHandsBackNothing()
    {
        PasswordDialogViewModel dialog = new(PasswordPurpose.OpenArchive, "archive");

        Assert.Null(dialog.Result);
    }
}
