using System.Linq;
using Enigma.Backup.Core.Hosting;
using Enigma.Backup.Desktop.Hosting;
using Enigma.Backup.Desktop.Services;
using Enigma.Backup.Desktop.ViewModels;
using Enigma.Backup.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Enigma.Backup.Desktop.Tests.Hosting;

/// <summary>
/// The composition root, checked without starting Avalonia.
/// </summary>
/// <remarks>
/// A missing registration would otherwise only show up as the app failing to open a window, which is
/// the worst possible moment to find out — the main view model alone takes nine dependencies.
/// </remarks>
public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void TheMainViewModelResolvesWithEveryDependencyItNeeds()
    {
        using ServiceProvider provider = Build();

        MainWindowViewModel viewModel = provider.GetRequiredService<MainWindowViewModel>();

        Assert.NotNull(viewModel.Editor);
        Assert.Equal("Enigma.Backup", viewModel.Title);
    }

    [Fact]
    public void TheUiSeamsResolveToTheirAvaloniaImplementations()
    {
        using ServiceProvider provider = Build();

        Assert.IsType<AvaloniaUiDispatcher>(provider.GetRequiredService<IUiDispatcher>());
        Assert.IsType<FilePicker>(provider.GetRequiredService<IFilePicker>());
        Assert.IsType<DialogService>(provider.GetRequiredService<IDialogService>());
    }

    [Fact]
    public void TheWindowProviderIsOneInstance_SoFillingItInReachesEveryDialog()
    {
        using ServiceProvider provider = Build();

        MainWindowProvider settable = provider.GetRequiredService<MainWindowProvider>();
        IMainWindowProvider read = provider.GetRequiredService<IMainWindowProvider>();

        Assert.Same(settable, read);
        Assert.Null(read.Window);
    }

    [Fact]
    public void ViewsAndViewModelsArePerConsumer()
    {
        ServiceCollection services = [];
        services.AddEnigmaBackupDesktop();

        Assert.All(
            services.Where(descriptor =>
                descriptor.ServiceType == typeof(MainWindow)
                || descriptor.ServiceType == typeof(MainWindowViewModel)
                || descriptor.ServiceType == typeof(ProfileEditorViewModel)),
            descriptor => Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime));

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(MainWindow));
    }

    [Fact]
    public void RegisteringTwiceChangesNothing()
    {
        ServiceCollection services = [];
        services.AddEnigmaBackupCore();
        services.AddEnigmaBackupDesktop();
        services.AddEnigmaBackupDesktop();

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<IUiDispatcher>(), provider.GetRequiredService<IUiDispatcher>());
    }

    private static ServiceProvider Build()
    {
        ServiceCollection services = [];

        services.AddLogging();
        services.AddEnigmaBackupCore();
        services.AddEnigmaBackupDesktop();

        return services.BuildServiceProvider();
    }
}
