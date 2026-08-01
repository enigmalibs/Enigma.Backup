using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Enigma.Backup.Desktop.ViewModels;

/// <summary>
/// The part every modal dialog's view model shares: the ability to say it is finished.
/// </summary>
/// <remarks>
/// The answer itself stays on the derived view model rather than travelling through
/// <c>Window.Close(result)</c>, so a caller reads it off the view model it created. That keeps the
/// answer strongly typed, and it means a dialog closed with the window button is not a special case:
/// the view model still holds whatever it decided a dismissal means.
/// </remarks>
public abstract class DialogViewModel : ObservableObject
{
    /// <summary>Raised when the dialog has its answer and the window should close.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Asks the window hosting this view model to close.</summary>
    protected void RequestClose() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
