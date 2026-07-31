using Enigma.Backup.Core.Hosting;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Core.Scanning;
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
