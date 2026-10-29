using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Enigma.Avalonia.Desktop.Controls.ContentDialog;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Backup.Core.Errors;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Core.Restoring;
using Enigma.Backup.Desktop.Settings;
using Enigma.Backup.Desktop.ViewModels;
using Enigma.Backup.Desktop.Views;
using Enigma.Icons.Avalonia;
using Enigma.Icons.Phosphor;

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
/// There is one host for the whole app, and showing a dialog on it replaces whatever it held. So the
/// dialogs take turns: one asked for while another is open waits for it to close. Nothing in the
/// workflow asks two questions at once; the safety net's dialog is what can arrive in the middle of
/// one, and it must wait rather than take the question's place.
/// </para>
/// <para>
/// This is one of the two places allowed to name the control library's services — the view models
/// see only <see cref="IDialogService"/>, which is what keeps them testable without a UI.
/// </para>
/// </remarks>
public sealed class DialogService : IDialogService
{
    /// <summary>The host's turn: held from the moment a dialog is raised until it has closed.</summary>
    private readonly SemaphoreSlim _turn = new(1, 1);

    private readonly IContentDialogService _dialogs;
    private readonly IUiDispatcher _dispatcher;
    private readonly IFilePicker _filePicker;
    private readonly IAppSettingsStore _settings;
    private readonly IThemeSwitcher _themes;
    private readonly IShellLauncher _launcher;

    /// <summary>Creates a dialog service.</summary>
    /// <param name="dialogs">The library's dialog host, registered by <see cref="App"/> at startup.</param>
    /// <param name="dispatcher">Marshals every dialog onto the UI thread.</param>
    /// <param name="filePicker">
    /// The folder picker the pattern prompt puts behind its Choose button — held here for the same
    /// reason the preferences are: this is where each dialog's view model is built.
    /// </param>
    /// <param name="settings">
    /// The preferences the Settings dialog reads and writes. Held here for the same reason the other
    /// questions' data is: this is where each dialog's view model is built.
    /// </param>
    /// <param name="themes">Applies the theme the Settings dialog chooses.</param>
    /// <param name="launcher">Opens the storage folder the Settings dialog offers.</param>
    public DialogService(
        IContentDialogService dialogs,
        IUiDispatcher dispatcher,
        IFilePicker filePicker,
        IAppSettingsStore settings,
        IThemeSwitcher themes,
        IShellLauncher launcher)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(filePicker);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(themes);
        ArgumentNullException.ThrowIfNull(launcher);

        _dialogs = dialogs;
        _dispatcher = dispatcher;
        _filePicker = filePicker;
        _settings = settings;
        _themes = themes;
        _launcher = launcher;
    }

    /// <inheritdoc />
    public Task<string?> RequestPasswordAsync(PasswordPurpose purpose, string subject, CancellationToken cancellationToken = default) =>
        ShowAsync(
            new PasswordDialogViewModel(purpose, subject),
            static viewModel => new PasswordDialog(viewModel),
            static (viewModel, dialog, button) =>
            {
                dialog.Title = viewModel.Title;
                dialog.IconData = Glyph(PhosphorIcon.LockKey);
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
                dialog.IconData = Glyph(PhosphorIcon.PencilSimple);
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
    public Task<PatternInput?> RequestPatternInputAsync(CancellationToken cancellationToken = default) =>
        ShowAsync(
            new PatternInputDialogViewModel(_filePicker),
            static viewModel => new PatternInputDialog(viewModel),
            static (viewModel, dialog, button) =>
            {
                dialog.Title = viewModel.Title;
                dialog.IconData = Glyph(PhosphorIcon.Asterisk);
                dialog.PrimaryButtonText = "Add";
                dialog.PrimaryButtonCommand = button(viewModel.AcceptCommand);
                dialog.CloseButtonText = "Cancel";
                dialog.CloseButtonCommand = button(viewModel.CancelCommand);
                dialog.DefaultButton = DefaultButton.Primary;
            },
            static viewModel => viewModel.Result,
            cancellationToken,
            isPrimaryEnabled: static viewModel => viewModel.CanAccept);

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
                dialog.IconData = Glyph(PhosphorIcon.Warning);
                dialog.IconBrush = Brush("EnigmaWarningBrush");
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
                dialog.IconData = Glyph(PhosphorIcon.Warning);
                dialog.IconBrush = Brush("EnigmaWarningBrush");
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
    public async Task ShowSettingsAsync(CancellationToken cancellationToken = default) =>
        await ShowAsync(
            new SettingsDialogViewModel(_settings, _themes, _launcher),
            static viewModel => new SettingsDialog(viewModel),
            static (_, dialog, _) =>
            {
                dialog.Title = "Settings";
                dialog.IconData = Glyph(PhosphorIcon.GearSix);

                // Close, not OK: there is nothing to accept. Every choice inside has already applied,
                // so the button that would have cancelled them has nothing to undo either.
                dialog.CloseButtonText = "Close";
            },

            // No answer to read: the dialog's own state is the answer, and it is already on disk.
            static _ => true,
            cancellationToken);

    /// <inheritdoc />
    public async Task ShowAboutAsync(CancellationToken cancellationToken = default) =>
        await ShowAsync(
            new AboutDialogViewModel(),
            static viewModel => new AboutDialog(viewModel),
            static (_, dialog, _) =>
            {
                // No title and no icon: the card draws them as a column beside the content, and the
                // content already shows the app's own icon and name above everything else.
                dialog.Title = null;

                // One button, and it only dismisses: the dialog asks nothing.
                dialog.CloseButtonText = "Close";
                dialog.DefaultButton = DefaultButton.Close;
            },
            static _ => true,
            cancellationToken);

    /// <inheritdoc />
    public async Task ShowUnhandledErrorAsync(UnhandledErrorBatch errors, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(errors);

        await ShowAsync(
            new UnhandledErrorDialogViewModel(errors),
            static viewModel => new UnhandledErrorDialog(viewModel),
            static (_, dialog, _) =>
            {
                dialog.Title = "Something went wrong";
                dialog.IconData = Glyph(PhosphorIcon.Warning);
                dialog.IconBrush = Brush("EnigmaWarningBrush");

                // One button, and it only dismisses: the app is still running, and there is nothing to
                // decide.
                dialog.CloseButtonText = "Close";
                dialog.DefaultButton = DefaultButton.Close;
            },
            static _ => true,
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

                // The severity is the whole difference between "here is what happened" and "this went
                // wrong", so it is what the header shows. ConfirmAsync builds its question as a
                // Warning, which is why "delete this profile?" carries the warning icon rather than
                // the information one.
                (PhosphorIcon icon, string? brushKey) = viewModel switch
                {
                    { IsError: true } => (PhosphorIcon.WarningCircle, "EnigmaErrorBrush"),
                    { IsWarning: true } => (PhosphorIcon.Warning, "EnigmaWarningBrush"),
                    _ => (PhosphorIcon.Info, null),
                };

                dialog.IconData = Glyph(icon);

                if (brushKey is not null)
                {
                    dialog.IconBrush = Brush(brushKey);
                }

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

    /// <summary>One Phosphor glyph, as the geometry a dialog header draws.</summary>
    /// <param name="icon">Which glyph.</param>
    private static Geometry Glyph(PhosphorIcon icon) =>
        PhosphorIconSet.Instance.GetGlyph(icon, PhosphorWeight.Regular).ToGeometry();

    /// <summary>One of the theme's brushes, by key.</summary>
    /// <param name="key">The <c>Enigma*</c> brush key.</param>
    /// <returns>The brush, or <see langword="null"/> — which leaves the dialog its own default.</returns>
    /// <remarks>
    /// A dynamic resource cannot be assigned from here, but it does not need to be: the theme's brushes
    /// are not theme-scoped, only their colours are, so this one instance keeps following a variant
    /// switch. There is no <c>Application</c> at all in a headless test, hence the null.
    /// </remarks>
    private static IBrush? Brush(string key) =>
        Application.Current?.TryFindResource(key, out object? value) == true ? value as IBrush : null;

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

            // Wait for the host: a dialog already open keeps it until it closes. A run stopped while its
            // question waits here gets the same answer as one stopped before asking.
            try
            {
                await _turn.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return readResult(viewModel);
            }

            try
            {
                return await ShowOnHostAsync(viewModel, createContent, configure, readResult, cancellationToken, isPrimaryEnabled);
            }
            finally
            {
                _turn.Release();
            }
        });

    /// <summary>Raises one dialog on the host, which the caller holds the turn for, and reads its answer back.</summary>
    /// <typeparam name="TViewModel">The dialog's view model.</typeparam>
    /// <typeparam name="TResult">What the answer is.</typeparam>
    /// <param name="viewModel">The view model, already built with the question.</param>
    /// <param name="createContent">Builds the control that shows it.</param>
    /// <param name="configure">Titles the dialog and maps the question's answers onto its buttons.</param>
    /// <param name="readResult">Reads the answer once the dialog has closed.</param>
    /// <param name="cancellationToken">Closes the dialog if the run it belongs to is stopped.</param>
    /// <param name="isPrimaryEnabled">Whether the primary button can be pressed.</param>
    private async Task<TResult> ShowOnHostAsync<TViewModel, TResult>(
        TViewModel viewModel,
        Func<TViewModel, Control> createContent,
        Action<TViewModel, ContentDialog, Func<ICommand, ICommand>> configure,
        Func<TViewModel, TResult> readResult,
        CancellationToken cancellationToken,
        Func<TViewModel, bool>? isPrimaryEnabled)
        where TViewModel : DialogViewModel
    {
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
    }
}
