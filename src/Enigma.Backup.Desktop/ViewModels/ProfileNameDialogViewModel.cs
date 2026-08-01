using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Input;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Desktop.Services;

namespace Enigma.Backup.Desktop.ViewModels;

/// <summary>
/// The prompt behind "Save as" and "Rename": a profile name, checked before it is used rather than
/// after the store has refused it.
/// </summary>
/// <remarks>
/// A profile's name is its file name, so the rules are the core's
/// (<see cref="ProfileName.IsValid(string?)"/>) and not a second set invented here. Surrounding
/// spaces are trimmed rather than rejected — typing one is a slip, not an intent.
/// </remarks>
public sealed class ProfileNameDialogViewModel : DialogViewModel
{
    private readonly HashSet<string> _takenNames;

    /// <summary>Creates a prompt.</summary>
    /// <param name="request">What to ask, and which names are already taken.</param>
    public ProfileNameDialogViewModel(ProfileNameRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Title = request.Title;
        Prompt = request.Prompt;
        _takenNames = new HashSet<string>(request.TakenNames, StringComparer.OrdinalIgnoreCase);

        AcceptCommand = new RelayCommand(OnAccept, () => CanAccept);
        CancelCommand = new RelayCommand(OnCancel);

        Name = request.InitialName;
    }

    /// <summary>The dialog's title.</summary>
    public string Title { get; }

    /// <summary>The sentence above the text box.</summary>
    public string Prompt { get; }

    /// <summary>The name being typed.</summary>
    public string Name
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
    /// Why the name cannot be used, or <see langword="null"/> when it can. Silent while the box is
    /// still empty.
    /// </summary>
    public string? ValidationMessage
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>Whether the name can be accepted as it stands.</summary>
    public bool CanAccept
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>The accepted name, trimmed, or <see langword="null"/> while open or once cancelled.</summary>
    public string? Result { get; private set; }

    /// <summary>Accepts the name and closes the dialog.</summary>
    public RelayCommand AcceptCommand { get; }

    /// <summary>Abandons the prompt.</summary>
    public RelayCommand CancelCommand { get; }

    private void OnValidationChanged()
    {
        string candidate = Name.Trim();

        if (candidate.Length == 0)
        {
            ValidationMessage = null;
            SetAcceptable(false);

            return;
        }

        if (!ProfileName.IsValid(candidate))
        {
            ValidationMessage =
                $"A profile name can be up to {ProfileName.MaxLength} characters and cannot contain " +
                @"< > : "" / \ | ? * or end with a dot.";
            SetAcceptable(false);

            return;
        }

        if (_takenNames.Contains(candidate))
        {
            ValidationMessage = $"A profile named '{candidate}' already exists.";
            SetAcceptable(false);

            return;
        }

        ValidationMessage = null;
        SetAcceptable(true);
    }

    private void SetAcceptable(bool acceptable)
    {
        CanAccept = acceptable;
        AcceptCommand.NotifyCanExecuteChanged();
    }

    private void OnAccept()
    {
        Result = Name.Trim();
        RequestClose();
    }

    private void OnCancel()
    {
        Result = null;
        RequestClose();
    }
}
