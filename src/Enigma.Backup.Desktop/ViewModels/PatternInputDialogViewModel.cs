using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Desktop.Services;

namespace Enigma.Backup.Desktop.ViewModels;

/// <summary>
/// The prompt behind "Add pattern…": the folder a pattern applies to, and the glob matched inside it.
/// </summary>
/// <remarks>
/// <para>
/// The base folder is chosen with the folder picker rather than typed, so the pair the profile stores
/// is a real folder plus a relative glob — which is the whole reason a pattern is two fields and not
/// one path with a wildcard in it.
/// </para>
/// <para>
/// The pattern's rules are the core's, not a second set invented here: it is matched relative to the
/// base folder, so a rooted one could never match and is refused while it is being typed rather than
/// silently backing nothing up.
/// </para>
/// </remarks>
public sealed class PatternInputDialogViewModel : DialogViewModel
{
    private readonly IFilePicker _filePicker;

    /// <summary>Creates the prompt.</summary>
    /// <param name="filePicker">Opens the folder picker behind the Choose button.</param>
    public PatternInputDialogViewModel(IFilePicker filePicker)
    {
        ArgumentNullException.ThrowIfNull(filePicker);

        _filePicker = filePicker;

        ChooseBaseDirectoryCommand = new AsyncRelayCommand(OnChooseBaseDirectoryAsync);
        AcceptCommand = new RelayCommand(OnAccept, () => CanAccept);
        CancelCommand = new RelayCommand(OnCancel);
    }

    /// <summary>The dialog's title.</summary>
    public string Title => "Add a pattern";

    /// <summary>The sentence above the two fields.</summary>
    public string Prompt =>
        "A pattern is worked out again on every run. Choose the folder it applies to, then say what to match inside it.";

    /// <summary>How the pattern is read, shown under the box so the rules are where the typing is.</summary>
    public string Help =>
        "* matches inside one folder level and ** at any depth, so *.log takes the logs in the folder itself " +
        "and **/*.log takes them everywhere below it. A pattern that matches a folder's name takes that " +
        "folder with everything in it.";

    /// <summary>The folder the pattern is resolved against, as the picker returned it.</summary>
    public string BaseDirectory
    {
        get;
        private set
        {
            if (SetProperty(ref field, value ?? string.Empty))
            {
                OnValidationChanged();
            }
        }
    } = string.Empty;

    /// <summary>The glob being typed.</summary>
    public string Pattern
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
    /// Why the pair cannot be used, or <see langword="null"/> when it can. Silent while there is
    /// nothing to complain about yet.
    /// </summary>
    public string? ValidationMessage
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>Whether the pair can be accepted as it stands.</summary>
    public bool CanAccept
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>The accepted pattern, or <see langword="null"/> while open and once cancelled.</summary>
    public PatternInput? Result { get; private set; }

    /// <summary>Chooses the folder the pattern applies to.</summary>
    public AsyncRelayCommand ChooseBaseDirectoryCommand { get; }

    /// <summary>Accepts the pattern and closes the dialog.</summary>
    public RelayCommand AcceptCommand { get; }

    /// <summary>Abandons the prompt.</summary>
    public RelayCommand CancelCommand { get; }

    /// <summary>
    /// Whether a glob is rooted, which a pattern matched relative to a base folder can never be. Both
    /// platforms' shapes are refused whichever one this is, because a profile is portable.
    /// </summary>
    /// <param name="pattern">The trimmed pattern.</param>
    private static bool IsRooted(string pattern) =>
        pattern[0] is '/' or '\\' || (pattern.Length >= 2 && pattern[1] == ':');

    private async Task OnChooseBaseDirectoryAsync()
    {
        string? startAt = BaseDirectory.Length > 0 ? BaseDirectory : null;

        if (await _filePicker.PickFolderAsync("Choose the folder the pattern applies to", startAt) is string folder)
        {
            BaseDirectory = folder;
        }
    }

    private void OnValidationChanged()
    {
        string pattern = Pattern.Trim();

        if (BaseDirectory.Length == 0)
        {
            // Nothing typed yet is not a mistake; a pattern without a folder to apply it to is.
            ValidationMessage = pattern.Length == 0 ? null : "Choose the folder the pattern applies to.";
            SetAcceptable(false);

            return;
        }

        if (pattern.Length == 0)
        {
            ValidationMessage = null;
            SetAcceptable(false);

            return;
        }

        if (IsRooted(pattern))
        {
            ValidationMessage = "A pattern is matched inside the folder above, so it cannot start with a separator or a drive.";
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
        Result = new PatternInput(BaseDirectory, Pattern.Trim());
        RequestClose();
    }

    private void OnCancel()
    {
        Result = null;
        RequestClose();
    }
}
