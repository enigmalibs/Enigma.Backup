using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Backup.Core.Encryption;
using Enigma.Backup.Core.Restoring;
using Enigma.Backup.Core.Verification;
using Enigma.Backup.Desktop.Services;

namespace Enigma.Backup.Desktop.ViewModels;

/// <summary>
/// The Verify &amp; Restore page: check an archive against its checksum, or unpack it into a folder.
/// </summary>
/// <remarks>
/// Both are picker-driven end to end — they read nothing from any profile — and both are shown in the
/// run dialog, through the same <see cref="RunCoordinator"/> a backup uses, so only one of the three can
/// run at a time.
/// </remarks>
public sealed class VerifyRestorePageViewModel : ObservableObject
{
    private readonly IRestoreService _restores;
    private readonly IVerificationService _verifications;
    private readonly IFilePicker _filePicker;
    private readonly IDialogService _dialogs;
    private readonly RunCoordinator _runs;
    private readonly IUiDispatcher _dispatcher;

    /// <summary>Creates the page's view model.</summary>
    /// <param name="restores">Runs a restore.</param>
    /// <param name="verifications">Checks an archive against its checksum.</param>
    /// <param name="filePicker">Chooses the archive, and the folder a restore writes into.</param>
    /// <param name="dialogs">Asks for an archive's password, and what to do about a restore conflict.</param>
    /// <param name="runs">Runs one operation at a time and shows it in the run dialog.</param>
    /// <param name="dispatcher">Marshals the core's progress reports onto the UI thread.</param>
    public VerifyRestorePageViewModel(
        IRestoreService restores,
        IVerificationService verifications,
        IFilePicker filePicker,
        IDialogService dialogs,
        RunCoordinator runs,
        IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(restores);
        ArgumentNullException.ThrowIfNull(verifications);
        ArgumentNullException.ThrowIfNull(filePicker);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _restores = restores;
        _verifications = verifications;
        _filePicker = filePicker;
        _dialogs = dialogs;
        _runs = runs;
        _dispatcher = dispatcher;

        _runs.PropertyChanged += OnRunsChanged;

        RestoreCommand = new AsyncRelayCommand(OnRestoreAsync, () => !_runs.IsBusy);
        VerifyCommand = new AsyncRelayCommand(OnVerifyAsync, () => !_runs.IsBusy);
    }

    /// <summary>Restores a chosen archive into a chosen folder.</summary>
    public AsyncRelayCommand RestoreCommand { get; }

    /// <summary>Checks a chosen archive against its checksum, and its encrypted blocks with the password.</summary>
    public AsyncRelayCommand VerifyCommand { get; }

    private void OnRunsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RunCoordinator.IsBusy))
        {
            RestoreCommand.NotifyCanExecuteChanged();
            VerifyCommand.NotifyCanExecuteChanged();
        }
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

        await _runs.RunAsync("restore", "Restoring", discardsOutput: false, async (run, token) =>
        {
            DispatchedProgress<RestoreProgress> progress = new(_dispatcher, report => ApplyRestoreProgress(run, report));
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

        await _runs.RunAsync("verification", "Checking the archive", discardsOutput: false, async (run, token) =>
        {
            DispatchedProgress<VerificationProgress> progress = new(_dispatcher, report => ApplyVerificationProgress(run, report));

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

    private static void ApplyRestoreProgress(RunViewModel run, RestoreProgress progress) =>
        run.Report(
            $"Restoring — {DisplayFormat.Count(progress.FilesRestored, "file", "files")}, " +
            $"{DisplayFormat.Bytes(progress.BytesRestored)} written",
            progress.CurrentPath,
            progress.PercentComplete,
            isIndeterminate: false);

    // A verification reads one file from end to end, so there is no item within it to name.
    private static void ApplyVerificationProgress(RunViewModel run, VerificationProgress progress) =>
        run.Report(
            $"Checking — {DisplayFormat.Bytes(progress.BytesProcessed)} of {DisplayFormat.Bytes(progress.TotalBytes)}",
            currentItem: null,
            progress.PercentComplete,
            isIndeterminate: false);
}
