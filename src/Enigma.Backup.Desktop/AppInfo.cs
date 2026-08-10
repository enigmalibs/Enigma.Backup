using System;
using System.Reflection;

namespace Enigma.Backup.Desktop;

/// <summary>What this build calls itself: the name, the version, the copyright and the project page.</summary>
/// <remarks>
/// <para>
/// Read from the running assembly's own attributes, which the SDK fills in from
/// <c>Directory.Build.props</c> — so the About section cannot drift from the version that was
/// actually built, and a release bump needs no second edit here.
/// </para>
/// <para>
/// Nothing in here names Avalonia, which is what lets the Settings view model read it directly and a
/// headless test assert on it.
/// </para>
/// </remarks>
public static class AppInfo
{
    /// <summary>What the app is called, on screen and in the window title.</summary>
    public const string Name = "Enigma.Backup";

    /// <summary>Shown when a version cannot be read at all — which no shipped build can reach.</summary>
    private const string UnknownVersion = "unknown";

    /// <summary>Where the project lives.</summary>
    public static Uri ProjectPage { get; } = new("https://github.com/enigmalibs/Enigma.Backup");

    /// <summary>This build's version, without the build metadata the SDK appends.</summary>
    public static string Version { get; } = ResolveVersion(typeof(AppInfo).Assembly);

    /// <summary>This build's copyright line, ready to show as it stands.</summary>
    public static string Copyright { get; } = ResolveCopyright(typeof(AppInfo).Assembly);

    /// <summary>
    /// The readable half of an informational version: everything before the <c>+</c> that carries the
    /// build metadata.
    /// </summary>
    /// <param name="informationalVersion">
    /// The attribute's value, which the SDK writes as <c>1.2.0+&lt;git sha&gt;</c> in a repository and
    /// as a plain version outside one.
    /// </param>
    /// <returns>The version, or an empty string when there was nothing to read.</returns>
    /// <remarks>
    /// Public because it is the one part of this class a test can pin: the assembly a test runs against
    /// carries whatever attributes its own build gave it, so the stripping rule has to be checkable
    /// without forging one.
    /// </remarks>
    public static string ShortVersion(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return string.Empty;
        }

        int metadata = informationalVersion.IndexOf('+', StringComparison.Ordinal);

        return (metadata < 0 ? informationalVersion : informationalVersion[..metadata]).Trim();
    }

    /// <summary>Reads the version an assembly declares, preferring the one a human would recognise.</summary>
    /// <param name="assembly">The assembly to describe.</param>
    private static string ResolveVersion(Assembly assembly)
    {
        string informational = ShortVersion(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
        if (informational.Length > 0)
        {
            return informational;
        }

        // Only reachable if the informational attribute is missing or blank, which the SDK does not do
        // — but "Version unknown" beats an About section that throws on the way to being shown.
        return assembly.GetName().Version?.ToString(3) ?? UnknownVersion;
    }

    /// <summary>Reads the copyright an assembly declares.</summary>
    /// <param name="assembly">The assembly to describe.</param>
    private static string ResolveCopyright(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright?.Trim() ?? string.Empty;
}
