using System.Threading.Tasks;
using Enigma.Backup.Desktop.Tests.TestSupport;
using Enigma.Backup.Desktop.ViewModels;
using Xunit;

namespace Enigma.Backup.Desktop.Tests.ViewModels;

/// <summary>
/// The About dialog: what it says about the build, which lines it leaves out, and the button that
/// raises it.
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

        Assert.Equal("Enigma.Backup", viewModel.DisplayName);
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

    /// <remarks>
    /// The window has nothing to do with the dialog beyond raising it, so this is the one assertion that
    /// the button beside the gear is wired.
    /// </remarks>
    [Fact]
    public async Task TheButtonBesideTheGearOpensTheDialog()
    {
        Harness harness = new();

        Assert.True(harness.ViewModel.OpenAboutCommand.CanExecute(null));

        await harness.ViewModel.OpenAboutCommand.ExecuteAsync(null);

        Assert.Equal(1, harness.Dialogs.AboutRequests);
        Assert.Equal(0, harness.Dialogs.SettingsRequests);
    }

    /// <remarks>
    /// Like every other button in the top bar, it waits for a run to finish: the dialog host is where a
    /// run asks its questions, and About must not take it from one.
    /// </remarks>
    [Fact]
    public async Task TheButtonIsUnavailableWhileARunIsInProgress()
    {
        Harness harness = new();
        harness.FillEditor();

        bool availableWhileRunning = true;

        harness.Backups.Behaviour = (_, _, _, _, _) =>
        {
            availableWhileRunning = harness.ViewModel.OpenAboutCommand.CanExecute(null);

            return Task.FromResult(Results.Backup());
        };

        await harness.ViewModel.BackupCommand.ExecuteAsync(null);

        Assert.False(availableWhileRunning);
        Assert.True(harness.ViewModel.OpenAboutCommand.CanExecute(null));
    }
}
