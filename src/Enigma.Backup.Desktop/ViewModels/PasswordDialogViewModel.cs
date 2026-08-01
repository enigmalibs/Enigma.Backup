using System;
using CommunityToolkit.Mvvm.Input;
using Enigma.Backup.Desktop.Services;

namespace Enigma.Backup.Desktop.ViewModels;

/// <summary>
/// The password prompt: typed twice when it is about to encrypt a new archive, once when it is
/// opening one that already exists.
/// </summary>
/// <remarks>
/// The password lives here only for as long as the dialog does. It is a parameter to the run that
/// follows and is never written to a profile, a setting or the log — see the plan's "passwords are
/// parameters, never state".
/// </remarks>
public sealed class PasswordDialogViewModel : DialogViewModel
{
    /// <summary>Creates a prompt.</summary>
    /// <param name="purpose">Whether the password protects a new archive or opens an existing one.</param>
    /// <param name="subject">What the password is for — a profile name, or an archive's file name.</param>
    public PasswordDialogViewModel(PasswordPurpose purpose, string subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        Purpose = purpose;
        RequiresConfirmation = purpose == PasswordPurpose.ProtectNewArchive;

        (Title, Prompt) = purpose switch
        {
            PasswordPurpose.ProtectNewArchive => (
                "Choose a password",
                $"'{subject}' encrypts its archives. Choose the password this backup will be locked with — " +
                "it is not stored anywhere, and without it the archive cannot be opened again."),
            _ => (
                "Password required",
                $"'{subject}' is encrypted. Enter its password."),
        };

        AcceptCommand = new RelayCommand(OnAccept, () => CanAccept);
        CancelCommand = new RelayCommand(OnCancel);
    }

    /// <summary>Whether the password protects a new archive or opens an existing one.</summary>
    public PasswordPurpose Purpose { get; }

    /// <summary>The dialog's title.</summary>
    public string Title { get; }

    /// <summary>The sentence above the password box.</summary>
    public string Prompt { get; }

    /// <summary>Whether the password has to be typed a second time to guard against a typo.</summary>
    public bool RequiresConfirmation { get; }

    /// <summary>The password being typed.</summary>
    public string Password
    {
        get;
        set
        {
            if (SetProperty(ref field, value ?? string.Empty))
            {
                OnValidationChanged();
            }
        }
    } = string.Empty;

    /// <summary>The second copy of the password; unused when <see cref="RequiresConfirmation"/> is false.</summary>
    public string Confirmation
    {
        get;
        set
        {
            if (SetProperty(ref field, value ?? string.Empty))
            {
                OnValidationChanged();
            }
        }
    } = string.Empty;

    /// <summary>
    /// What is wrong with what has been typed so far, or <see langword="null"/> when there is nothing
    /// to say. Silent while a field is simply still empty — an untouched form is not an error.
    /// </summary>
    public string? ValidationMessage
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>Whether the password can be accepted as it stands.</summary>
    public bool CanAccept
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>The accepted password, or <see langword="null"/> while the dialog is open or once it was cancelled.</summary>
    public string? Result { get; private set; }

    /// <summary>Accepts the password and closes the dialog.</summary>
    public RelayCommand AcceptCommand { get; }

    /// <summary>Abandons the prompt, and with it the run that asked for it.</summary>
    public RelayCommand CancelCommand { get; }

    private void OnValidationChanged()
    {
        // Empty rather than whitespace, matching the core: a password of spaces is a password, and
        // refusing it here would make archives the application itself would accept unwritable.
        bool hasPassword = Password.Length > 0;
        bool matches = !RequiresConfirmation || string.Equals(Password, Confirmation, StringComparison.Ordinal);

        ValidationMessage = !matches && Confirmation.Length > 0
            ? "The two passwords do not match."
            : null;

        CanAccept = hasPassword && matches;
        AcceptCommand.NotifyCanExecuteChanged();
    }

    private void OnAccept()
    {
        Result = Password;
        RequestClose();
    }

    private void OnCancel()
    {
        Result = null;
        RequestClose();
    }
}
