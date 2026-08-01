using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Archiving;
using Enigma.Backup.Core.Backups;
using Enigma.Backup.Core.Encryption;
using Enigma.Backup.Core.Errors;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Core.Restoring;
using Enigma.Backup.Core.Tests.TestSupport;
using Enigma.Backup.Core.Verification;
using Xunit;

namespace Enigma.Backup.Core.Tests.Encryption;

/// <summary>
/// What the container is for: everything that goes wrong with an encrypted archive is caught, named,
/// and reported before a single byte of it is written to the user's disk.
/// </summary>
public sealed class Ebk1ContainerTests
{
    private const string Password = "correct horse battery staple";
    private const string WrongPassword = "correct horse battery stapl";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Backup_WithAnEncryptingProfileAndNoPassword_Throws()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "hello");
        BackupProfile profile = EncryptingProfile(temp);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Pipeline.Backup().RunAsync(profile, password: null, Strict(), progress: null, Token));

        // Nothing was created: the refusal happens before the scan, not halfway through a run.
        Assert.False(Directory.Exists(temp.Resolve("out")));
    }

    [Fact]
    public async Task Backup_ThatIsCancelled_LeavesNoHalfWrittenContainer()
    {
        using TempDirectory temp = new();
        temp.CreateDirectory("source");
        for (int index = 0; index < 20; index++)
        {
            File.WriteAllBytes(temp.Resolve($"source/file-{index:00}.bin"), RandomBytes(4096));
        }

        using CancellationTokenSource cancellation = new();
        SynchronousProgress<BackupProgress> progress = new(report =>
        {
            if (report.Phase == BackupPhase.Archiving && report.FilesProcessed == 3)
            {
                cancellation.Cancel();
            }
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Pipeline.Backup(encryption: Pipeline.FastEncryption(1024))
                .RunAsync(EncryptingProfile(temp), Password, Strict(), progress, cancellation.Token));

        // A container with no end-of-stream marker is exactly the file that must not survive: it looks
        // like a backup and is not one.
        Assert.Empty(Directory.GetFiles(temp.Resolve("out")));
    }

    [Fact]
    public async Task Restore_WithTheWrongPassword_SaysSo()
    {
        using TempDirectory temp = new();
        string archive = await BackupAsync(temp);

        EncryptedArchiveException exception = await Assert.ThrowsAsync<EncryptedArchiveException>(() =>
            RestoreAsync(archive, temp.Resolve("restored"), WrongPassword));

        Assert.Equal(EncryptedArchiveProblem.WrongPasswordOrCorrupted, exception.Problem);
        Assert.Contains("password is wrong", exception.Message, StringComparison.Ordinal);

        // The failure comes before anything is written: nothing is authenticated, so nothing is trusted.
        Assert.Empty(Directory.GetFileSystemEntries(temp.Resolve("restored")));
    }

    [Fact]
    public async Task Restore_WithoutAPassword_AsksForOne()
    {
        using TempDirectory temp = new();
        string archive = await BackupAsync(temp);

        EncryptedArchiveException exception = await Assert.ThrowsAsync<EncryptedArchiveException>(() =>
            RestoreAsync(archive, temp.Resolve("restored"), password: null));

        Assert.Equal(EncryptedArchiveProblem.PasswordRequired, exception.Problem);
        Assert.Contains("is encrypted", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Restore_OfAnUnencryptedArchive_IgnoresAPassword()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "hello");
        BackupProfile profile = Pipeline.Profile("Documents", temp.Resolve("out"), CompressionAlgorithm.Zstd, temp.Resolve("source"));

        BackupResult backup = await Pipeline.Backup().RunAsync(profile, password: null, Strict(), progress: null, Token);
        RestoreResult restore = await RestoreAsync(backup.OutputPath, temp.Resolve("restored"), Password);

        Assert.Equal(RestoreOutcome.Completed, restore.Outcome);
        Assert.Equal("hello", await File.ReadAllTextAsync(temp.Resolve("restored/source/a.txt"), Token));
    }

    [Fact]
    public async Task Verify_WithTheWrongPassword_BlamesThePasswordRatherThanTheFile()
    {
        using TempDirectory temp = new();
        BackupResult backup = await BackupResultAsync(temp);

        VerificationResult result = await VerifyAsync(backup.OutputPath, WrongPassword);

        Assert.Equal(VerificationOutcome.AuthenticationFailed, result.Outcome);
        Assert.False(result.Succeeded);
        Assert.False(result.PayloadAuthenticated);
        Assert.True(result.IsEncrypted);

        // The digest still matches, and the message is allowed to be that specific because of it.
        Assert.Equal(backup.Sha256, result.ActualSha256);
        Assert.Contains("the password is wrong", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_WithoutAPassword_ChecksTheFileButNotItsContents()
    {
        using TempDirectory temp = new();
        BackupResult backup = await BackupResultAsync(temp);

        VerificationResult result = await VerifyAsync(backup.OutputPath, password: null);

        Assert.Equal(VerificationOutcome.PasswordRequired, result.Outcome);
        Assert.True(result.IsEncrypted);
        Assert.Null(result.PayloadAuthenticated);
        Assert.Equal(backup.Sha256, result.ActualSha256);
        Assert.Equal(new FileInfo(backup.OutputPath).Length, result.BytesRead);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Restore_OfAnArchiveWithATamperedChunk_Fails(int chunkIndex)
    {
        using TempDirectory temp = new();
        string archive = await BackupAsync(temp, payloadFiles: 12);
        IReadOnlyList<Ebk1Chunk> chunks = Ebk1File.ReadChunks(archive);
        Assert.True(chunks.Count > 3, $"The test needs several chunks, but the archive has {chunks.Count}.");

        Ebk1File.FlipByte(archive, chunks[chunkIndex].CiphertextOffset);

        EncryptedArchiveException exception = await Assert.ThrowsAsync<EncryptedArchiveException>(() =>
            RestoreAsync(archive, temp.Resolve("restored"), Password));

        Assert.Equal(EncryptedArchiveProblem.WrongPasswordOrCorrupted, exception.Problem);
    }

    [Fact]
    public async Task Restore_OfAnArchiveWithATamperedTag_Fails()
    {
        using TempDirectory temp = new();
        string archive = await BackupAsync(temp);
        IReadOnlyList<Ebk1Chunk> chunks = Ebk1File.ReadChunks(archive);

        Ebk1File.FlipByte(archive, chunks[0].TagOffset);

        EncryptedArchiveException exception = await Assert.ThrowsAsync<EncryptedArchiveException>(() =>
            RestoreAsync(archive, temp.Resolve("restored"), Password));

        Assert.Equal(EncryptedArchiveProblem.WrongPasswordOrCorrupted, exception.Problem);
    }

    [Fact]
    public async Task Restore_OfAnArchiveWhoseChunksWereReordered_Fails()
    {
        using TempDirectory temp = new();
        string archive = await BackupAsync(temp, payloadFiles: 12);
        IReadOnlyList<Ebk1Chunk> chunks = Ebk1File.ReadChunks(archive);
        Assert.True(chunks.Count > 3, $"The test needs several chunks, but the archive has {chunks.Count}.");

        // Every byte of the file is still a byte the backup wrote, and every tag is still its own
        // chunk's tag. Only the order changed — which the counter baked into each nonce and each
        // chunk's associated data is there to catch.
        Ebk1File.SwapChunks(archive, chunks[0], chunks[1]);

        EncryptedArchiveException exception = await Assert.ThrowsAsync<EncryptedArchiveException>(() =>
            RestoreAsync(archive, temp.Resolve("restored"), Password));

        Assert.Equal(EncryptedArchiveProblem.WrongPasswordOrCorrupted, exception.Problem);
    }

    [Fact]
    public async Task Restore_OfAnArchiveWithATamperedHeader_Fails()
    {
        using TempDirectory temp = new();
        string archive = await BackupAsync(temp);

        // The header is not encrypted, but it is authenticated: it is the first half of every chunk's
        // associated data, so changing one byte of the salt breaks the very first tag.
        Ebk1File.FlipByte(archive, Ebk1File.SaltOffset);

        EncryptedArchiveException exception = await Assert.ThrowsAsync<EncryptedArchiveException>(() =>
            RestoreAsync(archive, temp.Resolve("restored"), Password));

        Assert.Equal(EncryptedArchiveProblem.WrongPasswordOrCorrupted, exception.Problem);
    }

    [Fact]
    public async Task Restore_OfAContainerFromANewerVersion_SaysToUpgrade()
    {
        using TempDirectory temp = new();
        string archive = await BackupAsync(temp);

        Ebk1File.PatchByte(archive, Ebk1File.VersionOffset, 2);

        EncryptedArchiveException exception = await Assert.ThrowsAsync<EncryptedArchiveException>(() =>
            RestoreAsync(archive, temp.Resolve("restored"), Password));

        Assert.Equal(EncryptedArchiveProblem.UnsupportedVersion, exception.Problem);
        Assert.Contains("newer version of the application", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Restore_OfAContainerWithAnUnknownKeyDerivation_SaysToUpgrade()
    {
        using TempDirectory temp = new();
        string archive = await BackupAsync(temp);

        Ebk1File.PatchByte(archive, Ebk1File.KdfIdOffset, 9);

        EncryptedArchiveException exception = await Assert.ThrowsAsync<EncryptedArchiveException>(() =>
            RestoreAsync(archive, temp.Resolve("restored"), Password));

        Assert.Equal(EncryptedArchiveProblem.UnsupportedKeyDerivation, exception.Problem);
    }

    [Theory]
    [InlineData("an impossible chunk size")]
    [InlineData("an impossible memory cost")]
    [InlineData("a salt that is too short")]
    public async Task Restore_OfAContainerWithAnImpossibleHeaderField_IsRefused(string field)
    {
        using TempDirectory temp = new();
        string archive = await BackupAsync(temp);
        Ebk1HeaderView header = Ebk1File.ReadHeader(archive);

        switch (field)
        {
            case "an impossible chunk size":
                // A reader that believed this would try to allocate 4 GiB before reading a byte.
                Ebk1File.PatchUInt32(archive, Ebk1File.SaltOffset + header.Salt.Length, uint.MaxValue);
                break;

            case "an impossible memory cost":
                Ebk1File.PatchUInt32(archive, Ebk1File.MemoryKiBOffset, uint.MaxValue);
                break;

            default:
                Ebk1File.PatchByte(archive, Ebk1File.SaltLengthOffset, 0);
                break;
        }

        EncryptedArchiveException exception = await Assert.ThrowsAsync<EncryptedArchiveException>(() =>
            RestoreAsync(archive, temp.Resolve("restored"), Password));

        Assert.Equal(EncryptedArchiveProblem.MalformedHeader, exception.Problem);
    }

    [Theory]
    [InlineData("halfway through the header")]
    [InlineData("halfway through a chunk")]
    [InlineData("on a chunk boundary")]
    public async Task Restore_OfATruncatedContainer_SaysItIsIncomplete(string where)
    {
        using TempDirectory temp = new();
        string archive = await BackupAsync(temp, payloadFiles: 12);
        IReadOnlyList<Ebk1Chunk> chunks = Ebk1File.ReadChunks(archive);
        Assert.True(chunks.Count > 3, $"The test needs several chunks, but the archive has {chunks.Count}.");

        long length = where switch
        {
            "halfway through the header" => Ebk1File.SaltOffset - 2,
            "halfway through a chunk" => chunks[1].CiphertextOffset + 3,

            // The interesting one: the file stops exactly where a chunk ends, so every chunk it still
            // has decrypts perfectly. Nothing about the ciphertext says the payload is short — only
            // running out of file before the end-of-stream marker does.
            _ => chunks[^2].Offset,
        };

        Ebk1File.Truncate(archive, length);

        EncryptedArchiveException exception = await Assert.ThrowsAsync<EncryptedArchiveException>(() =>
            RestoreAsync(archive, temp.Resolve("restored"), Password));

        Assert.Equal(EncryptedArchiveProblem.Truncated, exception.Problem);
        Assert.Contains("incomplete", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_OfAContainerMissingOnlyItsEndOfStreamMarker_Fails()
    {
        using TempDirectory temp = new();
        BackupResult backup = await BackupResultAsync(temp, payloadFiles: 12);
        IReadOnlyList<Ebk1Chunk> chunks = Ebk1File.ReadChunks(backup.OutputPath);

        // Cut off nothing but the marker. Every byte of payload is still there and still
        // authenticates, so a restore — which stops as soon as the tar inside says it is done —
        // succeeds, and correctly so: it read nothing it had not verified.
        Ebk1File.Truncate(backup.OutputPath, chunks[^1].Offset);
        Assert.Equal(RestoreOutcome.Completed, (await RestoreAsync(backup.OutputPath, temp.Resolve("restored"), Password)).Outcome);

        // Verification is the check that reads the container to the end, and that is where the
        // missing marker is caught.
        VerificationResult result = await VerifyAsync(backup.OutputPath, Password);

        Assert.False(result.PayloadAuthenticated);
        Assert.Equal(VerificationOutcome.HashMismatch, result.Outcome);
    }

    [Fact]
    public async Task Verify_OfAContainerWithDataAppendedToIt_Fails()
    {
        using TempDirectory temp = new();
        BackupResult backup = await BackupResultAsync(temp);

        await File.AppendAllTextAsync(backup.OutputPath, "and something extra", Token);

        VerificationResult result = await VerifyAsync(backup.OutputPath, Password);

        // The checksum notices first, because the file grew — but the container notices too, on its
        // own terms: there is nothing legitimate after the end-of-stream marker.
        Assert.Equal(VerificationOutcome.HashMismatch, result.Outcome);
        Assert.False(result.PayloadAuthenticated);
    }

    [Fact]
    public async Task Verify_OfAContainerWithATamperedChunk_ReportsBothProblems()
    {
        using TempDirectory temp = new();
        BackupResult backup = await BackupResultAsync(temp);
        IReadOnlyList<Ebk1Chunk> chunks = Ebk1File.ReadChunks(backup.OutputPath);

        Ebk1File.FlipByte(backup.OutputPath, chunks[0].CiphertextOffset);

        VerificationResult result = await VerifyAsync(backup.OutputPath, Password);

        Assert.Equal(VerificationOutcome.HashMismatch, result.Outcome);
        Assert.False(result.PayloadAuthenticated);
        Assert.NotEqual(backup.Sha256, result.ActualSha256);

        // The whole file was still read: a failed decryption does not cost the checksum its coverage.
        Assert.Equal(new FileInfo(backup.OutputPath).Length, result.BytesRead);
    }

    [Fact]
    public async Task Restore_OfAFileThatOnlyClaimsToBeAContainer_IsRefused()
    {
        using TempDirectory temp = new();
        Directory.CreateDirectory(temp.Resolve("out"));
        string archive = temp.Resolve("out/backup.tar.zst.enc");
        await File.WriteAllBytesAsync(archive, [.. "EBK1"u8, 1, 1, 0, 0], Token);

        EncryptedArchiveException exception = await Assert.ThrowsAsync<EncryptedArchiveException>(() =>
            RestoreAsync(archive, temp.Resolve("restored"), Password));

        Assert.Equal(EncryptedArchiveProblem.Truncated, exception.Problem);
    }

    [Fact]
    public async Task Restore_OfAContainerHoldingSomethingOtherThanAnArchive_IsRefused()
    {
        using TempDirectory temp = new();

        // A container whose payload is neither a zstd frame nor a gzip member: correctly encrypted,
        // correctly authenticated, and still not an archive.
        string archive = await WriteContainerAsync(temp, "this is not a compressed tarball, however well sealed it is"u8.ToArray());

        ArchiveFormatException exception = await Assert.ThrowsAsync<ArchiveFormatException>(() =>
            RestoreAsync(archive, temp.Resolve("restored"), Password));

        Assert.Contains("not an Enigma.Backup archive", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Restore_OfAContainerBuiltFromTheSpecification_Works()
    {
        using TempDirectory temp = new();

        // The payload here is a real archive taken from a real backup, but the container around it was
        // written by TestSupport/Ebk1Writer, straight from the format spec. If the two implementations
        // did not agree on the header layout, the nonce, or the associated data, this could not open.
        temp.WriteFile("source/a.txt", "hello from a container this application did not write");
        BackupProfile plain = Pipeline.Profile("Documents", temp.Resolve("plain"), CompressionAlgorithm.Zstd, temp.Resolve("source"));
        BackupResult unencrypted = await Pipeline.Backup().RunAsync(plain, password: null, Strict(), progress: null, Token);

        string archive = await WriteContainerAsync(temp, await File.ReadAllBytesAsync(unencrypted.OutputPath, Token));
        RestoreResult restore = await RestoreAsync(archive, temp.Resolve("restored"), Password);

        Assert.Equal(RestoreOutcome.Completed, restore.Outcome);
        Assert.Equal(
            "hello from a container this application did not write",
            await File.ReadAllTextAsync(temp.Resolve("restored/source/a.txt"), Token));
    }

    /// <summary>Writes a container built by <see cref="Ebk1Writer"/> into the temp directory.</summary>
    private static async Task<string> WriteContainerAsync(TempDirectory temp, byte[] payload)
    {
        Directory.CreateDirectory(temp.Resolve("out"));
        string archive = temp.Resolve("out/backup.tar.zst.enc");
        await File.WriteAllBytesAsync(archive, Ebk1Writer.Create(Password, payload), Token);

        return archive;
    }

    private static BackupProfile EncryptingProfile(TempDirectory temp, CompressionAlgorithm compression = CompressionAlgorithm.Zstd)
    {
        BackupProfile profile = Pipeline.Profile("Documents", temp.Resolve("out"), compression, temp.Resolve("source"));
        profile.Encrypt = true;

        return profile;
    }

    private static async Task<string> BackupAsync(TempDirectory temp, int payloadFiles = 1) =>
        (await BackupResultAsync(temp, payloadFiles)).OutputPath;

    private static Task<BackupResult> BackupResultAsync(TempDirectory temp, int payloadFiles = 1)
    {
        temp.CreateDirectory("source");

        for (int index = 0; index < payloadFiles; index++)
        {
            File.WriteAllBytes(temp.Resolve($"source/file-{index:00}.bin"), RandomBytes(4096));
        }

        return Pipeline.Backup(encryption: Pipeline.FastEncryption(1024))
            .RunAsync(EncryptingProfile(temp), Password, Strict(), progress: null, Token);
    }

    /// <summary>
    /// Incompressible content, so the payload cannot shrink to a single chunk and quietly disarm the
    /// tests that need several of them.
    /// </summary>
    /// <param name="length">How many bytes to produce.</param>
    private static byte[] RandomBytes(int length)
    {
        byte[] contents = new byte[length];
        Random.Shared.NextBytes(contents);

        return contents;
    }

    private static Task<RestoreResult> RestoreAsync(string archivePath, string destination, string? password) =>
        Pipeline.Restore().RestoreAsync(
            archivePath,
            destination,
            password,
            new ScriptedConflictHandler(RestoreConflictDecision.Abort()),
            progress: null,
            Token);

    private static Task<VerificationResult> VerifyAsync(string archivePath, string? password) =>
        Pipeline.Verification().VerifyAsync(archivePath, password, progress: null, Token);

    private static IBackupErrorHandler Strict() => new ScriptedErrorHandler(BackupErrorDecision.Abort());
}
