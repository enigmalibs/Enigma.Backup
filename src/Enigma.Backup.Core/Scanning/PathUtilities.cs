using System;
using System.IO;

namespace Enigma.Backup.Core.Scanning;

/// <summary>
/// Path arithmetic the scan needs: comparing paths the way the running file system does, deciding
/// containment, and deriving an archive-safe top-level name from an input path.
/// </summary>
internal static class PathUtilities
{
    private static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    /// <summary>
    /// How paths compare on this platform: ordinal on Linux, case-insensitive where the file system
    /// is (Windows, macOS).
    /// </summary>
    public static StringComparison Comparison { get; } =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>Tells whether two normalized paths point at the same place.</summary>
    /// <param name="left">The first path.</param>
    /// <param name="right">The second path.</param>
    public static bool AreEqual(string left, string right) => string.Equals(left, right, Comparison);

    /// <summary>
    /// Tells whether <paramref name="path"/> is <paramref name="parent"/> itself or lies inside it.
    /// Both must already be normalized.
    /// </summary>
    /// <param name="path">The candidate descendant.</param>
    /// <param name="parent">The candidate ancestor.</param>
    public static bool IsSameOrUnder(string path, string parent)
    {
        if (AreEqual(path, parent))
        {
            return true;
        }

        if (path.Length <= parent.Length || !path.StartsWith(parent, Comparison))
        {
            return false;
        }

        // "/foo" contains "/foo/bar" but not "/foobar"; a root such as "/" or "C:\" already ends
        // with its separator, so the next character is part of the child name.
        return IsSeparator(path[parent.Length]) || IsSeparator(parent[^1]);
    }

    /// <summary>
    /// Canonicalizes a path — absolute, no redundant segments, no trailing separator — reporting
    /// failure instead of throwing, so a path that is malformed on this platform can be reported
    /// through the error handler like any other missing input.
    /// </summary>
    /// <param name="path">The path to canonicalize.</param>
    /// <param name="normalized">The canonical path, or <paramref name="path"/> unchanged on failure.</param>
    /// <returns><see langword="true"/> when the path could be canonicalized.</returns>
    public static bool TryNormalize(string path, out string normalized)
    {
        try
        {
            normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            normalized = path;
            return false;
        }
    }

    /// <summary>
    /// Derives the top-level archive name for an input path: its own name, or the drive letter for a
    /// file-system root, sanitized so the archive can be extracted on Windows as well as Linux.
    /// </summary>
    /// <param name="path">The normalized input path.</param>
    /// <param name="sanitized">Whether characters had to be replaced to make the name portable.</param>
    /// <returns>A name usable as a top-level archive entry.</returns>
    public static string DeriveRootName(string path, out bool sanitized)
    {
        string name = Path.GetFileName(path);

        if (name.Length == 0)
        {
            // A file-system root: "C:\" has no file name, so fall back to the drive ("C"); "/" has
            // nothing at all to go on.
            name = path.Trim(Separators).TrimEnd(':');
        }

        string safe = PortableFileName.Sanitize(name, "root");
        sanitized = !string.Equals(safe, name, StringComparison.Ordinal);

        return safe;
    }

    private static bool IsSeparator(char character) =>
        character == Path.DirectorySeparatorChar || character == Path.AltDirectorySeparatorChar;
}
