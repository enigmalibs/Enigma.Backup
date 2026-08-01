using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Errors;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Core.Scanning;
using Enigma.Backup.Core.Tests.TestSupport;
using Xunit;

namespace Enigma.Backup.Core.Tests.Scanning;

public sealed class InputScannerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ScanAsync_WithNoInputs_ReturnsAnEmptyResult()
    {
        ScanResult result = await ScanAsync(new BackupProfile { Name = "empty" });

        Assert.Empty(result.Entries);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Skipped);
        Assert.Equal(0, result.TotalBytes);
    }

    [Fact]
    public async Task ScanAsync_DirectoryInput_EmitsEveryEntryUnderTheDirectoryNameInWalkOrder()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/a.txt", "aaa");
        temp.WriteFile("data/sub/b.txt", "bb");
        temp.CreateDirectory("data/empty");

        ScanResult result = await ScanAsync(Profile(temp.Resolve("data")));

        Assert.Equal(
            new[] { "data", "data/a.txt", "data/empty", "data/sub", "data/sub/b.txt" },
            ArchivePaths(result));
        Assert.Equal(2, result.FileCount);
        Assert.Equal(3, result.DirectoryCount);
        Assert.Equal(5, result.TotalBytes);
    }

    [Fact]
    public async Task ScanAsync_KeepsEmptyDirectoriesSoTheySurviveARoundTrip()
    {
        using TempDirectory temp = new();
        temp.CreateDirectory("data/nothing/in/here");

        ScanResult result = await ScanAsync(Profile(temp.Resolve("data")));

        Assert.Equal(new[] { "data", "data/nothing", "data/nothing/in", "data/nothing/in/here" }, ArchivePaths(result));
        Assert.All(result.Entries, entry => Assert.Equal(ScanEntryKind.Directory, entry.Kind));
    }

    [Fact]
    public async Task ScanAsync_FileInput_EmitsASingleEntryNamedAfterTheFile()
    {
        using TempDirectory temp = new();
        string file = temp.WriteFile("notes.txt", "hello");

        ScanResult result = await ScanAsync(Profile(file));

        ScanEntry entry = Assert.Single(result.Entries);
        Assert.Equal(ScanEntryKind.File, entry.Kind);
        Assert.Equal("notes.txt", entry.ArchivePath);
        Assert.Equal(file, entry.SourcePath);
        Assert.Equal(5, entry.Length);
        Assert.Equal(TimeSpan.Zero, entry.LastWriteTime.Offset);
        Assert.InRange(entry.LastWriteTime, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(5));
    }

    [Fact]
    public async Task ScanAsync_IncludesHiddenFiles()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/.hidden", "secret");

        ScanResult result = await ScanAsync(Profile(temp.Resolve("data")));

        Assert.Contains("data/.hidden", ArchivePaths(result));
    }

    [Fact]
    public async Task ScanAsync_ListsTheSameInputTwice_KeepsItOnceAndWarns()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/a.txt");
        string root = temp.Resolve("data");

        ScanResult result = await ScanAsync(Profile(root, root + Path.DirectorySeparatorChar));

        Assert.Equal(new[] { "data", "data/a.txt" }, ArchivePaths(result));
        Assert.Equal(ScanWarningKind.DuplicateInput, Assert.Single(result.Warnings).Kind);
    }

    [Fact]
    public async Task ScanAsync_NestedInputListedAfterItsParent_IsMergedIntoIt()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/sub/a.txt");

        ScanResult result = await ScanAsync(Profile(temp.Resolve("data"), temp.Resolve("data/sub")));

        Assert.Equal(new[] { "data", "data/sub", "data/sub/a.txt" }, ArchivePaths(result));
        ScanWarning warning = Assert.Single(result.Warnings);
        Assert.Equal(ScanWarningKind.NestedInputMerged, warning.Kind);
        Assert.Equal(temp.Resolve("data/sub"), warning.Path);
    }

    [Fact]
    public async Task ScanAsync_NestedInputListedBeforeItsParent_IsStillMergedIntoIt()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/sub/a.txt");

        ScanResult result = await ScanAsync(Profile(temp.Resolve("data/sub"), temp.Resolve("data")));

        Assert.Equal(new[] { "data", "data/sub", "data/sub/a.txt" }, ArchivePaths(result));
        Assert.Equal(ScanWarningKind.NestedInputMerged, Assert.Single(result.Warnings).Kind);
    }

    [Fact]
    public async Task ScanAsync_SiblingPathsWithACommonPrefix_AreNotTreatedAsNested()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/a.txt");
        temp.WriteFile("database/b.txt");

        ScanResult result = await ScanAsync(Profile(temp.Resolve("data"), temp.Resolve("database")));

        Assert.Empty(result.Warnings);
        Assert.Equal(new[] { "data", "data/a.txt", "database", "database/b.txt" }, ArchivePaths(result));
    }

    [Fact]
    public async Task ScanAsync_TwoInputsSharingATopLevelName_GivesTheSecondANumberedName()
    {
        using TempDirectory temp = new();
        temp.WriteFile("one/photos/a.txt");
        temp.WriteFile("two/photos/b.txt");

        ScanResult result = await ScanAsync(Profile(temp.Resolve("one/photos"), temp.Resolve("two/photos")));

        Assert.Equal(new[] { "photos", "photos/a.txt", "photos_2", "photos_2/b.txt" }, ArchivePaths(result));
        ScanWarning warning = Assert.Single(result.Warnings);
        Assert.Equal(ScanWarningKind.RootNameDisambiguated, warning.Kind);
        Assert.Equal(temp.Resolve("two/photos"), warning.Path);
    }

    [Fact]
    public async Task ScanAsync_ThreeInputsSharingATopLevelName_KeepNumberingUp()
    {
        using TempDirectory temp = new();
        temp.WriteFile("one/photos/a.txt");
        temp.WriteFile("two/photos/b.txt");
        temp.WriteFile("three/photos/c.txt");

        ScanResult result = await ScanAsync(
            Profile(temp.Resolve("one/photos"), temp.Resolve("two/photos"), temp.Resolve("three/photos")));

        Assert.Equal(new[] { "photos", "photos_2", "photos_3" }, ArchivePaths(result).Where(path => !path.Contains('/')).ToArray());
    }

    [Fact]
    public async Task ScanAsync_TwoFileInputsSharingAName_AreNumberedBeforeTheExtension()
    {
        using TempDirectory temp = new();
        temp.WriteFile("one/config.json", "1");
        temp.WriteFile("two/config.json", "2");

        ScanResult result = await ScanAsync(Profile(temp.Resolve("one/config.json"), temp.Resolve("two/config.json")));

        Assert.Equal(new[] { "config.json", "config_2.json" }, ArchivePaths(result));
    }

    [Fact]
    public async Task ScanAsync_TopLevelNameThatWindowsCouldNotRestore_IsMadePortableAndWarnedAbout()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The offending characters cannot be used in a Windows directory name.");

        using TempDirectory temp = new();
        temp.WriteFile("re:port/a.txt");

        ScanResult result = await ScanAsync(Profile(temp.Resolve("re:port")));

        Assert.Equal(new[] { "re_port", "re_port/a.txt" }, ArchivePaths(result));
        Assert.Equal(ScanWarningKind.RootNameSanitized, Assert.Single(result.Warnings).Kind);
    }

    [Fact]
    public async Task ScanAsync_ExcludeGlobEndingInDoubleStar_PrunesTheDirectoryItNames()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/src/a.cs");
        temp.WriteFile("data/src/bin/Debug/a.dll");
        temp.WriteFile("data/bin/b.dll");

        BackupProfile profile = Profile(temp.Resolve("data"));
        profile.ExcludeGlobs = ["**/bin/**"];

        ScanResult result = await ScanAsync(profile);

        Assert.Equal(new[] { "data", "data/src", "data/src/a.cs" }, ArchivePaths(result));
    }

    [Fact]
    public async Task ScanAsync_ExcludeGlobIsMatchedRelativeToTheInputItWasFoundUnder()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/app.log");
        temp.WriteFile("data/keep.txt");
        temp.WriteFile("data/sub/other.log");

        BackupProfile profile = Profile(temp.Resolve("data"));
        profile.ExcludeGlobs = ["*.log"];

        ScanResult result = await ScanAsync(profile);

        Assert.DoesNotContain("data/app.log", ArchivePaths(result));
        Assert.Contains("data/keep.txt", ArchivePaths(result));
        Assert.Contains("data/sub/other.log", ArchivePaths(result));
    }

    [Fact]
    public async Task ScanAsync_ExcludeGlobCanMatchEverywhereWithARecursivePrefix()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/app.log");
        temp.WriteFile("data/sub/other.log");
        temp.WriteFile("data/keep.txt");

        BackupProfile profile = Profile(temp.Resolve("data"));
        profile.ExcludeGlobs = ["**/*.log"];

        ScanResult result = await ScanAsync(profile);

        Assert.Equal(new[] { "data", "data/keep.txt", "data/sub" }, ArchivePaths(result));
    }

    [Fact]
    public async Task ScanAsync_BlankExcludeGlobsAreIgnored()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/a.txt");

        BackupProfile profile = Profile(temp.Resolve("data"));
        profile.ExcludeGlobs = ["", "   "];

        ScanResult result = await ScanAsync(profile);

        Assert.Equal(new[] { "data", "data/a.txt" }, ArchivePaths(result));
    }

    [Fact]
    public async Task ScanAsync_OutputDirectoryInsideAnInput_IsLeftOut()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/a.txt");
        temp.WriteFile("data/backups/previous.tar.zst");

        BackupProfile profile = Profile(temp.Resolve("data"));
        profile.OutputDirectory = temp.Resolve("data/backups");

        ScanResult result = await ScanAsync(profile);

        Assert.Equal(new[] { "data", "data/a.txt" }, ArchivePaths(result));
        ScanWarning warning = Assert.Single(result.Warnings);
        Assert.Equal(ScanWarningKind.OutputDirectoryExcluded, warning.Kind);
        Assert.Equal(temp.Resolve("data/backups"), warning.Path);
    }

    [Fact]
    public async Task ScanAsync_InputInsideTheOutputDirectory_IsLeftOut()
    {
        using TempDirectory temp = new();
        temp.WriteFile("out/stuff/a.txt");
        temp.WriteFile("data/b.txt");

        BackupProfile profile = Profile(temp.Resolve("out/stuff"), temp.Resolve("data"));
        profile.OutputDirectory = temp.Resolve("out");

        ScanResult result = await ScanAsync(profile);

        Assert.Equal(new[] { "data", "data/b.txt" }, ArchivePaths(result));
        Assert.Equal(ScanWarningKind.OutputDirectoryExcluded, Assert.Single(result.Warnings).Kind);
    }

    [Fact]
    public async Task ScanAsync_MissingInputAnsweredWithSkip_IsRecordedAndTheRestIsScanned()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/a.txt");
        ScriptedErrorHandler handler = new(BackupErrorDecision.Skip());

        ScanResult result = await ScanAsync(Profile(temp.Resolve("gone"), temp.Resolve("data")), handler);

        Assert.Equal(new[] { "data", "data/a.txt" }, ArchivePaths(result));
        Assert.Equal(temp.Resolve("gone"), Assert.Single(result.Skipped).Path);
        Assert.Equal(BackupErrorKind.MissingInput, Assert.Single(handler.Errors).Kind);
    }

    [Fact]
    public async Task ScanAsync_MissingInputAnsweredWithAbort_StopsTheRun()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/a.txt");
        ScriptedErrorHandler handler = new(BackupErrorDecision.Abort());

        BackupAbortedException exception = await Assert.ThrowsAsync<BackupAbortedException>(
            () => Scanner.ScanAsync(Profile(temp.Resolve("gone"), temp.Resolve("data")), handler, Token));

        BackupError? error = exception.Error;
        Assert.NotNull(error);
        Assert.Equal(BackupErrorKind.MissingInput, error.Kind);
        Assert.Equal(temp.Resolve("gone"), error.Path);
    }

    [Fact]
    public async Task ScanAsync_MissingInputAnsweredWithRetry_AsksAgain()
    {
        using TempDirectory temp = new();
        ScriptedErrorHandler handler = new(BackupErrorDecision.Skip(), BackupErrorDecision.Retry());

        ScanResult result = await ScanAsync(Profile(temp.Resolve("gone")), handler);

        Assert.Equal(2, handler.Errors.Count);
        Assert.Single(result.Skipped);
    }

    [Fact]
    public async Task ScanAsync_ApplyToAll_AsksOnceAndSkipsEveryFurtherFailure()
    {
        using TempDirectory temp = new();

        // The fallback would abort the run, so reaching it at all fails this test.
        ScriptedErrorHandler handler = new(BackupErrorDecision.Abort(), BackupErrorDecision.Skip(applyToAll: true));

        ScanResult result = await ScanAsync(
            Profile(temp.Resolve("gone-one"), temp.Resolve("gone-two"), temp.Resolve("gone-three")),
            handler);

        Assert.Single(handler.Errors);
        Assert.Equal(3, result.Skipped.Count);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public async Task ScanAsync_ApplyToAllIsNeverHonouredForRetry()
    {
        using TempDirectory temp = new();
        ScriptedErrorHandler handler = new(
            BackupErrorDecision.Skip(),
            new BackupErrorDecision(BackupErrorAction.Retry, ApplyToAll: true));

        ScanResult result = await ScanAsync(Profile(temp.Resolve("gone")), handler);

        // Had "retry, apply to all" stuck, the scan would never have asked a second time — or stopped.
        Assert.Equal(2, handler.Errors.Count);
        Assert.Single(result.Skipped);
    }

    [Fact]
    public async Task ScanAsync_DirectorySymbolicLink_IsNotFollowedAndIsWarnedAbout()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/real/x.txt");
        CreateDirectoryLinkOrSkip(temp.Resolve("data/link"), temp.Resolve("data/real"));

        ScanResult result = await ScanAsync(Profile(temp.Resolve("data")));

        Assert.Equal(new[] { "data", "data/real", "data/real/x.txt" }, ArchivePaths(result));
        ScanWarning warning = Assert.Single(result.Warnings);
        Assert.Equal(ScanWarningKind.SymbolicLinkSkipped, warning.Kind);
        Assert.Equal(temp.Resolve("data/link"), warning.Path);
    }

    [Fact]
    public async Task ScanAsync_DirectorySymbolicLinkAsAnInput_IsNotFollowedEither()
    {
        using TempDirectory temp = new();
        temp.WriteFile("real/x.txt");
        CreateDirectoryLinkOrSkip(temp.Resolve("link"), temp.Resolve("real"));

        ScanResult result = await ScanAsync(Profile(temp.Resolve("link")));

        Assert.Empty(result.Entries);
        Assert.Equal(ScanWarningKind.SymbolicLinkSkipped, Assert.Single(result.Warnings).Kind);
    }

    [Fact]
    public async Task ScanAsync_UnreadableDirectoryAnsweredWithSkip_IsRecordedAndTheRestIsScanned()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/readable/a.txt");
        string locked = temp.CreateDirectory("data/locked");
        temp.WriteFile("data/locked/secret.txt");
        MakeUnreadableOrSkip(locked);

        try
        {
            ScriptedErrorHandler handler = new(BackupErrorDecision.Skip());

            ScanResult result = await ScanAsync(Profile(temp.Resolve("data")), handler);

            Assert.Equal(new[] { "data", "data/locked", "data/readable", "data/readable/a.txt" }, ArchivePaths(result));
            Assert.Equal(locked, Assert.Single(result.Skipped).Path);
            Assert.Equal(BackupErrorKind.AccessDenied, Assert.Single(handler.Errors).Kind);
        }
        finally
        {
            RestoreAccess(locked);
        }
    }

    [Fact]
    public async Task ScanAsync_UnreadableDirectoryAnsweredWithAbort_StopsTheRun()
    {
        using TempDirectory temp = new();
        string locked = temp.CreateDirectory("data/locked");
        MakeUnreadableOrSkip(locked);

        try
        {
            ScriptedErrorHandler handler = new(BackupErrorDecision.Abort());

            BackupAbortedException exception = await Assert.ThrowsAsync<BackupAbortedException>(
                () => Scanner.ScanAsync(Profile(temp.Resolve("data")), handler, Token));

            BackupError? error = exception.Error;
            Assert.NotNull(error);
            Assert.Equal(locked, error.Path);
        }
        finally
        {
            RestoreAccess(locked);
        }
    }

    [Fact]
    public async Task ScanAsync_HonoursCancellation()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/a.txt");
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Scanner.ScanAsync(Profile(temp.Resolve("data")), new ScriptedErrorHandler(BackupErrorDecision.Skip()), cancellation.Token));
    }

    [Fact]
    public async Task ScanAsync_RejectsMissingArguments()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => Scanner.ScanAsync(null!, new ScriptedErrorHandler(BackupErrorDecision.Skip()), Token));
        await Assert.ThrowsAsync<ArgumentNullException>(() => Scanner.ScanAsync(new BackupProfile(), null!, Token));
    }

    private static InputScanner Scanner { get; } = new();

    private static BackupProfile Profile(params string[] inputs) => new()
    {
        Name = "test",
        Inputs = [.. inputs],
    };

    private static Task<ScanResult> ScanAsync(BackupProfile profile, ScriptedErrorHandler? handler = null) =>
        Scanner.ScanAsync(profile, handler ?? new ScriptedErrorHandler(BackupErrorDecision.Abort()), Token);

    private static string[] ArchivePaths(ScanResult result) => [.. result.Entries.Select(entry => entry.ArchivePath)];

    /// <summary>
    /// Takes every permission off a directory, skipping the test when that does not actually make it
    /// unreadable — which is the case on Windows, and for a privileged user anywhere.
    /// </summary>
    private static void MakeUnreadableOrSkip(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Directory access is governed by ACLs on Windows, not by a file mode.");
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Assert.Skip($"Directory permissions cannot be removed here: {exception.Message}");
            return;
        }

        try
        {
            _ = new DirectoryInfo(path).GetFileSystemInfos();
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        RestoreAccess(path);
        Assert.Skip("The directory is still readable after removing its permissions (running as a privileged user?).");
    }

    private static void RestoreAccess(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Nothing to restore, or nothing that can be: the temp directory cleanup copes either way.
        }
    }

    private static void CreateDirectoryLinkOrSkip(string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Assert.Skip($"Symbolic links cannot be created here: {exception.Message}");
        }
    }
}
