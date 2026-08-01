using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Enigma.Backup.Desktop.Hosting;

/// <summary>
/// The <see cref="IHostLifetime"/> for an app whose process is owned by Avalonia, not by the host.
/// Both methods do nothing: starting and stopping are driven by the desktop lifetime (see
/// <see cref="App.OnFrameworkInitializationCompleted"/>).
/// </summary>
/// <remarks>
/// <para>
/// This replaces the <c>ConsoleLifetime</c> that <see cref="Host.CreateApplicationBuilder()"/>
/// installs by default, which is actively wrong here. It registers POSIX signal handlers that set
/// <c>Cancel = true</c> for SIGTERM and SIGINT — suppressing the runtime's own termination — and then
/// merely signals <c>StopApplication</c>, expecting <c>RunAsync</c> to notice and call
/// <c>StopAsync</c>. This app calls <c>Start</c> rather than <c>Run</c>, because the foreground loop
/// belongs to Avalonia, so nothing ever notices: the signal is swallowed and the window stays open.
/// </para>
/// <para>
/// The visible symptom is an app that cannot be shut down by a session manager — SIGTERM at logout
/// is ignored, the session hangs, and the process is eventually killed. Found by the PHASE06 release
/// smoke test. With the handlers gone, SIGTERM terminates the process the ordinary way, and closing
/// the window still stops the host through the desktop lifetime's <c>Exit</c> event as before.
/// </para>
/// </remarks>
public sealed class AvaloniaHostLifetime : IHostLifetime
{
    /// <inheritdoc />
    public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
