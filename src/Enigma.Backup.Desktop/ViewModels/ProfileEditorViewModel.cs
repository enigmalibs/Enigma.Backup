using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Desktop.Services;

namespace Enigma.Backup.Desktop.ViewModels;

/// <summary>
/// Everything a <see cref="BackupProfile"/> holds, as the window edits it: the inputs, the exclude
/// patterns, the output folder, the compression and whether the archive is encrypted.
/// </summary>
/// <remarks>
/// <para>
/// This is the profile's *working copy*. It is filled from a stored profile, edited freely, and read
/// back out with <see cref="ToProfile"/> when something needs one — a save, or a run. Nothing here
/// touches the store, so a profile is only written when the user asks for it.
/// </para>
/// <para>
/// The password is deliberately absent: it is prompted per run and never part of a profile.
/// </para>
/// </remarks>
public sealed class ProfileEditorViewModel : ObservableObject
{
    private readonly IFilePicker _filePicker;
    private bool _loading;

    /// <summary>Creates an empty editor.</summary>
    /// <param name="filePicker">Opens the system pickers behind the Add and Browse buttons.</param>
    public ProfileEditorViewModel(IFilePicker filePicker)
    {
        ArgumentNullException.ThrowIfNull(filePicker);

        _filePicker = filePicker;

        Inputs.CollectionChanged += OnInputsChanged;
        ExcludeGlobs.CollectionChanged += OnExcludesChanged;

        AddFilesCommand = new AsyncRelayCommand(OnAddFilesAsync);
        AddFolderCommand = new AsyncRelayCommand(OnAddFolderAsync);
        RemoveInputCommand = new RelayCommand(OnRemoveInput, () => SelectedInput is not null);
        ClearInputsCommand = new RelayCommand(OnClearInputs, () => Inputs.Count > 0);
        BrowseOutputCommand = new AsyncRelayCommand(OnBrowseOutputAsync);
        AddExcludeCommand = new RelayCommand(OnAddExclude, () => NewExcludeGlob.Trim().Length > 0);
        RemoveExcludeCommand = new RelayCommand(OnRemoveExclude, () => SelectedExcludeGlob is not null);
    }

    /// <summary>Raised whenever anything in the profile changes, so the window can keep up.</summary>
    public event EventHandler? Changed;

    /// <summary>The files and folders to back up.</summary>
    public ObservableCollection<string> Inputs { get; } = [];

    /// <summary>The input the list has selected, or <see langword="null"/> when none is.</summary>
    public string? SelectedInput
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                RemoveInputCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>The glob patterns whose matches are left out of the scan.</summary>
    public ObservableCollection<string> ExcludeGlobs { get; } = [];

    /// <summary>The pattern the list has selected, or <see langword="null"/> when none is.</summary>
    public string? SelectedExcludeGlob
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                RemoveExcludeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>The pattern being typed into the box above the exclude list.</summary>
    public string NewExcludeGlob
    {
        get;
        set
        {
            if (SetProperty(ref field, value ?? string.Empty))
            {
                AddExcludeCommand.NotifyCanExecuteChanged();
            }
        }
    } = string.Empty;

    /// <summary>Where the timestamped archive and its checksum file are written.</summary>
    public string OutputDirectory
    {
        get;
        set
        {
            if (SetProperty(ref field, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(CanRun));
                MarkChanged();
            }
        }
    } = string.Empty;

    /// <summary>Every compression the app offers.</summary>
    public IReadOnlyList<CompressionOption> CompressionOptions => CompressionOption.All;

    /// <summary>The compression the drop-down has selected.</summary>
    public CompressionOption SelectedCompression
    {
        get;
        set
        {
            if (SetProperty(ref field, value ?? CompressionOption.All[0]))
            {
                MarkChanged();
            }
        }
    } = CompressionOption.All[0];

    /// <summary>Whether the archive is encrypted, with a password prompted at the start of every run.</summary>
    public bool Encrypt
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                MarkChanged();
            }
        }
    }

    /// <summary>Whether the profile has been edited since it was loaded or last saved.</summary>
    public bool IsDirty
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>
    /// Whether there is enough here to run: something to back up, and somewhere to put it. This is
    /// what disables the Backup button.
    /// </summary>
    public bool CanRun => Inputs.Count > 0 && OutputDirectory.Trim().Length > 0;

    /// <summary>Adds any number of chosen files to the inputs.</summary>
    public AsyncRelayCommand AddFilesCommand { get; }

    /// <summary>Adds a chosen folder to the inputs.</summary>
    public AsyncRelayCommand AddFolderCommand { get; }

    /// <summary>Removes the selected input.</summary>
    public RelayCommand RemoveInputCommand { get; }

    /// <summary>Removes every input.</summary>
    public RelayCommand ClearInputsCommand { get; }

    /// <summary>Chooses the output folder.</summary>
    public AsyncRelayCommand BrowseOutputCommand { get; }

    /// <summary>Adds the typed pattern to the exclude list.</summary>
    public RelayCommand AddExcludeCommand { get; }

    /// <summary>Removes the selected exclude pattern.</summary>
    public RelayCommand RemoveExcludeCommand { get; }

    /// <summary>Fills the editor from a stored profile, leaving it clean.</summary>
    /// <param name="profile">The profile to show.</param>
    public void LoadFrom(BackupProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        _loading = true;
        try
        {
            Inputs.Clear();
            foreach (string input in profile.Inputs)
            {
                Inputs.Add(input);
            }

            ExcludeGlobs.Clear();
            foreach (string glob in profile.ExcludeGlobs)
            {
                ExcludeGlobs.Add(glob);
            }

            SelectedInput = null;
            SelectedExcludeGlob = null;
            NewExcludeGlob = string.Empty;
            OutputDirectory = profile.OutputDirectory;
            SelectedCompression = CompressionOption.For(profile.Compression);
            Encrypt = profile.Encrypt;
        }
        finally
        {
            _loading = false;
        }

        IsDirty = false;
        RefreshCommands();
        OnPropertyChanged(nameof(CanRun));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Empties the editor, as "New profile" does.</summary>
    public void Reset() => LoadFrom(new BackupProfile());

    /// <summary>Reads the editor back out as a profile.</summary>
    /// <param name="name">The name to give it; it becomes the prefix of every archive it produces.</param>
    /// <returns>A profile carrying exactly what the editor shows.</returns>
    public BackupProfile ToProfile(string name) =>
        new()
        {
            Name = name,
            Inputs = [.. Inputs],
            OutputDirectory = OutputDirectory.Trim(),
            Compression = SelectedCompression.Value,
            Encrypt = Encrypt,
            ExcludeGlobs = [.. ExcludeGlobs],
        };

    /// <summary>Records that what the editor holds has just been saved.</summary>
    public void MarkClean() => IsDirty = false;

    /// <summary>
    /// Adds paths to the inputs, ignoring any the profile already lists — this is what both the
    /// pickers and dropping files onto the window go through.
    /// </summary>
    /// <param name="paths">The paths to add.</param>
    /// <returns>How many were actually added.</returns>
    public int AddInputs(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        int added = 0;
        foreach (string path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            string normalized = Normalize(path);
            if (Inputs.Any(existing => string.Equals(existing, normalized, PathComparison)))
            {
                continue;
            }

            Inputs.Add(normalized);
            added++;
        }

        return added;
    }

    /// <summary>
    /// How paths are compared on this platform. Two inputs differing only in case are the same
    /// folder on Windows and two different ones on Linux, and the scan already merges what overlaps.
    /// </summary>
    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>Drops a trailing separator, so "C:\Docs\" and "C:\Docs" are not listed twice.</summary>
    private static string Normalize(string path)
    {
        string trimmed = path.Trim();

        // A root such as "/" or "C:\" is all separator; trimming it would leave nothing.
        string shortened = Path.TrimEndingDirectorySeparator(trimmed);

        return shortened.Length > 0 ? shortened : trimmed;
    }

    private async Task OnAddFilesAsync()
    {
        IReadOnlyList<string> files = await _filePicker.PickInputFilesAsync();

        AddInputs(files);
    }

    private async Task OnAddFolderAsync()
    {
        if (await _filePicker.PickFolderAsync("Add a folder to back up") is string folder)
        {
            AddInputs([folder]);
        }
    }

    private async Task OnBrowseOutputAsync()
    {
        string? startAt = OutputDirectory.Trim().Length > 0 ? OutputDirectory : null;

        if (await _filePicker.PickFolderAsync("Choose where backups are written", startAt) is string folder)
        {
            OutputDirectory = folder;
        }
    }

    private void OnRemoveInput()
    {
        if (SelectedInput is string input)
        {
            Inputs.Remove(input);
            SelectedInput = null;
        }
    }

    private void OnClearInputs()
    {
        Inputs.Clear();
        SelectedInput = null;
    }

    private void OnAddExclude()
    {
        string pattern = NewExcludeGlob.Trim();
        if (pattern.Length == 0 || ExcludeGlobs.Contains(pattern, StringComparer.Ordinal))
        {
            NewExcludeGlob = string.Empty;

            return;
        }

        ExcludeGlobs.Add(pattern);
        NewExcludeGlob = string.Empty;
    }

    private void OnRemoveExclude()
    {
        if (SelectedExcludeGlob is string pattern)
        {
            ExcludeGlobs.Remove(pattern);
            SelectedExcludeGlob = null;
        }
    }

    private void OnInputsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ClearInputsCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanRun));
        MarkChanged();
    }

    private void OnExcludesChanged(object? sender, NotifyCollectionChangedEventArgs e) => MarkChanged();

    private void RefreshCommands()
    {
        RemoveInputCommand.NotifyCanExecuteChanged();
        ClearInputsCommand.NotifyCanExecuteChanged();
        AddExcludeCommand.NotifyCanExecuteChanged();
        RemoveExcludeCommand.NotifyCanExecuteChanged();
    }

    private void MarkChanged()
    {
        if (_loading)
        {
            return;
        }

        IsDirty = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
