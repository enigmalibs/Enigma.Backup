using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Enigma.Backup.Desktop.Services;

/// <summary>The default <see cref="IUnhandledErrorReporter"/>.</summary>
/// <remarks>
/// <para>
/// The log is written at once, on whatever thread reported, because that thread may be about to end the
/// process. What is shown is decided on the UI thread, by <see cref="UnhandledErrorPolicy"/>, and shown
/// through <see cref="IDialogService"/> — which waits for any question already on the one dialog host
/// to be answered, so the error never takes that question's place.
/// </para>
/// <para>
/// One exception often arrives twice: a command that faults is rethrown on the dispatcher, and the task
/// it faulted is later found unobserved as well. The second arrival is a debug line, not a second error.
/// </para>
/// <para>
/// If the dialog itself fails, that is logged and no dialog is tried again in this session: the net
/// must not loop on itself.
/// </para>
/// </remarks>
public sealed class UnhandledErrorReporter : IUnhandledErrorReporter
{
    private readonly IDialogService _dialogs;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogger<UnhandledErrorReporter> _logger;
    private readonly UnhandledErrorPolicy _policy;

    // Every exception already logged. Thread-safe, and weak, so a reported exception is not kept alive.
    private readonly ConditionalWeakTable<Exception, object> _reported = [];

    private bool _broken;

    /// <summary>Creates the reporter.</summary>
    /// <param name="dialogs">Shows the dialog.</param>
    /// <param name="dispatcher">Takes every decision onto the UI thread, where the dialog lives.</param>
    /// <param name="time">Measures how long ago an error was seen.</param>
    /// <param name="logger">Receives every exception, in full.</param>
    public UnhandledErrorReporter(
        IDialogService dialogs,
        IUiDispatcher dispatcher,
        TimeProvider time,
        ILogger<UnhandledErrorReporter> logger)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);

        _dialogs = dialogs;
        _dispatcher = dispatcher;
        _logger = logger;
        _policy = new UnhandledErrorPolicy(time);
    }

    /// <inheritdoc />
    public void Report(Exception exception, UnhandledErrorSource source)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (!_reported.TryAdd(exception, source))
        {
            _logger.LogDebug("An exception already reported reached the safety net again ({Source}).", source);

            return;
        }

        if (_policy.IsIgnoredCancellation(exception))
        {
            _logger.LogDebug(exception, "A cancellation reached the safety net ({Source}); it is not an error.", source);

            return;
        }

        _logger.LogError(exception, "An unhandled exception reached the safety net ({Source}).", source);

        _dispatcher.Post(() => Offer(exception));
    }

    /// <inheritdoc />
    public void ShutdownStarted() => _policy.IsShuttingDown = true;

    private void Offer(Exception exception)
    {
        switch (_policy.Offer(exception))
        {
            case UnhandledErrorDecision.Queue:
                _ = PresentAsync();
                break;
            case UnhandledErrorDecision.Suppress:
                _logger.LogDebug(
                    "The same error was shown less than {Window} ago; it is not shown again.",
                    UnhandledErrorPolicy.RepeatWindow);
                break;
            default:
                break;
        }
    }

    /// <summary>Shows the pending batch, then any that came while it was open. Never throws: nothing awaits it.</summary>
    private async Task PresentAsync()
    {
        while (!_broken && _policy.BeginShow() is { } batch)
        {
            try
            {
                await _dialogs.ShowUnhandledErrorAsync(batch);
            }
            catch (Exception exception)
            {
                // Logged, never reported: a dialog that cannot be shown would otherwise report itself
                // again and again.
                _broken = true;
                _logger.LogError(exception, "The safety net's dialog could not be shown; later errors are only logged.");
            }
            finally
            {
                _policy.EndShow();
            }
        }
    }
}
