using System.Threading.Tasks;

namespace Enigma.Backup.Desktop.Services;

/// <summary>
/// The banner that says how a run ended, in the window rather than in a dialog: nothing has to be
/// clicked away before the next thing can be done.
/// </summary>
/// <remarks>
/// It carries the outcome, not the detail — the "Last run" panel keeps the archive path, the sizes, the
/// durations and the per-file lists, because after a run that half-worked those lists are exactly what
/// the user needs.
/// </remarks>
public interface INotificationService
{
    /// <summary>Shows a notification, replacing whichever one is on screen.</summary>
    /// <param name="title">The outcome in a couple of words.</param>
    /// <param name="message">The sentence that says what happened.</param>
    /// <param name="severity">How the notification is coloured.</param>
    Task ShowAsync(string title, string message, NotificationSeverity severity);

    /// <summary>Takes the notification away, whether or not one is showing.</summary>
    Task HideAsync();
}
