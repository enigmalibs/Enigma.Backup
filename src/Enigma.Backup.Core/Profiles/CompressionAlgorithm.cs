using System.Text.Json.Serialization;

namespace Enigma.Backup.Core.Profiles;

/// <summary>The compression applied to the tar stream of a backup.</summary>
/// <remarks>
/// <para>Serialized by name, so profile files stay readable and survive reordering.</para>
/// <para>
/// <see cref="Zip"/> is the documented impurity here: it is an archive container of its own rather
/// than a codec wrapped around a tar stream. It lives in this enum because the choice a user makes is
/// "what kind of file do I get", which is one control — see <c>docs/plan/FEATURE-7F99.md</c>.
/// </para>
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<CompressionAlgorithm>))]
public enum CompressionAlgorithm
{
    /// <summary>Zstandard — the default: markedly faster than gzip at a better ratio.</summary>
    Zstd,

    /// <summary>Gzip — slower and larger, but readable by every stock tool on both platforms.</summary>
    Gzip,

    /// <summary>
    /// A plain Deflate <c>.zip</c>, which every file manager opens without this application. Never
    /// encrypted: <c>System.IO.Compression</c> cannot, and every way of making it so would either
    /// stop Explorer opening the file or offer security it does not have.
    /// </summary>
    Zip,
}
