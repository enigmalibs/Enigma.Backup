using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Backup.Desktop.Services;
using Enigma.Backup.Desktop.Settings;

namespace Enigma.Backup.Desktop.ViewModels;

/// <summary>The Settings page: the app's own preferences, where they live, and About.</summary>
/// <remarks>
/// <para>
/// A choice applies the moment it is made rather than on an OK button — a theme is judged by looking
/// at it, so there is nothing to confirm and nothing to read back afterwards.
/// </para>
/// <para>
/// Applying and persisting are two calls on two seams and this view model makes both, so a change is
/// on screen and on disk in the same step. Nothing here names Avalonia: the variant is assigned
/// through <see cref="IThemeSwitcher"/>, the file is written through <see cref="IAppSettingsStore"/>,
/// and the folder is opened through <see cref="IShellLauncher"/>. About is the one dialog the page
/// raises, through <see cref="IDialogService"/> — it is reached from here and nowhere else.
/// </para>
/// </remarks>
public sealed class SettingsPageViewModel : ObservableObject
{
    private readonly IAppSettingsStore _store;
    private readonly IThemeSwitcher _themes;
    private readonly IShellLauncher _launcher;

    /// <summary>
    /// What is on disk, read once when the page is built and updated in step with every change that is
    /// written back — so it is also what "the value this page started from" means.
    /// </summary>
    private readonly AppSettings _stored;

    /// <summary>Creates the page's view model over the stored preferences.</summary>
    /// <param name="store">Where the preferences are read from and written back to.</param>
    /// <param name="themes">Applies a chosen theme to the running app.</param>
    /// <param name="launcher">Opens the storage folder on the desktop.</param>
    /// <param name="dialogs">Shows the About dialog.</param>
    public SettingsPageViewModel(IAppSettingsStore store, IThemeSwitcher themes, IShellLauncher launcher, IDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(themes);
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(dialogs);

        _store = store;
        _themes = themes;
        _launcher = launcher;

        StorageFolder = ResolveStorageFolder(store.FilePath);

        // Read here rather than injected: the page shows what is on disk when the app starts, and from
        // then on it is the only thing that writes the file.
        _stored = store.Load();

        OpenStorageFolderCommand = new AsyncRelayCommand(OnOpenStorageFolderAsync);
        OpenAboutCommand = new AsyncRelayCommand(() => dialogs.ShowAboutAsync());

        SelectedTheme = ThemeOption.For(_stored.Theme);
    }

    /// <summary>
    /// The folder holding <c>settings.json</c> and the <c>profiles</c> directory — the one thing worth
    /// opening, since neither file is much use without the other.
    /// </summary>
    /// <remarks>
    /// Derived from the store's own file rather than from the store type's static default, so it is
    /// whatever this instance actually reads and writes.
    /// </remarks>
    public string StorageFolder { get; }

    /// <summary>Where the app's files are, and the one of them that is safe to delete.</summary>
    public string StorageDescription =>
        $"Your profiles and preferences are kept in {StorageFolder}. Delete settings.json from there and the app goes back to following the system.";

    /// <summary>Opens the folder the app's files live in, creating it if this is a fresh install.</summary>
    public AsyncRelayCommand OpenStorageFolderCommand { get; }

    /// <summary>The version of the running build, as the About card shows it.</summary>
    public string Version => AppInfo.Version;

    /// <summary>Shows the About dialog every Enigma app has — from this page, and nowhere else.</summary>
    public AsyncRelayCommand OpenAboutCommand { get; }

    /// <summary>Every theme the drop-down offers.</summary>
    public IReadOnlyList<ThemeOption> ThemeOptions { get; } = ThemeOption.All;

    /// <summary>
    /// The theme in use. Assigning it repaints the app and writes the choice out — there is no
    /// separate "apply", because a theme nobody can see is not a choice anybody can make.
    /// </summary>
    public ThemeOption SelectedTheme
    {
        get;
        set
        {
            if (!SetProperty(ref field, value ?? ThemeOption.All[0]))
            {
                return;
            }

            // The stored value is where this came from, so an assignment that lands back on it — the
            // one the constructor makes — is neither a theme to apply nor a file to rewrite.
            if (field.Value == _stored.Theme)
            {
                return;
            }

            _themes.Apply(field.Value);

            _stored.Theme = field.Value;
            _store.Save(_stored);
        }
    } = ThemeOption.All[0];

    /// <summary>The folder a settings file sits in, which is also where the profiles are.</summary>
    /// <param name="settingsFilePath">The store's own file.</param>
    /// <returns>The parent folder, or the path itself when it has no parent to name.</returns>
    /// <remarks>
    /// The fallback is unreachable through <c>AppSettingsStore</c>, which always composes
    /// <c>&lt;config&gt;/Enigma.Backup/settings.json</c> — but a path with no parent must not turn into
    /// a null the button then tries to open.
    /// </remarks>
    private static string ResolveStorageFolder(string settingsFilePath) =>
        Path.GetDirectoryName(settingsFilePath) is { Length: > 0 } folder ? folder : settingsFilePath;

    /// <summary>Opens the storage folder, making it first if nothing has been written there yet.</summary>
    /// <remarks>
    /// A fresh install has no folder at all: the first profile and the first preference both create it,
    /// and neither has happened. Refusing to open it — or opening an error dialog about it — would be
    /// an odd answer to "show me where my files go", so the button makes the folder it promises.
    /// </remarks>
    private Task OnOpenStorageFolderAsync()
    {
        try
        {
            Directory.CreateDirectory(StorageFolder);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A folder that cannot be made is one the launcher will decline to open, and the launcher
            // is what says so. There is nothing to add here, and nothing worth a dialog.
        }

        return _launcher.OpenFolderAsync(StorageFolder);
    }
}
