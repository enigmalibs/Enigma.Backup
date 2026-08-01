namespace Enigma.Backup.Desktop.Services;

/// <summary>How much weight a message dialog should carry.</summary>
public enum MessageSeverity
{
    /// <summary>Something worked, or is simply worth knowing.</summary>
    Information,

    /// <summary>Something finished, but not entirely as asked.</summary>
    Warning,

    /// <summary>Something did not work.</summary>
    Error,
}
