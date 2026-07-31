using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.Backup.Core.Archiving;

/// <summary>
/// Identifies what an archive file actually is from its leading bytes.
/// </summary>
/// <remarks>
/// The file name is never consulted: a renamed or extension-less archive still restores, and a file
/// that merely <em>looks</em> like one by its name is rejected before anything is written.
/// </remarks>
internal static class ArchiveFormatDetector
{
    /// <summary>Number of leading bytes <see cref="Detect"/> needs.</summary>
    public const int SignatureLength = 4;

    private static readonly byte[] GzipMagic = [0x1F, 0x8B];
    private static readonly byte[] ZstdMagic = [0x28, 0xB5, 0x2F, 0xFD];
    private static readonly byte[] Ebk1Magic = [(byte)'E', (byte)'B', (byte)'K', (byte)'1'];

    /// <summary>Identifies the layer the leading bytes belong to.</summary>
    /// <param name="signature">The first <see cref="SignatureLength"/> bytes of the file.</param>
    /// <exception cref="ArchiveFormatException">The bytes match no known layer.</exception>
    public static ArchiveLayer Detect(ReadOnlySpan<byte> signature)
    {
        if (signature.StartsWith(Ebk1Magic))
        {
            return ArchiveLayer.Ebk1;
        }

        if (signature.StartsWith(ZstdMagic))
        {
            return ArchiveLayer.Zstd;
        }

        if (signature.StartsWith(GzipMagic))
        {
            return ArchiveLayer.Gzip;
        }

        throw new ArchiveFormatException(
            "The file is not an Enigma.Backup archive: it is neither a zstd nor a gzip stream, nor an encrypted EBK1 container.");
    }

    /// <summary>
    /// Reads the leading bytes of <paramref name="stream"/>, identifies the layer and rewinds the
    /// stream so the caller can hand it to a decompressor unchanged.
    /// </summary>
    /// <param name="stream">A seekable stream positioned at the start of the file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <exception cref="ArchiveFormatException">The file is too short, or matches no known layer.</exception>
    public static async Task<ArchiveLayer> DetectAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        byte[] signature = new byte[SignatureLength];
        int read = await stream.ReadAtLeastAsync(signature, SignatureLength, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
        stream.Seek(0, SeekOrigin.Begin);

        if (read < SignatureLength)
        {
            throw new ArchiveFormatException("The file is too small to be an archive.");
        }

        return Detect(signature);
    }
}
