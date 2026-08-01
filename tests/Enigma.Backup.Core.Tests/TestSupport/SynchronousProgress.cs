using System;

namespace Enigma.Backup.Core.Tests.TestSupport;

/// <summary>
/// An <see cref="IProgress{T}"/> that runs its callback on the thread that reported, unlike
/// <see cref="Progress{T}"/>, which posts elsewhere.
/// </summary>
/// <remarks>
/// That makes the reports a test can collect deterministic, and it lets a test act <em>during</em> a
/// run — cancelling it, or changing a file while its entry is being written.
/// </remarks>
/// <typeparam name="T">The kind of progress report.</typeparam>
public sealed class SynchronousProgress<T> : IProgress<T>
{
    private readonly Action<T> _onReport;

    /// <summary>Creates a reporter.</summary>
    /// <param name="onReport">Called for every report, as it happens.</param>
    public SynchronousProgress(Action<T> onReport)
    {
        ArgumentNullException.ThrowIfNull(onReport);

        _onReport = onReport;
    }

    /// <inheritdoc />
    public void Report(T value) => _onReport(value);
}
