using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Archiving;
using Enigma.Backup.Core.Backups;
using Enigma.Backup.Core.Encryption;
using Enigma.Backup.Core.Errors;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Core.Restoring;
using Enigma.Backup.Core.Tests.TestSupport;
using Xunit;

namespace Enigma.Backup.Core.Tests.Restoring;

public sealed class RestoreServiceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RestoreAsync_CreatesTheDestinationAndEveryParentDirectory()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/nested/deep/a.txt", "hello");
        string archive = await BackupAsync(temp);

        RestoreResult result = await RestoreAsync(archive, temp.Resolve("restored/not/there/yet"));

        Assert.Equal(RestoreOutcome.Completed, result.Outcome);
        Assert.Equal("hello", await File.ReadAllTextAsync(temp.Resolve("restored/not/there/yet/source/nested/deep/a.txt"), Token));
    }

    [Fact]
    public async Task RestoreAsync_WritesFilesThatAreNotThereWithoutAsking()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "from the archive");
        string archive = await BackupAsync(temp);
        temp.WriteFile("restored/source/other.txt", "already here");

        ScriptedConflictHandler handler = new(RestoreConflictDecision.Abort());
        RestoreResult result = await RestoreAsync(archive, temp.Resolve("restored"), handler);

        Assert.Empty(handler.Conflicts);
        Assert.Equal(1, result.FileCount);
        Assert.Equal("already here", await File.ReadAllTextAsync(temp.Resolve("restored/source/other.txt"), Token));
        Assert.Equal("from the archive", await File.ReadAllTextAsync(temp.Resolve("restored/source/a.txt"), Token));
    }

    [Fact]
    public async Task RestoreAsync_WhenAConflictIsSkipped_KeepsTheFileOnDiskAndListsIt()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "from the archive");
        string archive = await BackupAsync(temp);
        string existing = temp.WriteFile("restored/source/a.txt", "mine");

        ScriptedConflictHandler handler = new(RestoreConflictDecision.Skip());
        RestoreResult result = await RestoreAsync(archive, temp.Resolve("restored"), handler);

        Assert.Equal(0, result.FileCount);
        Assert.Equal(existing, Assert.Single(result.Skipped).Path);
        Assert.Equal("mine", await File.ReadAllTextAsync(existing, Token));
    }

    [Fact]
    public async Task RestoreAsync_WhenAConflictIsOverwritten_ReplacesTheFile()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "from the archive");
        string archive = await BackupAsync(temp);
        string existing = temp.WriteFile("restored/source/a.txt", "mine, and much longer than the archived one");

        ScriptedConflictHandler handler = new(RestoreConflictDecision.Overwrite());
        RestoreResult result = await RestoreAsync(archive, temp.Resolve("restored"), handler);

        RestoreConflict conflict = Assert.Single(handler.Conflicts);
        Assert.Equal("source/a.txt", conflict.ArchivePath);
        Assert.Equal(16, conflict.ArchiveLength);
        Assert.True(conflict.ExistingLength > conflict.ArchiveLength);
        Assert.Equal(1, result.FileCount);
        Assert.Equal("from the archive", await File.ReadAllTextAsync(existing, Token));
    }

    [Fact]
    public async Task RestoreAsync_WhenAConflictIsAborted_StopsAndKeepsWhatItHadAlreadyWritten()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "first");
        temp.WriteFile("source/b.txt", "second");
        temp.WriteFile("source/c.txt", "third");
        string archive = await BackupAsync(temp);
        temp.WriteFile("restored/source/b.txt", "mine");

        ScriptedConflictHandler handler = new(RestoreConflictDecision.Abort());
        RestoreResult result = await RestoreAsync(archive, temp.Resolve("restored"), handler);

        Assert.Equal(RestoreOutcome.Aborted, result.Outcome);
        Assert.Equal(1, result.FileCount);
        Assert.True(File.Exists(temp.Resolve("restored/source/a.txt")));
        Assert.False(File.Exists(temp.Resolve("restored/source/c.txt")));
        Assert.Equal("mine", await File.ReadAllTextAsync(temp.Resolve("restored/source/b.txt"), Token));
    }

    [Fact]
    public async Task RestoreAsync_WithApplyToAll_AsksAboutTheFirstConflictOnly()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "from the archive");
        temp.WriteFile("source/b.txt", "from the archive");
        string archive = await BackupAsync(temp);
        temp.WriteFile("restored/source/a.txt", "mine");
        temp.WriteFile("restored/source/b.txt", "mine");

        ScriptedConflictHandler handler = new(RestoreConflictDecision.Abort(), RestoreConflictDecision.Overwrite(applyToAll: true));
        RestoreResult result = await RestoreAsync(archive, temp.Resolve("restored"), handler);

        Assert.Single(handler.Conflicts);
        Assert.Equal(2, result.FileCount);
        Assert.Equal("from the archive", await File.ReadAllTextAsync(temp.Resolve("restored/source/b.txt"), Token));
    }

    [Fact]
    public async Task RestoreAsync_DoesNotWriteAFileOverADirectory()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "from the archive");
        string archive = await BackupAsync(temp);
        temp.CreateDirectory("restored/source/a.txt");

        RestoreResult result = await RestoreAsync(archive, temp.Resolve("restored"));

        Assert.Equal(0, result.FileCount);
        Assert.Contains("is a directory", Assert.Single(result.Skipped).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RestoreAsync_StripsALeadingSeparatorSoAnAbsoluteEntryLandsInTheDestination()
    {
        using TempDirectory temp = new();
        string archive = temp.Resolve("out/hand-made.tar.gz");
        await WriteTarballAsync(archive, (TarEntryType.RegularFile, "/etc/config.conf", "not yours"));

        RestoreResult result = await RestoreAsync(archive, temp.Resolve("restored"));

        // What tar itself does with an absolute member name: drop the leading slash and stay inside the
        // directory that was asked for.
        Assert.Equal(1, result.FileCount);
        Assert.Equal("not yours", await File.ReadAllTextAsync(temp.Resolve("restored/etc/config.conf"), Token));
    }

    [Theory]
    [InlineData("../escaped.txt")]
    [InlineData("source/../../escaped.txt")]
    [InlineData("source/./a.txt")]
    public async Task RestoreAsync_RefusesAnEntryThatWouldNotLandInTheDestination(string entryName)
    {
        using TempDirectory temp = new();
        string archive = temp.Resolve("out/hand-made.tar.gz");
        await WriteTarballAsync(archive, (TarEntryType.RegularFile, entryName, "gotcha"));

        ArchiveFormatException exception = await Assert.ThrowsAsync<ArchiveFormatException>(() =>
            RestoreAsync(archive, temp.Resolve("restored")));

        Assert.Contains("destination", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(temp.Resolve("escaped.txt")));
        Assert.Empty(Directory.GetFiles(temp.Resolve("restored"), "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task RestoreAsync_ReportsAnEntryTypeItDoesNotWrite()
    {
        using TempDirectory temp = new();
        string archive = temp.Resolve("out/hand-made.tar.gz");
        await WriteTarballAsync(
            archive,
            (TarEntryType.RegularFile, "source/a.txt", "hello"),
            (TarEntryType.SymbolicLink, "source/link.txt", null));

        RestoreResult result = await RestoreAsync(archive, temp.Resolve("restored"));

        Assert.Equal(1, result.FileCount);
        Assert.Equal("source/link.txt", Assert.Single(result.Skipped).Path);
        Assert.False(File.Exists(temp.Resolve("restored/source/link.txt")));
    }

    [Fact]
    public async Task RestoreAsync_OfSomethingThatIsNotAnArchive_SaysSo()
    {
        using TempDirectory temp = new();
        string notAnArchive = temp.WriteFile("out/notes.txt", "just some text, definitely not an archive");

        ArchiveFormatException exception = await Assert.ThrowsAsync<ArchiveFormatException>(() =>
            RestoreAsync(notAnArchive, temp.Resolve("restored")));

        Assert.Contains("not an Enigma.Backup archive", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RestoreAsync_OfAFileTooSmallToIdentify_SaysSo()
    {
        using TempDirectory temp = new();
        string tiny = temp.WriteFile("out/tiny.bin", "ab");

        await Assert.ThrowsAsync<ArchiveFormatException>(() => RestoreAsync(tiny, temp.Resolve("restored")));
    }

    [Theory]
    [InlineData(CompressionAlgorithm.Zstd)]
    [InlineData(CompressionAlgorithm.Gzip)]
    public async Task RestoreAsync_OfATruncatedArchive_SaysItIsIncomplete(CompressionAlgorithm compression)
    {
        using TempDirectory temp = new();
        for (int index = 0; index < 40; index++)
        {
            temp.WriteFile($"source/file-{index:00}.txt", new string((char)('a' + (index % 26)), 4096));
        }

        string archive = await BackupAsync(temp, compression);
        using (FileStream file = new(archive, FileMode.Open, FileAccess.Write))
        {
            file.SetLength(file.Length / 2);
        }

        ArchiveFormatException exception = await Assert.ThrowsAsync<ArchiveFormatException>(() =>
            RestoreAsync(archive, temp.Resolve("restored")));

        Assert.Contains("damaged or incomplete", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RestoreAsync_OfAnEncryptedArchive_AsksForThePassword()
    {
        using TempDirectory temp = new();
        string encrypted = temp.Resolve("out/backup.tar.zst.enc");
        Directory.CreateDirectory(temp.Resolve("out"));
        await File.WriteAllBytesAsync(encrypted, [.. "EBK1"u8, 1, 1, 0, 0], Token);

        // The password is demanded from the marker alone, before the header is even parsed — the
        // container tests cover what happens once one is given.
        EncryptedArchiveException exception = await Assert.ThrowsAsync<EncryptedArchiveException>(() =>
            RestoreAsync(encrypted, temp.Resolve("restored")));

        Assert.Equal(EncryptedArchiveProblem.PasswordRequired, exception.Problem);
    }

    [Fact]
    public async Task RestoreAsync_ReportsProgressThatOnlyMovesForward()
    {
        using TempDirectory temp = new();
        for (int index = 0; index < 5; index++)
        {
            temp.WriteFile($"source/file-{index}.txt", new string('x', 200 * 1024));
        }

        string archive = await BackupAsync(temp);
        List<RestoreProgress> reports = [];

        RestoreResult result = await Pipeline.Restore().RestoreAsync(
            archive,
            temp.Resolve("restored"),
            password: null,
            new ScriptedConflictHandler(RestoreConflictDecision.Abort()),
            new SynchronousProgress<RestoreProgress>(reports.Add),
            Token);

        Assert.Equal(5, result.FileCount);
        Assert.Equal(5, reports[^1].FilesRestored);
        Assert.All(reports, report => Assert.InRange(report.Fraction, 0, 1));
        Assert.Equal(
            reports.Select(report => report.BytesRestored).OrderBy(bytes => bytes),
            reports.Select(report => report.BytesRestored));
    }

    [Fact]
    public async Task RestoreAsync_WithoutAnArchiveOrADestinationOrAHandler_Throws()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "hello");
        string archive = await BackupAsync(temp);

        await Assert.ThrowsAsync<ArgumentException>(() => RestoreAsync(" ", temp.Resolve("restored")));
        await Assert.ThrowsAsync<ArgumentException>(() => RestoreAsync(archive, " "));
        await Assert.ThrowsAsync<ArgumentNullException>(() => Pipeline.Restore().RestoreAsync(
            archive,
            temp.Resolve("restored"),
            password: null,
            null!,
            progress: null,
            Token));
    }

    [Fact]
    public async Task RestoreAsync_OfAnArchiveThatIsNotThere_Throws()
    {
        using TempDirectory temp = new();

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            RestoreAsync(temp.Resolve("out/nothing.tar.zst"), temp.Resolve("restored")));
    }

    private static async Task<string> BackupAsync(TempDirectory temp, CompressionAlgorithm compression = CompressionAlgorithm.Zstd)
    {
        BackupProfile profile = Pipeline.Profile("Documents", temp.Resolve("out"), compression, temp.Resolve("source"));
        BackupResult result = await Pipeline.Backup()
            .RunAsync(profile, password: null, new ScriptedErrorHandler(BackupErrorDecision.Abort()), progress: null, Token);

        return result.OutputPath;
    }

    private static Task<RestoreResult> RestoreAsync(string archive, string destination, IRestoreConflictHandler? handler = null) =>
        Pipeline.Restore().RestoreAsync(
            archive,
            destination,
            password: null,
            handler ?? new ScriptedConflictHandler(RestoreConflictDecision.Abort()),
            progress: null,
            Token);

    /// <summary>
    /// Writes a tarball by hand, so a restore can be given the entries a well-behaved backup would
    /// never produce.
    /// </summary>
    private static async Task WriteTarballAsync(string path, params (TarEntryType Type, string Name, string? Content)[] entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using FileStream file = File.Create(path);
        await using (GZipStream gzip = new(file, CompressionLevel.Fastest, leaveOpen: true))
        {
            await using TarWriter tar = new(gzip, TarEntryFormat.Pax, leaveOpen: true);

            foreach ((TarEntryType type, string name, string? content) in entries)
            {
                PaxTarEntry entry = new(type, name);

                if (type == TarEntryType.SymbolicLink)
                {
                    entry.LinkName = "a.txt";
                }
                else if (content is not null)
                {
                    entry.DataStream = new MemoryStream(Encoding.UTF8.GetBytes(content));
                }

                await tar.WriteEntryAsync(entry, Token);
            }
        }
    }
}
