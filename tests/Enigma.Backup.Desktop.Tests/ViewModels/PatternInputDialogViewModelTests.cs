using System.Threading.Tasks;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Desktop.Tests.TestSupport;
using Enigma.Backup.Desktop.ViewModels;
using Xunit;

namespace Enigma.Backup.Desktop.Tests.ViewModels;

public sealed class PatternInputDialogViewModelTests
{
    [Fact]
    public void AnEmptyPromptCannotBeAccepted_AndIsNotCalledAnError()
    {
        PatternInputDialogViewModel dialog = Dialog();

        Assert.False(dialog.CanAccept);
        Assert.Null(dialog.ValidationMessage);
        Assert.Equal(string.Empty, dialog.BaseDirectory);
    }

    [Fact]
    public async Task AFolderAndAPatternAreAccepted()
    {
        FakeFilePicker picker = new();
        picker.Folders.Enqueue("/var/log");
        PatternInputDialogViewModel dialog = new(picker);

        await dialog.ChooseBaseDirectoryCommand.ExecuteAsync(null);
        dialog.Pattern = "*.log";

        Assert.Equal("/var/log", dialog.BaseDirectory);
        Assert.True(dialog.CanAccept);
        Assert.Null(dialog.ValidationMessage);
    }

    [Fact]
    public void APatternWithoutAFolder_SaysWhatIsMissing()
    {
        PatternInputDialogViewModel dialog = Dialog();

        dialog.Pattern = "*.log";

        Assert.False(dialog.CanAccept);
        Assert.NotNull(dialog.ValidationMessage);
    }

    [Fact]
    public async Task AFolderWithoutAPattern_IsNotYetAnError()
    {
        PatternInputDialogViewModel dialog = await DialogWithFolderAsync("/var/log");

        Assert.False(dialog.CanAccept);
        Assert.Null(dialog.ValidationMessage);
    }

    [Theory]
    [InlineData("/absolute/*.log")]
    [InlineData(@"\absolute\*.log")]
    [InlineData(@"C:\logs\*.log")]
    public async Task ARootedPatternIsRefused_WhicheverPlatformsShapeItIs(string pattern)
    {
        PatternInputDialogViewModel dialog = await DialogWithFolderAsync("/var/log");

        dialog.Pattern = pattern;

        // It is matched *inside* the folder above, so a rooted pattern could only ever match nothing.
        Assert.False(dialog.CanAccept);
        Assert.NotNull(dialog.ValidationMessage);
    }

    [Fact]
    public async Task SurroundingSpacesAreTrimmedRatherThanRefused()
    {
        PatternInputDialogViewModel dialog = await DialogWithFolderAsync("/var/log");

        dialog.Pattern = "  *.log  ";

        Assert.True(dialog.CanAccept);

        dialog.AcceptCommand.Execute(null);

        PatternInput result = Assert.IsType<PatternInput>(dialog.Result);
        Assert.Equal("/var/log", result.BaseDirectory);
        Assert.Equal("*.log", result.Pattern);
    }

    [Fact]
    public async Task ChoosingNoFolder_LeavesTheOneAlreadyChosen()
    {
        PatternInputDialogViewModel dialog = await DialogWithFolderAsync("/var/log");

        await dialog.ChooseBaseDirectoryCommand.ExecuteAsync(null);

        Assert.Equal("/var/log", dialog.BaseDirectory);
    }

    [Fact]
    public async Task Cancelling_HandsBackNothing()
    {
        PatternInputDialogViewModel dialog = await DialogWithFolderAsync("/var/log");
        dialog.Pattern = "*.log";

        int closes = 0;
        dialog.CloseRequested += (_, _) => closes++;

        dialog.CancelCommand.Execute(null);

        Assert.Null(dialog.Result);
        Assert.Equal(1, closes);
    }

    [Fact]
    public async Task Accepting_ClosesTheDialogExactlyOnce()
    {
        PatternInputDialogViewModel dialog = await DialogWithFolderAsync("/var/log");
        dialog.Pattern = "*.log";

        int closes = 0;
        dialog.CloseRequested += (_, _) => closes++;

        dialog.AcceptCommand.Execute(null);

        Assert.Equal(1, closes);
    }

    private static PatternInputDialogViewModel Dialog() => new(new FakeFilePicker());

    private static async Task<PatternInputDialogViewModel> DialogWithFolderAsync(string folder)
    {
        FakeFilePicker picker = new();
        picker.Folders.Enqueue(folder);
        PatternInputDialogViewModel dialog = new(picker);

        await dialog.ChooseBaseDirectoryCommand.ExecuteAsync(null);

        return dialog;
    }
}
