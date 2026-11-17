using System;
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

            // The run dialog, registered the same way and for the same reason as the launcher: App hands
            // it the window's run dialog host, which IRunDialog does not name. One instance, because it
            // holds that host and the run it is showing.
            services.TryAddSingleton<RunDialog>();
            services.TryAddSingleton<IRunDialog>(static provider => provider.GetRequiredService<RunDialog>());

            // The safety net for exceptions nothing else caught. One instance, because App hands it to
            // the process-wide hooks once and it remembers what it has already seen. TimeProvider is
            // also the core's; TryAdd keeps whichever came first.
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IUnhandledErrorReporter, UnhandledErrorReporter>();

            // One run at a time across every page, so every page shares the one coordinator; and one
            // list of profiles, so a profile saved on one page is what every other page lists.
            services.TryAddSingleton<RunCoordinator>();
            services.TryAddSingleton<ProfileCatalog>();

            // The pages' view models live as long as the app: the window only hides a page it is not
            // showing, and a page has to come back as it was left.
            services.TryAddSingleton<BackupPageViewModel>();
            services.TryAddSingleton<VerifyRestorePageViewModel>();
            services.TryAddSingleton<ProfilesPageViewModel>();
            services.TryAddSingleton<SettingsPageViewModel>();

            // Transient: the window, its view model and an editor are cheap and per-consumer (see
            // dotnet-di-design) — the Backup page's one-off editor and the Profiles page's are two.
            services.AddTransient<ProfileEditorViewModel>();
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<MainWindow>();

            return services;
        }
    }
}
