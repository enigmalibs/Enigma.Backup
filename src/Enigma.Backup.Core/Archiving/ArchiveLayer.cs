namespace Enigma.Backup.Core.Archiving;

/// <summary>The outermost layer of an archive file, as identified by its leading bytes.</summary>
internal enum ArchiveLayer
{
    /// <summary>A gzip member: the tar stream is compressed with gzip and not encrypted.</summary>
    Gzip,

    /// <summary>A Zstandard frame: the tar stream is compressed with zstd and not encrypted.</summary>
    Zstd,

    /// <summary>An <c>EBK1</c> container: the compressed tar stream is encrypted (PHASE04).</summary>
    Ebk1,
}
