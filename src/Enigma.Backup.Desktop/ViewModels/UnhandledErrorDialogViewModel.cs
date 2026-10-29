using System;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using Enigma.Backup.Desktop.Services;

namespace Enigma.Backup.Desktop.ViewModels;

/// <summary>
/// What the safety net's dialog says: that something went wrong and the app is still running, what went
/// wrong, how many more errors came, and the details for a bug report.
/// </summary>
/// <remarks>
/// It follows its batch: an error counted into it while the dialog is open shows at once, in the count
/// and in the details. Copying the details is the view's job — the clipboard belongs to the window, and
/// nothing here may name it.
/// </remarks>
public sealed class UnhandledErrorDialogViewModel : DialogViewModel
{
    private const string RunningLead =
        "Something went wrong, but the application is still running. You can carry on, or close it and "
        + "start it again if something looks wrong.";

    private const string StartupLead =
        "Something went wrong while the application was starting, so it cannot open. Closing this "
        + "message ends it; the details below say what failed.";

    private readonly UnhandledErrorBatch _batch;

    /// <summary>Creates the view model over the errors to show, in an app that is still running.</summary>
    /// <param name="batch">The errors.</param>
    public UnhandledErrorDialogViewModel(UnhandledErrorBatch batch)
        : this(batch, RunningLead)
    {
    }

    private UnhandledErrorDialogViewModel(UnhandledErrorBatch batch, string lead)
    {
        ArgumentNullException.ThrowIfNull(batch);

        _batch = batch;
        Lead = lead;
        CloseCommand = new RelayCommand(RequestClose);

        batch.Changed += OnBatchChanged;
    }

    /// <summary>The sentence that opens the dialog.</summary>
    public string Lead { get; }

    /// <summary>What went wrong.</summary>
    public string Message => _batch.Message;

    /// <summary>Whether more errors came after the first.</summary>
    public bool HasMore => _batch.More > 0;

    /// <summary>How many more errors came after the first, as a sentence.</summary>
    public string MoreText =>
        _batch.More == 1
            ? "1 more error occurred."
            : $"{_batch.More.ToString(CultureInfo.CurrentCulture)} more errors occurred.";

    /// <summary>The details of every error, for a bug report.</summary>
    public string Details => _batch.Details;

    /// <summary>Closes the dialog. It asks nothing, so there is nothing to accept.</summary>
    public RelayCommand CloseCommand { get; }

    /// <summary>
    /// The dialog for a start that failed before the main window could open: the same dialog, except
    /// that it does not claim the app is still running — it is not, and closing it ends the process.
    /// </summary>
    /// <param name="exception">What failed.</param>
    /// <returns>The view model.</returns>
    public static UnhandledErrorDialogViewModel ForStartupFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return new UnhandledErrorDialogViewModel(new UnhandledErrorBatch(exception), StartupLead);
    }

    private void OnBatchChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(HasMore));
        OnPropertyChanged(nameof(MoreText));
        OnPropertyChanged(nameof(Details));
    }
}
