using System.Collections.Generic;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Desktop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace Enigma.Backup.Desktop.Tests.TestSupport;

/// <summary>
/// A <see cref="MainWindowViewModel"/> wired to fakes, with each of them to hand.
/// </summary>
/// <remarks>
/// This is the whole desktop layer minus Avalonia: real view models, fake seams. Everything the
/// window can do can be driven from here, and nothing opens.
/// </remarks>
internal sealed class Harness
{
    /// <summary>Creates a harness whose store starts empty.</summary>
    public Harness()
    {
        Runs = new RunCoordinator(RunDialog, NullLogger<RunCoordinator>.Instance);
        Catalog = new ProfileCatalog(Profiles, Dialogs, NullLogger<ProfileCatalog>.Instance);
        BackupEditor = new ProfileEditorViewModel(FilePicker, Dialogs);
        ProfileEditor = new ProfileEditorViewModel(FilePicker, Dialogs);
        FromProfileEditor = new ProfileEditorViewModel(FilePicker, Dialogs);
        BackupPage = new BackupPageViewModel(Backups, BackupEditor, FromProfileEditor, Catalog, Dialogs, Runs, Dispatcher);
        VerifyRestorePage = new VerifyRestorePageViewModel(Restores, Verifications, FilePicker, Dialogs, Runs, Dispatcher);
        ProfilesPage = new ProfilesPageViewModel(Profiles, Catalog, ProfileEditor, FilePicker, Dialogs, NullLogger<ProfilesPageViewModel>.Instance);
        SettingsPage = new SettingsPageViewModel(AppSettings, Themes, Launcher, Dialogs);
        Shell = new MainWindowViewModel(BackupPage, VerifyRestorePage, ProfilesPage, SettingsPage);
    }

    /// <summary>The in-memory profile store.</summary>
    public FakeProfileStore Profiles { get; } = new();

    /// <summary>The scripted backup service.</summary>
    public FakeBackupService Backups { get; } = new();

    /// <summary>The scripted restore service.</summary>
    public FakeRestoreService Restores { get; } = new();

    /// <summary>The scripted verification service.</summary>
    public FakeVerificationService Verifications { get; } = new();

    /// <summary>The scripted file and folder pickers.</summary>
    public FakeFilePicker FilePicker { get; } = new();

    /// <summary>The scripted dialogs.</summary>
    public FakeDialogService Dialogs { get; } = new();

    /// <summary>The run dialog, recording every run it was shown.</summary>
    public FakeRunDialog RunDialog { get; } = new();

    /// <summary>The dispatcher that runs everything inline.</summary>
    public ImmediateUiDispatcher Dispatcher { get; } = new();

    /// <summary>The Backup page's one-off backup.</summary>
    public ProfileEditorViewModel BackupEditor { get; }

    /// <summary>The profile the Profiles page is editing.</summary>
    public ProfileEditorViewModel ProfileEditor { get; }

    /// <summary>The profile the Backup page shows, read-only, when it backs up a profile.</summary>
    public ProfileEditorViewModel FromProfileEditor { get; }

    /// <summary>The profiles every page lists, over <see cref="Profiles"/>.</summary>
    public ProfileCatalog Catalog { get; }

    /// <summary>The one coordinator every page runs through.</summary>
    public RunCoordinator Runs { get; }

    /// <summary>The Backup page, around <see cref="BackupEditor"/>.</summary>
    public BackupPageViewModel BackupPage { get; }

    /// <summary>The Verify &amp; Restore page.</summary>
    public VerifyRestorePageViewModel VerifyRestorePage { get; }

    /// <summary>The Profiles page, around <see cref="ProfileEditor"/>.</summary>
    public ProfilesPageViewModel ProfilesPage { get; }

    /// <summary>The preferences file.</summary>
    public FakeAppSettingsStore AppSettings { get; } = new();

    /// <summary>The theme the app is painted in.</summary>
    public FakeThemeSwitcher Themes { get; } = new();

    /// <summary>The desktop's folder opener.</summary>
    public FakeShellLauncher Launcher { get; } = new();

    /// <summary>The Settings page.</summary>
    public SettingsPageViewModel SettingsPage { get; }

    /// <summary>The window, holding every page.</summary>
    public MainWindowViewModel Shell { get; }

    public static BackupProfile Profile(
        string name = "Documents",
        IList<string>? inputs = null,
        string outputDirectory = "/backups",
        CompressionAlgorithm compression = CompressionAlgorithm.Zstd,
        bool encrypt = false,
        IList<string>? excludeGlobs = null) =>
        new()
        {
            Name = name,
            Inputs = inputs ?? ["/home/jo/Documents"],
            OutputDirectory = outputDirectory,
            Compression = compression,
            Encrypt = encrypt,
            ExcludeGlobs = excludeGlobs ?? [],
        };

    /// <summary>Puts the Backup page's one-off editor in a state a backup can run from.</summary>
    /// <param name="encrypt">Whether the profile encrypts, which decides if a password is prompted for.</param>
    public void FillEditor(bool encrypt = false)
    {
        BackupEditor.AddInputs(["/home/jo/Documents"]);
        BackupEditor.OutputDirectory = "/backups";
        BackupEditor.Encrypt = encrypt;
    }

    /// <summary>Gives the Profiles page's editor the same minimal profile <see cref="FillEditor"/> gives the Backup page.</summary>
    public void FillProfileEditor()
    {
        ProfileEditor.AddInputs(["/home/jo/Documents"]);
        ProfileEditor.OutputDirectory = "/backups";
    }
}
