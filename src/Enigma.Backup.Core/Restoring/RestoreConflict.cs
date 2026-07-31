using System;

namespace Enigma.Backup.Core.Restoring;

/// <summary>
/// A file the restore is about to write that already exists, with both sides described so the user
/// can tell which one they want to keep.
/// </summary>
/// <param name="ArchivePath">The entry's path inside the archive.</param>
/// <param name="DestinationPath">The existing file the entry would replace.</param>
/// <param name="ArchiveLength">The entry's size in bytes.</param>
/// <param name="ArchiveLastWriteTime">The entry's last write timestamp, as recorded in the archive.</param>
/// <param name="ExistingLength">The existing file's size in bytes.</param>
/// <param name="ExistingLastWriteTime">The existing file's last write timestamp, in UTC.</param>
public sealed record RestoreConflict(
    string ArchivePath,
    string DestinationPath,
    long ArchiveLength,
    DateTimeOffset ArchiveLastWriteTime,
    long ExistingLength,
    DateTimeOffset ExistingLastWriteTime);
