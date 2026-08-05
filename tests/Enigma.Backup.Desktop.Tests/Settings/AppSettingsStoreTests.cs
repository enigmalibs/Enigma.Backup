using System;
using System.IO;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Desktop.Settings;
using Enigma.Backup.Desktop.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Enigma.Backup.Desktop.Tests.Settings;

/// <summary>
/// The preferences file: what it round-trips, and everything it refuses to fail on.
/// </summary>
/// <remarks>
/// The failure cases are the point. A theme choice is the least important thing this app stores, so
/// none of the ways it can be unreadable — absent, truncated, hand-edited to nonsense, written by a
/// build that is not this one — may be able to stop the app from opening. Every one of them has to
/// come back as "follow the system".
/// </remarks>
public sealed class AppSettingsStoreTests
{
    [Fact]
    public void AChoiceSurvivesBeingWrittenAndReadBack()
    {
        using TempDirectory temp = new();
        AppSettingsStore store = Store(temp);

        store.Save(new AppSettings { Theme = AppTheme.Dark });

        Assert.Equal(AppTheme.Dark, Store(temp).Load().Theme);
    }

    [Fact]
    public void TheFileIsReadableJsonNamingTheTheme()
    {
        using TempDirectory temp = new();
        AppSettingsStore store = Store(temp);

        store.Save(new AppSettings { Theme = AppTheme.Light });

        string json = File.ReadAllText(store.FilePath);

        // Written as a name, not an ordinal: the file is meant to be readable, and hand-editable.
        Assert.Contains("\"theme\": \"Light\"", json, StringComparison.Ordinal);
        Assert.Contains("\"formatVersion\": 1", json, StringComparison.Ordinal);
    }

    [Fact]
    public void SavingCreatesTheFolderAndLeavesNoTemporaryFileBehind()
    {
        using TempDirectory temp = new();
        AppSettingsStore store = new(Path.Combine(temp.Path, "nested", "deeper", "settings.json"), NullLogger<AppSettingsStore>.Instance);

        store.Save(new AppSettings { Theme = AppTheme.Dark });

        Assert.True(File.Exists(store.FilePath));
        Assert.False(File.Exists(store.FilePath + ".tmp"));
    }

    [Fact]
    public void AMissingFileReadsAsFollowingTheSystem_AndWritesNothing()
    {
        using TempDirectory temp = new();
        AppSettingsStore store = Store(temp);

        Assert.Equal(AppTheme.System, store.Load().Theme);
        Assert.False(File.Exists(store.FilePath));
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("\"a string\"")]
    [InlineData("{\"formatVersion\": 1, \"theme\": \"Aubergine\"}")]
    [InlineData("{\"formatVersion\": 1, \"theme\": null}")]
    [InlineData("{\"theme\": \"Dark\"}")]
    [InlineData("{\"formatVersion\": 2, \"theme\": \"Dark\"}")]
    [InlineData("{\"formatVersion\": \"one\", \"theme\": \"Dark\"}")]
    public void AFileThisBuildCannotReadFallsBackToFollowingTheSystem(string contents)
    {
        using TempDirectory temp = new();
        AppSettingsStore store = Store(temp);

        File.WriteAllText(store.FilePath, contents);

        AppSettings settings = store.Load();

        Assert.Equal(AppTheme.System, settings.Theme);
        Assert.Equal(AppSettings.CurrentFormatVersion, settings.FormatVersion);
    }

    [Fact]
    public void ACorruptFileIsNotRewrittenByReadingIt()
    {
        using TempDirectory temp = new();
        AppSettingsStore store = Store(temp);

        File.WriteAllText(store.FilePath, "{ truncated");
        store.Load();

        // Loading is a read: whatever the user has in there is still theirs to fix.
        Assert.Equal("{ truncated", File.ReadAllText(store.FilePath));
    }

    [Fact]
    public void AWriteThatCannotHappenIsDroppedRatherThanThrown()
    {
        using TempDirectory temp = new();

        // The parent of the settings file is itself a file, so neither the folder nor the write can
        // be made — the closest thing to a read-only configuration folder that is portable.
        string blocker = temp.WriteFile("blocked", "in the way");
        AppSettingsStore store = new(Path.Combine(blocker, "settings.json"), NullLogger<AppSettingsStore>.Instance);

        store.Save(new AppSettings { Theme = AppTheme.Dark });

        Assert.False(File.Exists(store.FilePath));
    }

    /// <remarks>
    /// Both are resolved from <c>ApplicationData</c> independently, so this is what pins the decision
    /// that the app's own preferences sit beside the profiles rather than in a folder of their own.
    /// </remarks>
    [Fact]
    public void TheDefaultFileSitsBesideTheProfileFolder()
    {
        Assert.Equal(
            Path.GetDirectoryName(ProfileStore.DefaultDirectory),
            Path.GetDirectoryName(AppSettingsStore.DefaultPath));

        Assert.Equal("settings.json", Path.GetFileName(AppSettingsStore.DefaultPath));
    }

    private static AppSettingsStore Store(TempDirectory temp) =>
        new(Path.Combine(temp.Path, "settings.json"), NullLogger<AppSettingsStore>.Instance);
}
