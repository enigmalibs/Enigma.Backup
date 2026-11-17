using Enigma.Backup.Desktop.ViewModels;
using Xunit;

namespace Enigma.Backup.Desktop.Tests.ViewModels;

/// <summary>
/// The About dialog: what it says about the build, and which lines it leaves out. The button that raises
/// it is on the Settings page (<see cref="SettingsPageViewModelTests"/>).
/// </summary>
/// <remarks>
/// The layout is every Enigma app's and lives in the view; what is pinned here is the content each line
/// binds to, and the two lines that disappear rather than show blank.
/// </remarks>
public sealed class AboutDialogViewModelTests
{
    [Fact]
    public void ItSaysWhatThisBuildIs()
    {
        AboutDialogViewModel viewModel = new("1.3.0", "0f6b505", "Copyright © 2026 Someone");

        Assert.Equal("Enigma Backup", viewModel.DisplayName);
        Assert.Equal("1.3.0", viewModel.Version);
        Assert.Equal("0f6b505", viewModel.BuildSha);
        Assert.True(viewModel.HasBuildSha);
        Assert.Equal("Copyright © 2026 Someone", viewModel.Copyright);
        Assert.True(viewModel.HasCopyright);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ABuildWithNoRevisionShowsNoBuildLine(string? buildSha)
    {
        AboutDialogViewModel viewModel = new("1.3.0", buildSha, "Copyright © 2026 Someone");

        Assert.False(viewModel.HasBuildSha);
    }

    [Fact]
    public void AnAssemblyWithNoCopyrightShowsNoCopyrightLine()
    {
        AboutDialogViewModel viewModel = new("1.3.0", "0f6b505", string.Empty);

        Assert.False(viewModel.HasCopyright);
    }

    [Fact]
    public void ByDefaultItDescribesTheRunningBuild()
    {
        AboutDialogViewModel viewModel = new();

        Assert.Equal(AppInfo.Version, viewModel.Version);
        Assert.Equal(AppInfo.BuildSha, viewModel.BuildSha);
        Assert.Equal(AppInfo.Copyright, viewModel.Copyright);
        Assert.DoesNotContain('+', viewModel.Version);
    }

    [Fact]
    public void CloseAsksTheDialogToClose()
    {
        AboutDialogViewModel viewModel = new();
        int closes = 0;

        viewModel.CloseRequested += (_, _) => closes++;
        viewModel.CloseCommand.Execute(null);

        Assert.Equal(1, closes);
    }
}
