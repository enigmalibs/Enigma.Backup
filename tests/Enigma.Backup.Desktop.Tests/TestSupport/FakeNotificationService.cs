using System.Collections.Generic;
using System.Threading.Tasks;
using Enigma.Backup.Desktop.Services;

namespace Enigma.Backup.Desktop.Tests.TestSupport;

/// <summary>An <see cref="INotificationService"/> that records every banner it was asked to show.</summary>
internal sealed class FakeNotificationService : INotificationService
{
    /// <summary>Every notification, in order.</summary>
    public List<(string Title, string Message, NotificationSeverity Severity)> Shown { get; } = [];

    /// <summary>How many times a notification was taken away.</summary>
    public int Hides { get; private set; }

    /// <inheritdoc />
    public Task ShowAsync(string title, string message, NotificationSeverity severity)
    {
        Shown.Add((title, message, severity));

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task HideAsync()
    {
        Hides++;

        return Task.CompletedTask;
    }
}
