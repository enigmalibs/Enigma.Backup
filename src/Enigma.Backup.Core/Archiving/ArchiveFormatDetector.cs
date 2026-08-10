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

    /// <summary>A zip's first local file header — what every archive with at least one entry starts with.</summary>
    private static readonly byte[] ZipLocalHeaderMagic = [(byte)'P', (byte)'K', 0x03, 0x04];

    /// <summary>
    /// A zip's end-of-central-directory record. An archive with no entries at all has no local file
    /// header to start with, so this is its first four bytes — and an entry-less run is reachable, from
    /// a profile whose every input was skipped.
    /// </summary>
    private static readonly byte[] ZipEndOfCentralDirectoryMagic = [(byte)'P', (byte)'K', 0x05, 0x06];

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

        if (signature.StartsWith(ZipLocalHeaderMagic) || signature.StartsWith(ZipEndOfCentralDirectoryMagic))
        {
            return ArchiveLayer.Zip;
        }

        throw new ArchiveFormatException(
            "The file is not an Enigma.Backup archive: it is not a zip, a zstd or a gzip stream, nor an encrypted EBK1 container.");
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

    /// <summary>
    /// Identifies the layer at the head of a stream that cannot be rewound, and returns a stream that
    /// still starts at its first byte.
    /// </summary>
    /// <remarks>
    /// This is how a decrypted payload is identified: an <c>EBK1</c> container does not record which
    /// compression it wraps, and it does not need to — the payload says so itself, in the same four
    /// bytes that identify an unencrypted archive.
    /// </remarks>
    /// <param name="source">A forward-only stream positioned at the payload's first byte.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The layer, and the stream to read the payload from.</returns>
    /// <exception cref="ArchiveFormatException">The payload is too short, or matches no known layer.</exception>
    public static async Task<(ArchiveLayer Layer, Stream Payload)> DetectForwardOnlyAsync(Stream source, CancellationToken cancellationToken = default)
    {
        byte[] signature = new byte[SignatureLength];
        int read = await source.ReadAtLeastAsync(signature, SignatureLength, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);

        if (read < SignatureLength)
        {
            throw new ArchiveFormatException("The archive's contents are too small to be a compressed archive.");
        }

        return (Detect(signature), new PrefixedReadStream(signature, source));
    }
}
