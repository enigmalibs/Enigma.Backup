using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Core.Tests.TestSupport;
using Xunit;

namespace Enigma.Backup.Core.Tests.Profiles;

public sealed class ProfileStoreTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void DefaultDirectory_IsUnderThePerUserConfigurationFolder()
    {
        string expectedTail = Path.Combine("Enigma.Backup", "profiles");

        Assert.EndsWith(expectedTail, ProfileStore.DefaultDirectory, StringComparison.Ordinal);
        Assert.True(Path.IsPathRooted(ProfileStore.DefaultDirectory));
    }

    [Fact]
    public async Task ListAsync_ReturnsNothingWhenTheStoreHasNeverBeenWrittenTo()
    {
        using TempDirectory temp = new();
        ProfileStore store = new(Path.Combine(temp.FullPath, "never-created"));

        Assert.Empty(await store.ListAsync(Token));
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsEveryField()
    {
        using TempDirectory temp = new();
        ProfileStore store = new(temp.FullPath);
        BackupProfile saved = SampleProfile();

        await store.SaveAsync(saved, Token);
        BackupProfile loaded = await store.LoadAsync(saved.Name, Token);

        // This profile uses nothing newer than version 1, so that is what it is written as — see
        // ProfileSerializationTests for the rule and what makes it matter.
        Assert.Equal(BackupProfile.MinimumFormatVersion, loaded.FormatVersion);
        Assert.Equal(saved.Name, loaded.Name);
        Assert.Equal(saved.Inputs, loaded.Inputs);
        Assert.Empty(loaded.PatternInputs);
        Assert.Equal(saved.OutputDirectory, loaded.OutputDirectory);
        Assert.Equal(saved.Compression, loaded.Compression);
        Assert.Equal(saved.Encrypt, loaded.Encrypt);
        Assert.Equal(saved.ExcludeGlobs, loaded.ExcludeGlobs);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsPatternInputsAsVersion2()
    {
        using TempDirectory temp = new();
        ProfileStore store = new(temp.FullPath);
        BackupProfile saved = SampleProfile();
        saved.PatternInputs = [new PatternInput("/var/log", "*.log"), new PatternInput("/data", "DirPrefix*")];

        await store.SaveAsync(saved, Token);
        BackupProfile loaded = await store.LoadAsync(saved.Name, Token);

        Assert.Equal(BackupProfile.CurrentFormatVersion, loaded.FormatVersion);
        Assert.Equal(saved.Inputs, loaded.Inputs);
        Assert.Equal(saved.PatternInputs, loaded.PatternInputs);
    }

    [Fact]
    public async Task SaveAsync_WritesOneFileNamedAfterTheProfileAndLeavesNoTemporaryBehind()
    {
        using TempDirectory temp = new();
        ProfileStore store = new(temp.FullPath);

        await store.SaveAsync(SampleProfile("Documents"), Token);

        Assert.Equal(
            new[] { Path.Combine(temp.FullPath, "Documents.json") },
            Directory.GetFiles(temp.FullPath));
    }

    [Fact]
    public async Task SaveAsync_ReplacesAProfileOfTheSameName()
    {
        using TempDirectory temp = new();
        ProfileStore store = new(temp.FullPath);

        await store.SaveAsync(SampleProfile(), Token);
        BackupProfile changed = SampleProfile();
        changed.Encrypt = false;
        changed.Inputs = ["/only/one"];
        await store.SaveAsync(changed, Token);

        BackupProfile loaded = await store.LoadAsync(changed.Name, Token);
        Assert.False(loaded.Encrypt);
        Assert.Equal(new[] { "/only/one" }, loaded.Inputs);
        Assert.Single(await store.ListAsync(Token));
    }

    [Fact]
    public async Task ListAsync_ReturnsEveryProfileNameSortedCaseInsensitively()
    {
        using TempDirectory temp = new();
        ProfileStore store = new(temp.FullPath);

        await store.SaveAsync(SampleProfile("zebra"), Token);
        await store.SaveAsync(SampleProfile("Apple"), Token);
        await store.SaveAsync(SampleProfile("mango"), Token);

        Assert.Equal(new[] { "Apple", "mango", "zebra" }, await store.ListAsync(Token));
    }

    [Fact]
    public async Task ListAsync_IgnoresFilesThatAreNotProfiles()
    {
        using TempDirectory temp = new();
        ProfileStore store = new(temp.FullPath);
        await store.SaveAsync(SampleProfile("Documents"), Token);
        File.WriteAllText(Path.Combine(temp.FullPath, "notes.txt"), "not a profile");

        Assert.Equal(new[] { "Documents" }, await store.ListAsync(Token));
    }

    [Fact]
    public async Task LoadAsync_ThrowsWhenTheProfileIsNotStored()
    {
        using TempDirectory temp = new();
        ProfileStore store = new(temp.FullPath);

        ProfileNotFoundException exception = await Assert.ThrowsAsync<ProfileNotFoundException>(
            () => store.LoadAsync("missing", Token));

        Assert.Equal("missing", exception.Name);
    }

    [Fact]
    public async Task LoadAsync_TakesTheNameFromTheFileRatherThanTheJson()
    {
        using TempDirectory temp = new();
        ProfileStore store = new(temp.FullPath);
        File.WriteAllText(
            Path.Combine(temp.FullPath, "OnDisk.json"),
            """{ "formatVersion": 1, "name": "SomethingElse", "inputs": [] }""");

        BackupProfile loaded = await store.LoadAsync("OnDisk", Token);

        Assert.Equal("OnDisk", loaded.Name);
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("sub/profile")]
    [InlineData("")]
    public async Task SaveAsync_RejectsANameThatCouldEscapeTheStore(string name)
    {
        using TempDirectory temp = new();
        ProfileStore store = new(temp.FullPath);

        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(SampleProfile(name), Token));
        Assert.False(Directory.Exists(Path.Combine(temp.FullPath, "..", "escape.json")));
    }

    [Fact]
    public async Task RenameAsync_MovesTheProfileAndUpdatesTheNameInTheFile()
    {
        using TempDirectory temp = new();
        ProfileStore store = new(temp.FullPath);
        await store.SaveAsync(SampleProfile("Before"), Token);

        await store.RenameAsync("Before", "After", Token);

        Assert.Equal(new[] { "After" }, await store.ListAsync(Token));
        Assert.False(File.Exists(Path.Combine(temp.FullPath, "Before.json")));
        Assert.Contains("\"name\": \"After\"", File.ReadAllText(Path.Combine(temp.FullPath, "After.json")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenameAsync_ThrowsWhenTheTargetNameIsTaken()
    {
        using TempDirectory temp = new();
        ProfileStore store = new(temp.FullPath);
        await store.SaveAsync(SampleProfile("One"), Token);
        await store.SaveAsync(SampleProfile("Two"), Token);

        ProfileAlreadyExistsException exception = await Assert.ThrowsAsync<ProfileAlreadyExistsException>(
            () => store.RenameAsync("One", "Two", Token));

        Assert.Equal("Two", exception.Name);
        Assert.Equal(new[] { "One", "Two" }, await store.ListAsync(Token));
    }

    [Fact]
    public async Task RenameAsync_ThrowsWhenTheProfileIsNotStored()
    {
        using TempDirectory temp = new();
        ProfileStore store = new(temp.FullPath);

        await Assert.ThrowsAsync<ProfileNotFoundException>(() => store.RenameAsync("missing", "other", Token));
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheProfile()
    {
        using TempDirectory temp = new();
        ProfileStore store = new(temp.FullPath);
        await store.SaveAsync(SampleProfile("Doomed"), Token);

        await store.DeleteAsync("Doomed", Token);

        Assert.Empty(await store.ListAsync(Token));
    }

    [Fact]
    public async Task DeleteAsync_ThrowsWhenTheProfileIsNotStored()
    {
        using TempDirectory temp = new();
        ProfileStore store = new(temp.FullPath);

        await Assert.ThrowsAsync<ProfileNotFoundException>(() => store.DeleteAsync("missing", Token));
    }

    [Fact]
    public async Task ExportAsync_WritesTheProfileToAnArbitraryPathAndCreatesItsParent()
    {
        using TempDirectory temp = new();
        ProfileStore store = new(Path.Combine(temp.FullPath, "store"));
        await store.SaveAsync(SampleProfile("Documents"), Token);

        string destination = Path.Combine(temp.FullPath, "elsewhere", "exported.json");
        await store.ExportAsync("Documents", destination, Token);

        Assert.True(File.Exists(destination));
        Assert.Contains("\"formatVersion\": 1", File.ReadAllText(destination), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportAsync_ThrowsWhenTheProfileIsNotStored()
    {
        using TempDirectory temp = new();
        ProfileStore store = new(Path.Combine(temp.FullPath, "store"));

        await Assert.ThrowsAsync<ProfileNotFoundException>(
            () => store.ExportAsync("missing", Path.Combine(temp.FullPath, "out.json"), Token));
    }

    [Fact]
    public async Task ImportAsync_AddsAnExportedProfileToTheStore()
    {
        using TempDirectory temp = new();
        ProfileStore source = new(Path.Combine(temp.FullPath, "source"));
        ProfileStore target = new(Path.Combine(temp.FullPath, "target"));
        await source.SaveAsync(SampleProfile("Documents"), Token);

        string exchange = Path.Combine(temp.FullPath, "exchange.json");
        await source.ExportAsync("Documents", exchange, Token);
        BackupProfile imported = await target.ImportAsync(exchange, Token);

        Assert.Equal("Documents", imported.Name);
        Assert.Equal(new[] { "Documents" }, await target.ListAsync(Token));
        Assert.Equal(CompressionAlgorithm.Gzip, (await target.LoadAsync("Documents", Token)).Compression);
    }

    [Fact]
    public async Task ImportAsync_NumbersTheNameRatherThanOverwritingAnExistingProfile()
    {
        using TempDirectory temp = new();
        ProfileStore store = new(Path.Combine(temp.FullPath, "store"));
        await store.SaveAsync(SampleProfile("Documents"), Token);

        string exchange = Path.Combine(temp.FullPath, "exchange.json");
        await store.ExportAsync("Documents", exchange, Token);

        Assert.Equal("Documents_2", (await store.ImportAsync(exchange, Token)).Name);
        Assert.Equal("Documents_3", (await store.ImportAsync(exchange, Token)).Name);
        Assert.Equal(new[] { "Documents", "Documents_2", "Documents_3" }, await store.ListAsync(Token));
    }

    [Fact]
    public async Task ImportAsync_FallsBackToTheFileNameWhenTheProfileNameIsUnusable()
    {
        using TempDirectory temp = new();
        ProfileStore store = new(Path.Combine(temp.FullPath, "store"));
        string exchange = temp.WriteFile("weird name.json", """{ "formatVersion": 1, "name": "../evil", "inputs": [] }""");

        BackupProfile imported = await store.ImportAsync(exchange, Token);

        Assert.Equal("weird name", imported.Name);
        Assert.Equal(new[] { "weird name" }, await store.ListAsync(Token));
    }

    [Fact]
    public async Task ImportAsync_ThrowsWhenTheFileDoesNotExist()
    {
        using TempDirectory temp = new();
        ProfileStore store = new(Path.Combine(temp.FullPath, "store"));

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => store.ImportAsync(Path.Combine(temp.FullPath, "nothing.json"), Token));
    }

    private static BackupProfile SampleProfile(string name = "Documents") => new()
    {
        Name = name,
        Inputs = ["/data/one", "/data/two"],
        OutputDirectory = "/backups",
        Compression = CompressionAlgorithm.Gzip,
        Encrypt = true,
        ExcludeGlobs = ["**/bin/**", "*.tmp"],
    };
}
