using System.Collections.Generic;
using System.Linq;
using Enigma.Backup.Core.Profiles;

namespace Enigma.Backup.Desktop.ViewModels;

/// <summary>One entry of the compression drop-down.</summary>
/// <param name="Value">The algorithm this entry selects.</param>
/// <param name="Label">Its name, with the extension it produces.</param>
/// <param name="Description">The one-line trade-off, shown beside the drop-down.</param>
public sealed record CompressionOption(CompressionAlgorithm Value, string Label, string Description)
{
    /// <summary>Every algorithm the app offers, in the order they are shown.</summary>
    public static IReadOnlyList<CompressionOption> All { get; } =
    [
        new CompressionOption(
            CompressionAlgorithm.Zstd,
            "Zstandard (.tar.zst)",
            "Faster, and smaller than gzip. The default."),
        new CompressionOption(
            CompressionAlgorithm.Gzip,
            "Gzip (.tar.gz)",
            "Slower and larger, but every stock tool on both platforms opens it."),
    ];

    /// <summary>Finds the entry for an algorithm, falling back to the first when it is not one this build offers.</summary>
    /// <param name="value">The algorithm to look up.</param>
    public static CompressionOption For(CompressionAlgorithm value) =>
        All.FirstOrDefault(option => option.Value == value) ?? All[0];
}
