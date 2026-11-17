using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace Enigma.Backup.Desktop.ViewModels;

/// <summary>
/// The Profiles page: the profile bar — which profile is loaded, and New, Save, Save as…, Rename…,
/// Delete…, Import… and Export… — and the profile being edited.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here knows about Avalonia. Files are chosen through <see cref="IFilePicker"/> and questions
/// are asked through <see cref="IDialogService"/>, which is why a test can save, rename and import a
/// profile without a window ever existing.
/// </para>
/// <para>
/// The editor is a working copy of a profile. The store is read on demand and written only when the
/// user asks for it, so an edited profile that has not been saved is exactly that, and the profile bar
/// says so. Every write is followed by a reload of the shared <see cref="ProfileCatalog"/>, so the other
/// pages list what was just written.
/// </para>
/// </remarks>
public sealed class ProfilesPageViewModel : ObservableObject
{
    private readonly IProfileStore _profiles;
    private readonly ProfileCatalog _catalog;
    private readonly IFilePicker _filePicker;
    private readonly IDialogService _dialogs;
    private readonly ILogger<ProfilesPageViewModel> _logger;

    private bool _reloading;

    /// <summary>Creates the page's view model.</summary>
    /// <param name="profiles">The store the profile bar writes.</param>
    /// <param name="catalog">The profiles every page lists, reloaded after every write.</param>
    /// <param name="editor">The working copy of the profile being edited.</param>
    /// <param name="filePicker">Chooses the file a profile is imported from or exported to.</param>
    /// <param name="dialogs">Asks for names and confirmations, and reports what failed.</param>
    /// <param name="logger">Records what the profile bar did, to the console and the debugger.</param>
    public ProfilesPageViewModel(
        IProfileStore profiles,
        ProfileCatalog catalog,
        ProfileEditorViewModel editor,
        IFilePicker filePicker,
        IDialogService dialogs,
        ILogger<ProfilesPageViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(filePicker);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(logger);

        _profiles = profiles;
        _catalog = catalog;
        _filePicker = filePicker;
        _dialogs = dialogs;
        _logger = logger;

        Editor = editor;
        Editor.Changed += OnEditorChanged;
        _catalog.Reloaded += OnCatalogReloaded;

        RefreshProfilesCommand = new AsyncRelayCommand(OnRefreshProfilesAsync);
        NewProfileCommand = new RelayCommand(OnNewProfile);
        SaveProfileCommand = new AsyncRelayCommand(OnSaveProfileAsync);
        SaveProfileAsCommand = new AsyncRelayCommand(OnSaveProfileAsAsync);
        RenameProfileCommand = new AsyncRelayCommand(OnRenameProfileAsync, () => HasSelectedProfile);
        DeleteProfileCommand = new AsyncRelayCommand(OnDeleteProfileAsync, () => HasSelectedProfile);
        ImportProfileCommand = new AsyncRelayCommand(OnImportProfileAsync);
        ExportProfileCommand = new AsyncRelayCommand(OnExportProfileAsync, () => HasSelectedProfile);
    }

    /// <summary>The profile being edited.</summary>
    public ProfileEditorViewModel Editor { get; }

    /// <summary>Every profile in the store, by name.</summary>
    public ObservableCollection<string> ProfileNames => _catalog.Names;

    /// <summary>The profile the bar has selected, or <see langword="null"/> when the editor is not a stored profile.</summary>
    public string? SelectedProfileName
    {
        get;
        set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasSelectedProfile));
            OnPropertyChanged(nameof(ProfileStatus));
            RenameProfileCommand.NotifyCanExecuteChanged();
            DeleteProfileCommand.NotifyCanExecuteChanged();
            ExportProfileCommand.NotifyCanExecuteChanged();

            // A selection dropped because its profile left the store is not one the user made, and must
            // not reach the editor.
            if (!_reloading)
            {
                ShowSelectedProfile();
            }
        }
    }

    /// <summary>Whether a stored profile is selected — what Rename, Delete and Export need.</summary>
    public bool HasSelectedProfile => SelectedProfileName is not null;

    /// <summary>Where the profile stands, shown beside the profile bar.</summary>
    public string ProfileStatus => SelectedProfileName is null
        ? Editor.IsDirty ? "Not saved as a profile yet." : "No profile loaded."
        : Editor.IsDirty ? "Unsaved changes." : "Saved.";

    /// <summary>
    /// What the page last did, in one line — a profile saved, renamed, imported, inputs added.
    /// </summary>
    public string StatusText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "Ready.";

    /// <summary>Re-reads the profile store; run once when the window opens.</summary>
    public AsyncRelayCommand RefreshProfilesCommand { get; }

    /// <summary>Empties the editor so a new profile can be built.</summary>
    public RelayCommand NewProfileCommand { get; }

    /// <summary>Writes the editor to the selected profile, asking for a name when there is none.</summary>
    public AsyncRelayCommand SaveProfileCommand { get; }

    /// <summary>Writes the editor to a new profile under a name that is not taken.</summary>
    public AsyncRelayCommand SaveProfileAsCommand { get; }

    /// <summary>Gives the selected profile a different name.</summary>
    public AsyncRelayCommand RenameProfileCommand { get; }

    /// <summary>Deletes the selected profile, after confirming it.</summary>
    public AsyncRelayCommand DeleteProfileCommand { get; }

    /// <summary>Copies a profile file from anywhere on disk into the store.</summary>
    public AsyncRelayCommand ImportProfileCommand { get; }

    /// <summary>Writes the selected profile out to a file anywhere on disk.</summary>
    public AsyncRelayCommand ExportProfileCommand { get; }

    /// <summary>
    /// Adds paths dropped onto the page to the profile's inputs — the drag-and-drop half of "add file /
    /// add folder".
    /// </summary>
    /// <param name="paths">The dropped paths.</param>
    public void AddDroppedInputs(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        int added = Editor.AddInputs(paths);

        StatusText = added == 0
            ? "Nothing was added — those paths are already in the profile."
            : $"Added {DisplayFormat.Count(added, "input", "inputs")}.";
    }

    private void OnEditorChanged(object? sender, EventArgs e) => OnPropertyChanged(nameof(ProfileStatus));

    /// <summary>
    /// Lets go of a selected profile the store no longer has — deleted, renamed, or its file broken — and
    /// leaves the editor as it is: nothing was asked to be thrown away.
    /// </summary>
    private void OnCatalogReloaded(object? sender, EventArgs e)
    {
        if (SelectedProfileName is string name && !_catalog.Contains(name))
        {
            _reloading = true;
            try
            {
                SelectedProfileName = null;
            }
            finally
            {
                _reloading = false;
            }
        }
    }

    private void ShowSelectedProfile()
    {
        // Deselecting leaves the editor alone: nothing was asked to be thrown away.
        if (SelectedProfileName is string name && _catalog.TryGet(name, out BackupProfile? profile))
        {
            Editor.LoadFrom(profile);
        }
    }

    private async Task OnRefreshProfilesAsync()
    {
        if (!await _catalog.ReloadAsync())
        {
            return;
        }

        // Opening onto the first profile beats opening onto nothing; a store with none stays empty.
        if (SelectedProfileName is null && ProfileNames.Count > 0)
        {
            SelectedProfileName = ProfileNames[0];
        }
    }

    /// <summary>Selects a profile and shows it, when it is one the store has.</summary>
    private void SelectProfile(string name)
    {
        if (_catalog.Contains(name))
        {
            SelectedProfileName = name;
        }
    }

    private void OnNewProfile()
    {
        SelectedProfileName = null;
        Editor.Reset();
        StatusText = "Started a new profile.";
    }

    private async Task OnSaveProfileAsync()
    {
        string? name = SelectedProfileName;

        if (name is null)
        {
            name = await AskForProfileNameAsync(
                "Save the profile",
                "Give the profile a name. It is also the name every archive it produces starts with.",
                initialName: string.Empty,
                keepAvailable: null);

            if (name is null)
            {
                return;
            }
        }

        await SaveUnderAsync(name);
    }

    private async Task OnSaveProfileAsAsync()
    {
        string? name = await AskForProfileNameAsync(
            "Save the profile as",
            "Give the copy a name.",
            initialName: SelectedProfileName ?? string.Empty,
            keepAvailable: null);

        if (name is not null)
        {
            await SaveUnderAsync(name);
        }
    }

    private async Task SaveUnderAsync(string name)
    {
        BackupProfile profile = Editor.ToProfile(name);

        if (!await TryStoreAsync(() => _profiles.SaveAsync(profile), "The profile could not be saved"))
        {
            return;
        }

        await _catalog.ReloadAsync();
        SelectProfile(name);
        Editor.MarkClean();

        OnPropertyChanged(nameof(ProfileStatus));
        StatusText = $"Saved the profile '{name}'.";
        _logger.LogInformation("Saved the profile {Profile}.", name);
    }

    private async Task OnRenameProfileAsync()
    {
        if (SelectedProfileName is not string current)
        {
            return;
        }

        string? name = await AskForProfileNameAsync(
            "Rename the profile",
            $"Give '{current}' a new name.",
            initialName: current,
            keepAvailable: current);

        if (name is null || string.Equals(name, current, StringComparison.Ordinal))
        {
            return;
        }

        if (!await TryStoreAsync(() => _profiles.RenameAsync(current, name), "The profile could not be renamed"))
        {
            return;
        }

        await _catalog.ReloadAsync();
        SelectProfile(name);
        StatusText = $"Renamed '{current}' to '{name}'.";
        _logger.LogInformation("Renamed the profile {OldProfile} to {NewProfile}.", current, name);
    }

    private async Task OnDeleteProfileAsync()
    {
        if (SelectedProfileName is not string name)
        {
            return;
        }

        bool confirmed = await _dialogs.ConfirmAsync(
            "Delete the profile",
            $"Delete the profile '{name}'? The archives it has already produced are left where they are.",
            "Delete");

        if (!confirmed)
        {
            return;
        }

        if (!await TryStoreAsync(() => _profiles.DeleteAsync(name), "The profile could not be deleted"))
        {
            return;
        }

        await _catalog.ReloadAsync();
        SelectedProfileName = null;
        Editor.Reset();
        StatusText = $"Deleted the profile '{name}'.";
        _logger.LogInformation("Deleted the profile {Profile}.", name);
    }

    private async Task OnImportProfileAsync()
    {
        if (await _filePicker.PickProfileToImportAsync() is not string path)
        {
            return;
        }

        BackupProfile? imported = null;

        if (!await TryStoreAsync(async () => imported = await _profiles.ImportAsync(path), "The profile could not be imported"))
        {
            return;
        }

        await _catalog.ReloadAsync();

        if (imported is not null)
        {
            SelectProfile(imported.Name);
            StatusText = $"Imported '{Path.GetFileName(path)}' as the profile '{imported.Name}'.";
            _logger.LogInformation("Imported the profile {Profile} from {Path}.", imported.Name, path);
        }
    }

    private async Task OnExportProfileAsync()
    {
        if (SelectedProfileName is not string name)
        {
            return;
        }

        if (await _filePicker.PickProfileExportPathAsync($"{name}.json") is not string path)
        {
            return;
        }

        if (!await TryStoreAsync(() => _profiles.ExportAsync(name, path), "The profile could not be exported"))
        {
            return;
        }

        // What is written is the *stored* profile, so say so — the editor may have moved on since.
        StatusText = $"Exported the saved profile '{name}' to '{path}'.";
        _logger.LogInformation("Exported the profile {Profile} to {Path}.", name, path);
    }

    private async Task<string?> AskForProfileNameAsync(string title, string prompt, string initialName, string? keepAvailable)
    {
        IReadOnlyCollection<string> taken = keepAvailable is null
            ? [.. ProfileNames]
            : [.. ProfileNames.Where(name => !string.Equals(name, keepAvailable, StringComparison.OrdinalIgnoreCase))];

        return await _dialogs.RequestProfileNameAsync(new ProfileNameRequest(title, prompt, initialName, taken));
    }

    private async Task<bool> TryStoreAsync(Func<Task> operation, string failureTitle)
    {
        try
        {
            await operation();

            return true;
        }
        catch (Exception exception) when (exception
            is ProfileNotFoundException
            or ProfileAlreadyExistsException
            or ProfileFormatException
            or ArgumentException
            or IOException
            or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "A profile operation failed.");
            await _dialogs.ShowMessageAsync(failureTitle, exception.Message, MessageSeverity.Error);

            return false;
        }
    }
}
