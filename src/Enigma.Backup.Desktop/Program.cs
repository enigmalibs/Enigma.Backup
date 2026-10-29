using System;
using Avalonia;
using Enigma.Backup.Desktop.Services;

namespace Enigma.Backup.Desktop;

internal static class Program
{
    // The Avalonia desktop lifetime owns the foreground loop; the IHost is created, started and
    // stopped by App (see App.axaml.cs), so Main stays synchronous. The process-wide exception hooks
    // come first, so a failure anywhere in the start is caught too.
    [STAThread]
    public static void Main(string[] args)
    {
        UnhandledExceptionHooks.Install();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Also used by the Avalonia XAML previewer/designer, which requires this exact signature.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
