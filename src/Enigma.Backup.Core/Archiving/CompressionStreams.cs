using System;
using System.IO;
using System.IO.Compression;
using Enigma.Backup.Core.Profiles;
using ZstdSharp;

namespace Enigma.Backup.Core.Archiving;

/// <summary>
/// Creates the compression layer of the pipeline, and names the file it produces.
/// </summary>
/// <remarks>
/// Every stream is created with <c>leaveOpen</c>, so the pipeline owns its own layers and disposes
/// them inside-out — the compressor must flush its epilogue before the file below it closes.
/// </remarks>
internal static class CompressionStreams
{
    /// <summary>The file extension of an uncompressed-name-plus-compression archive, per algorithm.</summary>
    /// <param name="algorithm">The algorithm the archive is written with.</param>
    public static string GetExtension(CompressionAlgorithm algorithm) => algorithm switch
    {
        CompressionAlgorithm.Zstd => ".tar.zst",
        CompressionAlgorithm.Gzip => ".tar.gz",
        _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unknown compression algorithm."),
    };

    /// <summary>Wraps <paramref name="destination"/> in a compressor.</summary>
    /// <param name="destination">The stream the compressed bytes are written to; not disposed.</param>
    /// <param name="algorithm">The algorithm to compress with.</param>
    public static Stream CreateCompressor(Stream destination, CompressionAlgorithm algorithm)
    {
        ArgumentNullException.ThrowIfNull(destination);

        return algorithm switch
        {
            // Zstd's own default level (3): the point of choosing zstd is the speed/ratio balance,
            // and a backup that is slower than gzip would defeat it.
            CompressionAlgorithm.Zstd => new CompressionStream(destination, Compressor.DefaultCompressionLevel, bufferSize: 0, leaveOpen: true),
            CompressionAlgorithm.Gzip => new GZipStream(destination, CompressionLevel.Optimal, leaveOpen: true),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unknown compression algorithm."),
        };
    }

    /// <summary>
    /// Tells whether an exception means "these bytes are not the archive they claim to be" — a corrupt
    /// or truncated stream — rather than a failure of the machine reading it.
    /// </summary>
    /// <remarks>
    /// Each layer reports damage in its own currency: zstd throws <see cref="ZstdException"/>, gzip
    /// throws <see cref="InvalidDataException"/>, and a stream that simply stops early ends up as an
    /// <see cref="EndOfStreamException"/> in the tar reader. Naming them in one place keeps that
    /// knowledge next to the layers it belongs to.
    /// </remarks>
    /// <param name="exception">The exception to classify.</param>
    public static bool IsCorruptData(Exception exception) =>
        exception is InvalidDataException or EndOfStreamException or FormatException or ZstdException;

    /// <summary>Wraps <paramref name="source"/> in a decompressor.</summary>
    /// <param name="source">The stream the compressed bytes are read from; not disposed.</param>
    /// <param name="layer">The layer detected in the file.</param>
    public static Stream CreateDecompressor(Stream source, ArchiveLayer layer)
    {
        ArgumentNullException.ThrowIfNull(source);

        return layer switch
        {
            // checkEndOfStream makes a truncated file fail instead of returning a short archive.
            ArchiveLayer.Zstd => new DecompressionStream(source, bufferSize: 0, checkEndOfStream: true, leaveOpen: true),
            ArchiveLayer.Gzip => new GZipStream(source, CompressionMode.Decompress, leaveOpen: true),
            _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, "The layer is not a compression layer."),
        };
    }
}
