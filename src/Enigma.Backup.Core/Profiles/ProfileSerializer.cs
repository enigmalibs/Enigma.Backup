using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Enigma.Backup.Core.Profiles;

/// <summary>
/// Reads and writes the profile JSON. The <c>formatVersion</c> guard runs against the raw document
/// rather than the deserialized object, so a file that simply omits the field is rejected instead of
/// silently inheriting the current version from <see cref="BackupProfile"/>'s property initializer.
/// </summary>
/// <remarks>
/// A profile is written as the *lowest* version that can express it: version 2 only once it uses
/// something version 1 has no field for. That is what keeps the promise made for every profile
/// already on disk — one that uses no newer feature is still written as version 1 and still opens in
/// the builds that only read version 1, while one that does use a newer feature is refused by them
/// loudly rather than read as less than it says.
/// </remarks>
internal static class ProfileSerializer
{
    /// <summary>The format version that introduced the fields version 1 has no place for.</summary>
    private const int ExtendedFormatVersion = 2;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Renders <paramref name="profile"/> as the JSON stored on disk.</summary>
    /// <param name="profile">
    /// The profile to write. Its <see cref="BackupProfile.FormatVersion"/> is set to the version that
    /// was written, so the object and the file agree afterwards.
    /// </param>
    /// <returns>The JSON text, indented and UTF-8 friendly.</returns>
    /// <exception cref="ProfileFormatException">The profile carries a format version this build does not write.</exception>
    public static string Serialize(BackupProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.FormatVersion is < BackupProfile.MinimumFormatVersion or > BackupProfile.CurrentFormatVersion)
        {
            throw new ProfileFormatException(
                $"Profile '{profile.Name}' declares format version {profile.FormatVersion}; this build writes " +
                $"versions {BackupProfile.MinimumFormatVersion} to {BackupProfile.CurrentFormatVersion} only.");
        }

        profile.FormatVersion = RequiredFormatVersion(profile);

        return JsonSerializer.Serialize(profile, SerializerOptions);
    }

    /// <summary>Parses profile JSON, rejecting anything this build cannot read.</summary>
    /// <param name="json">The JSON text.</param>
    /// <param name="source">The file the text came from, used in error messages.</param>
    /// <returns>The parsed profile, with null collections normalized away.</returns>
    /// <exception cref="ProfileFormatException">The text is not a profile of the current format version.</exception>
    public static BackupProfile Deserialize(string json, string source)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, DocumentOptions);
        }
        catch (JsonException exception)
        {
            throw new ProfileFormatException($"'{source}' is not a valid profile file: {exception.Message}", exception);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ProfileFormatException($"'{source}' is not a valid profile file: the root is not a JSON object.");
            }

            if (ReadFormatVersion(document.RootElement) is not int formatVersion)
            {
                throw new ProfileFormatException($"'{source}' does not declare a valid 'formatVersion'.");
            }

            if (formatVersion is < BackupProfile.MinimumFormatVersion or > BackupProfile.CurrentFormatVersion)
            {
                throw new ProfileFormatException(
                    $"'{source}' declares profile format version {formatVersion}; this build reads " +
                    $"versions {BackupProfile.MinimumFormatVersion} to {BackupProfile.CurrentFormatVersion} only.");
            }

            BackupProfile? profile;
            try
            {
                profile = document.Deserialize<BackupProfile>(SerializerOptions);
            }
            catch (JsonException exception)
            {
                throw new ProfileFormatException($"'{source}' is not a valid profile file: {exception.Message}", exception);
            }

            if (profile is null)
            {
                throw new ProfileFormatException($"'{source}' is not a valid profile file: it contains no profile.");
            }

            return Normalize(profile);
        }
    }

    /// <summary>
    /// The lowest format version that can express <paramref name="profile"/>: version 2 only once it
    /// carries something version 1 has no field for, or names a value version 1 never knew.
    /// </summary>
    /// <remarks>
    /// <c>Zip</c> is a version-2 value of a version-1 field: the builds that read version 1 only would
    /// throw on the unknown enum name anyway, and saying 2 makes them refuse the file with the format
    /// message instead of a JSON one.
    /// </remarks>
    private static int RequiredFormatVersion(BackupProfile profile) =>
        profile.PatternInputs is { Count: > 0 } || profile.Compression == CompressionAlgorithm.Zip
            ? ExtendedFormatVersion
            : BackupProfile.MinimumFormatVersion;

    /// <summary>Replaces the nulls a hand-edited file can smuggle past the non-nullable properties.</summary>
    private static BackupProfile Normalize(BackupProfile profile)
    {
        if (profile.Name is null)
        {
            profile.Name = string.Empty;
        }

        if (profile.OutputDirectory is null)
        {
            profile.OutputDirectory = string.Empty;
        }

        if (profile.Inputs is null)
        {
            profile.Inputs = [];
        }

        if (profile.ExcludeGlobs is null)
        {
            profile.ExcludeGlobs = [];
        }

        profile.PatternInputs = NormalizePatternInputs(profile.PatternInputs);

        return profile;
    }

    /// <summary>
    /// Rebuilds the pattern list without the nulls a hand-edited file can put in it. A blank base
    /// folder or pattern is kept rather than dropped: the scan reports it, where dropping it would
    /// silently back up less than the profile says.
    /// </summary>
    private static List<PatternInput> NormalizePatternInputs(IList<PatternInput>? patternInputs)
    {
        List<PatternInput> normalized = [];

        if (patternInputs is null)
        {
            return normalized;
        }

        foreach (PatternInput? patternInput in patternInputs)
        {
            if (patternInput is null)
            {
                continue;
            }

            normalized.Add(patternInput.BaseDirectory is null || patternInput.Pattern is null
                ? new PatternInput(patternInput.BaseDirectory ?? string.Empty, patternInput.Pattern ?? string.Empty)
                : patternInput);
        }

        return normalized;
    }

    /// <summary>
    /// Finds <c>formatVersion</c> in the raw document, accepting any casing so a hand-written file
    /// is not rejected over a capital F.
    /// </summary>
    private static int? ReadFormatVersion(JsonElement root)
    {
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (!string.Equals(property.Name, "formatVersion", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out int version)
                ? version
                : null;
        }

        return null;
    }
}
