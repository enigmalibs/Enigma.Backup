using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Backups;
using Enigma.Backup.Core.Errors;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Core.Scanning;
using Enigma.Backup.Core.Tests.TestSupport;
using Xunit;

namespace Enigma.Backup.Core.Tests.Backups;

public sealed class BackupServiceTests
{
    private static readonly DateTimeOffset RunTime = new(2026, 7, 31, 23, 45, 6, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RunAsync_NamesTheArchiveAfterTheProfileAndTheRunTime()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "hello");

        BackupResult result = await RunAsync(temp);

        Assert.Equal(temp.Resolve("out/Documents_2026-07-31_234506.tar.zst"), result.OutputPath);
        Assert.True(File.Exists(result.OutputPath));
    }

    [Fact]
    public async Task RunAsync_SanitizesAProfileNameThatIsNoUseAsAFileName()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "hello");

        BackupResult result = await RunAsync(temp, profileName: "Home: photos/2026");

        Assert.Equal(temp.Resolve("out/Home_ photos_2026_2026-07-31_234506.tar.zst"), result.OutputPath);
    }

    [Fact]
    public async Task RunAsync_TwiceWithinTheSameSecond_DoesNotOverwriteTheFirstArchive()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "hello");

        BackupResult first = await RunAsync(temp);
        BackupResult second = await RunAsync(temp);

        Assert.Equal(temp.Resolve("out/Documents_2026-07-31_234506.tar.zst"), first.OutputPath);
        Assert.Equal(temp.Resolve("out/Documents_2026-07-31_234506_2.tar.zst"), second.OutputPath);
        Assert.True(File.Exists(first.OutputPath));
    }

    [Fact]
    public async Task RunAsync_WritesASidecarThatSha256sumCanCheck()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "hello");

        BackupResult result = await RunAsync(temp);

        string expected = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(result.OutputPath, Token)));
        string sidecar = await File.ReadAllTextAsync(result.SidecarPath, Token);

        Assert.Equal(result.OutputPath + ".sha256", result.SidecarPath);
        Assert.Equal(expected, result.Sha256);
        Assert.Equal($"{expected}  {Path.GetFileName(result.OutputPath)}\n", sidecar);
    }

    [Fact]
    public async Task RunAsync_ReportsWhatItArchived()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "hello");
        temp.WriteFile("source/nested/b.txt", "world!");

        BackupResult result = await RunAsync(temp);

        Assert.Equal(2, result.FileCount);
        Assert.Equal(2, result.DirectoryCount);
        Assert.Equal(11, result.BytesProcessed);
        Assert.Equal(new FileInfo(result.OutputPath).Length, result.ArchiveBytes);
        Assert.Empty(result.Skipped);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.ScanWarnings);
    }

    [Fact]
    public async Task RunAsync_CarriesTheScansWarningsIntoTheSummary()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "hello");

        BackupResult result = await RunAsync(temp, extraInputs: [temp.Resolve("source/a.txt")]);

        ScanWarning warning = Assert.Single(result.ScanWarnings);
        Assert.Equal(ScanWarningKind.NestedInputMerged, warning.Kind);
    }

    [Fact]
    public async Task RunAsync_ReportsProgressThroughEveryPhase()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "hello");
        temp.WriteFile("source/b.txt", "world");

        List<BackupProgress> reports = [];
        await RunAsync(temp, progress: new SynchronousProgress<BackupProgress>(reports.Add));

        Assert.Equal(BackupPhase.Scanning, reports[0].Phase);
        Assert.Equal(0, reports[0].FileCount);
        Assert.Equal(BackupPhase.Completed, reports[^1].Phase);
        Assert.Equal(1.0, reports[^1].Fraction, precision: 10);
        Assert.Equal(2, reports[^1].FilesProcessed);

        BackupProgress firstFile = reports.First(report => report.Phase == BackupPhase.Archiving);
        Assert.Equal(temp.Resolve("source/a.txt"), firstFile.CurrentPath);
        Assert.Equal(2, firstFile.FileCount);
        Assert.Equal(10, firstFile.TotalBytes);
    }

    [Fact]
    public async Task RunAsync_ReportsProgressWhileASingleLargeFileIsBeingWritten()
    {
        using TempDirectory temp = new();
        Directory.CreateDirectory(temp.Resolve("source"));
        await File.WriteAllBytesAsync(temp.Resolve("source/big.bin"), new byte[4 * 1024 * 1024], Token);

        List<BackupProgress> reports = [];
        await RunAsync(temp, progress: new SynchronousProgress<BackupProgress>(reports.Add));

        // One report per megabyte of content, so the bar moves inside a single big file instead of
        // jumping from 0 to 100 when it finishes.
        List<BackupProgress> during = reports
            .Where(report => report.Phase == BackupPhase.Archiving && report.FilesProcessed == 0 && report.BytesProcessed > 0)
            .ToList();

        Assert.True(during.Count >= 3, $"expected several mid-file reports, got {during.Count}");
        Assert.Equal(during.OrderBy(report => report.BytesProcessed).Select(report => report.BytesProcessed), during.Select(report => report.BytesProcessed));
    }

    [Fact]
    public async Task RunAsync_WhenCancelled_ThrowsAndLeavesNothingBehind()
    {
        using TempDirectory temp = new();
        for (int index = 0; index < 20; index++)
        {
            temp.WriteFile($"source/file-{index:00}.txt", new string('x', 1024));
        }

        using CancellationTokenSource cancellation = new();
        SynchronousProgress<BackupProgress> progress = new(report =>
        {
            if (report.Phase == BackupPhase.Archiving && report.FilesProcessed == 3)
            {
                cancellation.Cancel();
            }
        });

        BackupProfile profile = Profile(temp);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Pipeline.Backup(Clock()).RunAsync(profile, password: null, Strict(), progress, cancellation.Token));

        Assert.Empty(Directory.GetFiles(temp.Resolve("out")));
    }

    [Fact]
    public async Task RunAsync_WhenAnErrorIsAborted_ThrowsAndLeavesNothingBehind()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "hello");
        string denied = temp.WriteFile("source/denied.txt", "secret");
        if (!AccessDenial.TryDeny(denied))
        {
            Assert.Skip("This user can read the file regardless of its mode, so access cannot be denied.");
        }

        ScriptedErrorHandler handler = new(BackupErrorDecision.Abort());

        BackupAbortedException exception = await Assert.ThrowsAsync<BackupAbortedException>(() =>
            Pipeline.Backup(Clock()).RunAsync(Profile(temp), password: null, handler, progress: null, Token));

        Assert.Equal(BackupErrorKind.AccessDenied, Assert.Single(handler.Errors).Kind);
        Assert.Equal(denied, exception.Error?.Path);
        Assert.Empty(Directory.GetFiles(temp.Resolve("out")));
    }

    [Fact]
    public async Task RunAsync_WhenAnErrorIsSkipped_FinishesWithoutTheEntryAndListsIt()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "hello");
        string denied = temp.WriteFile("source/denied.txt", "secret");
        if (!AccessDenial.TryDeny(denied))
        {
            Assert.Skip("This user can read the file regardless of its mode, so access cannot be denied.");
        }

        ScriptedErrorHandler handler = new(BackupErrorDecision.Skip());

        BackupResult result = await Pipeline.Backup(Clock())
            .RunAsync(Profile(temp), password: null, handler, progress: null, Token);

        Assert.Equal(1, result.FileCount);
        SkippedEntry skipped = Assert.Single(result.Skipped);
        Assert.Equal(denied, skipped.Path);
        Assert.True(File.Exists(result.OutputPath));
    }

    [Fact]
    public async Task RunAsync_WhenAnErrorIsRetried_ArchivesTheEntryOnTheSecondAttempt()
    {
        using TempDirectory temp = new();
        string denied = temp.WriteFile("source/denied.txt", "secret");
        if (!AccessDenial.TryDeny(denied))
        {
            Assert.Skip("This user can read the file regardless of its mode, so access cannot be denied.");
        }

        // The user fixes what went wrong and clicks Retry, which is exactly what the handler does here.
        ScriptedErrorHandler handler = new(BackupErrorDecision.Skip(), BackupErrorDecision.Retry())
        {
            BeforeAnswering = error => AccessDenial.Restore(error.Path),
        };

        BackupResult result = await Pipeline.Backup(Clock())
            .RunAsync(Profile(temp), password: null, handler, progress: null, Token);

        Assert.Single(handler.Errors);
        Assert.Equal(1, result.FileCount);
        Assert.Empty(result.Skipped);
    }

    [Fact]
    public async Task RunAsync_AppliesAnApplyToAllAnswerAcrossTheScanAndTheWrite()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "hello");
        string denied = temp.WriteFile("source/denied.txt", "secret");
        if (!AccessDenial.TryDeny(denied))
        {
            Assert.Skip("This user can read the file regardless of its mode, so access cannot be denied.");
        }

        // A missing input fails during the scan, the unreadable file during the write: one policy spans
        // both, so "apply to all" is asked exactly once for the whole run.
        ScriptedErrorHandler handler = new(BackupErrorDecision.Abort(), BackupErrorDecision.Skip(applyToAll: true));
        BackupProfile profile = Profile(temp, extraInputs: [temp.Resolve("gone")]);

        BackupResult result = await Pipeline.Backup(Clock())
            .RunAsync(profile, password: null, handler, progress: null, Token);

        Assert.Single(handler.Errors);
        Assert.Equal(2, result.Skipped.Count);
        Assert.Equal(1, result.FileCount);
    }

    [Fact]
    public async Task RunAsync_WhenAFileShrinksMidEntry_PadsItAndWarns()
    {
        using TempDirectory temp = new();
        Directory.CreateDirectory(temp.Resolve("source"));
        string path = temp.Resolve("source/growing.log");
        await File.WriteAllBytesAsync(path, new byte[4 * 1024 * 1024], Token);

        // Truncating the file from inside a mid-file progress report is the deterministic way to make a
        // source change while its entry is being written.
        bool truncated = false;
        SynchronousProgress<BackupProgress> progress = new(report =>
        {
            if (!truncated && report.BytesProcessed > 1024 * 1024)
            {
                truncated = true;
                using FileStream file = new(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                file.SetLength(1024);
            }
        });

        BackupResult result = await Pipeline.Backup(Clock())
            .RunAsync(Profile(temp), password: null, Strict(), progress, Token);

        BackupWarning warning = Assert.Single(result.Warnings);
        Assert.Equal(BackupWarningKind.SourceShrankWhileReading, warning.Kind);
        Assert.Equal(path, warning.Path);
        Assert.Equal(4 * 1024 * 1024, result.BytesProcessed);
    }

    [Fact]
    public async Task RunAsync_WithAnEncryptingProfile_SaysEncryptionIsNotAvailableYet()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "hello");
        BackupProfile profile = Profile(temp);
        profile.Encrypt = true;

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            Pipeline.Backup(Clock()).RunAsync(profile, "password", Strict(), progress: null, Token));

        Assert.False(Directory.Exists(temp.Resolve("out")));
    }

    [Fact]
    public async Task RunAsync_WithoutAnOutputDirectory_Throws()
    {
        using TempDirectory temp = new();
        BackupProfile profile = Profile(temp);
        profile.OutputDirectory = "   ";

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Pipeline.Backup(Clock()).RunAsync(profile, password: null, Strict(), progress: null, Token));
    }

    [Fact]
    public async Task RunAsync_WithoutAProfileOrAHandler_Throws()
    {
        using TempDirectory temp = new();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Pipeline.Backup(Clock()).RunAsync(null!, password: null, Strict(), progress: null, Token));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Pipeline.Backup(Clock()).RunAsync(Profile(temp), password: null, null!, progress: null, Token));
    }

    private static Task<BackupResult> RunAsync(
        TempDirectory temp,
        string profileName = "Documents",
        IProgress<BackupProgress>? progress = null,
        string[]? extraInputs = null) =>
        Pipeline.Backup(Clock()).RunAsync(
            Profile(temp, profileName, extraInputs),
            password: null,
            Strict(),
            progress,
            Token);

    private static BackupProfile Profile(TempDirectory temp, string profileName = "Documents", string[]? extraInputs = null) =>
        Pipeline.Profile(
            profileName,
            temp.Resolve("out"),
            CompressionAlgorithm.Zstd,
            [temp.Resolve("source"), .. extraInputs ?? []]);

    private static TimeProvider Clock() => new FixedTimeProvider(RunTime);

    private static IBackupErrorHandler Strict() => new ScriptedErrorHandler(BackupErrorDecision.Abort());
}
