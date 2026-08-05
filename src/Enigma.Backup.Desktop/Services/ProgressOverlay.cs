using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Backup.Desktop.Views;

namespace Enigma.Backup.Desktop.Services;

/// <summary>The <see cref="IProgressOverlay"/> that draws the run card on the library's overlay host.</summary>
/// <remarks>
/// <para>
/// One of the few places allowed to name the control library's services. The card is ours —
/// <c>IOverlayService</c> takes any control and then holds it live, so progress is reported by
/// mutating the card that is already on screen, never by showing it a second time.
/// </para>
/// <para>
/// Every call arrives on the UI thread already: a run is started by a command, and the core's progress
/// reaches the view model through <see cref="DispatchedProgress{T}"/>.
/// </para>
/// </remarks>
public sealed class ProgressOverlay : IProgressOverlay
{
    private readonly IOverlayService _overlays;

    private ProgressOverlayCard? _card;

    /// <summary>Creates the run overlay.</summary>
    /// <param name="overlays">The library's overlay host, registered by <see cref="App"/> at startup.</param>
    public ProgressOverlay(IOverlayService overlays)
    {
        ArgumentNullException.ThrowIfNull(overlays);

        _overlays = overlays;
    }

    /// <inheritdoc />
    public Task ShowAsync(string title, ICommand cancelCommand)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(cancelCommand);

        // Indeterminate to start with: a run knows how much there is to do only once it has scanned.
        _card = new ProgressOverlayCard
        {
            Title = title,
            CancelCommand = cancelCommand,
            IsIndeterminate = true,
        };

        return _overlays.ShowAsync(_card);
    }

    /// <inheritdoc />
    public void Update(string message, string? currentItem, double percent, bool isIndeterminate)
    {
        // A report can still arrive after the run ended and the card was taken away. There is then
        // nothing to draw on, and that is not a failure.
        if (_card is not ProgressOverlayCard card)
        {
            return;
        }

        card.Message = message;
        card.CurrentItem = currentItem;
        card.Progress = percent;
        card.IsIndeterminate = isIndeterminate;
    }

    /// <inheritdoc />
    public async Task HideAsync()
    {
        try
        {
            await _overlays.HideAsync();
        }
        finally
        {
            // Dropped whatever happened above: a card that could not be taken off screen must at
            // least not still be collecting the next run's progress reports.
            _card = null;
        }
    }
}
