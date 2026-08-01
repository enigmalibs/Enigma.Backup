using System;
using System.Threading.Tasks;

namespace Enigma.Backup.Desktop.Services;

/// <summary>
/// The UI thread, as something a view model can ask for without naming Avalonia.
/// </summary>
/// <remarks>
/// The core reports progress and asks about errors from whatever thread its pipeline happens to be
/// on, and both of those end up touching UI-bound state. This is the one seam that marshals them
/// back — the app implements it over <c>Dispatcher.UIThread</c>, the tests implement it by running
/// the work immediately, which is what makes the view models testable without a UI at all.
/// </remarks>
public interface IUiDispatcher
{
    /// <summary>Runs <paramref name="action"/> on the UI thread without waiting for it.</summary>
    /// <param name="action">The work to run.</param>
    void Post(Action action);

    /// <summary>Runs <paramref name="operation"/> on the UI thread and waits for its result.</summary>
    /// <typeparam name="T">What the operation produces.</typeparam>
    /// <param name="operation">The work to run.</param>
    /// <returns>What the operation produced.</returns>
    Task<T> InvokeAsync<T>(Func<Task<T>> operation);
}
