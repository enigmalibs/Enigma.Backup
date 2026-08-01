using Avalonia.Controls;

namespace Enigma.Backup.Desktop.Services;

/// <summary>The <see cref="IMainWindowProvider"/> the composition root fills in at startup.</summary>
public sealed class MainWindowProvider : IMainWindowProvider
{
    /// <summary>The main window; set once by <see cref="App"/> when it has been resolved.</summary>
    public Window? Window { get; set; }
}
