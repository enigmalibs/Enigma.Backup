using System;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Desktop.Services;
using Enigma.Backup.Desktop.Tests.TestSupport;
using Xunit;

namespace Enigma.Backup.Desktop.Tests.Services;

/// <summary>
/// What the safety net decides to show: which exceptions are not errors, which join the dialog already
/// there, and which were seen too recently to show again.
/// </summary>
public sealed class UnhandledErrorPolicyTests
{
    private readonly ManualTimeProvider _time = new();

    [Fact]
    public void TheFirstErrorOpensANewDialog()
    {
        UnhandledErrorPolicy policy = new(_time);
        InvalidOperationException error = Thrown.Error();

        Assert.Equal(UnhandledErrorDecision.Queue, policy.Offer(error));
        Assert.Same(error, policy.Pending?.First);
        Assert.Null(policy.Showing);
    }

    [Fact]
    public void AnErrorThatComesWhileOneWaits_IsCountedIntoIt()
    {
        UnhandledErrorPolicy policy = new(_time);
        policy.Offer(Thrown.Error("first"));

        Assert.Equal(UnhandledErrorDecision.Merge, policy.Offer(Thrown.Error("second")));
        Assert.Equal(1, policy.Pending?.More);
    }

    [Fact]
    public void AnErrorThatComesWhileOneIsOnScreen_IsCountedIntoIt()
    {
        UnhandledErrorPolicy policy = new(_time);
        policy.Offer(Thrown.Error("first"));
        UnhandledErrorBatch? showing = policy.BeginShow();

        Assert.Equal(UnhandledErrorDecision.Merge, policy.Offer(Thrown.Error("second")));
        Assert.NotNull(showing);
        Assert.Equal(1, showing.More);
        Assert.Null(policy.Pending);
    }

    /// <remarks>
    /// One exception can reach two hooks — a fault rethrown on the dispatcher whose task the finaliser
    /// also finds unobserved — and must not count twice.
    /// </remarks>
    [Fact]
    public void TheSameInstanceTwiceIsIgnoredTheSecondTime()
    {
        UnhandledErrorPolicy policy = new(_time);
        InvalidOperationException error = Thrown.Error();

        policy.Offer(error);

        Assert.Equal(UnhandledErrorDecision.Ignore, policy.Offer(error));
        Assert.Equal(0, policy.Pending?.More);
    }

    [Fact]
    public void TheSameErrorAgainWithinAMinuteOfClosing_IsNotShownAgain()
    {
        UnhandledErrorPolicy policy = new(_time);
        policy.Offer(Thrown.Error());
        policy.BeginShow();
        policy.EndShow();

        _time.Advance(UnhandledErrorPolicy.RepeatWindow - TimeSpan.FromSeconds(1));

        Assert.Equal(UnhandledErrorDecision.Suppress, policy.Offer(Thrown.Error()));
        Assert.Null(policy.Pending);
    }

    [Fact]
    public void TheSameErrorAgainAfterTheWindow_IsShownAgain()
    {
        UnhandledErrorPolicy policy = new(_time);
        policy.Offer(Thrown.Error());
        policy.BeginShow();
        policy.EndShow();

        _time.Advance(UnhandledErrorPolicy.RepeatWindow);

        Assert.Equal(UnhandledErrorDecision.Queue, policy.Offer(Thrown.Error()));
    }

    /// <remarks>
    /// "The same error" is the same type, message <em>and</em> place: the same message thrown from
    /// somewhere else is a different failure, and is shown.
    /// </remarks>
    [Fact]
    public void ADifferentErrorRightAfterClosing_IsShown()
    {
        UnhandledErrorPolicy policy = new(_time);
        policy.Offer(Thrown.Error());
        policy.BeginShow();
        policy.EndShow();

        Assert.Equal(UnhandledErrorDecision.Queue, policy.Offer(Thrown.ErrorElsewhere()));
    }

    [Fact]
    public void EveryErrorOfAClosedBatchIsHeldBack_NotOnlyTheFirst()
    {
        UnhandledErrorPolicy policy = new(_time);
        policy.Offer(Thrown.Error("first"));
        policy.Offer(Thrown.Error("second"));
        policy.BeginShow();
        policy.EndShow();

        Assert.Equal(UnhandledErrorDecision.Suppress, policy.Offer(Thrown.Error("second")));
    }

    [Fact]
    public void ACancellationSomeoneAskedFor_IsNotAnError()
    {
        UnhandledErrorPolicy policy = new(_time);
        using CancellationTokenSource source = new();
        source.Cancel();

        Assert.Equal(UnhandledErrorDecision.Ignore, policy.Offer(new OperationCanceledException(source.Token)));
        Assert.Null(policy.Pending);
    }

    /// <remarks>A timeout is a cancellation too, but nobody asked for it — that is a failure worth seeing.</remarks>
    [Fact]
    public void ACancellationNobodyAskedFor_IsAnError()
    {
        UnhandledErrorPolicy policy = new(_time);

        Assert.Equal(UnhandledErrorDecision.Queue, policy.Offer(new OperationCanceledException()));
    }

    [Fact]
    public void WhileTheAppCloses_NoCancellationIsAnError()
    {
        UnhandledErrorPolicy policy = new(_time) { IsShuttingDown = true };

        Assert.True(policy.IsIgnoredCancellation(new TaskCanceledException()));
        Assert.False(policy.IsIgnoredCancellation(Thrown.Error()));
    }

    [Fact]
    public void AnAggregateOfRequestedCancellationsIsIgnored_ButNotOneWithARealErrorInIt()
    {
        UnhandledErrorPolicy policy = new(_time);
        using CancellationTokenSource source = new();
        source.Cancel();
        OperationCanceledException requested = new(source.Token);

        Assert.True(policy.IsIgnoredCancellation(new AggregateException(requested, new OperationCanceledException(source.Token))));
        Assert.False(policy.IsIgnoredCancellation(new AggregateException(requested, Thrown.Error())));
    }

    [Fact]
    public void BeginShowWithNothingPending_ShowsNothing()
    {
        UnhandledErrorPolicy policy = new(_time);

        Assert.Null(policy.BeginShow());

        // And closing what was never shown changes nothing either.
        policy.EndShow();
        Assert.Null(policy.Showing);
    }
}
