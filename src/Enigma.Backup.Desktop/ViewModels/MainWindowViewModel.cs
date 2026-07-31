using CommunityToolkit.Mvvm.ComponentModel;

namespace Enigma.Backup.Desktop.ViewModels;

/// <summary>View model for the application's main window.</summary>
public sealed class MainWindowViewModel : ObservableObject
{
    /// <summary>Text shown in the window's title bar and in the taskbar.</summary>
    public string Title { get; set => SetProperty(ref field, value); } = "Enigma.Backup";
}
