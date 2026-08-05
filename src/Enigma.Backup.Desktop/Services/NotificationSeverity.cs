namespace Enigma.Backup.Desktop.Services;

/// <summary>
/// What a notification is reporting, which is also how it is coloured. One value per way a run can
/// end, so mapping a <see cref="ViewModels.RunSummary"/> onto it is total.
/// </summary>
public enum NotificationSeverity
{
    /// <summary>The run did exactly what was asked.</summary>
    Success,

    /// <summary>The run finished, with something the user should read.</summary>
    Warning,

    /// <summary>The run did not do what was asked.</summary>
    Error,
}
