using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Enigma.Backup.Desktop.Settings;

/// <summary>
/// The file-backed <see cref="IAppSettingsStore"/>: one <c>settings.json</c> in the per-user
/// configuration folder, beside the <c>profiles</c> directory the core writes.
/// </summary>
/// <remarks>
/// <para>
/// The <c>formatVersion</c> guard runs against the raw document rather than the deserialized object,
/// exactly as <c>ProfileSerializer</c>'s does, so a file that simply omits the field is rejected
/// instead of silently inheriting the current version from <see cref="AppSettings"/>'s property
/// initializer.
/// </para>
/// <para>
/// Where a profile that cannot be read is an error the user is shown, a preference that cannot be
/// read is not: every failure here ends in the defaults and a log line. Losing a theme choice must
/// never be able to stop the app from opening.
/// </para>
/// </remarks>
public sealed class AppSettingsStore : IAppSettingsStore
{
    private const string TempExtension = ".tmp";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,

        // The theme is written as its name, not its ordinal: this file is meant to be readable, and
        // hand-editing it to "Dark" should work. An unrecognised name is a JsonException, which is
        // caught like any other malformed file — the plain answer to a value this build cannot read.
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly ILogger<AppSettingsStore> _logger;

    /// <summary>Creates a store over the per-user configuration folder.</summary>
    /// <param name="logger">Records the files that could not be read or written.</param>
    public AppSettingsStore(ILogger<AppSettingsStore> logger)
        : this(DefaultPath, logger)
    {
    }

    /// <summary>Creates a store over an explicit file — how tests point it somewhere harmless.</summary>
    /// <param name="path">The file the preferences are read from and written to.</param>
    /// <param name="logger">Records the files that could not be read or written.</param>
    public AppSettingsStore(string path, ILogger<AppSettingsStore> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(logger);

        FilePath = Path.GetFullPath(path);
        _logger = logger;
    }

    /// <summary>
    /// The per-user settings file: <c>%APPDATA%\Enigma.Backup\settings.json</c> on Windows,
    /// <c>~/.config/Enigma.Backup/settings.json</c> on Linux.
    /// </summary>
    public static string DefaultPath { get; } = ResolveDefaultPath();

    /// <inheritdoc />
    public string FilePath { get; }

    /// <inheritdoc />
    public AppSettings Load()
    {
        if (!File.Exists(FilePath))
        {
            // The normal case until the first preference is changed: nothing to read, nothing wrong.
            _logger.LogDebug("There is no settings file at {Path}; the defaults apply.", FilePath);

            return new AppSettings();
        }

        try
        {
            return Parse(File.ReadAllText(FilePath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(exception, "The settings file {Path} could not be read; the defaults apply.", FilePath);

            return new AppSettings();
        }
    }

    /// <inheritdoc />
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string temporaryPath = FilePath + TempExtension;

        try
        {
            string? parent = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }

            // Written beside the target and then moved over it, so an interrupted save leaves the
            // previous choice intact rather than a half-written file the next start has to reject.
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, SerializerOptions));
            File.Move(temporaryPath, FilePath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The choice still applies to the running app; it just will not survive a restart. That
            // is not worth a dialog, and it is certainly not worth taking the app down for.
            _logger.LogWarning(exception, "The settings file {Path} could not be written.", FilePath);

            TryDelete(temporaryPath);
        }
    }

    private static string ResolveDefaultPath()
    {
        // ApplicationData is %APPDATA% on Windows and $XDG_CONFIG_HOME (or ~/.config) on Linux — the
        // same root ProfileStore resolves, which is what puts settings.json beside profiles/.
        string configurationRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (configurationRoot.Length == 0)
        {
            configurationRoot = Path.Combine(AppContext.BaseDirectory, "config");
        }

        return Path.Combine(configurationRoot, "Enigma.Backup", "settings.json");
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // The half-written temporary file is not worth a second log line.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Parses the settings JSON, falling back to the defaults for anything this build cannot read.</summary>
    /// <param name="json">The file's text.</param>
    /// <exception cref="JsonException">The text is not JSON, or a value is not one this build knows.</exception>
    private AppSettings Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json, DocumentOptions);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            _logger.LogWarning("The settings file {Path} is not a JSON object; the defaults apply.", FilePath);

            return new AppSettings();
        }

        int? formatVersion = ReadFormatVersion(document.RootElement);
        if (formatVersion != AppSettings.CurrentFormatVersion)
        {
            _logger.LogWarning(
                "The settings file {Path} declares format version {DeclaredVersion}; this build reads version {Version} only, so the defaults apply.",
                FilePath,
                formatVersion,
                AppSettings.CurrentFormatVersion);

            return new AppSettings();
        }

        return document.Deserialize<AppSettings>(SerializerOptions) ?? new AppSettings();
    }

    /// <summary>
    /// Finds <c>formatVersion</c> in the raw document, accepting any casing so a hand-written file is
    /// not rejected over a capital F.
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
