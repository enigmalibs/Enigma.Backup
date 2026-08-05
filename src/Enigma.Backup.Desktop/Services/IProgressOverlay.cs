using System.Threading.Tasks;
using System.Windows.Input;

namespace Enigma.Backup.Desktop.Services;

/// <summary>
/// The run card: what covers the window while a backup, restore or verification is running, and the
/// only place that run can be stopped from.
/// </summary>
/// <remarks>
/// <para>
/// A seam, not a control. The view model says "a run started", "this is how far it has got" and "it is
/// over", and never learns that the other side of this is an Avalonia overlay — which is what lets a
/// test drive a whole run and count what the card was told.
/// </para>
/// <para>
/// <see cref="HideAsync"/> is always safe, including when nothing is showing: the caller's job is to
/// put it in a <c>finally</c>, and a scrim left up over an app nobody can click is the one failure
/// this seam must not have.
/// </para>
/// </remarks>
public interface IProgressOverlay
{
    /// <summary>Raises the card over the window.</summary>
    /// <param name="title">What is running, as the card's heading — "Backing up".</param>
    /// <param name="cancelCommand">
    /// What the card's Cancel button runs. Cancel has to be *on* the card: behind the scrim, a button
    /// in the window itself cannot be clicked.
    /// </param>
    Task ShowAsync(string title, ICommand cancelCommand);

    /// <summary>Moves the card on. Does nothing when there is no card showing.</summary>
    /// <param name="message">What is happening, in one line.</param>
    /// <param name="currentItem">The file being processed, or <see langword="null"/> when there is none.</param>
    /// <param name="percent">How far the run has got, from 0 to 100.</param>
    /// <param name="isIndeterminate">Whether how much there is to do is still unknown.</param>
    void Update(string message, string? currentItem, double percent, bool isIndeterminate);

    /// <summary>Takes the card away, whether or not one is showing.</summary>
    Task HideAsync();
}
