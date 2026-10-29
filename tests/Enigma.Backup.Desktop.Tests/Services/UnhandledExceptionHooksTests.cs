using System;
using System.Linq;
using Enigma.Backup.Desktop.Services;
using Enigma.Backup.Desktop.Tests.TestSupport;
using Xunit;

namespace Enigma.Backup.Desktop.Tests.Services;

/// <summary>
/// The process-wide hooks: what they keep before the safety net exists, and what they hand it once it does.
/// </summary>
/// <remarks>
/// The hooks are static, so these tests share their state and run alone in their own collection; each
/// one starts by draining whatever an earlier one left behind. <c>Install</c> itself is not called here:
/// it would subscribe this whole test process to the real unhandled-exception events.
/// </remarks>
[Collection(nameof(UnhandledExceptionHooksCollection))]
public sealed class UnhandledExceptionHooksTests
{
    public UnhandledExceptionHooksTests() => Drain();

    [Fact]
    public void WhatIsReportedBeforeTheReporterExists_IsHandedToItOnAttach()
    {
        InvalidOperationException early = Thrown.Error("before the window");

        UnhandledExceptionHooks.Report(early, UnhandledErrorSource.Startup);

        RecordingReporter reporter = new();
        UnhandledExceptionHooks.Attach(reporter);

        (Exception exception, UnhandledErrorSource source) = Assert.Single(reporter.Reports);
        Assert.Same(early, exception);
        Assert.Equal(UnhandledErrorSource.Startup, source);

        UnhandledExceptionHooks.Detach();
    }

    [Fact]
    public void OnceAttached_ReportsGoStraightThrough()
    {
        RecordingReporter reporter = new();
        UnhandledExceptionHooks.Attach(reporter);

        UnhandledExceptionHooks.Report(Thrown.Error(), UnhandledErrorSource.Dispatcher);

        Assert.Equal(UnhandledErrorSource.Dispatcher, Assert.Single(reporter.Reports).Source);

        UnhandledExceptionHooks.Detach();
    }

    /// <remarks>A failure in a loop before the window opens must not grow the buffer without bound.</remarks>
    [Fact]
    public void AtMostThirtyTwoEarlyReportsAreKept()
    {
        for (int index = 0; index < UnhandledExceptionHooks.EarlyLimit + 10; index++)
        {
            UnhandledExceptionHooks.Report(Thrown.Error($"early {index}"), UnhandledErrorSource.UnobservedTask);
        }

        RecordingReporter reporter = new();
        UnhandledExceptionHooks.Attach(reporter);

        Assert.Equal(UnhandledExceptionHooks.EarlyLimit, reporter.Reports.Count);
        Assert.Equal("early 0", reporter.Reports.First().Exception.Message);

        UnhandledExceptionHooks.Detach();
    }

    [Fact]
    public void AfterDetach_ReportsAreKeptForTheNextReporter()
    {
        RecordingReporter first = new();
        UnhandledExceptionHooks.Attach(first);
        UnhandledExceptionHooks.Detach();

        UnhandledExceptionHooks.Report(Thrown.Error("late"), UnhandledErrorSource.Dispatcher);

        Assert.Empty(first.Reports);

        RecordingReporter second = new();
        UnhandledExceptionHooks.Attach(second);

        Assert.Equal("late", Assert.Single(second.Reports).Exception.Message);

        UnhandledExceptionHooks.Detach();
    }

    [Fact]
    public void ANullReportIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => UnhandledExceptionHooks.Report(null!, UnhandledErrorSource.Dispatcher));
        Assert.Throws<ArgumentNullException>(() => UnhandledExceptionHooks.Attach(null!));
    }

    /// <summary>Empties what earlier tests left in the static buffer, and leaves no reporter attached.</summary>
    private static void Drain()
    {
        UnhandledExceptionHooks.Attach(new RecordingReporter());
        UnhandledExceptionHooks.Detach();
    }
}

/// <summary>The hooks' tests share static state, so they never run beside one another.</summary>
[CollectionDefinition(nameof(UnhandledExceptionHooksCollection), DisableParallelization = true)]
public sealed class UnhandledExceptionHooksCollection;
