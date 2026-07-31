using System.Text.Json.Serialization;

namespace Enigma.Backup.Core.Profiles;

/// <summary>The compression applied to the tar stream of a backup.</summary>
/// <remarks>Serialized by name, so profile files stay readable and survive reordering.</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<CompressionAlgorithm>))]
public enum CompressionAlgorithm
{
    /// <summary>Zstandard — the default: markedly faster than gzip at a better ratio.</summary>
    Zstd,

    /// <summary>Gzip — slower and larger, but readable by every stock tool on both platforms.</summary>
    Gzip,
}
