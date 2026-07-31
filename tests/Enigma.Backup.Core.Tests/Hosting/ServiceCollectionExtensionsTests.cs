using System;
using Enigma.Backup.Core.Backups;
using Enigma.Backup.Core.Hosting;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Core.Restoring;
using Enigma.Backup.Core.Scanning;
using Enigma.Backup.Core.Tests.TestSupport;
using Enigma.Backup.Core.Verification;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Enigma.Backup.Core.Tests.Hosting;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddEnigmaBackupCore_ReturnsTheSameCollection()
    {
        ServiceCollection services = new();

        IServiceCollection result = services.AddEnigmaBackupCore();

        Assert.Same(services, result);
    }

    [Fact]
    public void AddEnigmaBackupCore_BuildsAValidatedProvider()
    {
        ServiceCollection services = new();
        services.AddEnigmaBackupCore();

        // Validating scopes and on-build proves every registration the core adds is resolvable —
        // the guard that keeps this honest as PHASE03-PHASE04 fill the composition root.
        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });

        Assert.NotNull(provider);
    }

    [Fact]
    public void AddEnigmaBackupCore_ResolvesTheProfileStoreOverThePerUserFolder()
    {
        using ServiceProvider provider = BuildProvider();

        IProfileStore store = provider.GetRequiredService<IProfileStore>();

        Assert.Equal(ProfileStore.DefaultDirectory, store.DirectoryPath);
        Assert.Same(store, provider.GetRequiredService<IProfileStore>());
    }

    [Fact]
    public void AddEnigmaBackupCore_ResolvesTheInputScanner()
    {
        using ServiceProvider provider = BuildProvider();

        IInputScanner scanner = provider.GetRequiredService<IInputScanner>();

        Assert.IsType<InputScanner>(scanner);
        Assert.Same(scanner, provider.GetRequiredService<IInputScanner>());
    }

    [Fact]
    public void AddEnigmaBackupCore_ResolvesTheArchiveServicesAsSingletons()
    {
        using ServiceProvider provider = BuildProvider();

        IBackupService backup = provider.GetRequiredService<IBackupService>();
        IRestoreService restore = provider.GetRequiredService<IRestoreService>();
        IVerificationService verification = provider.GetRequiredService<IVerificationService>();

        Assert.IsType<BackupService>(backup);
        Assert.IsType<RestoreService>(restore);
        Assert.IsType<VerificationService>(verification);
        Assert.Same(backup, provider.GetRequiredService<IBackupService>());
        Assert.Same(restore, provider.GetRequiredService<IRestoreService>());
        Assert.Same(verification, provider.GetRequiredService<IVerificationService>());
    }

    [Fact]
    public void AddEnigmaBackupCore_ResolvesTheSystemClock()
    {
        using ServiceProvider provider = BuildProvider();

        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public void AddEnigmaBackupCore_LeavesAnExistingClockInPlace()
    {
        ServiceCollection services = new();
        FixedTimeProvider replacement = new(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        services.AddSingleton<TimeProvider>(replacement);

        services.AddEnigmaBackupCore();

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Same(replacement, provider.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public void AddEnigmaBackupCore_LeavesAnExistingRegistrationInPlace()
    {
        ServiceCollection services = new();
        ProfileStore replacement = new(System.IO.Path.GetTempPath());
        services.AddSingleton<IProfileStore>(replacement);

        services.AddEnigmaBackupCore();

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Same(replacement, provider.GetRequiredService<IProfileStore>());
    }

    private static ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        services.AddEnigmaBackupCore();

        return services.BuildServiceProvider();
    }
}
