using System;
using System.Threading.Tasks;

namespace Enigma.Backup.Desktop.Services;

/// <summary>Hands something over to the desktop: a folder to the file manager, a link to the browser.</summary>
/// <remarks>
/// <para>
/// The seam exists because opening either one is the platform's job, not this app's, and the only API
/// for it is an Avalonia one hanging off the window — which no view model may name. Behind this
/// interface the Settings dialog can say "open my folder" and a headless test can watch it being said.
/// </para>
/// <para>
/// Neither call reports whether the platform obliged. There is nothing useful to say when a desktop
/// has no file manager registered, and nothing this app could do about it either; the implementation
/// logs what happened and the user sees the same thing they would see from any other launcher — their
/// file manager, or nothing. What must never happen is an exception travelling out of a button.
/// </para>
/// </remarks>
public interface IShellLauncher
{
    /// <summary>Shows a folder in the desktop's file manager.</summary>
    /// <param name="path">The folder, which the caller has already made sure exists.</param>
    Task OpenFolderAsync(string path);

    /// <summary>Opens a link in the desktop's browser.</summary>
    /// <param name="uri">Where to go.</param>
    Task OpenUriAsync(Uri uri);
}
