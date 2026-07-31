using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.Backup.Core.Archiving;

/// <summary>
/// The <c>.sha256</c> file written beside every archive, in the format <c>sha256sum</c> reads and
/// writes: the lowercase hex digest, two spaces, the archive's file name, one LF.
/// </summary>
/// <remarks>
/// Only the file name is recorded, never a path, so <c>sha256sum -c backup.tar.zst.sha256</c> works
/// in the output directory on either platform no matter where the backup was made.
/// </remarks>
internal static class Sha256Sidecar
{
    /// <summary>The extension appended to the archive's own name.</summary>
    public const string Extension = ".sha256";

    /// <summary>Number of characters in a SHA-256 digest written as hex.</summary>
    private const int HexLength = 64;

    /// <summary>The sidecar path belonging to an archive.</summary>
    /// <param name="archivePath">The archive's path.</param>
    public static string GetPath(string archivePath) => archivePath + Extension;

    /// <summary>Writes the sidecar for an archive.</summary>
    /// <param name="archivePath">The archive the digest was computed from.</param>
    /// <param name="hashHex">The lowercase hex digest.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The path written.</returns>
    public static async Task<string> WriteAsync(string archivePath, string hashHex, CancellationToken cancellationToken = default)
    {
        string sidecarPath = GetPath(archivePath);
        string line = string.Create(CultureInfo.InvariantCulture, $"{hashHex}  {Path.GetFileName(archivePath)}\n");

        await File.WriteAllTextAsync(sidecarPath, line, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken).ConfigureAwait(false);

        return sidecarPath;
    }

    /// <summary>
    /// Reads the digest out of a sidecar's contents, accepting both <c>sha256sum</c> modes
    /// (<c>hash  name</c> and <c>hash *name</c>) and ignoring blank lines.
    /// </summary>
    /// <param name="contents">The sidecar file's contents.</param>
    /// <param name="hashHex">The digest found, lowercased.</param>
    /// <returns><see langword="true"/> when a digest could be read.</returns>
    public static bool TryParse(string? contents, out string hashHex)
    {
        hashHex = string.Empty;

        if (contents is null)
        {
            return false;
        }

        foreach (ReadOnlySpan<char> line in contents.AsSpan().EnumerateLines())
        {
            ReadOnlySpan<char> trimmed = line.Trim();
            if (trimmed.IsEmpty)
            {
                continue;
            }

            int end = trimmed.IndexOfAny(' ', '\t');
            ReadOnlySpan<char> token = end < 0 ? trimmed : trimmed[..end];

            if (token.Length != HexLength || !IsHex(token))
            {
                return false;
            }

            hashHex = token.ToString().ToLowerInvariant();
            return true;
        }

        return false;
    }

    private static bool IsHex(ReadOnlySpan<char> value)
    {
        foreach (char character in value)
        {
            if (!char.IsAsciiHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}
