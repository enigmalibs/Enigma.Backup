using System;
using System.Linq;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Backup.Core.Hosting;
using Enigma.Backup.Desktop.Hosting;
using Enigma.Backup.Desktop.Services;
using Enigma.Backup.Desktop.ViewModels;
using Enigma.Backup.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
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

    /// <remarks>
    /// The default <c>ConsoleLifetime</c> cancels SIGTERM and then waits for a <c>StopAsync</c> this
    /// app never makes (Avalonia owns the loop, so the host is started, not run) — an app that cannot
    /// be shut down at logout. It is registered by the host builder before this extension runs, so
    /// the registration has to replace it rather than defer to it.
    /// </remarks>
    [Fact]
    public void TheHostLifetimeReplacesTheConsoleOne_SoSigtermIsNotSwallowed()
    {
        ServiceCollection services = [];
        services.AddSingleton<IHostLifetime, ConsoleLifetime>();

        services.AddEnigmaBackupDesktop();

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.IsType<AvaloniaHostLifetime>(provider.GetRequiredService<IHostLifetime>());
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IHostLifetime));
    }

    /// <remarks>
    /// Three of these own a host control and two own the window's storage provider, all handed over
    /// exactly once in <c>App</c>. A second instance would be one nobody ever wired, and every call
    /// on it would throw — so "singleton" is the behaviour under test, not a preference.
    /// </remarks>
    [Fact]
    public void TheLibrarysServicesResolveToOneInstanceEach()
    {
        using ServiceProvider provider = Build();

        Assert.IsType<ContentDialogService>(provider.GetRequiredService<IContentDialogService>());
        Assert.IsType<OverlayService>(provider.GetRequiredService<IOverlayService>());
        Assert.IsType<InfoBarService>(provider.GetRequiredService<IInfoBarService>());
        Assert.IsType<FileDialogService>(provider.GetRequiredService<IFileDialogService>());
        Assert.IsType<FolderDialogService>(provider.GetRequiredService<IFolderDialogService>());
        Assert.IsType<NavigationService>(provider.GetRequiredService<INavigationService>());

        Assert.Same(provider.GetRequiredService<IContentDialogService>(), provider.GetRequiredService<IContentDialogService>());
        Assert.Same(provider.GetRequiredService<IOverlayService>(), provider.GetRequiredService<IOverlayService>());
        Assert.Same(provider.GetRequiredService<IInfoBarService>(), provider.GetRequiredService<IInfoBarService>());
        Assert.Same(provider.GetRequiredService<IFileDialogService>(), provider.GetRequiredService<IFileDialogService>());
        Assert.Same(provider.GetRequiredService<IFolderDialogService>(), provider.GetRequiredService<IFolderDialogService>());
        Assert.Same(provider.GetRequiredService<INavigationService>(), provider.GetRequiredService<INavigationService>());
    }

    /// <remarks>
    /// <c>MainWindowProvider</c> existed only because dialogs and pickers needed an owner window that
    /// could not be injected without closing a cycle. The library's services need no window, so it is
    /// gone — and nothing may quietly bring it back, because a service that holds the window is how
    /// Avalonia types start leaking towards the view models again. Asserted by name rather than by
    /// type, since the type no longer exists to be named.
    /// </remarks>
    [Fact]
    public void NothingHoldsTheMainWindowAnyMore()
    {
        ServiceCollection services = [];
        services.AddEnigmaBackupDesktop();

        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType.Name.Contains("MainWindowProvider", StringComparison.Ordinal));
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
