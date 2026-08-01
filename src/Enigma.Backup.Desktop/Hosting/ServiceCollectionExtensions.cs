using Enigma.Backup.Desktop.Services;
using Enigma.Backup.Desktop.ViewModels;
using Enigma.Backup.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
            // The main window is handed to the provider once it has been resolved (see App): the
            // services that open dialogs over it cannot take it as a dependency without closing a
            // cycle, since the window is built from the view model that depends on them.
            services.TryAddSingleton<MainWindowProvider>();
            services.TryAddSingleton<IMainWindowProvider>(static provider => provider.GetRequiredService<MainWindowProvider>());

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
