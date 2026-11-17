using System;
using CommunityToolkit.Mvvm.Input;

namespace Enigma.Backup.Desktop.ViewModels;

/// <summary>
/// One backup, restore or verification, as its dialog shows it: how far it has got while it runs, and
/// what it left behind once it is over.
/// </summary>
/// <remarks>
/// <para>
/// A run is shown from its first report to its outcome in the same dialog, so this carries both halves.
/// <see cref="IsRunning"/> is what tells them apart, and it only ever goes one way: once
/// <see cref="Finish"/> has been called, the run is over and the dialog can be closed.
/// </para>
/// <para>
/// Cancel belongs to the run rather than to whoever started it: the button is in the dialog, and the
/// dialog is all the user can reach while a run is on screen.
/// </para>
/// </remarks>
public sealed class RunViewModel : DialogViewModel
{
    private readonly Action _cancel;

    /// <summary>Creates the state of a run that is just starting.</summary>
    /// <param name="title">What is running, as the dialog's heading — "Backing up".</param>
    /// <param name="cancel">Stops the run; called once, by <see cref="CancelCommand"/>.</param>
    public RunViewModel(string title, Action cancel)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(cancel);

        Title = title;
        _cancel = cancel;

        CancelCommand = new RelayCommand(OnCancel, () => IsRunning && !IsCancelling);
        CloseCommand = new RelayCommand(RequestClose, () => !IsRunning);
    }

    /// <summary>What is running, as the dialog's heading while it runs — "Backing up".</summary>
    public string Title { get; }

    /// <summary>
    /// The dialog's heading: <see cref="Title"/> while the run is going, then how it ended — the same
    /// three readings the outcome has.
    /// </summary>
    public string DialogTitle => Summary switch
    {
        null => Title,
        { IsSuccess: true } => "Done",
        { IsFailure: true } => "Something went wrong",
        _ => "Finished, with something to report",
    };

    /// <summary>What is happening, in one line.</summary>
    public string Message
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>The file being processed right now, or <see langword="null"/> when there is none.</summary>
    public string? CurrentItem
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>How far the run has got, from 0 to 100.</summary>
    public double Percent
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>
    /// Whether the bar should simply animate — true until the run knows how much there is to do, which
    /// for a backup is only once its scan has finished.
    /// </summary>
    public bool IsIndeterminate
    {
        get;
        private set => SetProperty(ref field, value);
    } = true;

    /// <summary>Whether the run is still going. Starts true, and turns false exactly once.</summary>
    public bool IsRunning
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(IsFinished));
                CancelCommand.NotifyCanExecuteChanged();
                CloseCommand.NotifyCanExecuteChanged();
            }
        }
    } = true;

    /// <summary>Whether the run is over, and the dialog shows its outcome.</summary>
    public bool IsFinished => !IsRunning;

    /// <summary>Whether Cancel has been pressed and the run is on its way to stopping.</summary>
    public bool IsCancelling
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                CancelCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>What the run left behind, or <see langword="null"/> while it is still going.</summary>
    public RunSummary? Summary
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasSummary));
                OnPropertyChanged(nameof(DialogTitle));
            }
        }
    }

    /// <summary>Whether there is an outcome to show.</summary>
    public bool HasSummary => Summary is not null;

    /// <summary>Stops the run. The dialog stays up to show it stopping, and then how it ended.</summary>
    public RelayCommand CancelCommand { get; }

    /// <summary>Closes the dialog — Enter, once the run is over.</summary>
    public RelayCommand CloseCommand { get; }

    /// <summary>Moves the run on. Ignored once it is over: a late report must not rewrite an outcome.</summary>
    /// <param name="message">What is happening, in one line.</param>
    /// <param name="currentItem">The file being processed, or <see langword="null"/> when there is none.</param>
    /// <param name="percent">How far the run has got, from 0 to 100.</param>
    /// <param name="isIndeterminate">Whether how much there is to do is still unknown.</param>
    public void Report(string message, string? currentItem, double percent, bool isIndeterminate)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!IsRunning)
        {
            return;
        }

        Message = message;
        CurrentItem = currentItem;
        Percent = percent;
        IsIndeterminate = isIndeterminate;
    }

    /// <summary>Ends the run: the dialog turns to its outcome and can be closed.</summary>
    /// <param name="summary">What the run left behind.</param>
    public void Finish(RunSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        if (!IsRunning)
        {
            return;
        }

        Summary = summary;
        CurrentItem = null;
        IsIndeterminate = false;
        IsCancelling = false;
        IsRunning = false;
    }

    private void OnCancel()
    {
        // The dialog's buttons do not consult CanExecute, so a press that arrives once the run is over —
        // or a second one while it stops — is refused here.
        if (!IsRunning || IsCancelling)
        {
            return;
        }

        // Only the line changes: the bar and the file being worked on are still true until the run
        // actually stops, and the dialog is what the user is looking at when they press this.
        Message = "Cancelling…";
        IsCancelling = true;

        _cancel();
    }
}
