using System;
using System.Linq;
using System.Threading.Tasks;
using Enigma.Backup.Desktop.Settings;
using Enigma.Backup.Desktop.Tests.TestSupport;
using Enigma.Backup.Desktop.ViewModels;
using Xunit;

namespace Enigma.Backup.Desktop.Tests.ViewModels;

/// <summary>The Settings dialog: the theme it opens on, what a choice does, and the gear that raises it.</summary>
/// <remarks>
/// A choice applies and persists in the same step, so the two seams are asserted together — a theme
/// that was painted but not written is a preference that vanishes at the next start, and one that was
/// written but not painted is a click that appears to do nothing.
/// </remarks>
public sealed class SettingsDialogViewModelTests
{
    [Fact]
    public void ItOpensOnTheStoredTheme_AndTouchesNothing()
    {
        FakeAppSettingsStore store = new() { Stored = new AppSettings { Theme = AppTheme.Dark } };
        FakeThemeSwitcher themes = new();

        SettingsDialogViewModel viewModel = new(store, themes);

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
        SettingsDialogViewModel viewModel = new(new FakeAppSettingsStore(), new FakeThemeSwitcher());

        Assert.Equal(AppTheme.System, viewModel.SelectedTheme.Value);
        Assert.Equal("System", viewModel.SelectedTheme.Label);
    }

    [Fact]
    public void AChoiceIsAppliedAndPersisted_ExactlyOnce()
    {
        FakeAppSettingsStore store = new();
        FakeThemeSwitcher themes = new();
        SettingsDialogViewModel viewModel = new(store, themes);

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
        SettingsDialogViewModel viewModel = new(store, themes);

        viewModel.SelectedTheme = ThemeOption.For(AppTheme.Dark);

        Assert.Empty(themes.Applied);
        Assert.Empty(store.Saves);
    }

    [Fact]
    public void EveryFurtherChoiceIsAppliedAndPersistedInTurn()
    {
        FakeAppSettingsStore store = new();
        FakeThemeSwitcher themes = new();
        SettingsDialogViewModel viewModel = new(store, themes);

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
        SettingsDialogViewModel viewModel = new(new FakeAppSettingsStore(), new FakeThemeSwitcher());

        AppTheme[] expected = [AppTheme.System, AppTheme.Light, AppTheme.Dark];

        Assert.Equal(expected, viewModel.ThemeOptions.Select(static option => option.Value));
        Assert.All(viewModel.ThemeOptions, static option => Assert.NotEmpty(option.Description));
    }

    [Fact]
    public void ItNamesTheFileTheChoiceIsStoredIn()
    {
        FakeAppSettingsStore store = new();
        SettingsDialogViewModel viewModel = new(store, new FakeThemeSwitcher());

        Assert.Contains(store.FilePath, viewModel.StorageNote, StringComparison.Ordinal);
    }

    [Fact]
    public void CloseAsksTheDialogToClose()
    {
        SettingsDialogViewModel viewModel = new(new FakeAppSettingsStore(), new FakeThemeSwitcher());
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
}
