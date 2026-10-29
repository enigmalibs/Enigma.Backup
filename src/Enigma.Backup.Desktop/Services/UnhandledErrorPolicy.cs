using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Enigma.Backup.Desktop.Services;

/// <summary>
/// Decides what the safety net shows: which exceptions are not errors at all, which wait for the dialog
/// already there, and which were seen too recently to show again. Nothing here touches a window, so it
/// is tested without one.
/// </summary>
/// <remarks>
/// <para>
/// One dialog at a time. An error that comes while one waits to be shown, or while one is on screen, is
/// counted into it ("2 more errors occurred") rather than queued behind it: a loop of failures must not
/// open a dialog per failure. An error identical to one the user closed less than
/// <see cref="RepeatWindow"/> ago is logged and not shown again, so something that fails on every
/// click does not reopen the dialog on every click.
/// </para>
/// <para>
/// Every member is called on the UI thread, except <see cref="IsIgnoredCancellation"/>, which only reads.
/// </para>
/// </remarks>
public sealed class UnhandledErrorPolicy
{
    /// <summary>How long an error the user has seen is not shown again.</summary>
    public static readonly TimeSpan RepeatWindow = TimeSpan.FromMinutes(1);

    private static readonly object Seen = new();

    private readonly TimeProvider _time;
    private readonly ConditionalWeakTable<Exception, object> _offered = [];
    private readonly Dictionary<string, long> _closedAt = new(StringComparer.Ordinal);

    /// <summary>Creates a policy.</summary>
    /// <param name="time">Measures how long ago an error was seen.</param>
    public UnhandledErrorPolicy(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);

        _time = time;
    }

    /// <summary>Whether the app is closing, when no cancellation is worth a word.</summary>
    public bool IsShuttingDown { get; set; }

    /// <summary>The batch waiting to be shown, or <see langword="null"/>.</summary>
    public UnhandledErrorBatch? Pending { get; private set; }

    /// <summary>The batch on screen, or <see langword="null"/>.</summary>
    public UnhandledErrorBatch? Showing { get; private set; }

    /// <summary>
    /// Answers whether an exception is a cancellation that is no error: one while the app closes, or one
    /// whose own token was cancelled — a stop someone asked for. A cancellation nobody asked for, such as
    /// a timeout, is an error.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns><see langword="true"/> when it is not worth showing.</returns>
    public bool IsIgnoredCancellation(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            OperationCanceledException cancelled => IsShuttingDown || cancelled.CancellationToken.IsCancellationRequested,
            AggregateException { InnerExceptions.Count: > 0 } aggregate => AllIgnored(aggregate),
            _ => false,
        };
    }

    /// <summary>Decides what an exception does.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The decision; on <see cref="UnhandledErrorDecision.Queue"/>, <see cref="Pending"/> holds it.</returns>
    public UnhandledErrorDecision Offer(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // The same exception can reach two hooks: a fault rethrown on the dispatcher whose task the
        // finaliser also finds unobserved.
        if (_offered.TryGetValue(exception, out _))
        {
            return UnhandledErrorDecision.Ignore;
        }

        _offered.Add(exception, Seen);

        if (IsIgnoredCancellation(exception))
        {
            return UnhandledErrorDecision.Ignore;
        }

        if ((Showing ?? Pending) is { } open)
        {
            open.Add(exception);

            return UnhandledErrorDecision.Merge;
        }

        if (_closedAt.TryGetValue(SignatureOf(exception), out long closed) && _time.GetElapsedTime(closed) < RepeatWindow)
        {
            return UnhandledErrorDecision.Suppress;
        }

        Pending = new UnhandledErrorBatch(exception);

        return UnhandledErrorDecision.Queue;
    }

    /// <summary>Takes the pending batch on screen.</summary>
    /// <returns>The batch, or <see langword="null"/> when none was waiting.</returns>
    public UnhandledErrorBatch? BeginShow()
    {
        if (Pending is not { } batch)
        {
            return null;
        }

        Pending = null;
        Showing = batch;

        return batch;
    }

    /// <summary>
    /// The batch on screen was closed: none of its errors is shown again for <see cref="RepeatWindow"/>.
    /// </summary>
    public void EndShow()
    {
        if (Showing is not { } batch)
        {
            return;
        }

        Showing = null;

        long now = _time.GetTimestamp();

        foreach (Exception error in batch.Errors)
        {
            _closedAt[SignatureOf(error)] = now;
        }
    }

    private bool AllIgnored(AggregateException aggregate)
    {
        foreach (Exception inner in aggregate.InnerExceptions)
        {
            if (!IsIgnoredCancellation(inner))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>What makes two errors "the same one again": type, message and where it was thrown.</summary>
    private static string SignatureOf(Exception exception) =>
        exception.GetType().FullName + "\n" + exception.Message + "\n" + exception.StackTrace;
}
