using Enigma.Backup.Desktop.Settings;

namespace Enigma.Backup.Desktop.Services;

/// <summary>Paints the whole app in a theme variant.</summary>
/// <remarks>
/// The seam exists for a single line of Avalonia — an assignment to the application's requested
/// variant — because that line would otherwise sit in a view model, and no view model in this app
/// names Avalonia. It is also what lets a test watch the theme being applied without an
/// <c>Application</c> existing at all.
/// </remarks>
public interface IThemeSwitcher
{
    /// <summary>Repaints the app, immediately.</summary>
    /// <param name="theme">
    /// Which variant, with <see cref="AppTheme.System"/> meaning "whatever the operating system asks
    /// for, now and after it changes".
    /// </param>
    void Apply(AppTheme theme);
}
