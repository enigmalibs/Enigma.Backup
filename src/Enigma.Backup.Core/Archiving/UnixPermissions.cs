using System;
using System.IO;

namespace Enigma.Backup.Core.Archiving;

/// <summary>
/// Reads and applies Unix file modes, so the executable bit on a script survives a backup and a
/// restore on Linux.
/// </summary>
/// <remarks>
/// Windows has no equivalent, so there the mode is neither read nor applied: an archive made on
/// Windows carries tar's defaults, and an archive made on Linux restores on Windows with its modes
/// ignored rather than mangled.
/// </remarks>
internal static class UnixPermissions
{
    /// <summary>The mode of an existing file or directory, or <see langword="null"/> on Windows.</summary>
    /// <param name="path">The path to read.</param>
    public static UnixFileMode? Read(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            return File.GetUnixFileMode(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The mode is a nicety; failing to read it must never fail the entry.
            return null;
        }
    }

    /// <summary>Applies a mode read out of an archive, where the platform supports it.</summary>
    /// <param name="path">The restored file or directory.</param>
    /// <param name="mode">The mode recorded in the archive entry.</param>
    public static void Apply(string path, UnixFileMode mode)
    {
        if (OperatingSystem.IsWindows() || mode == UnixFileMode.None)
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, mode);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
