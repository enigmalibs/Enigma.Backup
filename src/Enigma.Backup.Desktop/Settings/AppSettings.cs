namespace Enigma.Backup.Desktop.Settings;

/// <summary>
/// The desktop app's own preferences: the things that mean nothing to a command line and therefore
/// have no business in <c>Enigma.Backup.Core</c>. Persisted as JSON by
/// <see cref="IAppSettingsStore"/>, beside the folder that holds the profiles.
/// </summary>
/// <remarks>
/// Nothing here is required for the app to work: the file is absent until a preference is changed,
/// and every value has a default that stands on its own.
/// </remarks>
public sealed class AppSettings
{
    /// <summary>The settings format this build reads and writes.</summary>
    public const int CurrentFormatVersion = 1;

    /// <summary>
    /// Version of the JSON layout. A file declaring anything other than
    /// <see cref="CurrentFormatVersion"/> is ignored rather than guessed at — unlike a profile, a
    /// preference is not worth refusing to start over, so the defaults simply stand in.
    /// </summary>
    public int FormatVersion { get; set; } = CurrentFormatVersion;

    /// <summary>Which variant the app is painted in.</summary>
    public AppTheme Theme { get; set; } = AppTheme.System;
}
