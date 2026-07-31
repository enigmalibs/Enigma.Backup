using Enigma.Backup.Core.Hosting;
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
        // the guard that keeps this honest as PHASE02-PHASE04 fill the composition root.
        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });

        Assert.NotNull(provider);
    }
}
