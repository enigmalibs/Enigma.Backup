using System;
using System.IO;

namespace Enigma.Backup.Core.Tests.TestSupport;

/// <summary>
/// Makes a real file unreadable, so the pipeline's Skip / Retry / Abort path can be tested against
/// the file system rather than a mock.
/// </summary>
/// <remarks>
/// Not every machine can produce the condition: Windows has no equivalent of a mode, and root reads
/// a file whatever its mode says. <see cref="TryDeny"/> reports that instead of pretending, so a test
/// can skip rather than pass for the wrong reason.
/// </remarks>
public static class AccessDenial
{
    /// <summary>Tries to make <paramref name="path"/> unreadable by this user.</summary>
    /// <param name="path">The file to lock away.</param>
    /// <returns><see langword="true"/> when the file really cannot be read any more.</returns>
    public static bool TryDeny(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return false;
        }

        File.SetUnixFileMode(path, UnixFileMode.None);

        try
        {
            using FileStream probe = File.OpenRead(path);
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    /// <summary>Gives read and write access back, the way a user would before answering Retry.</summary>
    /// <param name="path">The file to unlock.</param>
    public static void Restore(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
