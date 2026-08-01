using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Enigma.Backup.Core.Hosting;
using Enigma.Backup.Desktop.Hosting;
using Enigma.Backup.Desktop.Services;
using Enigma.Backup.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Enigma.Backup.Desktop;

public partial class App : Application
{
    private IHost? _host;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        // Console + debug sinks only: this app has no file or remote logging.
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddDebug();

        builder.Services.AddEnigmaBackupCore();
        builder.Services.AddEnigmaBackupDesktop();

        _host = builder.Build();
        _host.Start();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Resolved from the container (with its view model injected), never new'd up here.
            MainWindow window = _host.Services.GetRequiredService<MainWindow>();

            // The dialogs and file pickers need a window to open over, and this is the first moment
            // one exists — see MainWindowProvider for why they cannot simply ask for it.
            _host.Services.GetRequiredService<MainWindowProvider>().Window = window;

            desktop.MainWindow = window;
            desktop.Exit += (_, _) => _host?.StopAsync().GetAwaiter().GetResult();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
