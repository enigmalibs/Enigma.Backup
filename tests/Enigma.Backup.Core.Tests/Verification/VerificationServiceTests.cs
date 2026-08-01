using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Backups;
using Enigma.Backup.Core.Errors;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Core.Tests.TestSupport;
using Enigma.Backup.Core.Verification;
using Xunit;

namespace Enigma.Backup.Core.Tests.Verification;

public sealed class VerificationServiceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task VerifyAsync_OfAnUntouchedArchive_Succeeds()
    {
        using TempDirectory temp = new();
        BackupResult backup = await BackupAsync(temp);

        VerificationResult result = await VerifyAsync(backup.OutputPath);

        Assert.Equal(VerificationOutcome.Succeeded, result.Outcome);
        Assert.True(result.Succeeded);
        Assert.Equal(backup.Sha256, result.ActualSha256);
        Assert.Equal(backup.Sha256, result.ExpectedSha256);
        Assert.Equal(new FileInfo(backup.OutputPath).Length, result.BytesRead);
        Assert.Contains("is intact", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyAsync_OfAnArchiveWithOneFlippedBit_ReportsAMismatch()
    {
        using TempDirectory temp = new();
        BackupResult backup = await BackupAsync(temp);

        // Bit rot, a bad copy, a tampered file: whatever the cause, the bytes no longer hash the same.
        using (FileStream file = new(backup.OutputPath, FileMode.Open, FileAccess.ReadWrite))
        {
            file.Seek(file.Length / 2, SeekOrigin.Begin);
            int original = file.ReadByte();
            file.Seek(file.Length / 2, SeekOrigin.Begin);
            file.WriteByte((byte)(original ^ 0x01));
        }

        VerificationResult result = await VerifyAsync(backup.OutputPath);

        Assert.Equal(VerificationOutcome.HashMismatch, result.Outcome);
        Assert.False(result.Succeeded);
        Assert.Equal(backup.Sha256, result.ExpectedSha256);
        Assert.NotEqual(backup.Sha256, result.ActualSha256);
        Assert.Contains("has changed", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyAsync_OfAnArchiveWithATamperedSidecar_ReportsAMismatch()
    {
        using TempDirectory temp = new();
        BackupResult backup = await BackupAsync(temp);
        await File.WriteAllTextAsync(backup.SidecarPath, $"{new string('0', 64)}  {Path.GetFileName(backup.OutputPath)}\n", Token);

        VerificationResult result = await VerifyAsync(backup.OutputPath);

        Assert.Equal(VerificationOutcome.HashMismatch, result.Outcome);
        Assert.Equal(new string('0', 64), result.ExpectedSha256);
    }

    [Fact]
    public async Task VerifyAsync_WithoutASidecar_SaysThereIsNothingToCompareAgainst()
    {
        using TempDirectory temp = new();
        BackupResult backup = await BackupAsync(temp);
        File.Delete(backup.SidecarPath);

        VerificationResult result = await VerifyAsync(backup.OutputPath);

        Assert.Equal(VerificationOutcome.SidecarMissing, result.Outcome);
        Assert.Null(result.ExpectedSha256);
        Assert.Null(result.ActualSha256);
        Assert.Equal(0, result.BytesRead);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a digest at all\n")]
    [InlineData("deadbeef  backup.tar.zst\n")]
    public async Task VerifyAsync_WithAnUnreadableSidecar_SaysSo(string contents)
    {
        using TempDirectory temp = new();
        BackupResult backup = await BackupAsync(temp);
        await File.WriteAllTextAsync(backup.SidecarPath, contents, Token);

        VerificationResult result = await VerifyAsync(backup.OutputPath);

        Assert.Equal(VerificationOutcome.SidecarUnreadable, result.Outcome);
    }

    [Fact]
    public async Task VerifyAsync_AcceptsTheBinaryModeSidecarFormat()
    {
        using TempDirectory temp = new();
        BackupResult backup = await BackupAsync(temp);

        // sha256sum writes "hash *name" in binary mode and "hash  name" in text mode; both are its own
        // output, so both have to be readable here.
        await File.WriteAllTextAsync(
            backup.SidecarPath,
            $"{backup.Sha256.ToUpperInvariant()} *{Path.GetFileName(backup.OutputPath)}\n",
            Token);

        VerificationResult result = await VerifyAsync(backup.OutputPath);

        Assert.Equal(VerificationOutcome.Succeeded, result.Outcome);
    }

    [Fact]
    public async Task VerifyAsync_ReportsProgressUpToTheWholeArchive()
    {
        using TempDirectory temp = new();
        BackupResult backup = await BackupAsync(temp, fileCount: 40, fileSize: 200 * 1024);

        List<VerificationProgress> reports = [];
        VerificationResult result = await Pipeline.Verification()
            .VerifyAsync(backup.OutputPath, password: null, new SynchronousProgress<VerificationProgress>(reports.Add), Token);

        Assert.Equal(VerificationOutcome.Succeeded, result.Outcome);
        Assert.Equal(0, reports[0].BytesProcessed);
        Assert.Equal(new FileInfo(backup.OutputPath).Length, reports[^1].BytesProcessed);
        Assert.Equal(1.0, reports[^1].Fraction, precision: 10);
        Assert.Equal(reports.Select(report => report.BytesProcessed).OrderBy(bytes => bytes), reports.Select(report => report.BytesProcessed));
    }

    [Fact]
    public async Task VerifyAsync_OfAnArchiveThatIsNotThere_Throws()
    {
        using TempDirectory temp = new();

        await Assert.ThrowsAsync<FileNotFoundException>(() => VerifyAsync(temp.Resolve("out/nothing.tar.zst")));
        await Assert.ThrowsAsync<ArgumentException>(() => VerifyAsync("  "));
    }

    private static async Task<BackupResult> BackupAsync(TempDirectory temp, int fileCount = 1, int fileSize = 32)
    {
        for (int index = 0; index < fileCount; index++)
        {
            temp.WriteFile($"source/file-{index:00}.txt", new string((char)('a' + (index % 26)), fileSize));
        }

        BackupProfile profile = Pipeline.Profile("Documents", temp.Resolve("out"), CompressionAlgorithm.Zstd, temp.Resolve("source"));

        return await Pipeline.Backup()
            .RunAsync(profile, password: null, new ScriptedErrorHandler(BackupErrorDecision.Abort()), progress: null, Token);
    }

    private static Task<VerificationResult> VerifyAsync(string archivePath) =>
        Pipeline.Verification().VerifyAsync(archivePath, password: null, progress: null, Token);
}
