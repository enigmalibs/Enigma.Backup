using System;
using System.Linq;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Backup.Core.Hosting;
using Enigma.Backup.Desktop.Hosting;
using Enigma.Backup.Desktop.Services;
using Enigma.Backup.Desktop.Settings;
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
/// the worst possible moment to find out — the window's view model holds every page, and the pages
/// take most of the app's services between them.
/// </remarks>
public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void TheMainViewModelResolvesWithEveryPageItShows()
    {
        using ServiceProvider provider = Build();

        MainWindowViewModel viewModel = provider.GetRequiredService<MainWindowViewModel>();

        Assert.NotNull(viewModel.BackupPage.Single);
        Assert.NotNull(viewModel.ProfilesPage.Editor);
        Assert.NotSame(viewModel.BackupPage.Single, viewModel.ProfilesPage.Editor);
        Assert.NotSame(viewModel.BackupPage.Single, viewModel.BackupPage.FromProfile);
        Assert.NotNull(viewModel.VerifyRestorePage);
        Assert.NotNull(viewModel.SettingsPage);
        Assert.Equal(AppPage.Backup, viewModel.CurrentPage);
    }

    /// <remarks>
    /// The window only hides a page it is not showing, so each page has to be the one instance for the
    /// app's lifetime — and both run pages have to share the one coordinator, or two runs could start at
    /// once.
    /// </remarks>
    [Fact]
    public void ThePagesAndTheRunCoordinatorAreOneInstanceEach()
    {
        using ServiceProvider provider = Build();

        Assert.Same(provider.GetRequiredService<BackupPageViewModel>(), provider.GetRequiredService<BackupPageViewModel>());
        Assert.Same(provider.GetRequiredService<VerifyRestorePageViewModel>(), provider.GetRequiredService<VerifyRestorePageViewModel>());
        Assert.Same(provider.GetRequiredService<SettingsPageViewModel>(), provider.GetRequiredService<SettingsPageViewModel>());
        Assert.Same(provider.GetRequiredService<ProfilesPageViewModel>(), provider.GetRequiredService<ProfilesPageViewModel>());
        Assert.Same(provider.GetRequiredService<ProfileCatalog>(), provider.GetRequiredService<ProfileCatalog>());
        Assert.Same(provider.GetRequiredService<RunCoordinator>(), provider.GetRequiredService<RunCoordinator>());
        Assert.Same(
            provider.GetRequiredService<BackupPageViewModel>(),
            provider.GetRequiredService<MainWindowViewModel>().BackupPage);
    }

    [Fact]
    public void TheUiSeamsResolveToTheirAvaloniaImplementations()
    {
        using ServiceProvider provider = Build();

        Assert.IsType<AvaloniaUiDispatcher>(provider.GetRequiredService<IUiDispatcher>());
        Assert.IsType<FilePicker>(provider.GetRequiredService<IFilePicker>());
        Assert.IsType<DialogService>(provider.GetRequiredService<IDialogService>());
        Assert.IsType<RunDialog>(provider.GetRequiredService<IRunDialog>());
        Assert.IsType<ThemeSwitcher>(provider.GetRequiredService<IThemeSwitcher>());
        Assert.IsType<ShellLauncher>(provider.GetRequiredService<IShellLauncher>());
    }

    /// <remarks>
    /// <c>App</c> hands the launcher the window through its concrete type, because
    /// <c>IShellLauncher</c> deliberately names no Avalonia type. That only works if both registrations
    /// resolve to the same object — otherwise the one the Settings page calls is a second instance
    /// nobody ever wired, and every click throws.
    /// </remarks>
    [Fact]
    public void TheLauncherAppWiresIsTheLauncherTheDialogsCall()
    {
        using ServiceProvider provider = Build();

        Assert.Same(provider.GetRequiredService<ShellLauncher>(), provider.GetRequiredService<IShellLauncher>());
        Assert.Same(provider.GetRequiredService<IShellLauncher>(), provider.GetRequiredService<IShellLauncher>());
    }

    /// <remarks>
    /// The store has a second constructor taking an explicit file, which the container cannot resolve
    /// and which a test would silently get instead — so it is registered through a factory, and this is
    /// what proves the app's one is pointed at the per-user configuration folder.
    /// </remarks>
    [Fact]
    public void TheSettingsStoreResolvesOntoThePerUserConfigurationFile()
    {
        using ServiceProvider provider = Build();

        IAppSettingsStore store = provider.GetRequiredService<IAppSettingsStore>();

        Assert.IsType<AppSettingsStore>(store);
        Assert.Equal(AppSettingsStore.DefaultPath, store.FilePath);
        Assert.Same(store, provider.GetRequiredService<IAppSettingsStore>());
    }

    /// <remarks>
    /// <c>App</c> hands the run dialog its host through the concrete type, because <c>IRunDialog</c>
    /// names no Avalonia type. Both registrations have to resolve to the one instance — otherwise the
    /// one a run is shown on is a second dialog nobody handed a host, and every run throws.
    /// </remarks>
    [Fact]
    public void TheRunDialogAppWiresIsTheOneEveryRunIsShownOn()
    {
        using ServiceProvider provider = Build();

        Assert.Same(provider.GetRequiredService<RunDialog>(), provider.GetRequiredService<IRunDialog>());
        Assert.Same(provider.GetRequiredService<IRunDialog>(), provider.GetRequiredService<IRunDialog>());
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
    /// The dialog service owns a host control and the two pickers own the window's storage provider, all
    /// handed over exactly once in <c>App</c>. A second instance would be one nobody ever wired, and
    /// every call on it would throw — so "singleton" is the behaviour under test, not a preference.
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

    /// <remarks>
    /// <c>App</c> hands the reporter to the process-wide hooks once, and the reporter remembers which
    /// errors it has already shown — so a second instance would be one that forgets.
    /// </remarks>
    [Fact]
    public void TheSafetyNetIsOneInstance()
    {
        using ServiceProvider provider = Build();

        Assert.IsType<UnhandledErrorReporter>(provider.GetRequiredService<IUnhandledErrorReporter>());
        Assert.Same(provider.GetRequiredService<IUnhandledErrorReporter>(), provider.GetRequiredService<IUnhandledErrorReporter>());
        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
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
