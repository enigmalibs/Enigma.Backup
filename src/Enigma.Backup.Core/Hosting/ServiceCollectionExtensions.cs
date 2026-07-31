using System;
using Enigma.Backup.Core.Backups;
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

            // The clock the archive services take: the timestamp in an output file's name and the
            // duration in a summary. Registered rather than reached for statically, so a test can run
            // a backup at a fixed instant.
            services.TryAddSingleton(TimeProvider.System);

            services.TryAddSingleton<IBackupService, BackupService>();
            services.TryAddSingleton<IRestoreService, RestoreService>();
            services.TryAddSingleton<IVerificationService, VerificationService>();

            // The encryption container (PHASE04) registers here too.
            return services;
        }
    }
}
