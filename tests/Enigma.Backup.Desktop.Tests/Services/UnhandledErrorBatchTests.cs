using System;
using System.Reflection;
using Enigma.Backup.Desktop.Services;
using Enigma.Backup.Desktop.Tests.TestSupport;
using Xunit;

namespace Enigma.Backup.Desktop.Tests.Services;

/// <summary>What one error dialog says: the sentence at the top, and the details for a bug report.</summary>
public sealed class UnhandledErrorBatchTests
{
    [Fact]
    public void TheMessageIsTheFirstErrorsOwn()
    {
        UnhandledErrorBatch batch = new(Thrown.Error("The archive could not be opened."));

        Assert.Equal("The archive could not be opened.", batch.Message);
        Assert.Equal(0, batch.More);
    }

    /// <remarks>
    /// "One or more errors occurred" and "Exception has been thrown by the target of an invocation" say
    /// nothing; the exception inside them says what happened.
    /// </remarks>
    [Fact]
    public void WrappersAreSeenThrough()
    {
        InvalidOperationException inner = Thrown.Error("The real reason.");

        Assert.Equal("The real reason.", new UnhandledErrorBatch(new AggregateException(inner)).Message);
        Assert.Equal("The real reason.", new UnhandledErrorBatch(new TargetInvocationException(inner)).Message);
        Assert.Equal(
            "The real reason.",
            new UnhandledErrorBatch(new AggregateException(new TargetInvocationException(inner))).Message);
    }

    [Fact]
    public void AnAggregateOfSeveralErrorsSpeaksForItself()
    {
        AggregateException aggregate = new("Two things failed.", Thrown.Error("one"), Thrown.Error("two"));

        Assert.StartsWith("Two things failed.", new UnhandledErrorBatch(aggregate).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnErrorWithNoMessageIsNamedByItsType()
    {
        Assert.Equal(nameof(EmptyMessageException), UnhandledErrorBatch.Summarise(new EmptyMessageException()));
    }

    [Fact]
    public void TheDetailsCarryTypeMessageStackAndInnerExceptions()
    {
        InvalidOperationException inner = Thrown.Error("inner reason");
        Exception outer = new ApplicationException("outer reason", inner);

        string details = new UnhandledErrorBatch(outer).Details;

        Assert.Contains("System.ApplicationException: outer reason", details, StringComparison.Ordinal);
        Assert.Contains("--- Inner exception ---", details, StringComparison.Ordinal);
        Assert.Contains("System.InvalidOperationException: inner reason", details, StringComparison.Ordinal);
        Assert.Contains(nameof(Thrown.Error), details, StringComparison.Ordinal);
    }

    /// <remarks>
    /// <see cref="Exception.Data"/> is where an exception carries whatever its thrower attached, and a bug
    /// report is pasted somewhere public — so the details never include it.
    /// </remarks>
    [Fact]
    public void TheDetailsNeverIncludeTheExceptionsData()
    {
        InvalidOperationException error = Thrown.Error();
        error.Data["secret"] = "do-not-print-me";

        Assert.DoesNotContain("do-not-print-me", new UnhandledErrorBatch(error).Details, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDetailsStopFollowingInnerExceptionsAtEightLevels()
    {
        Exception error = new InvalidOperationException("level 0");
        for (int level = 1; level <= 12; level++)
        {
            error = new InvalidOperationException($"level {level}", error);
        }

        string details = UnhandledErrorBatch.BuildDetails(error);

        Assert.Contains("level 12", details, StringComparison.Ordinal);
        Assert.Contains("level 4", details, StringComparison.Ordinal);
        Assert.DoesNotContain("level 3", details, StringComparison.Ordinal);
    }

    [Fact]
    public void SeveralErrorsAreNumberedInTheDetails_AndCountedAsMore()
    {
        UnhandledErrorPolicy policy = new(new ManualTimeProvider());
        policy.Offer(Thrown.Error("first"));
        policy.Offer(Thrown.Error("second"));
        UnhandledErrorBatch batch = policy.Pending!;

        Assert.Equal(1, batch.More);
        Assert.Equal("first", batch.Message);
        Assert.Contains("Error 1 of 2", batch.Details, StringComparison.Ordinal);
        Assert.Contains("Error 2 of 2", batch.Details, StringComparison.Ordinal);
        Assert.Contains("second", batch.Details, StringComparison.Ordinal);
    }

    [Fact]
    public void ADialogOnScreenHearsOfEveryErrorCountedIntoIt()
    {
        UnhandledErrorPolicy policy = new(new ManualTimeProvider());
        policy.Offer(Thrown.Error("first"));
        UnhandledErrorBatch batch = policy.BeginShow()!;
        int changes = 0;
        batch.Changed += (_, _) => changes++;

        policy.Offer(Thrown.Error("second"));
        policy.Offer(Thrown.Error("third"));

        Assert.Equal(2, changes);
        Assert.Equal(2, batch.More);
    }

    private sealed class EmptyMessageException : Exception
    {
        public override string Message => string.Empty;
    }
}
