using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Backup.Core.Backups;
using Enigma.Backup.Core.Encryption;
using Enigma.Backup.Core.Errors;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Core.Restoring;
using Enigma.Backup.Core.Verification;
using Enigma.Backup.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace Enigma.Backup.Desktop.ViewModels;

/// <summary>
/// The main window: the profile bar, the profile being edited, the Backup / Restore / Verify
/// actions, the progress of whichever one is running, and the summary it left behind.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here knows about Avalonia. Files are chosen through <see cref="IFilePicker"/>, questions
/// are asked through <see cref="IDialogService"/>, the run's own card and the banner that reports its
/// outcome through <see cref="IProgressOverlay"/> and <see cref="INotificationService"/>, and the UI
/// thread is reached through <see cref="IUiDispatcher"/> — which is why a test can run a whole backup,
/// answer its error dialog and read the summary without a window ever existing.
/// </para>
/// <para>
/// The editor is a working copy of a profile. The store is read on demand and written only when the
/// user asks for it, so an edited profile that has not been saved is exactly that: the title carries
/// a <c>*</c> and the profile bar says so.
/// </para>
/// </remarks>
public sealed class MainWindowViewModel : ObservableObject
{
    /// <summary>The name archives take when a run happens before the profile has one.</summary>
    private const string UnsavedProfileName = "Backup";

    private readonly Dictionary<string, BackupProfile> _stored = new(StringComparer.OrdinalIgnoreCase);

    private readonly IProfileStore _profiles;
    private readonly IBackupService _backups;
    private readonly IRestoreService _restores;
    private readonly IVerificationService _verifications;
    private readonly IFilePicker _filePicker;
    private readonly IDialogService _dialogs;
    private readonly IProgressOverlay _overlay;
    private readonly INotificationService _notifications;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogger<MainWindowViewModel> _logger;

    private CancellationTokenSource? _cancellation;
    private bool _reloading;

    /// <summary>Creates the window's view model.</summary>
    /// <param name="profiles">The store the profile bar lists, loads and writes.</param>
    /// <param name="backups">Runs a backup.</param>
    /// <param name="restores">Runs a restore.</param>
    /// <param name="verifications">Checks an archive against its checksum.</param>
    /// <param name="editor">The working copy of the profile being edited.</param>
    /// <param name="filePicker">Opens the system file and folder pickers.</param>
    /// <param name="dialogs">Asks the user everything that needs a modal answer.</param>
    /// <param name="overlay">Shows the running operation, and takes its Cancel button.</param>
    /// <param name="notifications">Reports how each run ended, as a banner in the window.</param>
    /// <param name="dispatcher">Marshals the core's progress reports onto the UI thread.</param>
    /// <param name="logger">Records what each run did, to the console and the debugger.</param>
    public MainWindowViewModel(
        IProfileStore profiles,
        IBackupService backups,
        IRestoreService restores,
        IVerificationService verifications,
        ProfileEditorViewModel editor,
        IFilePicker filePicker,
        IDialogService dialogs,
        IProgressOverlay overlay,
        INotificationService notifications,
        IUiDispatcher dispatcher,
        ILogger<MainWindowViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(backups);
        ArgumentNullException.ThrowIfNull(restores);
        ArgumentNullException.ThrowIfNull(verifications);
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(filePicker);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(notifications);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(logger);

        _profiles = profiles;
        _backups = backups;
        _restores = restores;
        _verifications = verifications;
        _filePicker = filePicker;
        _dialogs = dialogs;
        _overlay = overlay;
        _notifications = notifications;
        _dispatcher = dispatcher;
        _logger = logger;

        Editor = editor;
        Editor.Changed += OnEditorChanged;

        RefreshProfilesCommand = new AsyncRelayCommand(OnRefreshProfilesAsync, () => !IsBusy);
        NewProfileCommand = new RelayCommand(OnNewProfile, () => !IsBusy);
        SaveProfileCommand = new AsyncRelayCommand(OnSaveProfileAsync, () => !IsBusy);
        SaveProfileAsCommand = new AsyncRelayCommand(OnSaveProfileAsAsync, () => !IsBusy);
        RenameProfileCommand = new AsyncRelayCommand(OnRenameProfileAsync, () => !IsBusy && HasSelectedProfile);
        DeleteProfileCommand = new AsyncRelayCommand(OnDeleteProfileAsync, () => !IsBusy && HasSelectedProfile);
        ImportProfileCommand = new AsyncRelayCommand(OnImportProfileAsync, () => !IsBusy);
        ExportProfileCommand = new AsyncRelayCommand(OnExportProfileAsync, () => !IsBusy && HasSelectedProfile);
        OpenSettingsCommand = new AsyncRelayCommand(OnOpenSettingsAsync, () => !IsBusy);
        AddPatternCommand = new AsyncRelayCommand(OnAddPatternAsync, () => !IsBusy);

        BackupCommand = new AsyncRelayCommand(OnBackupAsync, () => !IsBusy && Editor.CanRun);
        RestoreCommand = new AsyncRelayCommand(OnRestoreAsync, () => !IsBusy);
        VerifyCommand = new AsyncRelayCommand(OnVerifyAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(OnCancel, () => IsBusy);
    }

    /// <summary>The profile being edited.</summary>
    public ProfileEditorViewModel Editor { get; }

    /// <summary>Every profile in the store, by name.</summary>
    public ObservableCollection<string> ProfileNames { get; } = [];

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
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(ProfileStatus));
            RefreshCommandStates();

            // While the list is being rebuilt the selection is dropped and restored for mechanical
            // reasons; only a selection the user made should reach the editor.
            if (!_reloading)
            {
                ShowSelectedProfile();
            }
        }
    }

    /// <summary>Whether a stored profile is selected — what Rename, Delete and Export need.</summary>
    public bool HasSelectedProfile => SelectedProfileName is not null;

    /// <summary>The window title: the product, the profile in use, and a <c>*</c> for unsaved edits.</summary>
    public string Title
    {
        get
        {
            string marker = Editor.IsDirty ? " *" : string.Empty;

            return SelectedProfileName is string name
                ? $"Enigma.Backup — {name}{marker}"
                : $"Enigma.Backup{marker}";
        }
    }

    /// <summary>Where the profile stands, shown beside the profile bar.</summary>
    public string ProfileStatus => SelectedProfileName is null
        ? Editor.IsDirty ? "Not saved as a profile yet." : "No profile loaded."
        : Editor.IsDirty ? "Unsaved changes." : "Saved.";

    /// <summary>Whether a backup, restore or verification is running.</summary>
    public bool IsBusy
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(IsIdle));
                RefreshCommandStates();
            }
        }
    }

    /// <summary>Whether nothing is running — what the editor's panel is enabled by.</summary>
    public bool IsIdle => !IsBusy;

    /// <summary>
    /// What is happening, in one line: the run card's message while something is running, and the last
    /// run's headline once it is over.
    /// </summary>
    public string StatusText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "Ready.";

    /// <summary>The file being processed right now, or <see langword="null"/> when there is none.</summary>
    public string? CurrentItem
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>How far the running operation has got, from 0 to 100.</summary>
    public double ProgressPercent
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>
    /// Whether the run card's bar should simply animate — true while a scan is running, because how
    /// much there is to do is not known until it has finished.
    /// </summary>
    public bool IsProgressIndeterminate
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>What the last run left behind, or <see langword="null"/> before the first one.</summary>
    public RunSummary? Summary
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasSummary));
            }
        }
    }

    /// <summary>Whether there is a summary to show.</summary>
    public bool HasSummary => Summary is not null;

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
    /// Opens the app's preferences — the gear in the profile bar. It belongs to no profile; it is
    /// simply the only bar the window has.
    /// </summary>
    public AsyncRelayCommand OpenSettingsCommand { get; }

    /// <summary>
    /// Adds a pattern input to the profile — the one input the editor cannot ask for on its own,
    /// because it takes a dialog rather than a picker.
    /// </summary>
    public AsyncRelayCommand AddPatternCommand { get; }

    /// <summary>Runs a backup of what the editor holds.</summary>
    public AsyncRelayCommand BackupCommand { get; }

    /// <summary>Restores a chosen archive into a chosen folder.</summary>
    public AsyncRelayCommand RestoreCommand { get; }

    /// <summary>Checks a chosen archive against its checksum.</summary>
    public AsyncRelayCommand VerifyCommand { get; }

    /// <summary>Stops whatever is running.</summary>
    public RelayCommand CancelCommand { get; }

    /// <summary>
    /// Adds paths dropped onto the window to the profile's inputs — the drag-and-drop half of "add
    /// file / add folder".
    /// </summary>
    /// <param name="paths">The dropped paths.</param>
    public void AddDroppedInputs(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        if (IsBusy)
        {
            return;
        }

        int added = Editor.AddInputs(paths);

        StatusText = added == 0
            ? "Nothing was added — those paths are already in the profile."
            : $"Added {DisplayFormat.Count(added, "input", "inputs")}.";
    }

    private void OnEditorChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ProfileStatus));
        BackupCommand.NotifyCanExecuteChanged();
    }

    private void ShowSelectedProfile()
    {
        // Deselecting leaves the editor alone: nothing was asked to be thrown away.
        if (SelectedProfileName is string name && _stored.TryGetValue(name, out BackupProfile? profile))
        {
            Editor.LoadFrom(profile);
        }
    }

    private async Task OnRefreshProfilesAsync()
    {
        if (!await ReloadStoreAsync())
        {
            return;
        }

        // Opening onto the first profile beats opening onto nothing; a store with none stays empty.
        if (SelectedProfileName is null && ProfileNames.Count > 0)
        {
            SelectedProfileName = ProfileNames[0];
        }
    }

    /// <summary>Re-reads the store into <see cref="ProfileNames"/>, leaving the editor alone.</summary>
    /// <returns><see langword="false"/> when the store itself could not be read.</returns>
    private async Task<bool> ReloadStoreAsync()
    {
        Dictionary<string, BackupProfile> loaded = new(StringComparer.OrdinalIgnoreCase);
        List<string> unreadable = [];

        try
        {
            foreach (string name in await _profiles.ListAsync())
            {
                try
                {
                    loaded[name] = await _profiles.LoadAsync(name);
                }
                catch (Exception exception) when (exception is ProfileFormatException or IOException or UnauthorizedAccessException)
                {
                    // One unreadable file must not hide every other profile.
                    unreadable.Add($"'{name}': {exception.Message}");
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "The profile folder could not be listed.");
            await _dialogs.ShowMessageAsync(
                "Profiles",
                $"The profile folder '{_profiles.DirectoryPath}' could not be read: {exception.Message}",
                MessageSeverity.Error);

            return false;
        }

        string? previous = SelectedProfileName;

        _reloading = true;
        try
        {
            _stored.Clear();
            ProfileNames.Clear();

            foreach ((string name, BackupProfile profile) in loaded.OrderBy(static entry => entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                _stored[name] = profile;
                ProfileNames.Add(name);
            }

            SelectedProfileName = previous is not null && _stored.ContainsKey(previous) ? previous : null;
        }
        finally
        {
            _reloading = false;
        }

        if (unreadable.Count > 0)
        {
            await _dialogs.ShowMessageAsync(
                "Profiles",
                "These profile files could not be read and are not listed:" + Environment.NewLine + string.Join(Environment.NewLine, unreadable),
                MessageSeverity.Warning);
        }

        return true;
    }

    /// <summary>Selects a profile and shows it, when it is one the store has.</summary>
    private void SelectProfile(string name)
    {
        if (_stored.ContainsKey(name))
        {
            SelectedProfileName = name;
        }
    }

    private void OnNewProfile()
    {
        SelectedProfileName = null;
        Editor.Reset();
        Summary = null;
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

        await ReloadStoreAsync();
        SelectProfile(name);
        Editor.MarkClean();

        OnPropertyChanged(nameof(Title));
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

        await ReloadStoreAsync();
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

        await ReloadStoreAsync();
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

        await ReloadStoreAsync();

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

    // The dialog owns everything about a preference — applying it, writing it out — so there is
    // nothing for the window to do afterwards.
    private Task OnOpenSettingsAsync() => _dialogs.ShowSettingsAsync();

    /// <summary>
    /// Asks for a pattern and adds it to the profile. The prompt lives here rather than on the editor
    /// because the editor asks the file system (through <see cref="IFilePicker"/>) and the window asks
    /// the user (through <see cref="IDialogService"/>) — a pattern is the second kind of question.
    /// </summary>
    private async Task OnAddPatternAsync()
    {
        if (await _dialogs.RequestPatternInputAsync() is not PatternInput patternInput)
        {
            return;
        }

        StatusText = Editor.AddPatternInput(patternInput.BaseDirectory, patternInput.Pattern)
            ? $"Added the pattern '{patternInput.Pattern}' under '{patternInput.BaseDirectory}'."
            : "Nothing was added — that pattern is already in the profile.";
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

    private async Task OnBackupAsync()
    {
        string name = SelectedProfileName ?? UnsavedProfileName;
        BackupProfile profile = Editor.ToProfile(name);

        string? password = null;
        if (profile.Encrypt)
        {
            password = await _dialogs.RequestPasswordAsync(PasswordPurpose.ProtectNewArchive, name);

            // No password, no archive: an encrypting profile has nothing to fall back to.
            if (password is null)
            {
                StatusText = "The backup was not started — no password was given.";

                return;
            }
        }

        await RunAsync("backup", "Backing up", discardsOutput: true, async token =>
        {
            DispatchedProgress<BackupProgress> progress = new(_dispatcher, ApplyBackupProgress);
            DialogBackupErrorHandler errors = new(_dialogs);

            BackupResult result = await _backups.RunAsync(profile, password, errors, progress, token);

            return RunSummary.FromBackup(result);
        });
    }

    private async Task OnRestoreAsync()
    {
        if (await _filePicker.PickArchiveAsync("Choose the archive to restore") is not string archive)
        {
            return;
        }

        if (await _filePicker.PickFolderAsync("Choose the folder to restore into") is not string destination)
        {
            return;
        }

        await RunAsync("restore", "Restoring", discardsOutput: false, async token =>
        {
            DispatchedProgress<RestoreProgress> progress = new(_dispatcher, ApplyRestoreProgress);
            DialogRestoreConflictHandler conflicts = new(_dialogs);
            string? password = null;

            while (true)
            {
                try
                {
                    RestoreResult result = await _restores.RestoreAsync(archive, destination, password, conflicts, progress, token);

                    return RunSummary.FromRestore(result);
                }
                catch (EncryptedArchiveException exception)
                    when (exception.Problem == EncryptedArchiveProblem.PasswordRequired && password is null)
                {
                    // Whether an archive is encrypted is a property of the file, not of its name, so
                    // the archive itself is what asks — and it asks before anything has been written.
                    password = await _dialogs.RequestPasswordAsync(PasswordPurpose.OpenArchive, Path.GetFileName(archive), token);

                    if (password is null)
                    {
                        return RunSummary.Cancelled("restore", discarded: false);
                    }
                }
            }
        });
    }

    private async Task OnVerifyAsync()
    {
        if (await _filePicker.PickArchiveAsync("Choose the archive to check") is not string archive)
        {
            return;
        }

        await RunAsync("verification", "Checking the archive", discardsOutput: false, async token =>
        {
            DispatchedProgress<VerificationProgress> progress = new(_dispatcher, ApplyVerificationProgress);

            VerificationResult result = await _verifications.VerifyAsync(archive, password: null, progress, token);

            // The checksum half needs no password; the encrypted half does, and the outcome says so.
            if (result.Outcome == VerificationOutcome.PasswordRequired)
            {
                string? password = await _dialogs.RequestPasswordAsync(PasswordPurpose.OpenArchive, Path.GetFileName(archive), token);

                if (password is not null)
                {
                    result = await _verifications.VerifyAsync(archive, password, progress, token);
                }
            }

            return RunSummary.FromVerification(result);
        });
    }

    /// <summary>
    /// Runs one operation with everything that surrounds every operation: the busy state, the
    /// cancellation source the Cancel button needs, the card that shows it running, the banner that
    /// reports how it ended, and a summary whatever the outcome.
    /// </summary>
    /// <param name="what">What is running, as it appears in messages — "backup", "restore", …</param>
    /// <param name="title">The same thing as the run card's heading — "Backing up", "Restoring", …</param>
    /// <param name="discardsOutput">Whether cancelling it also deletes a partial output file.</param>
    /// <param name="operation">The operation, which returns the summary of what it did.</param>
    private async Task RunAsync(string what, string title, bool discardsOutput, Func<CancellationToken, Task<RunSummary>> operation)
    {
        if (IsBusy)
        {
            return;
        }

        using CancellationTokenSource cancellation = new();

        _cancellation = cancellation;
        Summary = null;

        // Busy before the card goes up, so Cancel is live the moment it can be clicked.
        IsBusy = true;

        // Last run's outcome belongs to the last run.
        await _notifications.HideAsync();
        await _overlay.ShowAsync(title, CancelCommand);

        ReportProgress($"Starting the {what}…", currentItem: null, percent: 0, isIndeterminate: true);

        _logger.LogInformation("Starting the {Operation}.", what);

        try
        {
            Summary = await operation(cancellation.Token);
            _logger.LogInformation("The {Operation} finished: {Headline}", what, Summary.Headline);
        }
        catch (OperationCanceledException)
        {
            Summary = RunSummary.Cancelled(what, discardsOutput);
            _logger.LogInformation("The {Operation} was cancelled.", what);
        }
        catch (BackupAbortedException exception)
        {
            // The failure that was aborted on, not the wrapper's own "the run was aborted:" prefix.
            Summary = RunSummary.Aborted(exception.Error?.Message ?? exception.Message);
            _logger.LogWarning(exception, "The {Operation} was stopped by an Abort answer.", what);
        }
        catch (Exception exception)
        {
            // A run must not take the window down with it: whatever went wrong becomes the summary.
            Summary = RunSummary.Failed(what, exception.Message);
            _logger.LogError(exception, "The {Operation} failed.", what);
        }
        finally
        {
            _cancellation = null;
            IsBusy = false;
            IsProgressIndeterminate = false;
            CurrentItem = null;
            StatusText = Summary?.Headline ?? "Ready.";

            // The bar keeps whatever the last report left it at — a cancelled run stopped where it
            // stopped, and saying so is more honest than snapping to either end.
            if (Summary is { IsFailure: true })
            {
                ProgressPercent = 0;
            }

            // Whatever happened above, including a failure the catches did not expect: a scrim left up
            // is an app nobody can click.
            await _overlay.HideAsync();

            if (Summary is RunSummary summary)
            {
                await NotifyAsync(summary);
            }
        }
    }

    /// <summary>Reports how a run ended, as the banner the window shows until the next one starts.</summary>
    /// <param name="summary">What the run left behind.</param>
    private Task NotifyAsync(RunSummary summary)
    {
        // The headline is the message: the core already writes the sentence it wants shown, and the
        // title only has to say how to read it. Everything else stays in the "Last run" panel.
        (string title, NotificationSeverity severity) = summary switch
        {
            { IsSuccess: true } => ("Done", NotificationSeverity.Success),
            { IsFailure: true } => ("Something went wrong", NotificationSeverity.Error),
            _ => ("Finished, with something to report", NotificationSeverity.Warning),
        };

        return _notifications.ShowAsync(title, summary.Headline, severity);
    }

    /// <summary>
    /// Publishes one progress report to both places that read it — the window's own state and the run
    /// card — so the card can never drift from the view model.
    /// </summary>
    /// <param name="status">What is happening, in one line.</param>
    /// <param name="currentItem">The file being processed, or <see langword="null"/> when there is none.</param>
    /// <param name="percent">How far the run has got, from 0 to 100.</param>
    /// <param name="isIndeterminate">Whether how much there is to do is still unknown.</param>
    private void ReportProgress(string status, string? currentItem, double percent, bool isIndeterminate)
    {
        IsProgressIndeterminate = isIndeterminate;
        ProgressPercent = percent;
        CurrentItem = currentItem;
        StatusText = status;

        _overlay.Update(status, currentItem, percent, isIndeterminate);
    }

    private void OnCancel()
    {
        // Only the line changes: the bar and the file being worked on are still true until the run
        // actually stops, and the card is what the user is looking at when they press this.
        ReportProgress("Cancelling…", CurrentItem, ProgressPercent, IsProgressIndeterminate);

        // Finished between the button press and here, the source is simply gone; nothing to stop.
        _cancellation?.Cancel();
    }

    private void ApplyBackupProgress(BackupProgress progress)
    {
        string status = progress.Phase switch
        {
            BackupPhase.Scanning => "Scanning the inputs…",
            BackupPhase.Completed => "Writing the checksum…",
            _ => $"Archiving — {DisplayFormat.Bytes(progress.BytesProcessed)} of {DisplayFormat.Bytes(progress.TotalBytes)}, " +
                 $"{DisplayFormat.Count(progress.FilesProcessed, "file", "files")} done",
        };

        ReportProgress(status, progress.CurrentPath, progress.PercentComplete, progress.Phase == BackupPhase.Scanning);
    }

    private void ApplyRestoreProgress(RestoreProgress progress) =>
        ReportProgress(
            $"Restoring — {DisplayFormat.Count(progress.FilesRestored, "file", "files")}, " +
            $"{DisplayFormat.Bytes(progress.BytesRestored)} written",
            progress.CurrentPath,
            progress.PercentComplete,
            isIndeterminate: false);

    // A verification reads one file from end to end, so there is no item within it to name.
    private void ApplyVerificationProgress(VerificationProgress progress) =>
        ReportProgress(
            $"Checking — {DisplayFormat.Bytes(progress.BytesProcessed)} of {DisplayFormat.Bytes(progress.TotalBytes)}",
            currentItem: null,
            progress.PercentComplete,
            isIndeterminate: false);

    private void RefreshCommandStates()
    {
        RefreshProfilesCommand.NotifyCanExecuteChanged();
        NewProfileCommand.NotifyCanExecuteChanged();
        SaveProfileCommand.NotifyCanExecuteChanged();
        SaveProfileAsCommand.NotifyCanExecuteChanged();
        RenameProfileCommand.NotifyCanExecuteChanged();
        DeleteProfileCommand.NotifyCanExecuteChanged();
        ImportProfileCommand.NotifyCanExecuteChanged();
        ExportProfileCommand.NotifyCanExecuteChanged();
        OpenSettingsCommand.NotifyCanExecuteChanged();
        AddPatternCommand.NotifyCanExecuteChanged();
        BackupCommand.NotifyCanExecuteChanged();
        RestoreCommand.NotifyCanExecuteChanged();
        VerifyCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }
}
