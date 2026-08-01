using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Enigma.Backup.Core.Errors;
using Enigma.Backup.Core.Restoring;
using Enigma.Backup.Desktop.ViewModels;
using Enigma.Backup.Desktop.Views;

namespace Enigma.Backup.Desktop.Services;

/// <summary>The <see cref="IDialogService"/> that opens real windows.</summary>
/// <remarks>
/// <para>
/// Every question follows the same shape: build the dialog's view model, show a window over the main
/// one, and read the answer back off the view model once it closes. The answer never travels through
/// <c>Window.Close(result)</c>, so it stays strongly typed and a window closed with its title-bar
/// button is not a special case — the view model already holds what a dismissal means.
/// </para>
/// <para>
/// The core asks its questions from whatever thread its pipeline is on, so every dialog is opened
/// through <see cref="IUiDispatcher"/>; callers never have to know where they are.
/// </para>
/// </remarks>
public sealed class DialogService : IDialogService
{
    private readonly IMainWindowProvider _windows;
    private readonly IUiDispatcher _dispatcher;

    /// <summary>Creates a dialog service.</summary>
    /// <param name="windows">Supplies the window dialogs are shown over.</param>
    /// <param name="dispatcher">Marshals every dialog onto the UI thread.</param>
    public DialogService(IMainWindowProvider windows, IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _windows = windows;
        _dispatcher = dispatcher;
    }

    /// <inheritdoc />
    public Task<string?> RequestPasswordAsync(PasswordPurpose purpose, string subject, CancellationToken cancellationToken = default) =>
        ShowAsync(
            new PasswordDialogViewModel(purpose, subject),
            static viewModel => new PasswordDialog(viewModel),
            static viewModel => viewModel.Result,
            cancellationToken);

    /// <inheritdoc />
    public Task<string?> RequestProfileNameAsync(ProfileNameRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ShowAsync(
            new ProfileNameDialogViewModel(request),
            static viewModel => new ProfileNameDialog(viewModel),
            static viewModel => viewModel.Result,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<BackupErrorDecision> RequestErrorDecisionAsync(BackupError error, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(error);

        return ShowAsync(
            new ErrorDecisionDialogViewModel(error),
            static viewModel => new ErrorDecisionDialog(viewModel),
            static viewModel => viewModel.Decision,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<RestoreConflictDecision> RequestConflictDecisionAsync(RestoreConflict conflict, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conflict);

        return ShowAsync(
            new RestoreConflictDialogViewModel(conflict),
            static viewModel => new RestoreConflictDialog(viewModel),
            static viewModel => viewModel.Decision,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task ShowMessageAsync(string title, string message, MessageSeverity severity, CancellationToken cancellationToken = default) =>
        await ShowAsync(
            new MessageDialogViewModel(title, message, severity),
            static viewModel => new MessageDialog(viewModel),
            static viewModel => viewModel.Confirmed,
            cancellationToken);

    /// <inheritdoc />
    public Task<bool> ConfirmAsync(string title, string message, string confirmLabel, CancellationToken cancellationToken = default) =>
        ShowAsync(
            new MessageDialogViewModel(title, message, MessageSeverity.Warning, confirmLabel),
            static viewModel => new MessageDialog(viewModel),
            static viewModel => viewModel.Confirmed,
            cancellationToken);

    /// <summary>Shows one dialog and reads its answer back.</summary>
    /// <typeparam name="TViewModel">The dialog's view model.</typeparam>
    /// <typeparam name="TResult">What the answer is.</typeparam>
    /// <param name="viewModel">The view model, already built with the question.</param>
    /// <param name="createWindow">Builds the window around it.</param>
    /// <param name="readResult">Reads the answer once the window has closed.</param>
    /// <param name="cancellationToken">Closes the dialog if the run it belongs to is stopped.</param>
    private Task<TResult> ShowAsync<TViewModel, TResult>(
        TViewModel viewModel,
        Func<TViewModel, Window> createWindow,
        Func<TViewModel, TResult> readResult,
        CancellationToken cancellationToken)
        where TViewModel : DialogViewModel =>
        _dispatcher.InvokeAsync(async () =>
        {
            // Nothing to be modal to, or a run that has already been stopped: in both cases the view
            // model's own default stands in, which for every decision dialog is the cautious answer.
            // The first can only happen before the app has a window — that is, before anything can
            // ask a question at all.
            if (_windows.Window is not Window owner || cancellationToken.IsCancellationRequested)
            {
                return readResult(viewModel);
            }

            Window dialog = createWindow(viewModel);

            // A run stopped while its question is on screen takes the question with it. Guarded on
            // IsVisible because a registration can fire before the dialog has opened, and closing a
            // window that was never shown would leave it unable to open.
            await using CancellationTokenRegistration registration = cancellationToken.Register(
                () => _dispatcher.Post(() =>
                {
                    if (dialog.IsVisible)
                    {
                        dialog.Close();
                    }
                }));

            await dialog.ShowDialog(owner);

            return readResult(viewModel);
        });
}
