using System;
using System.Collections.Generic;
using System.ComponentModel;
using Enigma.Backup.Desktop.ViewModels;
using Xunit;

namespace Enigma.Backup.Desktop.Tests.ViewModels;

/// <summary>One run as its dialog shows it: running, stopping, and over.</summary>
public sealed class RunViewModelTests
{
    [Fact]
    public void ANewRun_IsRunningIndeterminateAndCanOnlyBeCancelled()
    {
        RunViewModel run = new("Backing up", static () => { });

        Assert.True(run.IsRunning);
        Assert.False(run.IsFinished);
        Assert.True(run.IsIndeterminate);
        Assert.False(run.HasSummary);
        Assert.Equal("Backing up", run.DialogTitle);
        Assert.True(run.CancelCommand.CanExecute(null));
        Assert.False(run.CloseCommand.CanExecute(null));
    }

    [Fact]
    public void Cancel_StopsTheRunOnceAndSaysSo()
    {
        int cancels = 0;
        RunViewModel run = new("Backing up", () => cancels++);
        run.Report("Archiving", "/docs/a.txt", 40, isIndeterminate: false);

        run.CancelCommand.Execute(null);
        run.CancelCommand.Execute(null);

        Assert.Equal(1, cancels);
        Assert.True(run.IsCancelling);
        Assert.Equal("Cancelling…", run.Message);
        Assert.False(run.CancelCommand.CanExecute(null));

        // The bar and the file are still what the run was doing when it was asked to stop.
        Assert.Equal(40, run.Percent);
        Assert.Equal("/docs/a.txt", run.CurrentItem);
    }

    [Fact]
    public void Finish_TurnsTheDialogToItsOutcome()
    {
        RunViewModel run = new("Backing up", static () => { });
        run.Report("Archiving", "/docs/a.txt", 40, isIndeterminate: false);

        run.Finish(RunSummary.Failed("backup", "the disk is full."));

        Assert.False(run.IsRunning);
        Assert.True(run.IsFinished);
        Assert.True(run.HasSummary);
        Assert.Null(run.CurrentItem);
        Assert.Equal("Something went wrong", run.DialogTitle);
        Assert.False(run.CancelCommand.CanExecute(null));
        Assert.True(run.CloseCommand.CanExecute(null));
    }

    [Fact]
    public void AFinishedRun_IgnoresLateReportsCancelsAndASecondFinish()
    {
        int cancels = 0;
        RunViewModel run = new("Restoring", () => cancels++);
        RunSummary first = RunSummary.Cancelled("restore", discarded: false);

        run.Finish(first);
        run.Report("Restoring — 3 files", "/late.txt", 90, isIndeterminate: false);
        run.CancelCommand.Execute(null);
        run.Finish(RunSummary.Failed("restore", "too late."));

        Assert.Same(first, run.Summary);
        Assert.Null(run.CurrentItem);
        Assert.NotEqual(90, run.Percent);
        Assert.Equal(0, cancels);
    }

    [Fact]
    public void Close_AsksTheDialogToClose()
    {
        RunViewModel run = new("Checking the archive", static () => { });
        int requests = 0;
        run.CloseRequested += (_, _) => requests++;

        run.Finish(RunSummary.Failed("verification", "it does not match."));
        run.CloseCommand.Execute(null);

        Assert.Equal(1, requests);
    }

    [Fact]
    public void Finish_RaisesWhatTheDialogFollows()
    {
        RunViewModel run = new("Backing up", static () => { });
        List<string?> raised = [];
        ((INotifyPropertyChanged)run).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        run.Finish(RunSummary.Cancelled("backup", discarded: true));

        Assert.Contains(nameof(RunViewModel.IsRunning), raised);
        Assert.Contains(nameof(RunViewModel.DialogTitle), raised);
        Assert.Contains(nameof(RunViewModel.HasSummary), raised);
    }

    [Fact]
    public void TheConstructor_RefusesNulls()
    {
        Assert.Throws<ArgumentNullException>(static () => new RunViewModel(null!, static () => { }));
        Assert.Throws<ArgumentNullException>(static () => new RunViewModel("Backing up", null!));
    }
}
