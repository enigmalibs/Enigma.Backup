using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
/// The acceptance criterion of the encryption container: an encrypted backup verifies and restores to
/// the tree it came from, for every compression the app offers, and gives nothing away in between.
/// </summary>
public sealed class EncryptedRoundTripTests
{
    private const string Password = "correct horse battery staple";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(CompressionAlgorithm.Zstd, ".tar.zst.enc")]
    [InlineData(CompressionAlgorithm.Gzip, ".tar.gz.enc")]
    public async Task BackupVerifyRestore_ReproducesTheInputTree(CompressionAlgorithm compression, string extension)
    {
        using TempDirectory temp = new();
        CreateSourceTree(temp);

        BackupResult backup = await BackupAsync(temp, compression);

        Assert.EndsWith(extension, backup.OutputPath, StringComparison.Ordinal);

        VerificationResult verification = await VerifyAsync(backup.OutputPath, Password);
        Assert.Equal(VerificationOutcome.Succeeded, verification.Outcome);
        Assert.True(verification.IsEncrypted);
        Assert.True(verification.PayloadAuthenticated);

        RestoreResult restore = await RestoreAsync(backup.OutputPath, temp.Resolve("restored"), Password);

        Assert.Equal(RestoreOutcome.Completed, restore.Outcome);
        Assert.Empty(restore.Skipped);
        Assert.Equal(
            FileTree.Snapshot(temp.Resolve("source")),
            FileTree.Snapshot(temp.Resolve("restored/source")));
    }

    [Fact]
    public async Task Backup_WithTheShippedParameters_RoundTrips()
    {
        // Every other test here runs Argon2id at its floor for speed. This one pays the real cost
        // once, so the parameters the application actually ships with are known to work end to end.
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "hello");

        BackupResult backup = await BackupAsync(temp, CompressionAlgorithm.Zstd, new Ebk1Options());

        Ebk1HeaderView header = Ebk1File.ReadHeader(backup.OutputPath);
        Assert.Equal(Ebk1Options.DefaultMemoryKiB, header.MemoryKiB);
        Assert.Equal(Ebk1Options.DefaultIterations, header.Iterations);
        Assert.Equal(Ebk1Options.DefaultParallelism, header.Parallelism);
        Assert.Equal(Ebk1Options.DefaultSaltLength, header.Salt.Length);
        Assert.Equal(Ebk1Options.DefaultChunkSize, header.ChunkSize);

        Assert.Equal(VerificationOutcome.Succeeded, (await VerifyAsync(backup.OutputPath, Password)).Outcome);
        await RestoreAsync(backup.OutputPath, temp.Resolve("restored"), Password);

        Assert.Equal("hello", await File.ReadAllTextAsync(temp.Resolve("restored/source/a.txt"), Token));
    }

    [Fact]
    public async Task Backup_WritesAnEbk1ContainerThatShowsNothingOfItsContents()
    {
        const string Secret = "a-string-that-must-not-appear-in-the-archive";

        using TempDirectory temp = new();
        // Repeated so it survives compression as a recognisable run rather than being coded away.
        temp.WriteFile("source/secret.txt", string.Join('\n', Enumerable.Repeat(Secret, 200)));

        BackupResult backup = await BackupAsync(temp, CompressionAlgorithm.Zstd);
        byte[] archive = await File.ReadAllBytesAsync(backup.OutputPath, Token);

        Assert.Equal("EBK1"u8.ToArray(), archive[..4]);
        Assert.DoesNotContain("secret.txt", Encoding.UTF8.GetString(archive), StringComparison.Ordinal);
        Assert.Equal(-1, archive.AsSpan().IndexOf(Encoding.UTF8.GetBytes(Secret)));
    }

    [Fact]
    public async Task Backup_WritesASidecarOverTheEncryptedBytes()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "hello");

        BackupResult backup = await BackupAsync(temp, CompressionAlgorithm.Zstd);

        // The digest covers the file as it lies on disk, so a checksum can be confirmed — by this
        // application or by sha256sum — without anybody knowing the password.
        byte[] archive = await File.ReadAllBytesAsync(backup.OutputPath, Token);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(archive)), backup.Sha256);

        string sidecar = await File.ReadAllTextAsync(backup.SidecarPath, Token);
        Assert.Equal($"{backup.Sha256}  {Path.GetFileName(backup.OutputPath)}\n", sidecar);

        VerificationResult verification = await VerifyAsync(backup.OutputPath, password: null);
        Assert.Equal(VerificationOutcome.PasswordRequired, verification.Outcome);
        Assert.Equal(backup.Sha256, verification.ActualSha256);
    }

    [Fact]
    public async Task Backup_OfAPayloadSpanningManyChunks_RoundTrips()
    {
        using TempDirectory temp = new();
        byte[] contents = new byte[512 * 1024];
        Random.Shared.NextBytes(contents);
        Directory.CreateDirectory(temp.Resolve("source"));
        await File.WriteAllBytesAsync(temp.Resolve("source/big.bin"), contents, Token);

        // 1 KiB chunks over incompressible data: hundreds of chunks, so every boundary — filling one,
        // starting the next, the short one at the end — is crossed many times over.
        BackupResult backup = await BackupAsync(temp, CompressionAlgorithm.Zstd, Pipeline.FastEncryption(1024));

        IReadOnlyList<Ebk1Chunk> chunks = Ebk1File.ReadChunks(backup.OutputPath);
        Assert.True(chunks.Count > 100, $"Expected the payload to span many chunks, but it produced {chunks.Count}.");

        // Every chunk is full but the last one, which holds the remainder, and the marker after it,
        // which holds nothing and still carries a tag of its own.
        Assert.All(chunks.Take(chunks.Count - 2), chunk => Assert.Equal(1024, chunk.PlainLength));
        Assert.InRange(chunks[^2].PlainLength, 1, 1024);
        Assert.Equal(0, chunks[^1].PlainLength);
        Assert.Equal(new FileInfo(backup.OutputPath).Length, chunks[^1].EndOffset);

        await RestoreAsync(backup.OutputPath, temp.Resolve("restored"), Password);

        byte[] restored = await File.ReadAllBytesAsync(temp.Resolve("restored/source/big.bin"), Token);
        Assert.Equal(SHA256.HashData(contents), SHA256.HashData(restored));
    }

    [Fact]
    public async Task Backup_RecordsItsKeyDerivationParametersInTheHeader()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "hello");

        Ebk1Options options = new()
        {
            MemoryKiB = 32,
            Iterations = 2,
            Parallelism = 2,
            SaltLength = 24,
            ChunkSize = 2048,
        };

        BackupResult backup = await BackupAsync(temp, CompressionAlgorithm.Gzip, options);
        Ebk1HeaderView header = Ebk1File.ReadHeader(backup.OutputPath);

        Assert.Equal(1, header.Version);
        Assert.Equal(1, header.KdfId);
        Assert.Equal(options.MemoryKiB, header.MemoryKiB);
        Assert.Equal(options.Iterations, header.Iterations);
        Assert.Equal(options.Parallelism, header.Parallelism);
        Assert.Equal(options.SaltLength, header.Salt.Length);
        Assert.Equal(options.ChunkSize, header.ChunkSize);
        Assert.Equal(8, header.NoncePrefix.Length);

        // And they are not decoration: the restore never sees `options`, only the file.
        await RestoreAsync(backup.OutputPath, temp.Resolve("restored"), Password);
        Assert.Equal("hello", await File.ReadAllTextAsync(temp.Resolve("restored/source/a.txt"), Token));
    }

    [Fact]
    public async Task TwoBackupsOfTheSameInput_ShareNoBytes()
    {
        using TempDirectory temp = new();
        temp.WriteFile("source/a.txt", "hello");

        BackupResult first = await BackupAsync(temp, CompressionAlgorithm.Zstd);
        BackupResult second = await BackupAsync(temp, CompressionAlgorithm.Zstd);

        Ebk1HeaderView left = Ebk1File.ReadHeader(first.OutputPath);
        Ebk1HeaderView right = Ebk1File.ReadHeader(second.OutputPath);

        // A fresh salt and a fresh nonce prefix per run: identical input must not produce identical
        // ciphertext, and a (key, nonce) pair must never be reused across containers.
        Assert.NotEqual(left.Salt, right.Salt);
        Assert.NotEqual(left.NoncePrefix, right.NoncePrefix);
        Assert.NotEqual(first.Sha256, second.Sha256);
    }

    /// <summary>A tree with everything the pipeline has to survive, as the unencrypted round-trip uses.</summary>
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

    private static Task<BackupResult> BackupAsync(
        TempDirectory temp,
        CompressionAlgorithm compression,
        Ebk1Options? encryption = null)
    {
        BackupProfile profile = Pipeline.Profile("Documents", temp.Resolve("out"), compression, temp.Resolve("source"));
        profile.Encrypt = true;

        return Pipeline.Backup(encryption: encryption)
            .RunAsync(profile, Password, new ScriptedErrorHandler(BackupErrorDecision.Abort()), progress: null, Token);
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
}
