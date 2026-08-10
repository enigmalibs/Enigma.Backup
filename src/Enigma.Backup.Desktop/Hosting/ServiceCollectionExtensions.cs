using Enigma.Backup.Desktop.Services;
using Enigma.Backup.Desktop.Settings;
using Enigma.Backup.Desktop.ViewModels;
using Enigma.Backup.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Enigma.Backup.Desktop.Hosting;

/// <summary>Composition root for the desktop app's views, view models and UI services.</summary>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the desktop app's views, view models and UI services in the container.</summary>
        /// <returns>The same collection, so registrations can be chained.</returns>
        public IServiceCollection AddEnigmaBackupDesktop()
        {
            // The control library's dialogs, overlay, notifications and pickers. Nothing here has a
            // window to be shown over: App hands each of them its host control, or the window's
            // storage provider, once the window exists.
            services.AddEnigmaAvaloniaDesktop();

            // Replace, not TryAdd: the host builder has already registered its ConsoleLifetime by
            // the time this runs, and that one swallows SIGTERM (see AvaloniaHostLifetime).
            services.Replace(ServiceDescriptor.Singleton<IHostLifetime, AvaloniaHostLifetime>());

            // Stateless UI seams: one instance serves the whole app.
            services.TryAddSingleton<IUiDispatcher, AvaloniaUiDispatcher>();
            services.TryAddSingleton<IFilePicker, FilePicker>();
            services.TryAddSingleton<IDialogService, DialogService>();
            services.TryAddSingleton<INotificationService, NotificationService>();
            services.TryAddSingleton<IThemeSwitcher, ThemeSwitcher>();

            // Registered under both its own type and the seam, because App has to hand it the window
            // and IShellLauncher deliberately names no Avalonia type — a launcher a test fakes must
            // not have a TopLevel on its interface. The factory keeps the two the same instance, so
            // the one App wires is the one every dialog calls.
            services.TryAddSingleton<ShellLauncher>();
            services.TryAddSingleton<IShellLauncher>(static provider => provider.GetRequiredService<ShellLauncher>());

            // The app's own preferences — Desktop-owned, because a theme variant means nothing to a
            // command line. Registered through a factory because the store's second constructor (an
            // explicit file) exists for tests and cannot be resolved from the container. Its state is
            // the file, so one instance is enough.
            services.TryAddSingleton<IAppSettingsStore>(static provider =>
                new AppSettingsStore(provider.GetRequiredService<ILogger<AppSettingsStore>>()));

            // Singleton because it holds the card it is currently showing: a second instance would be
            // one whose Update calls draw on nothing.
            services.TryAddSingleton<IProgressOverlay, ProgressOverlay>();

            // Transient: views and view models are cheap and per-consumer (see dotnet-di-design).
            services.AddTransient<ProfileEditorViewModel>();
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<MainWindow>();

            return services;
        }
    }
}
