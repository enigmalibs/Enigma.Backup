using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Archiving;
using Enigma.Backup.Core.Backups;
using Enigma.Backup.Core.Errors;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Core.Restoring;
using Enigma.Backup.Core.Tests.TestSupport;
using Enigma.Backup.Core.Verification;
using Xunit;

namespace Enigma.Backup.Core.Tests.Archiving;

/// <summary>
/// What the zip format has to deliver that the tar formats do not cover: a file a stock unzip tool
/// opens, the two signatures a zip can start with, the fidelity a DOS timestamp allows, and a restore
/// that walks hostile entry names down the same decision path as a tarball.
/// </summary>
public sealed class ZipFormatTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Backup_WritesAPlainZipWithEveryEntryTheScanFound()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "hello");
        temp.WriteFile("source/nested/b.txt", "world");
        temp.CreateDirectory("source/empty-dir");

        BackupResult backup = await BackupAsync(temp);

        Assert.EndsWith(".zip", backup.OutputPath, StringComparison.Ordinal);
        Assert.Equal(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, await LeadingBytesAsync(backup.OutputPath));

        // Read back through the stock reader, not ours: a folder is a trailing slash and nothing else,
        // and every name is forward-slashed whatever the platform separator is.
        using ZipArchive zip = ZipFile.OpenRead(backup.OutputPath);

        Assert.Equal(
            ["source/", "source/a.txt", "source/empty-dir/", "source/nested/", "source/nested/b.txt"],
            zip.Entries.Select(static entry => entry.FullName).Order(StringComparer.Ordinal));
        Assert.Equal("hello", await ReadEntryAsync(zip, "source/a.txt"));
    }

    [Fact]
    public async Task Backup_WritesTheSidecarOverTheZipsOwnBytes()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "hello");

        BackupResult backup = await BackupAsync(temp);

        byte[] written = await File.ReadAllBytesAsync(backup.OutputPath, Token);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(written)), backup.Sha256);
        Assert.True(File.Exists(backup.SidecarPath));

        VerificationResult verification = await Pipeline.Verification()
            .VerifyAsync(backup.OutputPath, password: null, progress: null, Token);
        Assert.Equal(VerificationOutcome.Succeeded, verification.Outcome);
    }

    [Fact]
    public async Task BackupAndRestore_KeepATimestampToTheTwoSecondsAZipCanHold()
    {
        using TempDirectory temp = new();
        string file = temp.WriteFile("source/nested/a.txt", "hello");
        DateTimeOffset fileTime = new(2019, 4, 3, 11, 22, 34, TimeSpan.Zero);
        DateTimeOffset directoryTime = new(2018, 2, 1, 9, 8, 6, TimeSpan.Zero);
        File.SetLastWriteTimeUtc(file, fileTime.UtcDateTime);
        Directory.SetLastWriteTimeUtc(temp.Resolve("source/nested"), directoryTime.UtcDateTime);

        BackupResult backup = await BackupAsync(temp);
        await RestoreAsync(backup.OutputPath, temp.Resolve("restored"));

        // Two seconds, not one: a DOS date stores the seconds field halved, so an odd second is the
        // resolution's own rounding rather than a lost timestamp.
        Assert.Equal(fileTime, FileTree.LastWriteTime(temp.Resolve("restored/source/nested/a.txt")), TimeSpan.FromSeconds(2));
        Assert.Equal(directoryTime, FileTree.LastWriteTime(temp.Resolve("restored/source/nested")), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Backup_ClampsATimestampADosDateCannotHold_RatherThanFailingTheRun()
    {
        using TempDirectory temp = new();
        string ancient = temp.WriteFile("source/ancient.txt", "before zip existed");
        string forged = temp.WriteFile("source/forged.txt", "long after this build");
        File.SetLastWriteTimeUtc(ancient, new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(forged, new DateTime(2200, 6, 15, 12, 0, 0, DateTimeKind.Utc));

        BackupResult backup = await BackupAsync(temp);

        Assert.Equal(2, backup.FileCount);

        using ZipArchive zip = ZipFile.OpenRead(backup.OutputPath);

        Assert.Equal(1980, EntryYear(zip, "source/ancient.txt"));
        Assert.Equal(2107, EntryYear(zip, "source/forged.txt"));
    }

    [Fact]
    public async Task Backup_WhenEveryInputWasSkipped_WritesAnEmptyZipThatStillRestores()
    {
        using TempDirectory temp = new();

        BackupProfile profile = Pipeline.Profile("Nothing", temp.Resolve("out"), CompressionAlgorithm.Zip, temp.Resolve("gone"));
        BackupResult backup = await Pipeline.Backup()
            .RunAsync(profile, password: null, new ScriptedErrorHandler(BackupErrorDecision.Skip()), progress: null, Token);

        Assert.Equal(0, backup.FileCount);
        Assert.Equal(0, backup.DirectoryCount);

        // A zip with no entries has no local file header to start with, so it opens with the
        // end-of-central-directory record instead — the second signature the detector has to know.
        Assert.Equal(new byte[] { 0x50, 0x4B, 0x05, 0x06 }, await LeadingBytesAsync(backup.OutputPath));

        RestoreResult restore = await RestoreAsync(backup.OutputPath, temp.Resolve("restored"));

        Assert.Equal(RestoreOutcome.Completed, restore.Outcome);
        Assert.Equal(0, restore.FileCount);
        Assert.Empty(Directory.GetFileSystemEntries(temp.Resolve("restored")));
    }

    [Fact]
    public async Task Restore_WhenAConflictIsSkipped_KeepsTheFileOnDiskAndListsIt()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "from the archive");
        BackupResult backup = await BackupAsync(temp);
        string existing = temp.WriteFile("restored/source/a.txt", "mine");

        ScriptedConflictHandler handler = new(RestoreConflictDecision.Skip());
        RestoreResult result = await RestoreAsync(backup.OutputPath, temp.Resolve("restored"), handler);

        Assert.Equal(0, result.FileCount);
        Assert.Equal(existing, Assert.Single(result.Skipped).Path);
        Assert.Equal("mine", await File.ReadAllTextAsync(existing, Token));
    }

    [Fact]
    public async Task Restore_WhenAConflictIsOverwritten_ReplacesTheFileAndReportsTheArchivedLength()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "from the archive");
        BackupResult backup = await BackupAsync(temp);
        string existing = temp.WriteFile("restored/source/a.txt", "mine, and much longer than the archived one");

        ScriptedConflictHandler handler = new(RestoreConflictDecision.Overwrite());
        RestoreResult result = await RestoreAsync(backup.OutputPath, temp.Resolve("restored"), handler);

        RestoreConflict conflict = Assert.Single(handler.Conflicts);
        Assert.Equal("source/a.txt", conflict.ArchivePath);
        Assert.Equal(16, conflict.ArchiveLength);
        Assert.Equal(1, result.FileCount);
        Assert.Equal("from the archive", await File.ReadAllTextAsync(existing, Token));
    }

    [Fact]
    public async Task Restore_WhenAConflictIsAborted_StopsAndKeepsWhatItHadAlreadyWritten()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "first");
        temp.WriteFile("source/b.txt", "second");
        temp.WriteFile("source/c.txt", "third");
        BackupResult backup = await BackupAsync(temp);
        temp.WriteFile("restored/source/b.txt", "mine");

        ScriptedConflictHandler handler = new(RestoreConflictDecision.Abort());
        RestoreResult result = await RestoreAsync(backup.OutputPath, temp.Resolve("restored"), handler);

        Assert.Equal(RestoreOutcome.Aborted, result.Outcome);
        Assert.Equal(1, result.FileCount);
        Assert.True(File.Exists(temp.Resolve("restored/source/a.txt")));
        Assert.False(File.Exists(temp.Resolve("restored/source/c.txt")));
        Assert.Equal("mine", await File.ReadAllTextAsync(temp.Resolve("restored/source/b.txt"), Token));
    }

    [Fact]
    public async Task Restore_WithApplyToAll_AsksAboutTheFirstConflictOnly()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "from the archive");
        temp.WriteFile("source/b.txt", "from the archive");
        BackupResult backup = await BackupAsync(temp);
        temp.WriteFile("restored/source/a.txt", "mine");
        temp.WriteFile("restored/source/b.txt", "mine");

        ScriptedConflictHandler handler = new(RestoreConflictDecision.Abort(), RestoreConflictDecision.Overwrite(applyToAll: true));
        RestoreResult result = await RestoreAsync(backup.OutputPath, temp.Resolve("restored"), handler);

        Assert.Single(handler.Conflicts);
        Assert.Equal(2, result.FileCount);
        Assert.Equal("from the archive", await File.ReadAllTextAsync(temp.Resolve("restored/source/b.txt"), Token));
    }

    [Theory]
    [InlineData("../escaped.txt")]
    [InlineData("source/../../escaped.txt")]
    [InlineData("source/./a.txt")]
    public async Task Restore_RefusesAZipEntryThatWouldNotLandInTheDestination(string entryName)
    {
        using TempDirectory temp = new();
        string archive = temp.Resolve("out/hand-made.zip");
        await WriteZipAsync(archive, (entryName, "gotcha"));

        ArchiveFormatException exception = await Assert.ThrowsAsync<ArchiveFormatException>(() =>
            RestoreAsync(archive, temp.Resolve("restored")));

        Assert.Contains("destination", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(temp.Resolve("escaped.txt")));
        Assert.Empty(Directory.GetFiles(temp.Resolve("restored"), "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Restore_StripsALeadingSeparatorSoAnAbsoluteZipEntryLandsInTheDestination()
    {
        using TempDirectory temp = new();
        string archive = temp.Resolve("out/hand-made.zip");
        await WriteZipAsync(archive, ("/etc/config.conf", "not yours"));

        RestoreResult result = await RestoreAsync(archive, temp.Resolve("restored"));

        Assert.Equal(1, result.FileCount);
        Assert.Equal("not yours", await File.ReadAllTextAsync(temp.Resolve("restored/etc/config.conf"), Token));
    }

    [Fact]
    public async Task Restore_OfADamagedZip_SaysItIsIncomplete()
    {
        using TempDirectory temp = new();
        for (int index = 0; index < 20; index++)
        {
            temp.WriteFile($"source/file-{index:00}.txt", new string((char)('a' + (index % 26)), 4096));
        }

        BackupResult backup = await BackupAsync(temp);
        using (FileStream file = new(backup.OutputPath, FileMode.Open, FileAccess.Write))
        {
            // Cut the central directory off: the entry data is still there, but nothing says what it is.
            file.SetLength(file.Length / 2);
        }

        ArchiveFormatException exception = await Assert.ThrowsAsync<ArchiveFormatException>(() =>
            RestoreAsync(backup.OutputPath, temp.Resolve("restored")));

        Assert.Contains("damaged or incomplete", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Backup_OfAProfileAskingForAnEncryptedZip_IsRefusedBeforeAnythingIsWritten()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "hello");

        BackupProfile profile = Pipeline.Profile("Documents", temp.Resolve("out"), CompressionAlgorithm.Zip, temp.Resolve("source"));
        profile.Encrypt = true;

        ProfileFormatException exception = await Assert.ThrowsAsync<ProfileFormatException>(() => Pipeline.Backup()
            .RunAsync(profile, "a password", new ScriptedErrorHandler(BackupErrorDecision.Abort()), progress: null, Token));

        Assert.Contains("zip", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(temp.Resolve("out")));
    }

    [Fact]
    public async Task Restore_OfAZipInsideAnEncryptedContainer_IsRefusedRatherThanGuessedAt()
    {
        using TempDirectory temp = new();
        string archive = temp.Resolve("out/wrapped.tar.zst.enc");
        Directory.CreateDirectory(temp.Resolve("out"));

        // Nothing writes this, which is the point: an EBK1 container holds a compressed tar stream, and
        // a payload that is something else is reported instead of being opened hopefully.
        await File.WriteAllBytesAsync(archive, Ebk1Writer.Create("secret", [0x50, 0x4B, 0x05, 0x06, 0, 0, 0, 0]), Token);

        ArchiveFormatException exception = await Assert.ThrowsAsync<ArchiveFormatException>(() => Pipeline.Restore()
            .RestoreAsync(
                archive,
                temp.Resolve("restored"),
                "secret",
                new ScriptedConflictHandler(RestoreConflictDecision.Abort()),
                progress: null,
                Token));

        Assert.Contains("another archive", exception.Message, StringComparison.Ordinal);
    }

    private static Task<BackupResult> BackupAsync(TempDirectory temp)
    {
        BackupProfile profile = Pipeline.Profile(
            "Documents",
            temp.Resolve("out"),
            CompressionAlgorithm.Zip,
            temp.Resolve("source"));

        return Pipeline.Backup().RunAsync(
            profile,
            password: null,
            new ScriptedErrorHandler(BackupErrorDecision.Abort()),
            progress: null,
            Token);
    }

    private static Task<RestoreResult> RestoreAsync(string archive, string destination, IRestoreConflictHandler? handler = null) =>
        Pipeline.Restore().RestoreAsync(
            archive,
            destination,
            password: null,
            handler ?? new ScriptedConflictHandler(RestoreConflictDecision.Abort()),
            progress: null,
            Token);

    private static async Task<byte[]> LeadingBytesAsync(string path)
    {
        byte[] leading = new byte[4];

        await using FileStream file = File.OpenRead(path);
        Assert.Equal(4, await file.ReadAtLeastAsync(leading, 4, throwOnEndOfStream: false, Token));

        return leading;
    }

    private static async Task<string> ReadEntryAsync(ZipArchive zip, string entryName)
    {
        ZipArchiveEntry entry = Assert.IsType<ZipArchiveEntry>(zip.GetEntry(entryName));

        await using Stream data = entry.Open();
        using StreamReader reader = new(data, Encoding.UTF8);

        return await reader.ReadToEndAsync(Token);
    }

    /// <summary>The year a zip entry's DOS timestamp records, as the stock reader hands it back.</summary>
    private static int EntryYear(ZipArchive zip, string entryName) =>
        Assert.IsType<ZipArchiveEntry>(zip.GetEntry(entryName)).LastWriteTime.Year;

    /// <summary>
    /// Writes a zip by hand, so a restore can be given the entry names a well-behaved backup would
    /// never produce.
    /// </summary>
    private static async Task WriteZipAsync(string path, params (string Name, string? Content)[] entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using FileStream file = File.Create(path);
        using ZipArchive zip = new(file, ZipArchiveMode.Create, leaveOpen: true);

        foreach ((string name, string? content) in entries)
        {
            ZipArchiveEntry entry = zip.CreateEntry(name);

            if (content is not null)
            {
                await using Stream target = entry.Open();

                await target.WriteAsync(Encoding.UTF8.GetBytes(content), Token);
            }
        }
    }
}
