using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.Logging;

namespace Enigma.Backup.Desktop.Services;

/// <summary>The <see cref="IShellLauncher"/> over the window's own <see cref="ILauncher"/>.</summary>
/// <remarks>
/// <para>
/// The launcher belongs to a <see cref="TopLevel"/>, so this one is handed the window once in
/// <see cref="App"/> — beside the picker services' storage provider, before the window is shown, and
/// for the same reason: there is no launcher until something is on screen to launch from. It is
/// registered as the concrete type as well as the interface precisely so <see cref="App"/> can make
/// that call without <see cref="IShellLauncher"/> having to name an Avalonia type and drag it into
/// every fake.
/// </para>
/// <para>
/// Both calls answer with a <see cref="bool"/> the platform decides, and either can throw for reasons
/// this app has no answer to — no file manager, a sandbox, a portal that timed out. All of it ends in
/// a log line: a preference dialog's convenience button is not worth an exception escaping into a
/// click handler.
/// </para>
/// </remarks>
public sealed class ShellLauncher : IShellLauncher
{
    private readonly ILogger<ShellLauncher> _logger;

    private TopLevel? _topLevel;

    /// <summary>Creates a launcher that is not wired to a window yet.</summary>
    /// <param name="logger">Records what the desktop refused to open.</param>
    public ShellLauncher(ILogger<ShellLauncher> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    /// <summary>Hands over the window every launch goes through. Called once, by <see cref="App"/>.</summary>
    /// <param name="topLevel">The window.</param>
    public void SetTopLevel(TopLevel topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);

        _topLevel = topLevel;
    }

    /// <inheritdoc />
    public Task OpenFolderAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return LaunchAsync(launcher => launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(path)), path);
    }

    /// <inheritdoc />
    public Task OpenUriAsync(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        return LaunchAsync(launcher => launcher.LaunchUriAsync(uri), uri.ToString());
    }

    /// <summary>Runs one launch and turns everything that can go wrong into a log line.</summary>
    /// <param name="launch">What to ask the platform for.</param>
    /// <param name="subject">What was being opened, for the log.</param>
    private async Task LaunchAsync(Func<ILauncher, Task<bool>> launch, string subject)
    {
        // Wired in App before the window is shown, so this is a programming error rather than a state
        // the user can reach — the same answer the control library's services give.
        if (_topLevel is not TopLevel topLevel)
        {
            throw new InvalidOperationException(
                $"{nameof(ShellLauncher)} has no window yet. {nameof(App)} calls {nameof(SetTopLevel)} once at startup, before the window is shown.");
        }

        try
        {
            if (!await launch(topLevel.Launcher).ConfigureAwait(true))
            {
                _logger.LogWarning("The desktop declined to open {Subject}.", subject);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "The desktop could not open {Subject}.", subject);
        }
    }
}
