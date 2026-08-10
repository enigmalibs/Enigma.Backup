using System;
using System.Formats.Tar;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Core.Scanning;

namespace Enigma.Backup.Core.Archiving;

/// <summary>
/// The <see cref="IArchiveEntryWriter"/> that produces this application's original output: a PAX tar
/// stream through a compressor.
/// </summary>
/// <remarks>
/// PAX rather than a classic header, because a path longer than 100 bytes is ordinary and a classic
/// header cannot hold one. The compressor and the tar writer are both created with <c>leaveOpen</c>:
/// they are this object's to close, in that order, while the stream underneath stays open for the
/// digest and the container epilogue below it.
/// </remarks>
internal sealed class TarEntryWriter : IArchiveEntryWriter
{
    private readonly Stream _compressor;
    private readonly TarWriter _tar;

    /// <summary>Creates a tar writer over a compressor over <paramref name="destination"/>.</summary>
    /// <param name="destination">The stream the compressed bytes are written to; not disposed here.</param>
    /// <param name="algorithm">The compression to apply.</param>
    public TarEntryWriter(Stream destination, CompressionAlgorithm algorithm)
    {
        ArgumentNullException.ThrowIfNull(destination);

        _compressor = CompressionStreams.CreateCompressor(destination, algorithm);

        try
        {
            _tar = new TarWriter(_compressor, TarEntryFormat.Pax, leaveOpen: true);
        }
        catch
        {
            _compressor.Dispose();

            throw;
        }
    }

    /// <inheritdoc />
    public async Task WriteDirectoryAsync(ScanEntry entry, CancellationToken cancellationToken)
    {
        // The trailing slash is what every other tar implementation writes for a directory.
        PaxTarEntry tarEntry = new(TarEntryType.Directory, entry.ArchivePath + '/')
        {
            ModificationTime = entry.LastWriteTime,
        };

        if (UnixPermissions.Read(entry.SourcePath) is UnixFileMode mode)
        {
            tarEntry.Mode = mode;
        }

        await _tar.WriteEntryAsync(tarEntry, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task WriteFileAsync(ScanEntry entry, Stream data, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(data);

        PaxTarEntry tarEntry = new(TarEntryType.RegularFile, entry.ArchivePath)
        {
            ModificationTime = entry.LastWriteTime,
            DataStream = data,
        };

        if (UnixPermissions.Read(entry.SourcePath) is UnixFileMode mode)
        {
            tarEntry.Mode = mode;
        }

        await _tar.WriteEntryAsync(tarEntry, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Inside-out: tar's end-of-archive marker has to reach the compressor before the compressor
        // flushes its own epilogue to the stream below it.
        await _tar.DisposeAsync().ConfigureAwait(false);
        await _compressor.DisposeAsync().ConfigureAwait(false);
    }
}
