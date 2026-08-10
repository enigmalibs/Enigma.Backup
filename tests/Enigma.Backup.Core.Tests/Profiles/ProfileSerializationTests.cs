using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Core.Tests.TestSupport;
using Xunit;

namespace Enigma.Backup.Core.Tests.Profiles;

/// <summary>
/// Covers the on-disk JSON shape and the <c>formatVersion</c> guard, exercised through the store's
/// export/import pair because that is how a profile file reaches the application from outside.
/// </summary>
public sealed class ProfileSerializationTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ExportedJson_UsesCamelCaseKeysAndNamedEnumValues()
    {
        using TempDirectory temp = new();
        ProfileStore store = new(Path.Combine(temp.FullPath, "store"));
        await store.SaveAsync(
            new BackupProfile
            {
                Name = "Documents",
                Inputs = ["/data"],
                OutputDirectory = "/backups",
                Compression = CompressionAlgorithm.Zstd,
                Encrypt = true,
                ExcludeGlobs = ["*.tmp"],
            },
            Token);

        string destination = Path.Combine(temp.FullPath, "exported.json");
        await store.ExportAsync("Documents", destination, Token);
        string json = File.ReadAllText(destination);

        Assert.Contains("\"formatVersion\": 1", json, StringComparison.Ordinal);
        Assert.Contains("\"outputDirectory\": \"/backups\"", json, StringComparison.Ordinal);
        Assert.Contains("\"compression\": \"Zstd\"", json, StringComparison.Ordinal);
        Assert.Contains("\"excludeGlobs\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
    }

    /// <remarks>
    /// This is what the release notes' compatibility claim rests on: a profile that uses nothing newer
    /// is still written as version 1, so the builds that only read version 1 still open it. It is a
    /// deliberate rule, not a side effect, which is why it has a test of its own.
    /// </remarks>
    [Fact]
    public async Task SavingAProfileThatUsesNoNewerFeature_StillWritesFormatVersion1()
    {
        string json = await ExportedJsonAsync(new BackupProfile
        {
            Name = "Documents",
            Inputs = ["/data"],
            OutputDirectory = "/backups",
            ExcludeGlobs = ["*.tmp"],
        });

        Assert.Contains("\"formatVersion\": 1", json, StringComparison.Ordinal);
        Assert.Contains("\"patternInputs\": []", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SavingAProfileWithAPatternInput_WritesFormatVersion2()
    {
        string json = await ExportedJsonAsync(new BackupProfile
        {
            Name = "Logs",
            OutputDirectory = "/backups",
            PatternInputs = [new PatternInput("/var/log", "*.log")],
        });

        Assert.Contains("\"formatVersion\": 2", json, StringComparison.Ordinal);
        Assert.Contains("\"baseDirectory\": \"/var/log\"", json, StringComparison.Ordinal);
        Assert.Contains("\"pattern\": \"*.log\"", json, StringComparison.Ordinal);
    }

    /// <remarks>
    /// Zip is a version-2 <em>value</em> of a version-1 field. Saying 2 is what makes an older build
    /// refuse the profile with its format message instead of an unknown-enum-name JSON error.
    /// </remarks>
    [Fact]
    public async Task SavingAProfileThatAsksForZip_WritesFormatVersion2()
    {
        string json = await ExportedJsonAsync(new BackupProfile
        {
            Name = "Shared",
            Inputs = ["/data"],
            OutputDirectory = "/backups",
            Compression = CompressionAlgorithm.Zip,
        });

        Assert.Contains("\"formatVersion\": 2", json, StringComparison.Ordinal);
        Assert.Contains("\"compression\": \"Zip\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_ReadsAVersion2ProfileWithItsPatterns()
    {
        BackupProfile profile = await ImportJsonAsync(
            """
            {
              "formatVersion": 2,
              "name": "Logs",
              "inputs": [ "/data" ],
              "patternInputs": [ { "baseDirectory": "/var/log", "pattern": "*.log" } ]
            }
            """);

        Assert.Equal("/data", Assert.Single(profile.Inputs));
        PatternInput patternInput = Assert.Single(profile.PatternInputs);
        Assert.Equal("/var/log", patternInput.BaseDirectory);
        Assert.Equal("*.log", patternInput.Pattern);
    }

    [Fact]
    public async Task ImportAsync_TreatsAMissingOrNullPatternListAsEmpty()
    {
        Assert.Empty((await ImportJsonAsync("""{ "formatVersion": 2, "name": "None" }""")).PatternInputs);
        Assert.Empty((await ImportJsonAsync("""{ "formatVersion": 2, "name": "Null", "patternInputs": null }""")).PatternInputs);
        Assert.Empty((await ImportJsonAsync("""{ "formatVersion": 2, "name": "Hole", "patternInputs": [ null ] }""")).PatternInputs);
    }

    [Fact]
    public async Task ImportAsync_TreatsAPatternsMissingFieldsAsBlankRatherThanRefusingTheFile()
    {
        // Blank rather than dropped: the scan reports a pattern it cannot resolve, and a profile that
        // quietly backs up less than it says is the worse of the two failures.
        BackupProfile profile = await ImportJsonAsync(
            """{ "formatVersion": 2, "name": "Partial", "patternInputs": [ { "pattern": null } ] }""");

        PatternInput patternInput = Assert.Single(profile.PatternInputs);
        Assert.Equal(string.Empty, patternInput.BaseDirectory);
        Assert.Equal(string.Empty, patternInput.Pattern);
    }

    /// <remarks>
    /// A hand-edited file can carry a version-2 field under a version-1 header. Reading it is the
    /// lenient half — the fields are there, and refusing them would lose data the user asked for — and
    /// the write rule is the strict half: the next save states version 2, which is what the profile
    /// actually is.
    /// </remarks>
    [Fact]
    public async Task AVersion1FileCarryingAPattern_IsReadAndSavedBackAsVersion2()
    {
        using TempDirectory temp = new();
        ProfileStore store = new(Path.Combine(temp.FullPath, "store"));
        string incoming = temp.WriteFile(
            "incoming.json",
            """
            {
              "formatVersion": 1,
              "name": "HandEdited",
              "patternInputs": [ { "baseDirectory": "/var/log", "pattern": "*.log" } ]
            }
            """);

        BackupProfile imported = await store.ImportAsync(incoming, Token);

        Assert.Single(imported.PatternInputs);
        Assert.Equal(2, imported.FormatVersion);

        string stored = File.ReadAllText(Path.Combine(store.DirectoryPath, "HandEdited.json"));
        Assert.Contains("\"formatVersion\": 2", stored, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_AppliesTheDocumentedDefaultsForOmittedFields()
    {
        BackupProfile profile = await ImportJsonAsync("""{ "formatVersion": 1, "name": "Minimal" }""");

        Assert.Equal(CompressionAlgorithm.Zstd, profile.Compression);
        Assert.False(profile.Encrypt);
        Assert.Empty(profile.Inputs);
        Assert.Empty(profile.ExcludeGlobs);
        Assert.Equal(string.Empty, profile.OutputDirectory);
    }

    [Fact]
    public async Task ImportAsync_TreatsExplicitNullCollectionsAsEmpty()
    {
        BackupProfile profile = await ImportJsonAsync(
            """{ "formatVersion": 1, "name": "Nulls", "inputs": null, "excludeGlobs": null, "outputDirectory": null }""");

        Assert.Empty(profile.Inputs);
        Assert.Empty(profile.ExcludeGlobs);
        Assert.Equal(string.Empty, profile.OutputDirectory);
    }

    [Fact]
    public async Task ImportAsync_IgnoresPropertiesItDoesNotKnow()
    {
        BackupProfile profile = await ImportJsonAsync(
            """{ "formatVersion": 1, "name": "Extra", "somethingNew": { "a": 1 } }""");

        Assert.Equal("Extra", profile.Name);
    }

    [Fact]
    public async Task ImportAsync_AcceptsAHandWrittenFormatVersionKeyOfAnyCasing()
    {
        BackupProfile profile = await ImportJsonAsync("""{ "FormatVersion": 1, "name": "HandWritten" }""");

        Assert.Equal("HandWritten", profile.Name);
    }

    [Fact]
    public async Task ImportAsync_AcceptsAnEnumValueOfAnyCasing()
    {
        BackupProfile profile = await ImportJsonAsync("""{ "formatVersion": 1, "name": "Lower", "compression": "gzip" }""");

        Assert.Equal(CompressionAlgorithm.Gzip, profile.Compression);
    }

    [Theory]
    [InlineData("""{ "name": "NoVersion" }""")]
    [InlineData("""{ "formatVersion": 3, "name": "FromTheFuture" }""")]
    [InlineData("""{ "formatVersion": 0, "name": "Zero" }""")]
    [InlineData("""{ "formatVersion": "1", "name": "NotANumber" }""")]
    [InlineData("""[ { "formatVersion": 1 } ]""")]
    [InlineData("not json at all")]
    [InlineData("")]
    public async Task ImportAsync_RejectsWhatItCannotRead(string json)
    {
        await Assert.ThrowsAsync<ProfileFormatException>(() => ImportJsonAsync(json));
    }

    [Fact]
    public async Task SaveAsync_RefusesToWriteAFormatVersionItDoesNotOwn()
    {
        using TempDirectory temp = new();
        ProfileStore store = new(temp.FullPath);

        await Assert.ThrowsAsync<ProfileFormatException>(
            () => store.SaveAsync(new BackupProfile { Name = "Future", FormatVersion = 99 }, Token));
    }

    private static async Task<BackupProfile> ImportJsonAsync(string json)
    {
        using TempDirectory temp = new();
        ProfileStore store = new(Path.Combine(temp.FullPath, "store"));
        string path = temp.WriteFile("incoming.json", json);

        return await store.ImportAsync(path, Token);
    }

    /// <summary>Saves a profile and hands back the JSON the store wrote for it.</summary>
    private static async Task<string> ExportedJsonAsync(BackupProfile profile)
    {
        using TempDirectory temp = new();
        ProfileStore store = new(Path.Combine(temp.FullPath, "store"));
        await store.SaveAsync(profile, Token);

        string destination = Path.Combine(temp.FullPath, "exported.json");
        await store.ExportAsync(profile.Name, destination, Token);

        return File.ReadAllText(destination);
    }
}
