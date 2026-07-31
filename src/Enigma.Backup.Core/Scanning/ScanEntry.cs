using System;

namespace Enigma.Backup.Core.Scanning;

/// <summary>One file or directory the scan decided to archive.</summary>
/// <param name="Kind">Whether this is a file or a directory.</param>
/// <param name="SourcePath">The absolute path to read from.</param>
/// <param name="ArchivePath">
/// The path stored in the archive: always relative, always with <c>/</c> separators, and always
/// beginning with the top-level name of the input it was found under.
/// </param>
/// <param name="Length">The file's size in bytes; zero for a directory.</param>
/// <param name="LastWriteTime">The last write timestamp, in UTC, recorded in the archive entry.</param>
public sealed record ScanEntry(
    ScanEntryKind Kind,
    string SourcePath,
    string ArchivePath,
    long Length,
    DateTimeOffset LastWriteTime);
