using System;
using System.Text.Json;

namespace Enigma.Backup.Core.Profiles;

/// <summary>
/// Reads and writes the profile JSON. The <c>formatVersion</c> guard runs against the raw document
/// rather than the deserialized object, so a file that simply omits the field is rejected instead of
/// silently inheriting the current version from <see cref="BackupProfile"/>'s property initializer.
/// </summary>
internal static class ProfileSerializer
{
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
    /// <param name="profile">The profile to write.</param>
    /// <returns>The JSON text, indented and UTF-8 friendly.</returns>
    /// <exception cref="ProfileFormatException">The profile carries a format version this build does not write.</exception>
    public static string Serialize(BackupProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.FormatVersion != BackupProfile.CurrentFormatVersion)
        {
            throw new ProfileFormatException(
                $"Profile '{profile.Name}' declares format version {profile.FormatVersion}; " +
                $"this build writes version {BackupProfile.CurrentFormatVersion} only.");
        }

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

            int? formatVersion = ReadFormatVersion(document.RootElement);
            if (formatVersion is null)
            {
                throw new ProfileFormatException($"'{source}' does not declare a valid 'formatVersion'.");
            }

            if (formatVersion != BackupProfile.CurrentFormatVersion)
            {
                throw new ProfileFormatException(
                    $"'{source}' declares profile format version {formatVersion}; " +
                    $"this build reads version {BackupProfile.CurrentFormatVersion} only.");
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

        return profile;
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
