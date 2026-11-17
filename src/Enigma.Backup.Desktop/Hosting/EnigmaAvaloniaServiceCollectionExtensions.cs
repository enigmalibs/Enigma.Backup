using Enigma.Avalonia.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Enigma.Backup.Desktop.Hosting;

/// <summary>Registers the six services shipped by <c>Enigma.Avalonia.Desktop</c>.</summary>
/// <remarks>
/// <para>
/// The package ships no registration extension of its own, so this is it. All six are singletons
/// because the dialog service owns a host control and the two pickers own the window's storage
/// provider, all handed over exactly once in <see cref="App"/> — a second instance would be one nobody
/// ever wired, and every call on it would throw.
/// </para>
/// <para>
/// Nothing outside <c>Desktop/Services/</c> injects these: the app's own
/// <see cref="Services.IDialogService"/> and <see cref="Services.IFilePicker"/> stand in front of
/// them, which is what keeps Avalonia types out of the view models.
/// </para>
/// </remarks>
public static class EnigmaAvaloniaServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the control library's services in the container.</summary>
        /// <returns>The same collection, so registrations can be chained.</returns>
        public IServiceCollection AddEnigmaAvaloniaDesktop()
        {
            services.TryAddSingleton<IContentDialogService, ContentDialogService>();
            services.TryAddSingleton<IFileDialogService, FileDialogService>();
            services.TryAddSingleton<IFolderDialogService, FolderDialogService>();

            // Registered for completeness, and unused: a run is shown in a dialog of its own rather
            // than on an overlay or a banner, and nothing builds pages through the navigation service.
            // Their hosts are not in the window, so calling one would throw — which is why nothing
            // does. Each costs one descriptor, and the library's set stays registered as a set rather
            // than as the three pieces we happen to need today.
            services.TryAddSingleton<IOverlayService, OverlayService>();
            services.TryAddSingleton<IInfoBarService, InfoBarService>();
            services.TryAddSingleton<INavigationService, NavigationService>();

            return services;
        }
    }
}
