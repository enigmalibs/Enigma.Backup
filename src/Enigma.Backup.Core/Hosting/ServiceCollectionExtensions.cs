using System;
using Enigma.Backup.Core.Backups;
using Enigma.Backup.Core.Encryption;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Core.Restoring;
using Enigma.Backup.Core.Scanning;
using Enigma.Backup.Core.Verification;
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
        /// and verification services) and the encryption defaults in the container.
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

            // The clock the archive services take: the timestamp in an output file's name and the
            // duration in a summary. Registered rather than reached for statically, so a test can run
            // a backup at a fixed instant.
            services.TryAddSingleton(TimeProvider.System);

            // What a *new* EBK1 container is written with. Reading never consults it — an archive's own
            // header carries everything needed to open it — so replacing this only changes future
            // backups, never the ability to restore old ones.
            services.TryAddSingleton(new Ebk1Options());

            services.TryAddSingleton<IBackupService, BackupService>();
            services.TryAddSingleton<IRestoreService, RestoreService>();
            services.TryAddSingleton<IVerificationService, VerificationService>();

            return services;
        }
    }
}
