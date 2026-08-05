using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Input;
using Enigma.Backup.Desktop.Services;
using Enigma.Backup.Desktop.Settings;

namespace Enigma.Backup.Desktop.ViewModels;

/// <summary>The Settings dialog: the app's own preferences, which today is the theme.</summary>
/// <remarks>
/// <para>
/// A choice applies the moment it is made rather than on an OK button — a theme is judged by looking
/// at it, so there is nothing to confirm and nothing to read back afterwards. The dialog is closed,
/// not accepted, which is why it has no result property and no accept command.
/// </para>
/// <para>
/// Applying and persisting are two calls on two seams and this view model makes both, so a change is
/// on screen and on disk in the same step. Nothing here names Avalonia: the variant is assigned
/// through <see cref="IThemeSwitcher"/> and the file is written through
/// <see cref="IAppSettingsStore"/>.
/// </para>
/// </remarks>
public sealed class SettingsDialogViewModel : DialogViewModel
{
    private readonly IAppSettingsStore _store;
    private readonly IThemeSwitcher _themes;

    /// <summary>
    /// What is on disk, read once when the dialog opens and updated in step with every change that is
    /// written back — so it is also what "the value this dialog started from" means.
    /// </summary>
    private readonly AppSettings _stored;

    /// <summary>Creates the dialog's view model over the stored preferences.</summary>
    /// <param name="store">Where the preferences are read from and written back to.</param>
    /// <param name="themes">Applies a chosen theme to the running app.</param>
    public SettingsDialogViewModel(IAppSettingsStore store, IThemeSwitcher themes)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(themes);

        _store = store;
        _themes = themes;

        // Read here rather than injected: reopening the dialog should show what is on disk, including
        // an edit made to the file by hand.
        _stored = store.Load();

        CloseCommand = new RelayCommand(RequestClose);

        SelectedTheme = ThemeOption.For(_stored.Theme);
    }

    /// <summary>Where the preferences are stored, said in full so the file can be found.</summary>
    public string StorageNote =>
        $"Stored in {_store.FilePath}. Delete that file and the app goes back to following the system.";

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

    /// <summary>Closes the dialog. There is nothing to accept: every choice has already applied.</summary>
    public RelayCommand CloseCommand { get; }
}
