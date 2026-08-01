using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Backup.Core.Errors;

namespace Enigma.Backup.Desktop.ViewModels;

/// <summary>
/// The Skip / Retry / Abort prompt a run raises for every file it cannot read.
/// </summary>
/// <remarks>
/// <para>
/// "Apply to all further errors" is honoured for Skip and Abort but not for Retry — the core's
/// <see cref="ErrorDecisionPolicy"/> refuses to make Retry sticky, because a permanent failure
/// answered with "retry, always" would never stop. The dialog matches that rather than promising
/// something the run would ignore.
/// </para>
/// <para>
/// Closing the window without choosing counts as Abort. Skipping silently is the one answer that
/// quietly leaves data out of a backup, so it is never the one nobody asked for.
/// </para>
/// </remarks>
public sealed class ErrorDecisionDialogViewModel : DialogViewModel
{
    /// <summary>Creates a prompt for one failure.</summary>
    /// <param name="error">The failure to show.</param>
    public ErrorDecisionDialogViewModel(BackupError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        Error = error;
        Title = error.Kind switch
        {
            BackupErrorKind.MissingInput => "A path is missing",
            BackupErrorKind.AccessDenied => "Access was denied",
            _ => "A file could not be read",
        };

        SkipCommand = new RelayCommand(() => Decide(BackupErrorAction.Skip));
        RetryCommand = new RelayCommand(() => Decide(BackupErrorAction.Retry));
        AbortCommand = new RelayCommand(() => Decide(BackupErrorAction.Abort));
    }

    /// <summary>The failure being asked about.</summary>
    public BackupError Error { get; }

    /// <summary>The dialog's title, which names the kind of failure.</summary>
    public string Title { get; }

    /// <summary>The file or directory that could not be read.</summary>
    public string Path => Error.Path;

    /// <summary>The core's ready-to-display explanation.</summary>
    public string Message => Error.Message;

    /// <summary>Whether the same answer should be used for every further error of this run.</summary>
    public bool ApplyToAll
    {
        get;
        set => SetProperty(ref field, value);
    }

    /// <summary>
    /// What was decided. Abort until a button is pressed, so a window dismissed with its close
    /// button stops the run rather than silently dropping the file.
    /// </summary>
    public BackupErrorDecision Decision { get; private set; } = BackupErrorDecision.Abort();

    /// <summary>Leaves this entry out of the archive and carries on.</summary>
    public RelayCommand SkipCommand { get; }

    /// <summary>Tries the same file again.</summary>
    public RelayCommand RetryCommand { get; }

    /// <summary>Stops the run and discards the partial archive.</summary>
    public RelayCommand AbortCommand { get; }

    private void Decide(BackupErrorAction action)
    {
        // Retry is never sticky, whatever the checkbox says — see the remarks.
        Decision = action == BackupErrorAction.Retry
            ? BackupErrorDecision.Retry()
            : new BackupErrorDecision(action, ApplyToAll);

        RequestClose();
    }
}
