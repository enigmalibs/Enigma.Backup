using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Enigma.Avalonia.Desktop.Controls.ContentDialog;
using Enigma.Backup.Desktop.ViewModels;
using Enigma.Backup.Desktop.Views;
using Enigma.Icons.Phosphor;

namespace Enigma.Backup.Desktop.Services;

/// <summary>The <see cref="IRunDialog"/> that shows a run on a <see cref="ContentDialog"/> of its own.</summary>
/// <remarks>
/// <para>
/// A host of its own, not the one <see cref="DialogService"/> asks its questions on: a run raises
/// questions while it is on screen — an unreadable file, a restore conflict, a password — and those have
/// to appear *above* it. On the one shared host they would replace the run, or wait for it to close and
/// never be answered. <c>MainWindow</c> puts this host just below the question host for that reason.
/// </para>
/// <para>
/// The library's dialog closes itself on any footer button, on Escape and on a click on its scrim, and
/// offers no way to refuse. While the run is going none of those may end it — a stray click must not
/// abandon a long backup's progress, and only Cancel may stop it — so a close that arrives while the run
/// is still going is undone at once: the dialog is reopened in the same call that closed it, before
/// anything is drawn. Cancel's own close is undone the same way; the dialog stays up to show the run
/// stopping, and then how it ended.
/// </para>
/// <para>
/// Every call arrives on the UI thread already: a run is started by a command, and the core's progress
/// reaches the run through <see cref="DispatchedProgress{T}"/>.
/// </para>
/// </remarks>
public sealed class RunDialog : IRunDialog
{
    private ContentDialog? _host;
    private RunViewModel? _run;

    /// <summary>
    /// Hands the dialog the control it draws on. <see cref="App"/> calls this once, before the window
    /// is shown; <see cref="ShowAsync"/> throws until it has.
    /// </summary>
    /// <param name="host">The window's run dialog host.</param>
    public void SetHost(ContentDialog host)
    {
        ArgumentNullException.ThrowIfNull(host);

        if (_host is not null)
        {
            _host.Closed -= OnClosed;
        }

        _host = host;
        _host.Closed += OnClosed;
    }

    /// <inheritdoc />
    public Task ShowAsync(RunViewModel run)
    {
        ArgumentNullException.ThrowIfNull(run);

        ContentDialog host = _host
            ?? throw new InvalidOperationException("The run dialog has no host. Call SetHost first.");

        Release();

        _run = run;
        run.PropertyChanged += OnRunChanged;
        run.CloseRequested += OnCloseRequested;

        host.Content = new RunView(run);
        Configure(host, run);
        host.IsOpen = true;

        return Task.CompletedTask;
    }

    /// <summary>Titles the dialog and sets its one button for where the run stands.</summary>
    private static void Configure(ContentDialog host, RunViewModel run)
    {
        host.Title = run.DialogTitle;

        if (run.IsRunning)
        {
            host.IconData = ThemeAssets.Glyph(PhosphorIcon.HourglassMedium);
            host.IconBrush = null;
            host.CloseButtonText = "Cancel";
            host.CloseButtonCommand = run.CancelCommand;

            // The dialog's buttons ignore CanExecute: a run already stopping is shown by greying the
            // button out, so a second press is not mistaken for something it could still do.
            host.IsCloseButtonEnabled = !run.IsCancelling;
            host.DefaultButton = DefaultButton.None;

            return;
        }

        // Over: Close is all there is left to do, and any way of closing now means it.
        (PhosphorIcon icon, string brushKey) = run.Summary switch
        {
            { IsSuccess: true } => (PhosphorIcon.CheckCircle, "EnigmaSuccessBrush"),
            { IsFailure: true } => (PhosphorIcon.WarningCircle, "EnigmaErrorBrush"),
            _ => (PhosphorIcon.Warning, "EnigmaWarningBrush"),
        };

        host.IconData = ThemeAssets.Glyph(icon);
        host.IconBrush = ThemeAssets.Brush(brushKey);
        host.CloseButtonText = "Close";
        host.CloseButtonCommand = null;
        host.IsCloseButtonEnabled = true;
        host.DefaultButton = DefaultButton.Close;
    }

    private void OnRunChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_host is ContentDialog host && ReferenceEquals(sender, _run) && _run is RunViewModel run
            && e.PropertyName is nameof(RunViewModel.IsRunning) or nameof(RunViewModel.IsCancelling) or nameof(RunViewModel.DialogTitle))
        {
            Configure(host, run);
        }
    }

    /// <summary>Enter, once the run is over: the content asks, and the host closes.</summary>
    private void OnCloseRequested(object? sender, EventArgs e)
    {
        if (_host is ContentDialog host && ReferenceEquals(sender, _run) && _run is { IsRunning: false })
        {
            _ = host.HideAsync();
        }
    }

    private void OnClosed(object? sender, DialogResult result)
    {
        if (_run is not RunViewModel run || sender is not ContentDialog host)
        {
            return;
        }

        if (run.IsRunning)
        {
            // Escape, the scrim, or Cancel's own close: none of them ends a run that is still going.
            host.IsOpen = true;

            return;
        }

        Release();
        host.Content = null;
    }

    /// <summary>Stops following the run the dialog showed, if any.</summary>
    private void Release()
    {
        if (_run is RunViewModel run)
        {
            run.PropertyChanged -= OnRunChanged;
            run.CloseRequested -= OnCloseRequested;
        }

        _run = null;
    }
}
