using System;
using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.Backup.Core.Encryption;

/// <summary>
/// The plaintext header of an <c>EBK1</c> container: everything a reader needs to rebuild the key and
/// walk the chunks, and nothing that helps an attacker.
/// </summary>
/// <remarks>
/// <para>
/// The layout, little-endian throughout, is:
/// </para>
/// <code>
/// magic        "EBK1"    4 bytes
/// version      u8        = 1
/// kdfId        u8        = 1 (Argon2id)
/// memoryKiB    u32 LE    Argon2id memory cost
/// iterations   u32 LE    Argon2id passes
/// parallelism  u32 LE    Argon2id lanes
/// saltLen      u8        followed by that many salt bytes
/// chunkSize    u32 LE    plaintext bytes per chunk
/// noncePrefix  8 bytes   random, per container
/// </code>
/// <para>
/// The header is not encrypted — it cannot be, since it says how to derive the key — but it is not
/// unprotected either: every byte of it goes into each chunk's associated data, so altering any field
/// makes the very first chunk fail its authentication tag.
/// </para>
/// </remarks>
internal sealed class Ebk1Header
{
    /// <summary>The container's magic bytes, also its signature for format detection.</summary>
    public static ReadOnlySpan<byte> Magic => "EBK1"u8;

    /// <summary>The container version this build writes and reads.</summary>
    public const byte CurrentVersion = 1;

    /// <summary>The <c>kdfId</c> value standing for Argon2id.</summary>
    public const byte Argon2idKdfId = 1;

    /// <summary>Length of a GCM authentication tag, in bytes.</summary>
    public const int TagLength = 16;

    /// <summary>Length of the GCM tag, in bits, as the cipher API wants it.</summary>
    public const int TagBits = TagLength * 8;

    /// <summary>Length of the per-container random nonce prefix, in bytes.</summary>
    public const int NoncePrefixLength = 8;

    /// <summary>Length of a GCM nonce: the prefix plus a big-endian chunk counter.</summary>
    public const int NonceLength = NoncePrefixLength + sizeof(uint);

    /// <summary>Length of the <c>plainLen</c> field in front of every chunk.</summary>
    public const int ChunkLengthPrefixLength = sizeof(uint);

    /// <summary>Bytes of header that precede the salt.</summary>
    private const int FixedPrefixLength = 4 + 1 + 1 + 4 + 4 + 4 + 1;

    /// <summary>Bytes of header that follow the salt.</summary>
    private const int FixedSuffixLength = 4 + NoncePrefixLength;

    private Ebk1Header(byte[] bytes, int memoryKiB, int iterations, int parallelism, byte[] salt, int chunkSize, byte[] noncePrefix)
    {
        Bytes = bytes;
        MemoryKiB = memoryKiB;
        Iterations = iterations;
        Parallelism = parallelism;
        Salt = salt;
        ChunkSize = chunkSize;
        NoncePrefix = noncePrefix;
    }

    /// <summary>The header exactly as it appears on disk; the prefix of every chunk's associated data.</summary>
    public byte[] Bytes { get; }

    /// <summary>Argon2id memory cost, in kibibytes.</summary>
    public int MemoryKiB { get; }

    /// <summary>Argon2id passes over memory.</summary>
    public int Iterations { get; }

    /// <summary>Argon2id lanes.</summary>
    public int Parallelism { get; }

    /// <summary>The random salt the key was derived with.</summary>
    public byte[] Salt { get; }

    /// <summary>Plaintext bytes per chunk; the last data chunk may be shorter.</summary>
    public int ChunkSize { get; }

    /// <summary>The random per-container half of every chunk's nonce.</summary>
    public byte[] NoncePrefix { get; }

    /// <summary>Builds a header for a new container, with a fresh random salt and nonce prefix.</summary>
    /// <param name="options">The parameters to write.</param>
    public static Ebk1Header Create(Ebk1Options options)
    {
        ArgumentNullException.ThrowIfNull(options);

        byte[] salt = RandomNumberGenerator.GetBytes(options.SaltLength);
        byte[] noncePrefix = RandomNumberGenerator.GetBytes(NoncePrefixLength);
        byte[] bytes = new byte[FixedPrefixLength + salt.Length + FixedSuffixLength];

        Span<byte> destination = bytes;
        Magic.CopyTo(destination);
        destination[4] = CurrentVersion;
        destination[5] = Argon2idKdfId;
        BinaryPrimitives.WriteUInt32LittleEndian(destination[6..], (uint)options.MemoryKiB);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[10..], (uint)options.Iterations);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[14..], (uint)options.Parallelism);
        destination[18] = (byte)salt.Length;
        salt.CopyTo(destination[FixedPrefixLength..]);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[(FixedPrefixLength + salt.Length)..], (uint)options.ChunkSize);
        noncePrefix.CopyTo(destination[(FixedPrefixLength + salt.Length + 4)..]);

        return new Ebk1Header(bytes, options.MemoryKiB, options.Iterations, options.Parallelism, salt, options.ChunkSize, noncePrefix);
    }

    /// <summary>Reads and validates a header from the start of a container.</summary>
    /// <param name="source">A stream positioned at the container's first byte.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <exception cref="EncryptedArchiveException">The header is cut short, or describes something this build cannot read.</exception>
    public static async Task<Ebk1Header> ReadAsync(Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        byte[] prefix = new byte[FixedPrefixLength];
        await ReadExactlyAsync(source, prefix, cancellationToken).ConfigureAwait(false);

        if (!prefix.AsSpan(0, Magic.Length).SequenceEqual(Magic))
        {
            throw Malformed("the file does not start with the EBK1 marker");
        }

        if (prefix[4] != CurrentVersion)
        {
            throw new EncryptedArchiveException(
                EncryptedArchiveProblem.UnsupportedVersion,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"This encrypted archive is version {prefix[4]}, and this version of Enigma.Backup only reads version {CurrentVersion}. A newer version of the application is needed to open it."));
        }

        if (prefix[5] != Argon2idKdfId)
        {
            throw new EncryptedArchiveException(
                EncryptedArchiveProblem.UnsupportedKeyDerivation,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"This encrypted archive derives its key with method {prefix[5]}, which this version of Enigma.Backup does not know. A newer version of the application is needed to open it."));
        }

        int memoryKiB = ReadBoundedUInt32(prefix.AsSpan(6), Ebk1Options.MinMemoryKiB, Ebk1Options.MaxMemoryKiB, "memory cost");
        int iterations = ReadBoundedUInt32(prefix.AsSpan(10), 1, Ebk1Options.MaxIterations, "iteration count");
        int parallelism = ReadBoundedUInt32(prefix.AsSpan(14), 1, Ebk1Options.MaxParallelism, "parallelism");

        int saltLength = prefix[18];
        if (saltLength is < Ebk1Options.MinSaltLength or > Ebk1Options.MaxSaltLength)
        {
            throw Malformed($"its salt length ({saltLength}) is outside the range this build accepts");
        }

        byte[] suffix = new byte[saltLength + FixedSuffixLength];
        await ReadExactlyAsync(source, suffix, cancellationToken).ConfigureAwait(false);

        byte[] salt = suffix[..saltLength];
        int chunkSize = ReadBoundedUInt32(suffix.AsSpan(saltLength), Ebk1Options.MinChunkSize, Ebk1Options.MaxChunkSize, "chunk size");
        byte[] noncePrefix = suffix[(saltLength + 4)..];

        byte[] bytes = new byte[prefix.Length + suffix.Length];
        prefix.CopyTo(bytes, 0);
        suffix.CopyTo(bytes, prefix.Length);

        return new Ebk1Header(bytes, memoryKiB, iterations, parallelism, salt, chunkSize, noncePrefix);
    }

    /// <summary>Allocates the associated-data buffer for this container, with room for the chunk counter.</summary>
    /// <remarks>
    /// The AAD is the whole header followed by the chunk's counter, so a chunk can neither be moved to
    /// another position nor lifted into a container whose header differs by a single byte.
    /// </remarks>
    public byte[] CreateAssociatedData()
    {
        byte[] associatedData = new byte[Bytes.Length + sizeof(uint)];
        Bytes.CopyTo(associatedData, 0);

        return associatedData;
    }

    /// <summary>Allocates the nonce buffer for this container, with the random prefix already in place.</summary>
    public byte[] CreateNonce()
    {
        byte[] nonce = new byte[NonceLength];
        NoncePrefix.CopyTo(nonce, 0);

        return nonce;
    }

    /// <summary>Points the nonce and associated data at one chunk.</summary>
    /// <param name="nonce">A buffer from <see cref="CreateNonce"/>.</param>
    /// <param name="associatedData">A buffer from <see cref="CreateAssociatedData"/>.</param>
    /// <param name="counter">The chunk's zero-based position in the container.</param>
    public static void StampChunk(Span<byte> nonce, Span<byte> associatedData, uint counter)
    {
        BinaryPrimitives.WriteUInt32BigEndian(nonce[NoncePrefixLength..], counter);
        BinaryPrimitives.WriteUInt32BigEndian(associatedData[^sizeof(uint)..], counter);
    }

    /// <summary>The exception raised when a container ends before it should.</summary>
    /// <param name="what">What was being read when the bytes ran out.</param>
    public static EncryptedArchiveException Truncated(string what) =>
        new(EncryptedArchiveProblem.Truncated,
            $"This encrypted archive is incomplete: it ends in the middle of {what}. The file was cut short, or the backup that wrote it never finished.");

    private static EncryptedArchiveException Malformed(string reason) =>
        new(EncryptedArchiveProblem.MalformedHeader, $"This file is not a readable EBK1 archive: {reason}.");

    private static int ReadBoundedUInt32(ReadOnlySpan<byte> source, int minimum, int maximum, string field)
    {
        uint value = BinaryPrimitives.ReadUInt32LittleEndian(source);

        if (value < (uint)minimum || value > (uint)maximum)
        {
            throw Malformed(string.Create(CultureInfo.InvariantCulture, $"its {field} ({value}) is outside the range this build accepts ({minimum}–{maximum})"));
        }

        return (int)value;
    }

    private static async Task ReadExactlyAsync(Stream source, byte[] buffer, CancellationToken cancellationToken)
    {
        int read = await source.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);

        if (read < buffer.Length)
        {
            throw Truncated("its header");
        }
    }
}
