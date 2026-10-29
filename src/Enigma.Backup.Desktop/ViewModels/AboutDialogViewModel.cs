using System;
using CommunityToolkit.Mvvm.Input;

namespace Enigma.Backup.Desktop.ViewModels;

/// <summary>What the About dialog says: which product this is, and which build of it is running.</summary>
/// <remarks>
/// <para>
/// The same dialog every Enigma app shows, in the same shape: icon, name, version, the revision the
/// build was cut from, and the copyright — nothing else. Read-only: nothing here changes while the
/// dialog is open, and there is no answer to read back once it closes.
/// </para>
/// <para>
/// A <see cref="DialogViewModel"/> nevertheless, so it closes the way every other dialog of this app
/// does: Escape is the host's, Enter is the content's key binding to <see cref="CloseCommand"/>.
/// </para>
/// </remarks>
public sealed class AboutDialogViewModel : DialogViewModel
{
    /// <summary>Creates the view model over the running build.</summary>
    public AboutDialogViewModel()
        : this(AppInfo.Version, AppInfo.BuildSha, AppInfo.Copyright)
    {
    }

    /// <summary>Creates the view model over given values.</summary>
    /// <param name="version">The version to show.</param>
    /// <param name="buildSha">The short revision to show, or <see langword="null"/> to show no build line.</param>
    /// <param name="copyright">The copyright notice, or an empty string to show no copyright line.</param>
    /// <remarks>
    /// The values are arguments rather than read here because <see cref="AppInfo"/> reports the assembly
    /// under test, which a test cannot vary: this is the seam that lets a build with no revision be
    /// asserted at all.
    /// </remarks>
    public AboutDialogViewModel(string version, string? buildSha, string copyright)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(copyright);

        Version = version;
        BuildSha = buildSha;
        Copyright = copyright;

        CloseCommand = new RelayCommand(RequestClose);
    }

    /// <summary>The product's name, as the window title says it.</summary>
    public string DisplayName => AppInfo.Name;

    /// <summary>The version of the running build.</summary>
    public string Version { get; }

    /// <summary>The short commit this build was cut from, or <see langword="null"/> if it recorded none.</summary>
    public string? BuildSha { get; }

    /// <summary>
    /// Whether there is a revision to show: a build with none shows no build line at all, not an empty
    /// one.
    /// </summary>
    public bool HasBuildSha => !string.IsNullOrEmpty(BuildSha);

    /// <summary>The copyright notice, or an empty string if the assembly carries none.</summary>
    public string Copyright { get; }

    /// <summary>Whether there is a copyright notice to show.</summary>
    public bool HasCopyright => Copyright.Length > 0;

    /// <summary>Closes the dialog. It asks nothing, so there is nothing to accept.</summary>
    public RelayCommand CloseCommand { get; }
}
