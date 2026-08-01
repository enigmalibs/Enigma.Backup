using System;
using CommunityToolkit.Mvvm.Input;
using Enigma.Backup.Desktop.Services;

namespace Enigma.Backup.Desktop.ViewModels;

/// <summary>
/// The one-message dialog, in both of its forms: something to acknowledge, or something to confirm
/// before it happens.
/// </summary>
public sealed class MessageDialogViewModel : DialogViewModel
{
    /// <summary>Creates a dialog that only has to be acknowledged.</summary>
    /// <param name="title">The dialog's title.</param>
    /// <param name="message">What to say.</param>
    /// <param name="severity">How much weight to give it.</param>
    public MessageDialogViewModel(string title, string message, MessageSeverity severity)
        : this(title, message, severity, confirmLabel: null)
    {
    }

    /// <summary>Creates a dialog in either form.</summary>
    /// <param name="title">The dialog's title.</param>
    /// <param name="message">What to say.</param>
    /// <param name="severity">How much weight to give it.</param>
    /// <param name="confirmLabel">
    /// The wording of the confirming button — "Delete", say — which also makes this a confirmation
    /// with a Cancel beside it. <see langword="null"/> for a plain message with a single OK.
    /// </param>
    public MessageDialogViewModel(string title, string message, MessageSeverity severity, string? confirmLabel)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);

        Title = title;
        Message = message;
        Severity = severity;
        IsConfirmation = confirmLabel is not null;
        ConfirmLabel = confirmLabel ?? "OK";

        ConfirmCommand = new RelayCommand(OnConfirm);
        CancelCommand = new RelayCommand(OnCancel);
    }

    /// <summary>The dialog's title.</summary>
    public string Title { get; }

    /// <summary>What the dialog says.</summary>
    public string Message { get; }

    /// <summary>How much weight the message carries.</summary>
    public MessageSeverity Severity { get; }

    /// <summary>Whether a Cancel button sits beside the confirming one.</summary>
    public bool IsConfirmation { get; }

    /// <summary>The wording of the confirming button.</summary>
    public string ConfirmLabel { get; }

    /// <summary>Whether the message is a failure, which the window shows in its accent.</summary>
    public bool IsError => Severity == MessageSeverity.Error;

    /// <summary>Whether the message is a caveat, which the window shows in its accent.</summary>
    public bool IsWarning => Severity == MessageSeverity.Warning;

    /// <summary>
    /// Whether the confirming button was pressed. A dismissed window leaves this
    /// <see langword="false"/>, so a confirmation nobody answered does not go ahead.
    /// </summary>
    public bool Confirmed { get; private set; }

    /// <summary>Acknowledges the message, or agrees to what it asked.</summary>
    public RelayCommand ConfirmCommand { get; }

    /// <summary>Declines; only shown for a confirmation.</summary>
    public RelayCommand CancelCommand { get; }

    private void OnConfirm()
    {
        Confirmed = true;
        RequestClose();
    }

    private void OnCancel()
    {
        Confirmed = false;
        RequestClose();
    }
}
