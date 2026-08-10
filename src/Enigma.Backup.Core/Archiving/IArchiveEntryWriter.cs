using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Scanning;

namespace Enigma.Backup.Core.Archiving;

/// <summary>
/// Writes the entries a scan produced into whatever kind of archive the profile asked for.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CompressionStreams"/> models stream codecs, and a zip is not one: it is an archive
/// container with its own entry table, so it cannot be a compressor wrapped around a tar stream. This
/// is the seam that difference needs — <c>BackupService</c> decides what to write once, and the rest of
/// the run (opening sources, the error handler, progress, the shrank/grew warnings) is the same code
/// either way.
/// </para>
/// <para>
/// Every implementation writes to a stream it does not own and closes its own layers inside-out on
/// disposal, so the digest below still covers the finished file.
/// </para>
/// </remarks>
internal interface IArchiveEntryWriter : IAsyncDisposable
{
    /// <summary>Writes a directory entry.</summary>
    /// <param name="entry">The directory the scan found.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task WriteDirectoryAsync(ScanEntry entry, CancellationToken cancellationToken);

    /// <summary>Writes a file entry, with its data.</summary>
    /// <param name="entry">The file the scan found.</param>
    /// <param name="data">
    /// The file's contents, which deliver exactly the length the entry was opened with; read to the end.
    /// </param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task WriteFileAsync(ScanEntry entry, Stream data, CancellationToken cancellationToken);
}
