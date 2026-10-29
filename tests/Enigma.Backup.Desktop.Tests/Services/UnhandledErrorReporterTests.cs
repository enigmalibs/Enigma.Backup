using System;
using System.Linq;
using System.Threading;
using Enigma.Backup.Desktop.Services;
using Enigma.Backup.Desktop.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Enigma.Backup.Desktop.Tests.Services;

/// <summary>The safety net's log: what it writes, and at which level.</summary>
public sealed class UnhandledErrorReporterTests
{
    private readonly RecordingLogger<UnhandledErrorReporter> _logger = new();
    private readonly ManualTimeProvider _time = new();

    [Fact]
    public void AnErrorIsLoggedAsAnError_WithWhereItWasCaught()
    {
        UnhandledErrorReporter reporter = Build();
        InvalidOperationException error = Thrown.Error();

        reporter.Report(error, UnhandledErrorSource.UnobservedTask);

        (LogLevel level, Exception? exception, string message) = _logger.Entries.First();
        Assert.Equal(LogLevel.Error, level);
        Assert.Same(error, exception);
        Assert.Contains(nameof(UnhandledErrorSource.UnobservedTask), message, StringComparison.Ordinal);
    }

    [Fact]
    public void ACancellationSomeoneAskedFor_IsOnlyADebugLine()
    {
        UnhandledErrorReporter reporter = Build();
        using CancellationTokenSource source = new();
        source.Cancel();

        reporter.Report(new OperationCanceledException(source.Token), UnhandledErrorSource.Dispatcher);

        Assert.Equal(LogLevel.Debug, Assert.Single(_logger.Entries).Level);
    }

    [Fact]
    public void OnceTheAppIsClosing_NoCancellationIsAnError()
    {
        UnhandledErrorReporter reporter = Build();

        reporter.ShutdownStarted();
        reporter.Report(new OperationCanceledException(), UnhandledErrorSource.Dispatcher);

        Assert.Equal(LogLevel.Debug, Assert.Single(_logger.Entries).Level);
    }

    private UnhandledErrorReporter Build() => new(_time, _logger);
}
