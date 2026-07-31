using System;

namespace Enigma.Backup.Core.Encryption;

/// <summary>
/// The parameters a <em>new</em> <c>EBK1</c> container is written with: the Argon2id cost, the salt
/// length, and how much plaintext one authenticated chunk holds.
/// </summary>
/// <remarks>
/// <para>
/// Reading never consults this type — every value that matters is recorded in the file's own header,
/// so an archive written by any build, with any settings, stays readable. Only the bounds below apply
/// on the way in, as a guard against a header that asks for an impossible allocation.
/// </para>
/// <para>
/// Registered as a singleton by <c>AddEnigmaBackupCore</c>. The defaults are RFC 9106's second
/// recommended Argon2id option (64 MiB, 3 passes, 4 lanes); tests replace them with cheap ones so a
/// key derivation does not cost 400 ms per case.
/// </para>
/// </remarks>
public sealed class Ebk1Options
{
    /// <summary>Argon2id memory cost, in kibibytes: 64 MiB.</summary>
    public const int DefaultMemoryKiB = 64 * 1024;

    /// <summary>Argon2id passes over memory.</summary>
    public const int DefaultIterations = 3;

    /// <summary>Argon2id lanes.</summary>
    public const int DefaultParallelism = 4;

    /// <summary>Length of the random salt, in bytes.</summary>
    public const int DefaultSaltLength = 16;

    /// <summary>
    /// Plaintext bytes per authenticated chunk: 1 MiB.
    /// </summary>
    /// <remarks>
    /// A chunk has to be held in memory to be encrypted, so the chunk size <em>is</em> the pipeline's
    /// memory floor. Framing costs 20 bytes per chunk — 0.002% at this size — so there is nothing to
    /// buy by making it larger.
    /// </remarks>
    public const int DefaultChunkSize = 1024 * 1024;

    /// <summary>Smallest chunk size this build writes or reads.</summary>
    public const int MinChunkSize = 1024;

    /// <summary>Largest chunk size this build writes or reads: 256 MiB.</summary>
    public const int MaxChunkSize = 256 * 1024 * 1024;

    /// <summary>Smallest salt Argon2 accepts.</summary>
    public const int MinSaltLength = 8;

    /// <summary>Largest salt the header's one-byte length field can describe.</summary>
    public const int MaxSaltLength = 255;

    /// <summary>Smallest Argon2id memory cost, in kibibytes.</summary>
    public const int MinMemoryKiB = 8;

    /// <summary>Largest Argon2id memory cost this build will attempt: 4 GiB.</summary>
    public const int MaxMemoryKiB = 4 * 1024 * 1024;

    /// <summary>Largest number of Argon2id passes this build will attempt.</summary>
    public const int MaxIterations = 10_000;

    /// <summary>Largest number of Argon2id lanes this build will attempt.</summary>
    public const int MaxParallelism = 1024;

    /// <summary>Argon2id memory cost, in kibibytes.</summary>
    public int MemoryKiB
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, MinMemoryKiB);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxMemoryKiB);
            field = value;
        }
    } = DefaultMemoryKiB;

    /// <summary>Argon2id passes over memory.</summary>
    public int Iterations
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxIterations);
            field = value;
        }
    } = DefaultIterations;

    /// <summary>Argon2id lanes.</summary>
    public int Parallelism
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxParallelism);
            field = value;
        }
    } = DefaultParallelism;

    /// <summary>Length of the random salt, in bytes.</summary>
    public int SaltLength
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, MinSaltLength);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxSaltLength);
            field = value;
        }
    } = DefaultSaltLength;

    /// <summary>Plaintext bytes per authenticated chunk.</summary>
    public int ChunkSize
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, MinChunkSize);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxChunkSize);
            field = value;
        }
    } = DefaultChunkSize;
}
