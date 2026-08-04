using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Enigma.Avalonia.Desktop.Controls.ContentDialog;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Backup.Core.Errors;
using Enigma.Backup.Core.Restoring;
using Enigma.Backup.Desktop.ViewModels;
using Enigma.Backup.Desktop.Views;

namespace Enigma.Backup.Desktop.Services;

/// <summary>The <see cref="IDialogService"/> that asks in the window, through the library's dialog host.</summary>
/// <remarks>
/// <para>
/// Every question follows the same shape: build the dialog's view model, raise the app's one
/// <c>ContentDialog</c> host around a <see cref="UserControl"/> that shows it, and read the answer
/// back off the view model once the dialog closes. The answer never travels through
/// <see cref="DialogResult"/>, so it stays strongly typed and a dismissal is not a special case —
/// <see cref="DialogResult.None"/> from Escape or a click on the scrim leaves the view model holding
/// whatever it decided a dismissal means, which for both decision dialogs is Abort.
/// </para>
/// <para>
/// The core asks its questions from whatever thread its pipeline is on, so every dialog is raised
/// through <see cref="IUiDispatcher"/>; callers never have to know where they are.
/// </para>
/// <para>
/// This is one of the two places allowed to name the control library's services — the view models
/// see only <see cref="IDialogService"/>, which is what keeps them testable without a UI.
/// </para>
/// </remarks>
public sealed class DialogService : IDialogService
{
    private readonly IContentDialogService _dialogs;
    private readonly IUiDispatcher _dispatcher;

    /// <summary>Creates a dialog service.</summary>
    /// <param name="dialogs">The library's dialog host, registered by <see cref="App"/> at startup.</param>
    /// <param name="dispatcher">Marshals every dialog onto the UI thread.</param>
    public DialogService(IContentDialogService dialogs, IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _dialogs = dialogs;
        _dispatcher = dispatcher;
    }

    /// <inheritdoc />
    public Task<string?> RequestPasswordAsync(PasswordPurpose purpose, string subject, CancellationToken cancellationToken = default) =>
        ShowAsync(
            new PasswordDialogViewModel(purpose, subject),
            static viewModel => new PasswordDialog(viewModel),
            static (viewModel, dialog, button) =>
            {
                dialog.Title = viewModel.Title;
                dialog.PrimaryButtonText = "OK";
                dialog.PrimaryButtonCommand = button(viewModel.AcceptCommand);
                dialog.CloseButtonText = "Cancel";
                dialog.CloseButtonCommand = button(viewModel.CancelCommand);
                dialog.DefaultButton = DefaultButton.Primary;
            },
            static viewModel =>
            {
                string? password = viewModel.Result;

                // The host keeps its content until the next dialog replaces it, so the box would
                // otherwise hold the plaintext in the visual tree for as long as the app runs. Its
                // text is bound, so clearing the view model clears it. Passwords stay parameters.
                viewModel.Password = string.Empty;
                viewModel.Confirmation = string.Empty;

                return password;
            },
            cancellationToken,
            isPrimaryEnabled: static viewModel => viewModel.CanAccept);

    /// <inheritdoc />
    public Task<string?> RequestProfileNameAsync(ProfileNameRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ShowAsync(
            new ProfileNameDialogViewModel(request),
            static viewModel => new ProfileNameDialog(viewModel),
            static (viewModel, dialog, button) =>
            {
                dialog.Title = viewModel.Title;
                dialog.PrimaryButtonText = "OK";
                dialog.PrimaryButtonCommand = button(viewModel.AcceptCommand);
                dialog.CloseButtonText = "Cancel";
                dialog.CloseButtonCommand = button(viewModel.CancelCommand);
                dialog.DefaultButton = DefaultButton.Primary;
            },
            static viewModel => viewModel.Result,
            cancellationToken,
            isPrimaryEnabled: static viewModel => viewModel.CanAccept);
    }

    /// <inheritdoc />
    public Task<BackupErrorDecision> RequestErrorDecisionAsync(BackupError error, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(error);

        return ShowAsync(
            new ErrorDecisionDialogViewModel(error),
            static viewModel => new ErrorDecisionDialog(viewModel),
            static (viewModel, dialog, button) =>
            {
                dialog.Title = viewModel.Title;
                dialog.PrimaryButtonText = "Skip";
                dialog.PrimaryButtonCommand = button(viewModel.SkipCommand);
                dialog.SecondaryButtonText = "Retry";
                dialog.SecondaryButtonCommand = button(viewModel.RetryCommand);
                dialog.CloseButtonText = "Abort the backup";
                dialog.CloseButtonCommand = button(viewModel.AbortCommand);
                dialog.DefaultButton = DefaultButton.Primary;
            },
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
            static (viewModel, dialog, button) =>
            {
                dialog.Title = "The file already exists";
                dialog.PrimaryButtonText = "Keep the file";
                dialog.PrimaryButtonCommand = button(viewModel.SkipCommand);
                dialog.SecondaryButtonText = "Overwrite";
                dialog.SecondaryButtonCommand = button(viewModel.OverwriteCommand);
                dialog.CloseButtonText = "Stop the restore";
                dialog.CloseButtonCommand = button(viewModel.AbortCommand);
                dialog.DefaultButton = DefaultButton.Primary;
            },
            static viewModel => viewModel.Decision,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task ShowMessageAsync(string title, string message, MessageSeverity severity, CancellationToken cancellationToken = default) =>
        await ShowMessageAsync(new MessageDialogViewModel(title, message, severity), cancellationToken);

    /// <inheritdoc />
    public Task<bool> ConfirmAsync(string title, string message, string confirmLabel, CancellationToken cancellationToken = default) =>
        ShowMessageAsync(new MessageDialogViewModel(title, message, MessageSeverity.Warning, confirmLabel), cancellationToken);

    private Task<bool> ShowMessageAsync(MessageDialogViewModel question, CancellationToken cancellationToken) =>
        ShowAsync(
            question,
            static viewModel => new MessageDialog(viewModel),
            static (viewModel, dialog, button) =>
            {
                dialog.Title = viewModel.Title;
                dialog.PrimaryButtonText = viewModel.ConfirmLabel;
                dialog.PrimaryButtonCommand = button(viewModel.ConfirmCommand);
                dialog.DefaultButton = DefaultButton.Primary;

                // A plain message has nothing to decline, so it gets no second button. Escape and the
                // scrim still dismiss it, which leaves Confirmed false — the cautious answer.
                if (viewModel.IsConfirmation)
                {
                    dialog.CloseButtonText = "Cancel";
                    dialog.CloseButtonCommand = button(viewModel.CancelCommand);
                }
            },
            static viewModel => viewModel.Confirmed,
            cancellationToken);

    /// <summary>Raises one dialog in the window and reads its answer back.</summary>
    /// <typeparam name="TViewModel">The dialog's view model.</typeparam>
    /// <typeparam name="TResult">What the answer is.</typeparam>
    /// <param name="viewModel">The view model, already built with the question.</param>
    /// <param name="createContent">Builds the control that shows it.</param>
    /// <param name="configure">
    /// Titles the dialog and maps the question's answers onto its own Primary/Secondary/Close
    /// buttons. Each command must be passed through the supplied wrapper.
    /// </param>
    /// <param name="readResult">Reads the answer once the dialog has closed.</param>
    /// <param name="cancellationToken">Closes the dialog if the run it belongs to is stopped.</param>
    /// <param name="isPrimaryEnabled">
    /// Whether the primary button can be pressed, for a question that validates what was typed. The
    /// dialog's buttons ignore a command's <c>CanExecute</c>, so this is what greys OK out.
    /// </param>
    private Task<TResult> ShowAsync<TViewModel, TResult>(
        TViewModel viewModel,
        Func<TViewModel, Control> createContent,
        Action<TViewModel, ContentDialog, Func<ICommand, ICommand>> configure,
        Func<TViewModel, TResult> readResult,
        CancellationToken cancellationToken,
        Func<TViewModel, bool>? isPrimaryEnabled = null)
        where TViewModel : DialogViewModel =>
        _dispatcher.InvokeAsync(async () =>
        {
            // A run that has already been stopped does not get to ask: the view model's own default
            // stands in, which for every decision dialog is the cautious answer.
            if (cancellationToken.IsCancellationRequested)
            {
                return readResult(viewModel);
            }

            bool closed = false;
            bool closingItself = false;

            // The dialog closes itself right after running the command bound to one of its buttons.
            // Closing it a second time from here would hand the *next* dialog this one's result, so
            // this acts only on an answer that arrived another way — the content's Enter binding.
            void OnCloseRequested(object? sender, EventArgs e)
            {
                if (!closingItself && !closed)
                {
                    _ = _dialogs.HideAsync();
                }
            }

            ICommand Button(ICommand inner) => new RelayCommand(() =>
            {
                closingItself = true;

                // The dialog's buttons do not consult CanExecute, so a still-invalid answer has to be
                // refused here. The dialog closes regardless, and an unanswered view model reads as
                // a dismissal — which is why the button is also greyed out (see isPrimaryEnabled).
                if (inner.CanExecute(null))
                {
                    inner.Execute(null);
                }
            });

            PropertyChangedEventHandler? onViewModelChanged = null;

            viewModel.CloseRequested += OnCloseRequested;

            // A run stopped while its question is on screen takes the question with it. The callback
            // is posted, so it is guarded on `closed` as well: there is one dialog host for the whole
            // app, and a late registration would otherwise close whatever had been asked since.
            await using CancellationTokenRegistration registration = cancellationToken.Register(
                () => _dispatcher.Post(() =>
                {
                    if (!closed)
                    {
                        _ = _dialogs.HideAsync();
                    }
                }));

            try
            {
                await _dialogs.ShowAsync(dialog =>
                {
                    dialog.Content = createContent(viewModel);
                    configure(viewModel, dialog, Button);

                    if (isPrimaryEnabled is not null)
                    {
                        dialog.IsPrimaryButtonEnabled = isPrimaryEnabled(viewModel);
                        onViewModelChanged = (_, _) => dialog.IsPrimaryButtonEnabled = isPrimaryEnabled(viewModel);
                        viewModel.PropertyChanged += onViewModelChanged;
                    }
                });
            }
            finally
            {
                closed = true;
                viewModel.CloseRequested -= OnCloseRequested;

                if (onViewModelChanged is not null)
                {
                    viewModel.PropertyChanged -= onViewModelChanged;
                }
            }

            return readResult(viewModel);
        });
}
