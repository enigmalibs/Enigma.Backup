using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Desktop.Services;
using Enigma.Backup.Desktop.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Enigma.Backup.Desktop.Tests.Services;

/// <summary>
/// The safety net end to end, minus the window: what it logs, what it shows, and what it holds back.
/// </summary>
/// <remarks>
/// The dispatcher runs everything inline and the dialog is a fake that closes at once unless told
/// otherwise, so "shown" means "handed to <see cref="IDialogService"/>".
/// </remarks>
public sealed class UnhandledErrorReporterTests
{
    private readonly RecordingLogger<UnhandledErrorReporter> _logger = new();
    private readonly FakeDialogService _dialogs = new();
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
    public void AnErrorIsShownInTheDialog()
    {
        UnhandledErrorReporter reporter = Build();
        InvalidOperationException error = Thrown.Error("The thing broke.");

        reporter.Report(error, UnhandledErrorSource.Dispatcher);

        UnhandledErrorBatch shown = Assert.Single(_dialogs.UnhandledErrors);
        Assert.Same(error, shown.First);
        Assert.Equal("The thing broke.", shown.Message);
    }

    [Fact]
    public void ACancellationSomeoneAskedFor_IsOnlyADebugLine_AndIsNotShown()
    {
        UnhandledErrorReporter reporter = Build();
        using CancellationTokenSource source = new();
        source.Cancel();

        reporter.Report(new OperationCanceledException(source.Token), UnhandledErrorSource.Dispatcher);

        Assert.Equal(LogLevel.Debug, Assert.Single(_logger.Entries).Level);
        Assert.Empty(_dialogs.UnhandledErrors);
    }

    [Fact]
    public void OnceTheAppIsClosing_NoCancellationIsAnError()
    {
        UnhandledErrorReporter reporter = Build();

        reporter.ShutdownStarted();
        reporter.Report(new OperationCanceledException(), UnhandledErrorSource.Dispatcher);

        Assert.Equal(LogLevel.Debug, Assert.Single(_logger.Entries).Level);
        Assert.Empty(_dialogs.UnhandledErrors);
    }

    [Fact]
    public void ErrorsThatComeWhileTheDialogIsOpen_AreCountedIntoIt()
    {
        TaskCompletionSource open = new();
        _dialogs.UnhandledErrorBehaviour = _ => open.Task;
        UnhandledErrorReporter reporter = Build();

        reporter.Report(Thrown.Error("first"), UnhandledErrorSource.Dispatcher);
        reporter.Report(Thrown.Error("second"), UnhandledErrorSource.Dispatcher);
        reporter.Report(Thrown.Error("third"), UnhandledErrorSource.UnobservedTask);

        UnhandledErrorBatch shown = Assert.Single(_dialogs.UnhandledErrors);
        Assert.Equal(2, shown.More);

        open.SetResult();
    }

    [Fact]
    public void TheSameErrorRightAfterTheDialogClosed_IsLoggedButNotShownAgain()
    {
        UnhandledErrorReporter reporter = Build();

        reporter.Report(Thrown.Error(), UnhandledErrorSource.Dispatcher);
        reporter.Report(Thrown.Error(), UnhandledErrorSource.Dispatcher);

        Assert.Single(_dialogs.UnhandledErrors);
        Assert.Equal(2, _logger.Entries.Count(entry => entry.Level == LogLevel.Error));
    }

    [Fact]
    public void TheSameErrorAMinuteLater_IsShownAgain()
    {
        UnhandledErrorReporter reporter = Build();

        reporter.Report(Thrown.Error(), UnhandledErrorSource.Dispatcher);
        _time.Advance(UnhandledErrorPolicy.RepeatWindow);
        reporter.Report(Thrown.Error(), UnhandledErrorSource.Dispatcher);

        Assert.Equal(2, _dialogs.UnhandledErrors.Count);
    }

    [Fact]
    public void ADifferentErrorAfterTheDialogClosed_OpensANewOne()
    {
        UnhandledErrorReporter reporter = Build();

        reporter.Report(Thrown.Error("first"), UnhandledErrorSource.Dispatcher);
        reporter.Report(Thrown.Error("second"), UnhandledErrorSource.Dispatcher);

        Assert.Equal(["first", "second"], _dialogs.UnhandledErrors.Select(batch => batch.Message));
    }

    /// <remarks>
    /// A dialog that cannot be shown must not report itself — that would loop — so the net falls back to
    /// logging for the rest of the session.
    /// </remarks>
    [Fact]
    public void WhenTheDialogItselfFails_LaterErrorsAreOnlyLogged()
    {
        _dialogs.UnhandledErrorBehaviour = _ => throw new InvalidOperationException("No host.");
        UnhandledErrorReporter reporter = Build();

        reporter.Report(Thrown.Error("first"), UnhandledErrorSource.Dispatcher);
        reporter.Report(Thrown.Error("second"), UnhandledErrorSource.Dispatcher);

        Assert.Single(_dialogs.UnhandledErrors);
        Assert.Contains(_logger.Entries, entry => entry.Exception?.Message == "No host.");
        Assert.Contains(_logger.Entries, entry => entry.Exception?.Message == "second");
    }

    /// <remarks>
    /// A faulted command is rethrown on the dispatcher, and its task is later found unobserved too: one
    /// failure, reaching the net twice. It is one error in the log and one dialog.
    /// </remarks>
    [Fact]
    public void TheSameExceptionReachingTwoHooks_IsOneErrorAndOneDialog()
    {
        UnhandledErrorReporter reporter = Build();
        InvalidOperationException error = Thrown.Error();

        reporter.Report(error, UnhandledErrorSource.Dispatcher);
        reporter.Report(error, UnhandledErrorSource.UnobservedTask);

        Assert.Single(_logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Single(_logger.Entries, entry => entry.Level == LogLevel.Debug);
        Assert.Single(_dialogs.UnhandledErrors);
    }

    [Fact]
    public void EveryDecisionIsTakenOnTheUiThread()
    {
        ImmediateUiDispatcher dispatcher = new();
        UnhandledErrorReporter reporter = new(_dialogs, dispatcher, _time, _logger);

        reporter.Report(Thrown.Error(), UnhandledErrorSource.UnobservedTask);

        Assert.Equal(1, dispatcher.PostCount);
    }

    private UnhandledErrorReporter Build() => new(_dialogs, new ImmediateUiDispatcher(), _time, _logger);
}
