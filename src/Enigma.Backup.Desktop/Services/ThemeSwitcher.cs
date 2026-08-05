using Avalonia;
using Avalonia.Styling;
using Enigma.Backup.Desktop.Settings;

namespace Enigma.Backup.Desktop.Services;

/// <summary>The <see cref="IThemeSwitcher"/> that assigns the application's requested variant.</summary>
/// <remarks>
/// Every control on screen re-colours in one step — no reload, no rebuilt visual tree — because the
/// control library's brushes are shared instances whose colours are theme-scoped. That is also why
/// every brush reference in this app is a <c>DynamicResource</c>: a <c>StaticResource</c> freezes the
/// colour and the control stops following a switch made here.
/// </remarks>
public sealed class ThemeSwitcher : IThemeSwitcher
{
    /// <inheritdoc />
    public void Apply(AppTheme theme)
    {
        // There is no Application in a headless test, and nothing on screen to repaint either.
        if (Application.Current is not Application application)
        {
            return;
        }

        application.RequestedThemeVariant = theme switch
        {
            AppTheme.Light => ThemeVariant.Light,
            AppTheme.Dark => ThemeVariant.Dark,

            // Default is the variant that follows the operating system, which is what System means —
            // and it is what App.axaml starts on, so anything unrecognised lands on the documented
            // default rather than on a variant nobody chose.
            _ => ThemeVariant.Default,
        };
    }
}
