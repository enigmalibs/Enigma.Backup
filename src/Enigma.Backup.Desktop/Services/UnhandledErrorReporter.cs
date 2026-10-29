using System;
using Microsoft.Extensions.Logging;

namespace Enigma.Backup.Desktop.Services;

/// <summary>The default <see cref="IUnhandledErrorReporter"/>.</summary>
/// <remarks>
/// The log is written at once, on whatever thread reported, because that thread may be about to end the
/// process. Only a cancellation is told apart, by <see cref="UnhandledErrorPolicy"/>: one someone asked
/// for, or any once the app is closing, is a debug line rather than an error.
/// </remarks>
public sealed class UnhandledErrorReporter : IUnhandledErrorReporter
{
    private readonly ILogger<UnhandledErrorReporter> _logger;
    private readonly UnhandledErrorPolicy _policy;

    /// <summary>Creates the reporter.</summary>
    /// <param name="time">Measures how long ago an error was seen.</param>
    /// <param name="logger">Receives every exception, in full.</param>
    public UnhandledErrorReporter(TimeProvider time, ILogger<UnhandledErrorReporter> logger)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _policy = new UnhandledErrorPolicy(time);
    }

    /// <inheritdoc />
    public void Report(Exception exception, UnhandledErrorSource source)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (_policy.IsIgnoredCancellation(exception))
        {
            _logger.LogDebug(exception, "A cancellation reached the safety net ({Source}); it is not an error.", source);

            return;
        }

        _logger.LogError(exception, "An unhandled exception reached the safety net ({Source}).", source);
    }

    /// <inheritdoc />
    public void ShutdownStarted() => _policy.IsShuttingDown = true;
}
