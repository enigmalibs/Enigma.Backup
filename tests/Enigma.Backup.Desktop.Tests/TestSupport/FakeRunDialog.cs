using System.Collections.Generic;
using System.Threading.Tasks;
using Enigma.Backup.Desktop.Services;
using Enigma.Backup.Desktop.ViewModels;

namespace Enigma.Backup.Desktop.Tests.TestSupport;

/// <summary>An <see cref="IRunDialog"/> that records every run it is shown instead of drawing it.</summary>
internal sealed class FakeRunDialog : IRunDialog
{
    /// <summary>Every run shown, in order.</summary>
    public List<RunViewModel> Runs { get; } = [];

    /// <summary>The run shown last, or <see langword="null"/> when none was.</summary>
    public RunViewModel? Last => Runs.Count > 0 ? Runs[^1] : null;

    /// <summary>What the last run left behind, or <see langword="null"/> when none ran or it has not finished.</summary>
    public RunSummary? LastSummary => Last?.Summary;

    public Task ShowAsync(RunViewModel run)
    {
        Runs.Add(run);

        return Task.CompletedTask;
    }
}
