using Enigma.Backup.Desktop.Services;
using Enigma.Backup.Desktop.ViewModels;
using Enigma.Backup.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

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

            // Transient: views and view models are cheap and per-consumer (see dotnet-di-design).
            services.AddTransient<ProfileEditorViewModel>();
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<MainWindow>();

            return services;
        }
    }
}
