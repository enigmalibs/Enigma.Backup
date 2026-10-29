using System;

namespace Enigma.Backup.Desktop.Services;

/// <summary>
/// The safety net: every exception nothing else caught ends up here, and is logged with where it was
/// caught.
/// </summary>
/// <remarks>
/// A safety net, not the error handling. A failure the app knows about is said where it happens, in
/// that place's own words — a run's failure in the outcome banner, an unreadable profile in a message —
/// and never reaches this.
/// </remarks>
public interface IUnhandledErrorReporter
{
    /// <summary>Logs an exception, from any thread.</summary>
    /// <param name="exception">The exception.</param>
    /// <param name="source">Where it was caught.</param>
    void Report(Exception exception, UnhandledErrorSource source);

    /// <summary>Says the app is closing: from here on a cancellation is never worth a word.</summary>
    void ShutdownStarted();
}
