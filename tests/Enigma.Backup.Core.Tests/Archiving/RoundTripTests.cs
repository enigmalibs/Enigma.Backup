using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Backups;
using Enigma.Backup.Core.Errors;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Core.Restoring;
using Enigma.Backup.Core.Tests.TestSupport;
using Enigma.Backup.Core.Verification;
using Xunit;

namespace Enigma.Backup.Core.Tests.Archiving;

/// <summary>
/// The acceptance criterion of the archive pipeline: what goes in comes back out, byte for byte, for
/// every compression algorithm the app offers.
/// </summary>
public sealed class RoundTripTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(CompressionAlgorithm.Zstd)]
    [InlineData(CompressionAlgorithm.Gzip)]
    [InlineData(CompressionAlgorithm.Zip)]
    public async Task BackupVerifyRestore_ReproducesTheInputTree(CompressionAlgorithm compression)
    {
        using TempDirectory temp = new();
        CreateSourceTree(temp);

        BackupResult backup = await RunBackupAsync(temp, compression);

        VerificationResult verification = await Pipeline.Verification()
            .VerifyAsync(backup.OutputPath, password: null, progress: null, Token);
        Assert.Equal(VerificationOutcome.Succeeded, verification.Outcome);

        RestoreResult restore = await RestoreAsync(backup.OutputPath, temp.Resolve("restored"));

        Assert.Equal(RestoreOutcome.Completed, restore.Outcome);
        Assert.Empty(restore.Skipped);
        Assert.Equal(
            FileTree.Snapshot(temp.Resolve("source")),
            FileTree.Snapshot(temp.Resolve("restored/source")));
    }

    [Theory]
    [InlineData(CompressionAlgorithm.Zstd)]
    [InlineData(CompressionAlgorithm.Gzip)]
    public async Task Backup_WritesAStandardCompressedTarball(CompressionAlgorithm compression)
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "hello");

        BackupResult backup = await RunBackupAsync(temp, compression);

        // The extension and the magic bytes both have to say the same thing: an unencrypted archive is
        // a plain .tar.zst / .tar.gz that stock tools can open without this application.
        byte[] leading = new byte[4];
        using (FileStream file = File.OpenRead(backup.OutputPath))
        {
            Assert.Equal(4, file.ReadAtLeast(leading, 4, throwOnEndOfStream: false));
        }

        if (compression == CompressionAlgorithm.Zstd)
        {
            Assert.EndsWith(".tar.zst", backup.OutputPath, StringComparison.Ordinal);
            Assert.Equal(new byte[] { 0x28, 0xB5, 0x2F, 0xFD }, leading);
        }
        else
        {
            Assert.EndsWith(".tar.gz", backup.OutputPath, StringComparison.Ordinal);
            Assert.Equal(new byte[] { 0x1F, 0x8B }, leading[..2]);
        }
    }

    [Fact]
    public async Task Restore_KeepsFileAndDirectoryTimestamps()
    {
        using TempDirectory temp = new();
        string file = temp.WriteFile("source/nested/a.txt", "hello");
        DateTimeOffset fileTime = new(2019, 4, 3, 11, 22, 33, TimeSpan.Zero);
        DateTimeOffset directoryTime = new(2018, 2, 1, 9, 8, 7, TimeSpan.Zero);
        File.SetLastWriteTimeUtc(file, fileTime.UtcDateTime);
        Directory.SetLastWriteTimeUtc(temp.Resolve("source/nested"), directoryTime.UtcDateTime);

        BackupResult backup = await RunBackupAsync(temp, CompressionAlgorithm.Zstd);
        await RestoreAsync(backup.OutputPath, temp.Resolve("restored"));

        Assert.Equal(fileTime, FileTree.LastWriteTime(temp.Resolve("restored/source/nested/a.txt")), TimeSpan.FromSeconds(1));
        Assert.Equal(directoryTime, FileTree.LastWriteTime(temp.Resolve("restored/source/nested")), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Restore_KeepsTheExecutableBit()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not exist on Windows.");
        }
        else
        {
            using TempDirectory temp = new();
            string script = temp.WriteFile("source/run.sh", "#!/bin/sh\necho hi\n");
            temp.WriteFile("source/plain.txt", "hello");
            const UnixFileMode executable = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
            File.SetUnixFileMode(script, executable);

            BackupResult backup = await RunBackupAsync(temp, CompressionAlgorithm.Zstd);
            await RestoreAsync(backup.OutputPath, temp.Resolve("restored"));

            Assert.Equal(executable, File.GetUnixFileMode(temp.Resolve("restored/source/run.sh")));
            Assert.False(File.GetUnixFileMode(temp.Resolve("restored/source/plain.txt")).HasFlag(UnixFileMode.UserExecute));
        }
    }

    [Fact]
    public async Task BackupAndRestore_SurviveAFileLargerThanEveryBuffer()
    {
        using TempDirectory temp = new();
        byte[] contents = new byte[5 * 1024 * 1024];
        Random.Shared.NextBytes(contents);
        Directory.CreateDirectory(temp.Resolve("source"));
        await File.WriteAllBytesAsync(temp.Resolve("source/big.bin"), contents, Token);

        BackupResult backup = await RunBackupAsync(temp, CompressionAlgorithm.Zstd);
        await RestoreAsync(backup.OutputPath, temp.Resolve("restored"));

        Assert.Equal(contents.Length, backup.BytesProcessed);
        byte[] restored = await File.ReadAllBytesAsync(temp.Resolve("restored/source/big.bin"), Token);
        Assert.Equal(SHA256.HashData(contents), SHA256.HashData(restored));
    }

    [Fact]
    public async Task Backup_WithSeveralInputs_RestoresEachUnderItsOwnTopLevelName()
    {
        using TempDirectory temp = new();
        temp.WriteFile("photos/one.jpg", "one");
        temp.WriteFile("papers/two.pdf", "two");
        string loose = temp.WriteFile("notes.txt", "three");

        BackupProfile profile = Pipeline.Profile(
            "Everything",
            temp.Resolve("out"),
            CompressionAlgorithm.Zstd,
            temp.Resolve("photos"),
            temp.Resolve("papers"),
            loose);

        BackupResult backup = await Pipeline.Backup()
            .RunAsync(profile, password: null, StrictHandler(), progress: null, Token);
        await RestoreAsync(backup.OutputPath, temp.Resolve("restored"));

        Assert.Equal(
            new[] { "notes.txt", "papers", "papers/two.pdf", "photos", "photos/one.jpg" },
            FileTree.Snapshot(temp.Resolve("restored")).Keys.ToArray());
    }

    [Fact]
    public async Task Backup_OfAnEmptyDirectoryOnly_StillRoundTrips()
    {
        using TempDirectory temp = new();
        temp.CreateDirectory("source/nothing/in/here");

        BackupResult backup = await RunBackupAsync(temp, CompressionAlgorithm.Gzip);
        await RestoreAsync(backup.OutputPath, temp.Resolve("restored"));

        Assert.Equal(0, backup.FileCount);
        Assert.Equal(4, backup.DirectoryCount);
        Assert.Equal(
            new[] { "source", "source/nothing", "source/nothing/in", "source/nothing/in/here" },
            FileTree.Snapshot(temp.Resolve("restored")).Keys.ToArray());
    }

    /// <summary>A tree with everything the pipeline has to survive: odd names, empty files, empty
    /// directories, binary content and a path too long for a classic tar header.</summary>
    private static void CreateSourceTree(TempDirectory temp)
    {
        temp.WriteFile("source/a.txt", "hello");
        temp.WriteFile("source/empty.txt", string.Empty);
        temp.WriteFile("source/.hidden", "dotfile");
        temp.WriteFile("source/spaces and 'quotes' #hash.txt", "odd name");
        temp.WriteFile("source/ünïcodé-名前.txt", "unicode name");
        temp.WriteFile($"source/nested/deep/{new string('n', 150)}.txt", "a name no classic tar header can hold");
        temp.CreateDirectory("source/empty-dir");

        byte[] binary = new byte[64 * 1024];
        Random.Shared.NextBytes(binary);
        File.WriteAllBytes(temp.Resolve("source/nested/deep/data.bin"), binary);
    }

    private static Task<BackupResult> RunBackupAsync(TempDirectory temp, CompressionAlgorithm compression)
    {
        BackupProfile profile = Pipeline.Profile("Documents", temp.Resolve("out"), compression, temp.Resolve("source"));

        return Pipeline.Backup().RunAsync(profile, password: null, StrictHandler(), progress: null, Token);
    }

    private static Task<RestoreResult> RestoreAsync(string archivePath, string destination) =>
        Pipeline.Restore().RestoreAsync(
            archivePath,
            destination,
            password: null,
            new ScriptedConflictHandler(RestoreConflictDecision.Abort()),
            progress: null,
            Token);

    /// <summary>A handler that aborts, so an unexpected error fails the test instead of being skipped.</summary>
    private static IBackupErrorHandler StrictHandler() => new ScriptedErrorHandler(BackupErrorDecision.Abort());
}
