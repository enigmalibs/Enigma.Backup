using Avalonia.Controls;

namespace Enigma.Backup.Desktop.Services;

/// <summary>Supplies the window that dialogs are shown over and pickers are opened from.</summary>
/// <remarks>
/// The main window cannot simply be injected into the services it needs: it is built from the view
/// model that depends on them, so asking for it directly would close a cycle. The composition root
/// hands it over once, after it has been resolved.
/// </remarks>
public interface IMainWindowProvider
{
    /// <summary>The main window, or <see langword="null"/> before the app has one.</summary>
    Window? Window { get; }
}
