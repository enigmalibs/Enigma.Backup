using System;
using System.Threading.Tasks;
using Enigma.Avalonia.Desktop.Controls.InfoBar;
using Enigma.Avalonia.Desktop.Services;

namespace Enigma.Backup.Desktop.Services;

/// <summary>The <see cref="INotificationService"/> that shows the outcome on the library's info bar.</summary>
/// <remarks>
/// One of the few places allowed to name the control library's services; the view models see only
/// <see cref="INotificationService"/> and its own <see cref="NotificationSeverity"/>, which is what
/// keeps <c>InfoBarSeverity</c> — an Avalonia type — out of them.
/// </remarks>
public sealed class NotificationService : INotificationService
{
    private readonly IInfoBarService _infoBars;

    /// <summary>Creates the notification service.</summary>
    /// <param name="infoBars">The library's info-bar host, registered by <see cref="App"/> at startup.</param>
    public NotificationService(IInfoBarService infoBars)
    {
        ArgumentNullException.ThrowIfNull(infoBars);

        _infoBars = infoBars;
    }

    /// <inheritdoc />
    public Task ShowAsync(string title, string message, NotificationSeverity severity)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);

        Task dismissed = _infoBars.ShowAsync(bar =>
        {
            bar.Title = title;
            bar.Message = message;
            bar.Severity = severity switch
            {
                NotificationSeverity.Success => InfoBarSeverity.Success,
                NotificationSeverity.Warning => InfoBarSeverity.Warning,

                // Anything that is not one of the three reads as the loudest of them, never as chatter.
                _ => InfoBarSeverity.Error,
            };
        });

        // The library's task completes when the banner is *dismissed*, not when it appears, so awaiting
        // it would hold the run open until the user clicked the banner away. It is handed back only
        // when it has already finished — which, since a host that was never registered throws before
        // the first await inside it, is how that wiring mistake still surfaces here.
        return dismissed.IsCompleted ? dismissed : Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task HideAsync() => _infoBars.HideAsync();
}
