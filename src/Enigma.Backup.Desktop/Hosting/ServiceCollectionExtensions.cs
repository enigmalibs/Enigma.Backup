using Enigma.Backup.Desktop.ViewModels;
using Enigma.Backup.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Enigma.Backup.Desktop.Hosting;

/// <summary>Composition root for the desktop app's views, view models and UI services.</summary>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the desktop app's views and view models in the container.</summary>
        /// <returns>The same collection, so registrations can be chained.</returns>
        public IServiceCollection AddEnigmaBackupDesktop()
        {
            // Transient: views and view models are cheap and per-consumer (see dotnet-di-design).
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<MainWindow>();

            return services;
        }
    }
}
