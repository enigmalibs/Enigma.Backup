using System.Collections.Generic;

namespace Enigma.Backup.Core.Profiles;

/// <summary>
/// A reusable backup definition: what to pack, where to put it, and how. Persisted as JSON by
/// <see cref="IProfileStore"/>; the password is never part of a profile.
/// </summary>
public sealed class BackupProfile
{
    /// <summary>The profile format this build reads and writes.</summary>
    public const int CurrentFormatVersion = 1;

    /// <summary>
    /// Version of the JSON layout. A file declaring anything other than
    /// <see cref="CurrentFormatVersion"/> is rejected rather than guessed at.
    /// </summary>
    public int FormatVersion { get; set; } = CurrentFormatVersion;

    /// <summary>
    /// The profile name, which is also its file name in the store and the prefix of every archive
    /// it produces. Must satisfy <see cref="ProfileName.IsValid(string?)"/>.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The files and directories to back up, as absolute paths.</summary>
    public IList<string> Inputs { get; set; } = [];

    /// <summary>The directory the timestamped archive and its <c>.sha256</c> sidecar are written to.</summary>
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>The compression applied to the tar stream.</summary>
    public CompressionAlgorithm Compression { get; set; } = CompressionAlgorithm.Zstd;

    /// <summary>Whether the archive is encrypted; the password is prompted per run, never stored.</summary>
    public bool Encrypt { get; set; }

    /// <summary>
    /// Glob patterns whose matches are left out of the scan, e.g. <c>**/bin/**</c> or <c>*.log</c>.
    /// Matched against each entry's path relative to the input it was found under.
    /// </summary>
    public IList<string> ExcludeGlobs { get; set; } = [];
}
