using System;
using System.Collections.Generic;
using System.IO;
using Enigma.Backup.Core.Scanning;

namespace Enigma.Backup.Core.Archiving;

/// <summary>
/// Turns an entry name out of an archive into a path on this machine, refusing anything that would
/// land outside the directory the user chose.
/// </summary>
/// <remarks>
/// Archives written here only ever contain relative, forward-slashed paths. A leading separator is
/// dropped and the entry lands inside the destination, which is what <c>tar</c> itself does with an
/// absolute member name. Anything that cannot be made to stay inside — a <c>..</c> segment, a
/// platform separator hidden inside a segment, a Windows drive — is refused loudly instead, rather
/// than written somewhere the user did not ask for.
/// </remarks>
internal static class ArchiveEntryPath
{
    /// <summary>Resolves an archive entry name against the restore destination.</summary>
    /// <param name="destinationRoot">The normalized, absolute destination directory.</param>
    /// <param name="entryName">The entry name as stored in the archive.</param>
    /// <param name="fullPath">The absolute path to write to.</param>
    /// <returns><see langword="false"/> when the name is unusable or would escape the destination.</returns>
    public static bool TryResolve(string destinationRoot, string entryName, out string fullPath)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(entryName))
        {
            return false;
        }

        string trimmed = entryName.Trim('/');
        if (trimmed.Length == 0 || Path.IsPathRooted(trimmed))
        {
            return false;
        }

        List<string> segments = [];
        foreach (string segment in trimmed.Split('/'))
        {
            if (segment.Length == 0 || segment is "." or "..")
            {
                return false;
            }

            // A backslash is a legal file-name character on Linux but a separator on Windows, so a
            // segment containing one is only ever ambiguous — and on Windows, an escape route. A
            // colon is legal on Linux too, and names an alternate data stream on Windows.
            if (segment.Contains('\\', StringComparison.Ordinal) || segment.Contains(Path.DirectorySeparatorChar))
            {
                return false;
            }

            if (OperatingSystem.IsWindows() && segment.Contains(':', StringComparison.Ordinal))
            {
                return false;
            }

            segments.Add(segment);
        }

        if (segments.Count == 0)
        {
            return false;
        }

        string combined = Path.Combine([destinationRoot, .. segments]);
        if (!PathUtilities.TryNormalize(combined, out string normalized) || !PathUtilities.IsSameOrUnder(normalized, destinationRoot))
        {
            return false;
        }

        fullPath = normalized;

        return true;
    }
}
