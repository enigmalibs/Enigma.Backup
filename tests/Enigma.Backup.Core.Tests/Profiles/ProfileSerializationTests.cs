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
    [InlineData("""{ "formatVersion": 2, "name": "FromTheFuture" }""")]
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
}
