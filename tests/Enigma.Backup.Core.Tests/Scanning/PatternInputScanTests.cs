using System;
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

/// <summary>
/// Pattern inputs: what a <c>(base folder, glob)</c> pair archives, and what it says when it archives
/// nothing.
/// </summary>
/// <remarks>
/// The matcher itself is internal and is covered here rather than directly, for the same reason the
/// exclude globs are: what matters is which entries come out of a real walk, and a pattern's rules
/// only mean something against a real tree.
/// </remarks>
public sealed class PatternInputScanTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ScanAsync_Pattern_TakesTheMatchesInTheBaseFolderItselfAndLeavesTheRestBehind()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/app.log", "one");
        temp.WriteFile("data/keep.txt");
        temp.WriteFile("data/sub/other.log");

        ScanResult result = await ScanAsync(Profile(temp.Resolve("data"), "*.log"));

        // One level only: "data/sub" holds a log, but no pattern asked for it, so neither the file nor
        // the folder that would have carried it appears.
        Assert.Equal(new[] { "data", "data/app.log" }, ArchivePaths(result));
        Assert.Empty(result.Warnings);
        Assert.Equal(1, result.FileCount);
        Assert.Equal(3, result.TotalBytes);
    }

    [Fact]
    public async Task ScanAsync_PatternWithARecursivePrefix_MatchesAtAnyDepth()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/app.log");
        temp.WriteFile("data/keep.txt");
        temp.WriteFile("data/sub/other.log");

        ScanResult result = await ScanAsync(Profile(temp.Resolve("data"), "**/*.log"));

        Assert.Equal(new[] { "data", "data/app.log", "data/sub", "data/sub/other.log" }, ArchivePaths(result));
    }

    [Fact]
    public async Task ScanAsync_PatternMatchingAFoldersName_TakesTheFolderWithEverythingInIt()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/logs2024/a.txt");
        temp.WriteFile("data/logs2024/deep/b.txt");
        temp.WriteFile("data/other/c.txt");

        ScanResult result = await ScanAsync(Profile(temp.Resolve("data"), "logs*"));

        Assert.Equal(
            new[] { "data", "data/logs2024", "data/logs2024/a.txt", "data/logs2024/deep", "data/logs2024/deep/b.txt" },
            ArchivePaths(result));
    }

    [Fact]
    public async Task ScanAsync_Pattern_KeepsTheFoldersOnTheWayToAMatchAndNoOthers()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/deep/nested/a.log");
        temp.WriteFile("data/elsewhere/b.txt");
        temp.CreateDirectory("data/empty");

        ScanResult result = await ScanAsync(Profile(temp.Resolve("data"), "**/*.log"));

        Assert.Equal(new[] { "data", "data/deep", "data/deep/nested", "data/deep/nested/a.log" }, ArchivePaths(result));
    }

    [Fact]
    public async Task ScanAsync_PatternMatchingAnEmptyFolder_StillArchivesIt()
    {
        using TempDirectory temp = new();
        temp.CreateDirectory("data/empty-cache");
        temp.WriteFile("data/other/a.txt");

        ScanResult result = await ScanAsync(Profile(temp.Resolve("data"), "empty*"));

        Assert.Equal(new[] { "data", "data/empty-cache" }, ArchivePaths(result));
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task ScanAsync_ExcludeGlobs_WinOverAPatternMatch()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/a.log");
        temp.WriteFile("data/skip/b.log");

        BackupProfile profile = Profile(temp.Resolve("data"), "**/*.log");
        profile.ExcludeGlobs = ["**/skip/**"];

        ScanResult result = await ScanAsync(profile);

        Assert.Equal(new[] { "data", "data/a.log" }, ArchivePaths(result));
    }

    [Fact]
    public async Task ScanAsync_ExcludeGlobCancellingOutThePattern_LeavesNothingAndSaysSo()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/a.log");

        BackupProfile profile = Profile(temp.Resolve("data"), "*.log");
        profile.ExcludeGlobs = ["*.log"];

        ScanResult result = await ScanAsync(profile);

        Assert.Equal(new[] { "data" }, ArchivePaths(result));
        Assert.Equal(ScanWarningKind.PatternMatchedNothing, Assert.Single(result.Warnings).Kind);
    }

    [Fact]
    public async Task ScanAsync_PatternThatMatchesNothing_WarnsAndStillRecordsTheBaseFolder()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/keep.txt");

        ScanResult result = await ScanAsync(Profile(temp.Resolve("data"), "*.log"));

        // Not an error — there may be no logs today — but the archive says where it looked.
        Assert.Equal(new[] { "data" }, ArchivePaths(result));
        ScanWarning warning = Assert.Single(result.Warnings);
        Assert.Equal(ScanWarningKind.PatternMatchedNothing, warning.Kind);
        Assert.Equal(temp.Resolve("data"), warning.Path);
        Assert.Contains("*.log", warning.Message, StringComparison.Ordinal);
        Assert.Empty(result.Skipped);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ScanAsync_BlankPattern_MatchesNothingAndWarns(string pattern)
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/a.txt");

        ScanResult result = await ScanAsync(Profile(temp.Resolve("data"), pattern));

        Assert.Equal(new[] { "data" }, ArchivePaths(result));
        Assert.Equal(ScanWarningKind.PatternMatchedNothing, Assert.Single(result.Warnings).Kind);
    }

    [Fact]
    public async Task ScanAsync_PatternIsCaseSensitiveExactlyWhereTheFileSystemIs()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/A.LOG");

        ScanResult result = await ScanAsync(Profile(temp.Resolve("data"), "*.log"));

        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            Assert.Equal(new[] { "data", "data/A.LOG" }, ArchivePaths(result));
        }
        else
        {
            Assert.Equal(new[] { "data" }, ArchivePaths(result));
        }
    }

    [Fact]
    public async Task ScanAsync_PatternWrittenWithBackslashes_IsReadTheSameWay()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/sub/a.log");

        ScanResult result = await ScanAsync(Profile(temp.Resolve("data"), @"sub\*.log"));

        Assert.Equal(new[] { "data", "data/sub", "data/sub/a.log" }, ArchivePaths(result));
    }

    [Fact]
    public async Task ScanAsync_MissingBaseFolderAnsweredWithSkip_IsRecordedAndNamesThePattern()
    {
        using TempDirectory temp = new();
        ScriptedErrorHandler handler = new(BackupErrorDecision.Skip());

        ScanResult result = await ScanAsync(Profile(temp.Resolve("gone"), "*.log"), handler);

        Assert.Empty(result.Entries);
        Assert.Equal(temp.Resolve("gone"), Assert.Single(result.Skipped).Path);

        BackupError error = Assert.Single(handler.Errors);
        Assert.Equal(BackupErrorKind.MissingInput, error.Kind);
        Assert.Contains("*.log", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScanAsync_MissingBaseFolderAnsweredWithAbort_StopsTheRun()
    {
        using TempDirectory temp = new();
        ScriptedErrorHandler handler = new(BackupErrorDecision.Abort());

        BackupAbortedException exception = await Assert.ThrowsAsync<BackupAbortedException>(
            () => Scanner.ScanAsync(Profile(temp.Resolve("gone"), "*.log"), handler, Token));

        Assert.Equal(temp.Resolve("gone"), exception.Error?.Path);
    }

    [Fact]
    public async Task ScanAsync_MissingBaseFolderAnsweredWithRetry_LooksAgain()
    {
        using TempDirectory temp = new();

        // The fallback would abort the run, so the retry has to be what succeeds here.
        ScriptedErrorHandler handler = new(BackupErrorDecision.Abort(), BackupErrorDecision.Retry())
        {
            BeforeAnswering = _ => temp.WriteFile("data/a.log"),
        };

        ScanResult result = await ScanAsync(Profile(temp.Resolve("data"), "*.log"), handler);

        Assert.Single(handler.Errors);
        Assert.Equal(new[] { "data", "data/a.log" }, ArchivePaths(result));
    }

    [Fact]
    public async Task ScanAsync_BaseFolderThatIsAFile_IsReportedAsAMissingBaseFolder()
    {
        using TempDirectory temp = new();
        string file = temp.WriteFile("notes.txt");
        ScriptedErrorHandler handler = new(BackupErrorDecision.Skip());

        ScanResult result = await ScanAsync(Profile(file, "*.log"), handler);

        Assert.Empty(result.Entries);
        Assert.Contains("is a file", Assert.Single(handler.Errors).Message, StringComparison.Ordinal);
        Assert.Single(result.Skipped);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ScanAsync_BlankBaseFolder_IsReportedRatherThanIgnored(string baseDirectory)
    {
        ScriptedErrorHandler handler = new(BackupErrorDecision.Skip());

        // Only a hand-edited profile can get here, and a pattern that quietly backs up nothing is the
        // one outcome a backup tool must not have.
        ScanResult result = await ScanAsync(Profile(baseDirectory, "*.log"), handler);

        Assert.Empty(result.Entries);
        Assert.Equal(BackupErrorKind.MissingInput, Assert.Single(handler.Errors).Kind);
        Assert.Single(result.Skipped);
    }

    [Fact]
    public async Task ScanAsync_PatternBaseSharingATopLevelNameWithAPathInput_IsNumberedRatherThanMerged()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/a.txt");

        BackupProfile profile = new()
        {
            Name = "test",
            Inputs = [temp.Resolve("data")],
            PatternInputs = [new PatternInput(temp.Resolve("data"), "*.txt")],
        };

        ScanResult result = await ScanAsync(profile);

        // A pattern is a rule, not a place: it never folds into the path input over the same folder.
        Assert.Equal(new[] { "data", "data/a.txt", "data_2", "data_2/a.txt" }, ArchivePaths(result));
        Assert.Equal(ScanWarningKind.RootNameDisambiguated, Assert.Single(result.Warnings).Kind);
    }

    [Fact]
    public async Task ScanAsync_PatternBaseInsideAPathInput_IsNotMergedIntoIt()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/sub/a.log");

        BackupProfile profile = new()
        {
            Name = "test",
            Inputs = [temp.Resolve("data")],
            PatternInputs = [new PatternInput(temp.Resolve("data/sub"), "*.log")],
        };

        ScanResult result = await ScanAsync(profile);

        Assert.Equal(new[] { "data", "data/sub", "data/sub/a.log", "sub", "sub/a.log" }, ArchivePaths(result));
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task ScanAsync_TwoPatternsOverTheSameFolder_AreBothResolved()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/a.log");
        temp.WriteFile("data/b.txt");

        BackupProfile profile = new()
        {
            Name = "test",
            PatternInputs =
            [
                new PatternInput(temp.Resolve("data"), "*.log"),
                new PatternInput(temp.Resolve("data"), "*.txt"),
            ],
        };

        ScanResult result = await ScanAsync(profile);

        Assert.Equal(new[] { "data", "data/a.log", "data_2", "data_2/b.txt" }, ArchivePaths(result));
    }

    [Fact]
    public async Task ScanAsync_PatternBaseInsideTheOutputDirectory_IsLeftOut()
    {
        using TempDirectory temp = new();
        temp.WriteFile("out/stuff/a.log");

        BackupProfile profile = Profile(temp.Resolve("out/stuff"), "*.log");
        profile.OutputDirectory = temp.Resolve("out");

        ScanResult result = await ScanAsync(profile);

        Assert.Empty(result.Entries);
        Assert.Equal(ScanWarningKind.OutputDirectoryExcluded, Assert.Single(result.Warnings).Kind);
    }

    [Fact]
    public async Task ScanAsync_OutputDirectoryInsideAPatternBase_IsLeftOut()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/a.log");
        temp.WriteFile("data/backups/previous.log");

        BackupProfile profile = Profile(temp.Resolve("data"), "**/*.log");
        profile.OutputDirectory = temp.Resolve("data/backups");

        ScanResult result = await ScanAsync(profile);

        Assert.Equal(new[] { "data", "data/a.log" }, ArchivePaths(result));
        ScanWarning warning = Assert.Single(result.Warnings);
        Assert.Equal(ScanWarningKind.OutputDirectoryExcluded, warning.Kind);
        Assert.Equal(temp.Resolve("data/backups"), warning.Path);
    }

    [Fact]
    public async Task ScanAsync_DirectorySymbolicLinkUnderAPatternBase_IsNotFollowed()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/real/x.log");
        CreateDirectoryLinkOrSkip(temp.Resolve("data/link"), temp.Resolve("data/real"));

        ScanResult result = await ScanAsync(Profile(temp.Resolve("data"), "**/*.log"));

        Assert.Equal(new[] { "data", "data/real", "data/real/x.log" }, ArchivePaths(result));
        Assert.Equal(ScanWarningKind.SymbolicLinkSkipped, Assert.Single(result.Warnings).Kind);
    }

    [Fact]
    public async Task ScanAsync_PatternBaseThatIsADirectorySymbolicLink_IsNotFollowedEither()
    {
        using TempDirectory temp = new();
        temp.WriteFile("real/x.log");
        CreateDirectoryLinkOrSkip(temp.Resolve("link"), temp.Resolve("real"));

        ScanResult result = await ScanAsync(Profile(temp.Resolve("link"), "*.log"));

        Assert.Empty(result.Entries);
        Assert.Equal(ScanWarningKind.SymbolicLinkSkipped, Assert.Single(result.Warnings).Kind);
    }

    [Fact]
    public async Task ScanAsync_PathsAndPatternsTogether_KeepTheirOwnTopLevelNames()
    {
        using TempDirectory temp = new();
        temp.WriteFile("docs/a.txt", "aaa");
        temp.WriteFile("logs/today.log", "bb");
        temp.WriteFile("logs/notes.txt");

        BackupProfile profile = new()
        {
            Name = "test",
            Inputs = [temp.Resolve("docs")],
            PatternInputs = [new PatternInput(temp.Resolve("logs"), "*.log")],
        };

        ScanResult result = await ScanAsync(profile);

        // Path inputs first, then the patterns, each under its own root name.
        Assert.Equal(new[] { "docs", "docs/a.txt", "logs", "logs/today.log" }, ArchivePaths(result));
        Assert.Equal(2, result.FileCount);
        Assert.Equal(2, result.DirectoryCount);
        Assert.Equal(5, result.TotalBytes);
    }

    [Fact]
    public async Task ScanAsync_HonoursCancellationWhileResolvingPatterns()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/a.log");
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Scanner.ScanAsync(
                Profile(temp.Resolve("data"), "*.log"),
                new ScriptedErrorHandler(BackupErrorDecision.Skip()),
                cancellation.Token));
    }

    [Fact]
    public async Task ScanAsync_NullPatternListOrEntry_IsSimplyNothingToResolve()
    {
        using TempDirectory temp = new();
        temp.WriteFile("data/a.txt");

        BackupProfile profile = new()
        {
            Name = "test",
            Inputs = [temp.Resolve("data")],
            PatternInputs = null!,
        };

        ScanResult result = await ScanAsync(profile);

        Assert.Equal(new[] { "data", "data/a.txt" }, ArchivePaths(result));

        profile.PatternInputs = [null!];
        result = await ScanAsync(profile);

        Assert.Equal(new[] { "data", "data/a.txt" }, ArchivePaths(result));
    }

    private static InputScanner Scanner { get; } = new();

    private static BackupProfile Profile(string baseDirectory, string pattern) => new()
    {
        Name = "test",
        PatternInputs = [new PatternInput(baseDirectory, pattern)],
    };

    private static Task<ScanResult> ScanAsync(BackupProfile profile, ScriptedErrorHandler? handler = null) =>
        Scanner.ScanAsync(profile, handler ?? new ScriptedErrorHandler(BackupErrorDecision.Abort()), Token);

    private static string[] ArchivePaths(ScanResult result) => [.. result.Entries.Select(entry => entry.ArchivePath)];

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
