using System;
using System.Collections.Generic;
using Enigma.Backup.Core.Backups;
using Enigma.Backup.Core.Encryption;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Core.Restoring;
using Enigma.Backup.Core.Scanning;
using Enigma.Backup.Core.Verification;

namespace Enigma.Backup.Core.Tests.TestSupport;

/// <summary>
/// Builds the archive services the way <c>AddEnigmaBackupCore</c> does, for tests that exercise the
/// pipeline rather than the container.
/// </summary>
public static class Pipeline
{
    /// <summary>
    /// Encryption parameters cheap enough to use in every test: Argon2id at its floor, and a chunk
    /// small enough that a few kilobytes of payload span many chunks.
    /// </summary>
    /// <remarks>
    /// The real defaults cost ~400 ms per key derivation, and a round-trip derives twice. Only the
    /// tests that are <em>about</em> the shipped parameters pay that; every other test would just be
    /// slow. What the archive is written with is recorded in its own header, so a cheap key produces a
    /// container of exactly the same shape.
    /// </remarks>
    /// <param name="chunkSize">Plaintext bytes per chunk.</param>
    public static Ebk1Options FastEncryption(int chunkSize = 4096) =>
        new()
        {
            MemoryKiB = 8,
            Iterations = 1,
            Parallelism = 1,
            SaltLength = 8,
            ChunkSize = chunkSize,
        };

    /// <summary>Creates a backup service.</summary>
    /// <param name="timeProvider">The clock to use; the system clock when omitted.</param>
    /// <param name="encryption">The container parameters to write with; <see cref="FastEncryption"/> when omitted.</param>
    public static BackupService Backup(TimeProvider? timeProvider = null, Ebk1Options? encryption = null) =>
        new(new InputScanner(), timeProvider ?? TimeProvider.System, encryption ?? FastEncryption());

    /// <summary>Creates a restore service.</summary>
    /// <param name="timeProvider">The clock to use; the system clock when omitted.</param>
    public static RestoreService Restore(TimeProvider? timeProvider = null) =>
        new(timeProvider ?? TimeProvider.System);

    /// <summary>Creates a verification service.</summary>
    /// <param name="timeProvider">The clock to use; the system clock when omitted.</param>
    public static VerificationService Verification(TimeProvider? timeProvider = null) =>
        new(timeProvider ?? TimeProvider.System);

    /// <summary>Builds a profile pointing at the given inputs.</summary>
    /// <param name="name">The profile name, which becomes the archive's name prefix.</param>
    /// <param name="outputDirectory">Where the archive is written.</param>
    /// <param name="compression">The compression to use.</param>
    /// <param name="inputs">The paths to back up.</param>
    public static BackupProfile Profile(string name, string outputDirectory, CompressionAlgorithm compression, params string[] inputs) =>
        new()
        {
            Name = name,
            OutputDirectory = outputDirectory,
            Compression = compression,
            Inputs = new List<string>(inputs),
        };
}
