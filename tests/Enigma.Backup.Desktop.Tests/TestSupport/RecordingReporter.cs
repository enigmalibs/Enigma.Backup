using System;
using System.Collections.Generic;
using Enigma.Backup.Desktop.Services;

namespace Enigma.Backup.Desktop.Tests.TestSupport;

/// <summary>An <see cref="IUnhandledErrorReporter"/> that records what reached it.</summary>
internal sealed class RecordingReporter : IUnhandledErrorReporter
{
    /// <summary>Every report, in order.</summary>
    public List<(Exception Exception, UnhandledErrorSource Source)> Reports { get; } = [];

    /// <summary>Whether <see cref="ShutdownStarted"/> was called.</summary>
    public bool IsShuttingDown { get; private set; }

    /// <inheritdoc />
    public void Report(Exception exception, UnhandledErrorSource source) => Reports.Add((exception, source));

    /// <inheritdoc />
    public void ShutdownStarted() => IsShuttingDown = true;
}
