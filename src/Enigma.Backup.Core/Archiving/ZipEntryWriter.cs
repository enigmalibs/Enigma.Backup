using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Scanning;

namespace Enigma.Backup.Core.Archiving;

/// <summary>
/// The <see cref="IArchiveEntryWriter"/> that produces a plain Deflate <c>.zip</c> — the format every
/// file manager opens without this application.
/// </summary>
/// <remarks>
/// <para>
/// The pipeline shape is unchanged: <c>FileStream → HashingWriteStream → ZipArchive</c>, one pass, no
/// temp file. The hashing stream is not seekable, which is what makes this work — <see cref="ZipArchive"/>
/// then writes each entry's sizes in a trailing data descriptor instead of seeking back to patch the
/// local header, so no byte the digest has already covered is ever rewritten.
/// </para>
/// <para>
/// The one cost is memory: the central directory's per-entry records are held until disposal, roughly a
/// hundred bytes per entry. That is linear in the number of entries, not in the size of the archive, so
/// the constant-memory promise survives in spirit — a million-file backup costs tens of megabytes.
/// </para>
/// <para>
/// Zip records less than tar does, and the difference is not compensated: timestamps are a DOS date with
/// a two-second resolution, no time zone and a hard 1980–2107 range, and Unix modes have nowhere to go at
/// all. A timestamp outside the range is clamped rather than refused — a forged far-future date on one
/// file must not stop a backup halfway through writing it.
/// </para>
/// </remarks>
internal sealed class ZipEntryWriter : IArchiveEntryWriter
{
    /// <summary>The earliest wall-clock time a DOS timestamp can hold.</summary>
    private static readonly DateTime EarliestDosTime = new(1980, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    /// <summary>The latest wall-clock time a DOS timestamp can hold.</summary>
    private static readonly DateTime LatestDosTime = new(2107, 12, 31, 23, 59, 58, DateTimeKind.Unspecified);

    private readonly ZipArchive _zip;

    /// <summary>Creates a zip archive over <paramref name="destination"/>.</summary>
    /// <param name="destination">The stream the archive is written to; not disposed here.</param>
    public ZipEntryWriter(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        _zip = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
    }

    /// <inheritdoc />
    public Task WriteDirectoryAsync(ScanEntry entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Trailing slash, no data, stored rather than deflated: that is a folder to every unzip tool,
        // and compressing nothing would only cost bytes.
        ZipArchiveEntry zipEntry = _zip.CreateEntry(entry.ArchivePath + '/', CompressionLevel.NoCompression);
        zipEntry.LastWriteTime = ToZipTime(entry.LastWriteTime);

        // Opened and closed at once, because an entry that is never opened is only written when the
        // archive is disposed — which would move every directory's header past the data of the files
        // that came after it.
        zipEntry.Open().Dispose();

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task WriteFileAsync(ScanEntry entry, Stream data, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(data);

        // Optimal, matching what gzip is written with: the point of choosing zip is that anything can
        // open it, so paying for the ratio is the right trade.
        ZipArchiveEntry zipEntry = _zip.CreateEntry(entry.ArchivePath, CompressionLevel.Optimal);
        zipEntry.LastWriteTime = ToZipTime(entry.LastWriteTime);

        await using Stream target = zipEntry.Open();

        await data.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        // Synchronous by necessity: this is what writes the central directory, and ZipArchive has no
        // asynchronous disposal. The stream below it is a hashing stream over a file, so the write is
        // a memcpy and a buffered file write, not a network round trip.
        _zip.Dispose();

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// The instant as a zip records it: the local wall-clock time, clamped to what a DOS date can hold.
    /// </summary>
    /// <param name="timestamp">The UTC timestamp the scan read off the file.</param>
    /// <remarks>
    /// A DOS timestamp carries no time zone, and every tool — Explorer included — reads one as local
    /// time, which is also how <see cref="ZipArchiveEntry.LastWriteTime"/> hands it back on the way in.
    /// So local is what goes in, or a zip made here would show two hours out in the file manager it
    /// exists for. The clamp is on that same wall clock: west of UTC, 1980-01-01Z is still 1979, and
    /// assigning it would throw — and one file's recorded time is not worth failing a backup over.
    /// </remarks>
    private static DateTimeOffset ToZipTime(DateTimeOffset timestamp)
    {
        DateTimeOffset local = timestamp.ToLocalTime();

        if (local.DateTime < EarliestDosTime)
        {
            return new DateTimeOffset(EarliestDosTime, local.Offset);
        }

        return local.DateTime > LatestDosTime ? new DateTimeOffset(LatestDosTime, local.Offset) : local;
    }
}
