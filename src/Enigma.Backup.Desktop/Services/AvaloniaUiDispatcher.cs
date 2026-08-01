using System;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Enigma.Backup.Desktop.Services;

/// <summary>The <see cref="IUiDispatcher"/> over Avalonia's UI thread.</summary>
/// <remarks>
/// Work already on the UI thread runs inline rather than being queued behind whatever else is
/// pending: a progress report that arrives on the UI thread should be applied now, not one frame
/// later, and a dialog opened from a command handler should not wait its turn.
/// </remarks>
public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    /// <inheritdoc />
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (Dispatcher.UIThread.CheckAccess())
        {
            action();

            return;
        }

        Dispatcher.UIThread.Post(action);
    }

    /// <inheritdoc />
    public Task<T> InvokeAsync<T>(Func<Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return Dispatcher.UIThread.CheckAccess() ? operation() : Dispatcher.UIThread.InvokeAsync(operation);
    }
}
