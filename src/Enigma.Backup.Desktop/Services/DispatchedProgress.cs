using System;

namespace Enigma.Backup.Desktop.Services;

/// <summary>
/// An <see cref="IProgress{T}"/> that applies every report on the UI thread.
/// </summary>
/// <remarks>
/// <see cref="Progress{T}"/> would do this too, but only if it happened to be constructed on the UI
/// thread — a dependency on where the object was created that is invisible at the point of use and
/// wrong in a test. Going through <see cref="IUiDispatcher"/> makes the marshaling explicit and lets
/// a test apply reports immediately, so what the view model shows after a run is deterministic.
/// </remarks>
/// <typeparam name="T">What the core reports.</typeparam>
internal sealed class DispatchedProgress<T> : IProgress<T>
{
    private readonly IUiDispatcher _dispatcher;
    private readonly Action<T> _apply;

    /// <summary>Creates a reporter.</summary>
    /// <param name="dispatcher">The UI thread to apply reports on.</param>
    /// <param name="apply">What to do with each report.</param>
    public DispatchedProgress(IUiDispatcher dispatcher, Action<T> apply)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(apply);

        _dispatcher = dispatcher;
        _apply = apply;
    }

    /// <inheritdoc />
    public void Report(T value) => _dispatcher.Post(() => _apply(value));
}
