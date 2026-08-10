using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Backup.Core.Hosting;
using Enigma.Backup.Desktop.Hosting;
using Enigma.Backup.Desktop.Services;
using Enigma.Backup.Desktop.Settings;
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

            // The control library's services own no window: they are handed the host controls that
            // draw them, and the window's storage provider, once — here, and before the window is
            // shown, because each of them throws InvalidOperationException from its very first call
            // if this is skipped. The hosts' z-order is fixed in MainWindow.axaml, not here.
            IServiceProvider services = _host.Services;
            services.GetRequiredService<IContentDialogService>().RegisterHost(window.HostDialog);
            services.GetRequiredService<IOverlayService>().RegisterHost(window.HostOverlay);
            services.GetRequiredService<IInfoBarService>().RegisterHost(window.HostInfoBar);
            services.GetRequiredService<IFileDialogService>().SetStorageProvider(window.StorageProvider);
            services.GetRequiredService<IFolderDialogService>().SetStorageProvider(window.StorageProvider);

            // Ours, wired the same way and for the same reason: a launcher belongs to a window, and
            // there is none until this point. Resolved as the concrete type, because the seam the view
            // models see names no Avalonia type at all.
            services.GetRequiredService<ShellLauncher>().SetTopLevel(window);

            // The stored theme, applied before the window is shown so the app never opens in one
            // variant and repaints into the other. A missing or unreadable settings file leaves the
            // ThemeVariant.Default that App.axaml starts on — which is "follow the OS".
            AppSettings settings = services.GetRequiredService<IAppSettingsStore>().Load();
            services.GetRequiredService<IThemeSwitcher>().Apply(settings.Theme);

            desktop.MainWindow = window;
            desktop.Exit += (_, _) => _host?.StopAsync().GetAwaiter().GetResult();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
