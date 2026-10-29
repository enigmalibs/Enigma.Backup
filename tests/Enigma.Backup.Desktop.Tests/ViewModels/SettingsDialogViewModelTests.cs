using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Enigma.Backup.Desktop;
using Enigma.Backup.Desktop.Settings;
using Enigma.Backup.Desktop.Tests.TestSupport;
using Enigma.Backup.Desktop.ViewModels;
using Xunit;

namespace Enigma.Backup.Desktop.Tests.ViewModels;

/// <summary>
/// The Settings dialog: the theme it opens on, what a choice does, where it says the app's files are,
/// and the gear that raises it.
/// </summary>
/// <remarks>
/// A theme choice applies and persists in the same step, so the two seams are asserted together — a
/// theme that was painted but not written is a preference that vanishes at the next start, and one that
/// was written but not painted is a click that appears to do nothing. The folder button is asserted
/// against the launcher for the same reason: it has no other observable effect.
/// </remarks>
public sealed class SettingsDialogViewModelTests
{
    [Fact]
    public void ItOpensOnTheStoredTheme_AndTouchesNothing()
    {
        FakeAppSettingsStore store = new() { Stored = new AppSettings { Theme = AppTheme.Dark } };
        FakeThemeSwitcher themes = new();

        SettingsDialogViewModel viewModel = new(store, themes, new FakeShellLauncher());

        Assert.Equal(AppTheme.Dark, viewModel.SelectedTheme.Value);
        Assert.Equal(1, store.Loads);

        // Opening the dialog is not a change: the app is already in that variant, and rewriting the
        // file for a choice nobody made would be a save the user never asked for.
        Assert.Empty(store.Saves);
        Assert.Empty(themes.Applied);
    }

    [Fact]
    public void ItOpensOnTheSystemThemeWhenNothingIsStored()
    {
        SettingsDialogViewModel viewModel = Build();

        Assert.Equal(AppTheme.System, viewModel.SelectedTheme.Value);
        Assert.Equal("System", viewModel.SelectedTheme.Label);
    }

    [Fact]
    public void AChoiceIsAppliedAndPersisted_ExactlyOnce()
    {
        FakeAppSettingsStore store = new();
        FakeThemeSwitcher themes = new();
        SettingsDialogViewModel viewModel = new(store, themes, new FakeShellLauncher());

        viewModel.SelectedTheme = ThemeOption.For(AppTheme.Light);

        Assert.Equal(AppTheme.Light, Assert.Single(themes.Applied));
        Assert.Equal(AppTheme.Light, Assert.Single(store.Saves));
        Assert.Equal(AppTheme.Light, store.Stored.Theme);
        Assert.Equal(AppSettings.CurrentFormatVersion, store.Stored.FormatVersion);
    }

    [Fact]
    public void ChoosingTheThemeAlreadyInUseChangesNothing()
    {
        FakeAppSettingsStore store = new() { Stored = new AppSettings { Theme = AppTheme.Dark } };
        FakeThemeSwitcher themes = new();
        SettingsDialogViewModel viewModel = new(store, themes, new FakeShellLauncher());

        viewModel.SelectedTheme = ThemeOption.For(AppTheme.Dark);

        Assert.Empty(themes.Applied);
        Assert.Empty(store.Saves);
    }

    [Fact]
    public void EveryFurtherChoiceIsAppliedAndPersistedInTurn()
    {
        FakeAppSettingsStore store = new();
        FakeThemeSwitcher themes = new();
        SettingsDialogViewModel viewModel = new(store, themes, new FakeShellLauncher());

        viewModel.SelectedTheme = ThemeOption.For(AppTheme.Dark);
        viewModel.SelectedTheme = ThemeOption.For(AppTheme.Light);

        // And back to where it started: "follow the system" is a value of its own, so returning to it
        // is a change like any other rather than a state nothing can reach again.
        viewModel.SelectedTheme = ThemeOption.For(AppTheme.System);

        AppTheme[] expected = [AppTheme.Dark, AppTheme.Light, AppTheme.System];

        Assert.Equal(expected, themes.Applied);
        Assert.Equal(expected, store.Saves);
        Assert.Equal(AppTheme.System, store.Stored.Theme);
    }

    [Fact]
    public void TheDropDownOffersTheThreeThemesInOrder()
    {
        SettingsDialogViewModel viewModel = Build();

        AppTheme[] expected = [AppTheme.System, AppTheme.Light, AppTheme.Dark];

        Assert.Equal(expected, viewModel.ThemeOptions.Select(static option => option.Value));
        Assert.All(viewModel.ThemeOptions, static option => Assert.NotEmpty(option.Description));
    }

    /// <remarks>
    /// The folder, not the file: settings.json is not much use without the profiles beside it, and the
    /// folder is what the Open button opens.
    /// </remarks>
    [Fact]
    public void ItNamesTheFolderTheAppsFilesAreKeptIn()
    {
        FakeAppSettingsStore store = new();
        SettingsDialogViewModel viewModel = new(store, new FakeThemeSwitcher(), new FakeShellLauncher());

        Assert.Equal(Path.GetDirectoryName(store.FilePath), viewModel.StorageFolder);
        Assert.Contains(viewModel.StorageFolder, viewModel.StorageDescription, StringComparison.Ordinal);

        // The sentence the old note carried, kept: deleting that one file is the documented way back to
        // "follow the system".
        Assert.Contains("settings.json", viewModel.StorageDescription, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenFolderShowsTheStorageFolderOnTheDesktop()
    {
        using TempDirectory temp = new();

        string folder = temp.Combine("Enigma.Backup");
        Directory.CreateDirectory(folder);

        FakeShellLauncher launcher = new();
        SettingsDialogViewModel viewModel = Build(launcher, Path.Combine(folder, "settings.json"));

        await viewModel.OpenStorageFolderCommand.ExecuteAsync(null);

        Assert.Equal(folder, Assert.Single(launcher.Folders));
    }

    /// <remarks>
    /// A fresh install has written neither a profile nor a preference, so the folder does not exist yet.
    /// "Show me where my files go" is a reasonable thing to ask before there are any, so the button
    /// makes the folder it promises rather than reporting that it is missing.
    /// </remarks>
    [Fact]
    public async Task OpenFolderCreatesTheFolderWhenNothingHasBeenWrittenYet()
    {
        using TempDirectory temp = new();

        string folder = temp.Combine("fresh", "Enigma.Backup");
        Assert.False(Directory.Exists(folder));

        FakeShellLauncher launcher = new();
        SettingsDialogViewModel viewModel = Build(launcher, Path.Combine(folder, "settings.json"));

        await viewModel.OpenStorageFolderCommand.ExecuteAsync(null);

        Assert.True(Directory.Exists(folder));
        Assert.Equal(folder, Assert.Single(launcher.Folders));
    }

    [Fact]
    public void CloseAsksTheDialogToClose()
    {
        SettingsDialogViewModel viewModel = Build();
        int closes = 0;

        viewModel.CloseRequested += (_, _) => closes++;
        viewModel.CloseCommand.Execute(null);

        Assert.Equal(1, closes);
    }

    /// <remarks>
    /// The preferences are not part of the backup workflow, so the window has nothing to do with the
    /// answer — it opens the dialog and that is all. This is the one assertion that the gear is wired.
    /// </remarks>
    [Fact]
    public async Task TheGearInTheProfileBarOpensTheDialog()
    {
        Harness harness = new();

        Assert.True(harness.ViewModel.OpenSettingsCommand.CanExecute(null));

        await harness.ViewModel.OpenSettingsCommand.ExecuteAsync(null);

        Assert.Equal(1, harness.Dialogs.SettingsRequests);
    }

    /// <summary>A view model over fakes, optionally pointed at a settings file of the test's choosing.</summary>
    /// <param name="launcher">The launcher to record against, when the test cares.</param>
    /// <param name="settingsFilePath">The store's file, when the test needs a real folder behind it.</param>
    private static SettingsDialogViewModel Build(FakeShellLauncher? launcher = null, string? settingsFilePath = null) =>
        new(
            settingsFilePath is null ? new FakeAppSettingsStore() : new FakeAppSettingsStore { FilePath = settingsFilePath },
            new FakeThemeSwitcher(),
            launcher ?? new FakeShellLauncher());
}
