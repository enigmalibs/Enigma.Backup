using System.Threading.Tasks;
using Enigma.Backup.Desktop.ViewModels;

namespace Enigma.Backup.Desktop.Services;

/// <summary>
/// The run dialog: what covers the window while a backup, restore or verification is running, the only
/// place that run can be stopped from, and where its outcome is read once it is over.
/// </summary>
/// <remarks>
/// <para>
/// A seam, not a control. The view model hands over a <see cref="RunViewModel"/> and drives it; it never
/// learns that the other side of this is an Avalonia dialog — which is what lets a test drive a whole
/// run and read what the dialog was shown.
/// </para>
/// <para>
/// The dialog cannot be closed while <see cref="RunViewModel.IsRunning"/> is true: its one button is
/// Cancel, and Escape or a click outside it do nothing. So the caller's one duty is to
/// <see cref="RunViewModel.Finish"/> the run whatever happens — a run left running is a dialog nobody
/// can close.
/// </para>
/// </remarks>
public interface IRunDialog
{
    /// <summary>
    /// Raises the dialog for a run that is starting, replacing whatever run it showed before. Returns
    /// once it is up; the user closes it, once the run is over.
    /// </summary>
    /// <param name="run">The run to show.</param>
    Task ShowAsync(RunViewModel run);
}
