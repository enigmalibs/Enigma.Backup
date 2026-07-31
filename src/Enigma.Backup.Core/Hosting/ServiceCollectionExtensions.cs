using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Core.Scanning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
            // Both are stateless once constructed, so a single instance serves the whole app; a run's
            // mutable state lives in the objects passed through the call, never in the service.
            // The store is registered through a factory because its second constructor (an explicit
            // directory) exists for tests and cannot be resolved from the container.
            services.TryAddSingleton<IProfileStore>(static _ => new ProfileStore());
            services.TryAddSingleton<IInputScanner, InputScanner>();

            // The archive pipeline (PHASE03) and the encryption container (PHASE04) register here too.
            return services;
        }
    }
}
