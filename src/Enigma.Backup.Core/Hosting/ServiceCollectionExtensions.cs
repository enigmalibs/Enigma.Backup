using Microsoft.Extensions.DependencyInjection;

namespace Enigma.Backup.Core.Hosting;

/// <summary>Composition root for the Enigma.Backup core services.</summary>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers every Enigma.Backup core service (profile store, scanner, backup, restore
        /// and verification services) in the container.
        /// </summary>
        /// <returns>The same collection, so registrations can be chained.</returns>
        public IServiceCollection AddEnigmaBackupCore()
        {
            // The core services themselves arrive with PHASE02 (profiles & scanning),
            // PHASE03 (archive pipeline) and PHASE04 (encryption container). This seam exists
            // from the scaffolding on, so the Desktop host never news up a core service.
            return services;
        }
    }
}
