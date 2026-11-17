using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Enigma.Backup.Core.Errors;
using Enigma.Backup.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace Enigma.Backup.Desktop.ViewModels;

/// <summary>
/// Runs one backup, restore or verification at a time, whichever page starts it, and shows it in the run
/// dialog from its first report to its outcome.
/// </summary>
/// <remarks>
/// <para>
/// One instance for the whole app: the pages that start runs share it, so a run started on one page
/// makes every page's run commands unavailable until it is over, and there is one place that guarantees
/// a run always ends closable.
/// </para>
/// <para>
/// Nothing here knows about Avalonia. A test drives it through a fake <see cref="IRunDialog"/> and reads
/// what each run was shown.
/// </para>
/// </remarks>
public sealed class RunCoordinator : ObservableObject
{
    private readonly IRunDialog _runDialog;
    private readonly ILogger<RunCoordinator> _logger;

    /// <summary>Creates the coordinator.</summary>
    /// <param name="runDialog">Shows each run, its Cancel button and then its outcome.</param>
    /// <param name="logger">Records what each run did, to the console and the debugger.</param>
    public RunCoordinator(IRunDialog runDialog, ILogger<RunCoordinator> logger)
    {
        ArgumentNullException.ThrowIfNull(runDialog);
        ArgumentNullException.ThrowIfNull(logger);

        _runDialog = runDialog;
        _logger = logger;
    }

    /// <summary>Whether a backup, restore or verification is running.</summary>
    public bool IsBusy
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>
    /// Runs one operation with everything that surrounds every operation: the busy state, the
    /// cancellation source behind the Cancel button, the dialog that shows it running and then how it
    /// ended, and a summary whatever the outcome.
    /// </summary>
    /// <param name="what">What is running, as it appears in messages — "backup", "restore", …</param>
    /// <param name="title">The same thing as the run dialog's heading — "Backing up", "Restoring", …</param>
    /// <param name="discardsOutput">Whether cancelling it also deletes a partial output file.</param>
    /// <param name="operation">
    /// The operation, handed the run its progress is reported on, which returns the summary of what it did.
    /// </param>
    /// <returns>
    /// What the run left behind, or <see langword="null"/> when it did not start because another one was
    /// already running.
    /// </returns>
    public async Task<RunSummary?> RunAsync(
        string what,
        string title,
        bool discardsOutput,
        Func<RunViewModel, CancellationToken, Task<RunSummary>> operation)
    {
        ArgumentNullException.ThrowIfNull(what);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(operation);

        if (IsBusy)
        {
            return null;
        }

        using CancellationTokenSource cancellation = new();

        // The run can only be cancelled while it is going, and it stops going in the finally below —
        // before this source is disposed at the end of the method.
        RunViewModel run = new(title, cancellation.Cancel);
        RunSummary? summary = null;

        IsBusy = true;

        try
        {
            await _runDialog.ShowAsync(run);

            run.Report($"Starting the {what}…", currentItem: null, percent: 0, isIndeterminate: true);
            _logger.LogInformation("Starting the {Operation}.", what);

            summary = await operation(run, cancellation.Token);
            _logger.LogInformation("The {Operation} finished: {Headline}", what, summary.Headline);
        }
        catch (OperationCanceledException)
        {
            summary = RunSummary.Cancelled(what, discardsOutput);
            _logger.LogInformation("The {Operation} was cancelled.", what);
        }
        catch (BackupAbortedException exception)
        {
            // The failure that was aborted on, not the wrapper's own "the run was aborted:" prefix.
            summary = RunSummary.Aborted(exception.Error?.Message ?? exception.Message);
            _logger.LogWarning(exception, "The {Operation} was stopped by an Abort answer.", what);
        }
        catch (Exception exception)
        {
            // A run must not take the window down with it: whatever went wrong becomes the summary.
            summary = RunSummary.Failed(what, exception.Message);
            _logger.LogError(exception, "The {Operation} failed.", what);
        }
        finally
        {
            IsBusy = false;

            // Whatever happened above, including a failure the catches did not expect: a run that is
            // never finished is a dialog that cannot be closed over an app nobody can click.
            summary ??= RunSummary.Failed(what, "it stopped without saying why.");
            run.Finish(summary);
        }

        return summary;
    }
}
