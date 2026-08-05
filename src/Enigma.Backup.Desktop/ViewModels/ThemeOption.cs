using System.Collections.Generic;
using System.Linq;
using Enigma.Backup.Desktop.Settings;

namespace Enigma.Backup.Desktop.ViewModels;

/// <summary>One entry of the theme drop-down.</summary>
/// <param name="Value">The variant this entry selects.</param>
/// <param name="Label">Its name, as the drop-down shows it.</param>
/// <param name="Description">The one-line explanation, shown beside the drop-down.</param>
public sealed record ThemeOption(AppTheme Value, string Label, string Description)
{
    /// <summary>Every theme the app offers, in the order they are shown.</summary>
    public static IReadOnlyList<ThemeOption> All { get; } =
    [
        new ThemeOption(
            AppTheme.System,
            "System",
            "Follows the operating system, and keeps following it. The default."),
        new ThemeOption(
            AppTheme.Light,
            "Light",
            "Always light, whatever the system is set to."),
        new ThemeOption(
            AppTheme.Dark,
            "Dark",
            "Always dark, whatever the system is set to."),
    ];

    /// <summary>Finds the entry for a theme, falling back to the first when it is not one this build offers.</summary>
    /// <param name="value">The theme to look up.</param>
    public static ThemeOption For(AppTheme value) =>
        All.FirstOrDefault(option => option.Value == value) ?? All[0];
}
