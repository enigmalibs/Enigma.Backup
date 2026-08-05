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
        Editor = new ProfileEditorViewModel(FilePicker);
        ViewModel = new MainWindowViewModel(
            Profiles,
            Backups,
            Restores,
            Verifications,
            Editor,
            FilePicker,
            Dialogs,
            Overlay,
            Notifications,
            Dispatcher,
            NullLogger<MainWindowViewModel>.Instance);
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

    /// <summary>The run card, recording what it was shown and told.</summary>
    public FakeProgressOverlay Overlay { get; } = new();

    /// <summary>The outcome banners.</summary>
    public FakeNotificationService Notifications { get; } = new();

    /// <summary>The dispatcher that runs everything inline.</summary>
    public ImmediateUiDispatcher Dispatcher { get; } = new();

    /// <summary>The profile editor the window hosts.</summary>
    public ProfileEditorViewModel Editor { get; }

    /// <summary>The view model under test.</summary>
    public MainWindowViewModel ViewModel { get; }

    /// <summary>Builds a profile with sensible defaults, overriding only what a test cares about.</summary>
    /// <param name="name">The profile's name.</param>
    /// <param name="inputs">What it backs up.</param>
    /// <param name="outputDirectory">Where its archives go.</param>
    /// <param name="compression">Which compression it uses.</param>
    /// <param name="encrypt">Whether it encrypts.</param>
    /// <param name="excludeGlobs">Its exclude patterns.</param>
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

    /// <summary>Puts the editor in a state a backup can run from.</summary>
    /// <param name="encrypt">Whether the profile encrypts, which decides if a password is prompted for.</param>
    public void FillEditor(bool encrypt = false)
    {
        Editor.AddInputs(["/home/jo/Documents"]);
        Editor.OutputDirectory = "/backups";
        Editor.Encrypt = encrypt;
    }
}
