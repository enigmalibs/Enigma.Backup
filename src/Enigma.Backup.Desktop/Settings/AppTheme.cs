namespace Enigma.Backup.Desktop.Settings;

/// <summary>
/// Which variant the app paints itself in. Three values, not two: "follow the operating system" has
/// to be a value of its own, or it would exist only until the first click and could never be
/// returned to.
/// </summary>
public enum AppTheme
{
    /// <summary>Follow the operating system, and keep following it when it changes. The default.</summary>
    System,

    /// <summary>Always light, whatever the system is set to.</summary>
    Light,

    /// <summary>Always dark, whatever the system is set to.</summary>
    Dark,
}
