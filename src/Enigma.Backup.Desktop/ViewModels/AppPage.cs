namespace Enigma.Backup.Desktop.ViewModels;

/// <summary>The pages the window's rail switches between.</summary>
public enum AppPage
{
    /// <summary>Back up files, folders and patterns, or a saved profile.</summary>
    Backup,

    /// <summary>Check an archive, or unpack it into a folder.</summary>
    VerifyRestore,

    /// <summary>Create, edit and manage profiles.</summary>
    Profiles,

    /// <summary>The app's own preferences, and About.</summary>
    Settings,
}
