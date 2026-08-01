using Enigma.Backup.Desktop.Services;
using Enigma.Backup.Desktop.ViewModels;
using Xunit;

namespace Enigma.Backup.Desktop.Tests.ViewModels;

public sealed class ProfileNameDialogViewModelTests
{
    [Fact]
    public void AnEmptyBoxCannotBeAccepted_AndIsNotCalledAnError()
    {
        ProfileNameDialogViewModel dialog = Dialog();

        Assert.False(dialog.CanAccept);
        Assert.Null(dialog.ValidationMessage);
    }

    [Fact]
    public void AUsableNameIsAccepted()
    {
        ProfileNameDialogViewModel dialog = Dialog();

        dialog.Name = "Documents";

        Assert.True(dialog.CanAccept);
        Assert.Null(dialog.ValidationMessage);
    }

    [Theory]
    [InlineData("bad/name")]
    [InlineData("bad\\name")]
    [InlineData("bad:name")]
    [InlineData("bad?name")]
    [InlineData("trailing.")]
    public void ANameThatCouldNotBeAFileIsRefused(string name)
    {
        ProfileNameDialogViewModel dialog = Dialog();

        dialog.Name = name;

        Assert.False(dialog.CanAccept);
        Assert.NotNull(dialog.ValidationMessage);
    }

    [Fact]
    public void ATakenNameIsRefused_WhateverItsCase()
    {
        ProfileNameDialogViewModel dialog = Dialog(taken: ["Documents"]);

        dialog.Name = "documents";

        Assert.False(dialog.CanAccept);
        Assert.Equal("A profile named 'documents' already exists.", dialog.ValidationMessage);
    }

    [Fact]
    public void SurroundingSpacesAreTrimmedRatherThanRefused()
    {
        ProfileNameDialogViewModel dialog = Dialog();

        dialog.Name = "  Documents  ";

        Assert.True(dialog.CanAccept);

        dialog.AcceptCommand.Execute(null);

        Assert.Equal("Documents", dialog.Result);
    }

    [Fact]
    public void TheInitialNameIsCheckedStraightAway()
    {
        ProfileNameDialogViewModel accepted = Dialog(initial: "Photos", taken: ["Documents"]);
        ProfileNameDialogViewModel refused = Dialog(initial: "Documents", taken: ["Documents"]);

        Assert.True(accepted.CanAccept);
        Assert.False(refused.CanAccept);
        Assert.NotNull(refused.ValidationMessage);
    }

    [Fact]
    public void Cancelling_HandsBackNothing()
    {
        ProfileNameDialogViewModel dialog = Dialog(initial: "Documents");

        int closes = 0;
        dialog.CloseRequested += (_, _) => closes++;

        dialog.CancelCommand.Execute(null);

        Assert.Null(dialog.Result);
        Assert.Equal(1, closes);
    }

    private static ProfileNameDialogViewModel Dialog(string initial = "", string[]? taken = null) =>
        new(new ProfileNameRequest("Save the profile", "Give the profile a name.", initial, taken ?? []));
}
