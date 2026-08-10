using System;
using System.Globalization;
using System.IO;
using Enigma.Backup.Core.Profiles;

namespace Enigma.Backup.Core.Archiving;

/// <summary>
/// Names the file a run produces: <c>&lt;SafeProfileName&gt;_yyyy-MM-dd_HHmmss.tar.zst</c>, with
/// <c>.enc</c> appended when the archive is encrypted.
/// </summary>
/// <remarks>
/// The timestamp is local time, because the name is read by a person browsing the output folder.
/// Two runs of the same profile within one second do not overwrite each other: the second gets a
/// numeric suffix, placed before the extension so <c>.tar.zst</c> stays intact.
/// </remarks>
internal static class ArchiveNaming
{
    /// <summary>The extension appended to an encrypted archive.</summary>
    public const string EncryptedExtension = ".enc";

    /// <summary>The extension of a plain zip archive.</summary>
    public const string ZipExtension = ".zip";

    /// <summary>Builds the name stem — everything before the extension — for one run.</summary>
    /// <param name="profileName">The profile's name; sanitized so any profile can produce a file.</param>
    /// <param name="timestamp">The run's local start time.</param>
    public static string BuildStem(string profileName, DateTimeOffset timestamp)
    {
        string safeName = PortableFileName.Sanitize(profileName, "backup");

        return string.Create(CultureInfo.InvariantCulture, $"{safeName}_{timestamp:yyyy-MM-dd_HHmmss}");
    }

    /// <summary>Builds the extension for one run, including the encryption suffix.</summary>
    /// <param name="algorithm">The compression the archive is written with.</param>
    /// <param name="encrypted">Whether the archive is encrypted.</param>
    /// <remarks>
    /// Zip names itself: it is a container rather than a codec around a tar stream, and it has no
    /// <c>.enc</c> arm because a zip is never encrypted — <c>BackupService</c> refuses the combination
    /// before a byte is written, so a <c>.zip.enc</c> is unreachable rather than merely unwanted.
    /// </remarks>
    public static string BuildExtension(CompressionAlgorithm algorithm, bool encrypted)
    {
        if (algorithm == CompressionAlgorithm.Zip)
        {
            return ZipExtension;
        }

        return encrypted ? CompressionStreams.GetExtension(algorithm) + EncryptedExtension : CompressionStreams.GetExtension(algorithm);
    }

    /// <summary>
    /// Combines a directory, a stem and an extension into a path nothing occupies yet, adding
    /// <c>_2</c>, <c>_3</c>, … to the stem while the file exists.
    /// </summary>
    /// <param name="directory">The output directory.</param>
    /// <param name="stem">The name stem from <see cref="BuildStem"/>.</param>
    /// <param name="extension">The extension from <see cref="BuildExtension"/>.</param>
    public static string ResolveFreePath(string directory, string stem, string extension)
    {
        string path = Path.Combine(directory, stem + extension);

        for (int suffix = 2; File.Exists(path); suffix++)
        {
            path = Path.Combine(directory, string.Create(CultureInfo.InvariantCulture, $"{stem}_{suffix}{extension}"));
        }

        return path;
    }
}
