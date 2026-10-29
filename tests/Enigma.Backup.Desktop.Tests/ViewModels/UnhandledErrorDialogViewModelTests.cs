using System;
using System.Collections.Generic;
using Enigma.Backup.Desktop.Services;
using Enigma.Backup.Desktop.Tests.TestSupport;
using Enigma.Backup.Desktop.ViewModels;
using Xunit;

namespace Enigma.Backup.Desktop.Tests.ViewModels;

/// <summary>What the safety net's dialog says, and how it follows errors that arrive while it is open.</summary>
public sealed class UnhandledErrorDialogViewModelTests
{
    [Fact]
    public void ItSaysTheAppIsStillRunning_AndWhatWentWrong()
    {
        UnhandledErrorDialogViewModel viewModel = new(new UnhandledErrorBatch(Thrown.Error("The thing broke.")));

        Assert.Contains("still running", viewModel.Lead, StringComparison.Ordinal);
        Assert.Equal("The thing broke.", viewModel.Message);
        Assert.False(viewModel.HasMore);
        Assert.Contains("System.InvalidOperationException: The thing broke.", viewModel.Details, StringComparison.Ordinal);
    }

    [Fact]
    public void OneMoreErrorIsSaidInTheSingular_SeveralInThePlural()
    {
        UnhandledErrorPolicy policy = new(new ManualTimeProvider());
        policy.Offer(Thrown.Error("first"));
        UnhandledErrorDialogViewModel viewModel = new(policy.BeginShow()!);

        policy.Offer(Thrown.Error("second"));
        Assert.True(viewModel.HasMore);
        Assert.Equal("1 more error occurred.", viewModel.MoreText);

        policy.Offer(Thrown.Error("third"));
        Assert.Equal("2 more errors occurred.", viewModel.MoreText);
    }

    [Fact]
    public void AnErrorCountedInWhileOpen_UpdatesTheCountAndTheDetails()
    {
        UnhandledErrorPolicy policy = new(new ManualTimeProvider());
        policy.Offer(Thrown.Error("first"));
        UnhandledErrorDialogViewModel viewModel = new(policy.BeginShow()!);
        List<string?> changed = [];
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        policy.Offer(Thrown.Error("second"));

        Assert.Contains(nameof(UnhandledErrorDialogViewModel.HasMore), changed);
        Assert.Contains(nameof(UnhandledErrorDialogViewModel.MoreText), changed);
        Assert.Contains(nameof(UnhandledErrorDialogViewModel.Details), changed);
        Assert.Contains("second", viewModel.Details, StringComparison.Ordinal);
    }

    /// <remarks>
    /// A start that failed has no app left running, so the dialog must not say there is: closing it ends
    /// the process.
    /// </remarks>
    [Fact]
    public void AFailedStartIsNotCalledARunningApp()
    {
        UnhandledErrorDialogViewModel viewModel =
            UnhandledErrorDialogViewModel.ForStartupFailure(Thrown.Error("The container could not be built."));

        Assert.DoesNotContain("still running", viewModel.Lead, StringComparison.Ordinal);
        Assert.Contains("starting", viewModel.Lead, StringComparison.Ordinal);
        Assert.Equal("The container could not be built.", viewModel.Message);
        Assert.False(viewModel.HasMore);
        Assert.Contains("The container could not be built.", viewModel.Details, StringComparison.Ordinal);
    }

    [Fact]
    public void CloseAsksTheDialogToClose()
    {
        UnhandledErrorDialogViewModel viewModel = new(new UnhandledErrorBatch(Thrown.Error()));
        int closes = 0;

        viewModel.CloseRequested += (_, _) => closes++;
        viewModel.CloseCommand.Execute(null);

        Assert.Equal(1, closes);
    }
}
