namespace Enigma.Backup.Core.Scanning;

/// <summary>What a <see cref="ScanEntry"/> stands for.</summary>
public enum ScanEntryKind
{
    /// <summary>A directory. Recorded in its own right so empty directories survive a round trip.</summary>
    Directory,

    /// <summary>A file, whose bytes are copied into the archive.</summary>
    File,
}
