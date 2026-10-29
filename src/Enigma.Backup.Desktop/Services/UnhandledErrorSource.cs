namespace Enigma.Backup.Desktop.Services;

/// <summary>Where an exception reached the safety net from.</summary>
public enum UnhandledErrorSource
{
    /// <summary>A command, an event handler or a posted callback threw on the UI thread.</summary>
    Dispatcher,

    /// <summary>A task faulted and nothing ever looked at it.</summary>
    UnobservedTask,

    /// <summary>An exception escaped on a thread with nothing above it; the process ends after it.</summary>
    AppDomain,

    /// <summary>The start of the app failed before the main window could be shown.</summary>
    Startup,
}
