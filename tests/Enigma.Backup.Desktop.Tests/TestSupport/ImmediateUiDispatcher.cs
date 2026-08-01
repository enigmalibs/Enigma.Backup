using System;
using System.Threading.Tasks;
using Enigma.Backup.Desktop.Services;

namespace Enigma.Backup.Desktop.Tests.TestSupport;

/// <summary>
/// The <see cref="IUiDispatcher"/> for a test: everything runs right where it was asked for.
/// </summary>
/// <remarks>
/// This is what makes a run's progress deterministic in a test. In the app a report is posted to the
/// UI thread and applied a moment later; here it is applied before <c>Report</c> returns, so what a
/// view model shows after an awaited run is exactly what the last report said.
/// </remarks>
internal sealed class ImmediateUiDispatcher : IUiDispatcher
{
    /// <summary>How many times something was marshaled — the proof that the seam is being used.</summary>
    public int PostCount { get; private set; }

    /// <inheritdoc />
    public void Post(Action action)
    {
        PostCount++;
        action();
    }

    /// <inheritdoc />
    public Task<T> InvokeAsync<T>(Func<Task<T>> operation) => operation();
}
