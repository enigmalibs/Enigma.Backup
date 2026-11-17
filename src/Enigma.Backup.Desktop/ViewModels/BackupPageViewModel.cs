using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Backup.Core.Backups;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Desktop.Services;

namespace Enigma.Backup.Desktop.ViewModels;

/// <summary>
/// The Backup page: what to back up (Input), where and how (Output), and the Back up button, whose run
/// is shown in the run dialog from its first report to its outcome.
/// </summary>
/// <remarks>
/// <para>
/// The page backs up one of two things, chosen at the top of Input. Either a one-off set of files,
/// folders and patterns (<see cref="Single"/>), kept for as long as the app is open and never written
/// anywhere; or a saved profile, picked by name and backed up exactly as it was last saved
/// (<see cref="FromProfile"/>, which the page shows but never edits). No profile is managed here — that
/// is the Profiles page.
/// </para>
/// <para>
/// Nothing here knows about Avalonia. A password is asked for through <see cref="IDialogService"/> and
/// the run is shown through the shared <see cref="RunCoordinator"/>, which is why a test can run a whole
/// backup and read its summary without a window ever existing.
/// </para>
/// </remarks>
public sealed class BackupPageViewModel : ObservableObject
{
    /// <summary>The name a one-off backup's archives start with.</summary>
    public const string OneOffName = "Backup";

    private readonly IBackupService _backups;
    private readonly ProfileCatalog _catalog;
    private readonly IDialogService _dialogs;
    private readonly RunCoordinator _runs;
    private readonly IUiDispatcher _dispatcher;

    /// <summary>Whether the user has picked a mode; until they have, the first reload picks one for them.</summary>
    private bool _modeChosen;

    /// <summary>Whether the catalog has been read at least once.</summary>
    private bool _loadedOnce;

    /// <summary>Creates the page's view model, on a one-off backup.</summary>
    /// <param name="backups">Runs a backup.</param>
    /// <param name="single">The one-off files, folders, patterns and output this page backs up.</param>
    /// <param name="fromProfile">The selected profile as last saved, shown read-only.</param>
    /// <param name="catalog">The saved profiles the drop-down lists.</param>
    /// <param name="dialogs">Asks for the password of an encrypted backup, and what to do about a file that cannot be read.</param>
    /// <param name="runs">Runs one operation at a time and shows it in the run dialog.</param>
    /// <param name="dispatcher">Marshals the core's progress reports onto the UI thread.</param>
    public BackupPageViewModel(
        IBackupService backups,
        ProfileEditorViewModel single,
        ProfileEditorViewModel fromProfile,
        ProfileCatalog catalog,
        IDialogService dialogs,
        RunCoordinator runs,
        IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(backups);
        ArgumentNullException.ThrowIfNull(single);
        ArgumentNullException.ThrowIfNull(fromProfile);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(dispatcher);

        if (ReferenceEquals(single, fromProfile))
        {
            throw new ArgumentException("The one-off backup and the profile shown need an editor each.", nameof(fromProfile));
        }

        _backups = backups;
        _catalog = catalog;
        _dialogs = dialogs;
        _runs = runs;
        _dispatcher = dispatcher;

        Single = single;
        FromProfile = fromProfile;

        Single.Changed += OnEditorChanged;
        FromProfile.Changed += OnEditorChanged;
        _runs.PropertyChanged += OnRunsChanged;
        _catalog.Reloaded += OnCatalogReloaded;

        BackupCommand = new AsyncRelayCommand(OnBackupAsync, () => !IsBusy && CanRun);
    }

    /// <summary>The one-off backup: its inputs, exclude patterns, output folder and archive options.</summary>
    public ProfileEditorViewModel Single { get; }

    /// <summary>The selected profile, as last saved — shown, never edited here.</summary>
    public ProfileEditorViewModel FromProfile { get; }

    /// <summary>
    /// Whether the page backs up a saved profile rather than a one-off set of files. Choosing it is a
    /// choice the page then keeps: the first load no longer decides it.
    /// </summary>
    public bool UseProfile
    {
        get;
        set
        {
            _modeChosen = true;

            if (SetProperty(ref field, value))
            {
                OnModeChanged();
            }
        }
    }

    /// <summary>Whether the page backs up a one-off set of files — the other radio button.</summary>
    public bool IsSingleMode
    {
        get => !UseProfile;
        set
        {
            // The radio button being unchecked says nothing on its own: its sibling being checked does.
            if (value)
            {
                UseProfile = false;
            }
        }
    }

    /// <summary>The editor whose output the Output section shows: the one-off, or the selected profile.</summary>
    public ProfileEditorViewModel ActiveEditor => UseProfile ? FromProfile : Single;

    /// <summary>Whether the Output section can be changed — only for a one-off; a profile's is changed on the Profiles page.</summary>
    public bool IsOutputEditable => !UseProfile;

    /// <summary>Every saved profile, by name.</summary>
    public ObservableCollection<string> ProfileNames => _catalog.Names;

    /// <summary>Whether there is any saved profile to choose.</summary>
    public bool HasProfiles => _catalog.Names.Count > 0;

    /// <summary>The profile to back up, or <see langword="null"/> when none is chosen.</summary>
    public string? SelectedProfileName
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                ShowSelectedProfile();
            }
        }
    }

    /// <summary>The first line of the profile's summary — what it backs up, in a sentence — or <see langword="null"/> when none is chosen.</summary>
    public string? SummaryLead
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>What the profile backs up: each folder or file, and each pattern as "pattern in folder".</summary>
    public IReadOnlyList<string> SummaryItems
    {
        get;
        private set => SetProperty(ref field, value);
    } = [];

    /// <summary>What the profile leaves out, in a line, or <see langword="null"/> when it leaves nothing out.</summary>
    public string? SummaryExcludes
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>Whether a backup, restore or verification is running — started here or on another page.</summary>
    public bool IsBusy => _runs.IsBusy;

    /// <summary>Whether nothing is running.</summary>
    public bool IsIdle => !IsBusy;

    /// <summary>Whether a drop has somewhere to land here: only a one-off has a list to add to, and only while nothing runs.</summary>
    public bool AcceptsDrops => IsIdle && !UseProfile;

    /// <summary>
    /// What Back up still needs, in one sentence beside the button — or <see langword="null"/> when it has
    /// everything. A button that is greyed out and does not say why reads as a bug.
    /// </summary>
    public string? MissingForBackup => UseProfile ? MissingForProfileBackup() : MissingForOneOffBackup();

    /// <summary>
    /// What the page last did, in one line — inputs added, a backup that did not start. A run's own
    /// progress and outcome are in its dialog, not here.
    /// </summary>
    public string StatusText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "Ready.";

    /// <summary>Runs a backup of the one-off, or of the chosen profile as last saved.</summary>
    public AsyncRelayCommand BackupCommand { get; }

    private bool CanRun => UseProfile
        ? SelectedProfileName is not null && FromProfile.CanRun
        : Single.CanRun;

    /// <summary>
    /// Adds paths dropped onto the page to its inputs — the drag-and-drop half of "add file / add folder".
    /// A profile's inputs are changed on the Profiles page, so a drop does nothing while one is chosen.
    /// </summary>
    /// <param name="paths">The dropped paths.</param>
    public void AddDroppedInputs(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        if (!AcceptsDrops)
        {
            return;
        }

        int added = Single.AddInputs(paths);

        StatusText = added == 0
            ? "Nothing was added — those paths are already in the list."
            : $"Added {DisplayFormat.Count(added, "input", "inputs")}.";
    }

    private string? MissingForOneOffBackup() => (Single.Inputs.Count > 0, Single.OutputDirectory.Trim().Length > 0) switch
    {
        (false, false) => "Add something to back up, and choose where to save the archive.",
        (false, true) => "Add a file, a folder or a pattern to back up.",
        (true, false) => "Choose where to save the archive.",
        _ => null,
    };

    private string? MissingForProfileBackup()
    {
        if (!HasProfiles)
        {
            return "Create a profile on the Profiles page first.";
        }

        if (SelectedProfileName is null)
        {
            return "Choose the profile to back up.";
        }

        if (FromProfile.Inputs.Count == 0)
        {
            return "This profile backs up nothing yet — add to it on the Profiles page.";
        }

        return FromProfile.OutputDirectory.Trim().Length == 0
            ? "This profile has no output folder — choose one on the Profiles page."
            : null;
    }

    /// <summary>Sets the mode on the user's behalf — the first load's default — without making it their choice.</summary>
    private void DefaultModeTo(bool useProfile)
    {
        UseProfile = useProfile;
        _modeChosen = false;
    }

    private void OnModeChanged()
    {
        // Switching to a profile with none chosen yet opens onto the first, as the Profiles page does.
        if (UseProfile && SelectedProfileName is null && ProfileNames.Count > 0)
        {
            SelectedProfileName = ProfileNames[0];
        }

        OnPropertyChanged(nameof(IsSingleMode));
        OnPropertyChanged(nameof(ActiveEditor));
        OnPropertyChanged(nameof(IsOutputEditable));
        OnPropertyChanged(nameof(AcceptsDrops));
        Refresh();
    }

    private void ShowSelectedProfile()
    {
        if (SelectedProfileName is string name && _catalog.TryGet(name, out BackupProfile? profile))
        {
            FromProfile.LoadFrom(profile);
            Summarize(profile);
        }
        else
        {
            // Nothing chosen shows nothing: an Output section with a previous profile's folder in it
            // would say where an archive goes that is not going to be written.
            FromProfile.Reset();
            Summarize(null);
        }

        Refresh();
    }

    private void Summarize(BackupProfile? profile)
    {
        if (profile is null)
        {
            SummaryLead = null;
            SummaryItems = [];
            SummaryExcludes = null;

            return;
        }

        List<string> items =
        [
            .. profile.Inputs,
            .. profile.PatternInputs.Select(static pattern => $"{pattern.Pattern} in {pattern.BaseDirectory}"),
        ];

        SummaryLead = items.Count == 0
            ? "This profile backs up nothing yet."
            : $"Backs up {DisplayFormat.Count(items.Count, "item", "items")}, as last saved:";
        SummaryItems = items;
        SummaryExcludes = profile.ExcludeGlobs.Count == 0
            ? null
            : $"Leaves out {string.Join(", ", profile.ExcludeGlobs)}";
    }

    /// <summary>
    /// Follows the store: a chosen profile that is still there is shown as it was just saved, one that
    /// has gone is let go of. The very first load also picks the mode, if the user has not.
    /// </summary>
    private void OnCatalogReloaded(object? sender, EventArgs e)
    {
        bool first = !_loadedOnce;
        _loadedOnce = true;

        OnPropertyChanged(nameof(HasProfiles));

        if (SelectedProfileName is string name && !_catalog.Contains(name))
        {
            SelectedProfileName = null;
        }
        else
        {
            ShowSelectedProfile();
        }

        // Someone who made profiles means to use them; someone who has none needs the editor.
        if (first && !_modeChosen)
        {
            DefaultModeTo(HasProfiles);
        }

        Refresh();
    }

    private void OnRunsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RunCoordinator.IsBusy))
        {
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(IsIdle));
            OnPropertyChanged(nameof(AcceptsDrops));
            BackupCommand.NotifyCanExecuteChanged();
        }
    }

    private void OnEditorChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        OnPropertyChanged(nameof(MissingForBackup));
        BackupCommand.NotifyCanExecuteChanged();
    }

    private async Task OnBackupAsync()
    {
        string name = UseProfile && SelectedProfileName is string profileName ? profileName : OneOffName;
        BackupProfile profile = ActiveEditor.ToProfile(name);

        string? password = null;
        if (profile.Encrypt)
        {
            password = await _dialogs.RequestPasswordAsync(PasswordPurpose.ProtectNewArchive, name);

            // No password, no archive: an encrypting backup has nothing to fall back to.
            if (password is null)
            {
                StatusText = "The backup was not started — no password was given.";

                return;
            }
        }

        await _runs.RunAsync("backup", "Backing up", discardsOutput: true, async (run, token) =>
        {
            DispatchedProgress<BackupProgress> progress = new(_dispatcher, report => ApplyBackupProgress(run, report));
            DialogBackupErrorHandler errors = new(_dialogs);

            BackupResult result = await _backups.RunAsync(profile, password, errors, progress, token);

            return RunSummary.FromBackup(result);
        });
    }

    private static void ApplyBackupProgress(RunViewModel run, BackupProgress progress)
    {
        string status = progress.Phase switch
        {
            BackupPhase.Scanning => "Scanning the inputs…",
            BackupPhase.Completed => "Writing the checksum…",
            _ => $"Archiving — {DisplayFormat.Bytes(progress.BytesProcessed)} of {DisplayFormat.Bytes(progress.TotalBytes)}, " +
                 $"{DisplayFormat.Count(progress.FilesProcessed, "file", "files")} done",
        };

        run.Report(status, progress.CurrentPath, progress.PercentComplete, progress.Phase == BackupPhase.Scanning);
    }
}
