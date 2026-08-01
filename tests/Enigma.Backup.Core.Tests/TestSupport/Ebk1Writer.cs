using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Enigma.Core.KeyDerivation;
using Enigma.Core.Padding;
using Enigma.Core.Symmetric.BlockCiphers;

namespace Enigma.Backup.Core.Tests.TestSupport;

/// <summary>
/// Writes an <c>EBK1</c> container from the specification, without touching the code that normally
/// writes one.
/// </summary>
/// <remarks>
/// Two things follow from that. A container this produces and the application opens proves the format
/// is a format — implementable from the written spec, not merely a shape one class agrees with
/// itself about. And it puts arbitrary payloads inside a perfectly valid container, which is the only
/// way to test what a restore does with contents that authenticate but are not an archive.
/// </remarks>
public static class Ebk1Writer
{
    private const int TagBits = 128;
    private const int TagLength = 16;
    private const int NoncePrefixLength = 8;

    /// <summary>Builds a complete container around <paramref name="payload"/>.</summary>
    /// <param name="password">The password the key is derived from.</param>
    /// <param name="payload">The bytes to seal.</param>
    /// <param name="chunkSize">Plaintext bytes per chunk.</param>
    /// <param name="memoryKiB">Argon2id memory cost, in kibibytes.</param>
    /// <param name="iterations">Argon2id passes.</param>
    /// <param name="parallelism">Argon2id lanes.</param>
    /// <param name="saltLength">Length of the random salt.</param>
    public static byte[] Create(
        string password,
        byte[] payload,
        int chunkSize = 1024,
        int memoryKiB = 8,
        int iterations = 1,
        int parallelism = 1,
        int saltLength = 8)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(saltLength);
        byte[] noncePrefix = RandomNumberGenerator.GetBytes(NoncePrefixLength);
        byte[] header = BuildHeader(memoryKiB, iterations, parallelism, salt, chunkSize, noncePrefix);

        byte[] key = new Argon2ServiceFactory().CreateArgon2Service().DeriveKey(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            memoryKiB,
            parallelism,
            32,
            Argon2Variant.Argon2id,
            Argon2Version.Version13);

        IBlockCipherService aes = new BlockCipherServiceFactory().CreateAesService(64 * 1024);
        using MemoryStream container = new();
        container.Write(header);

        uint counter = 0;
        for (int offset = 0; offset < payload.Length; offset += chunkSize)
        {
            int length = Math.Min(chunkSize, payload.Length - offset);
            WriteChunk(container, aes, key, header, noncePrefix, counter++, payload.AsSpan(offset, length).ToArray());
        }

        // The end-of-stream marker: an authenticated chunk carrying nothing.
        WriteChunk(container, aes, key, header, noncePrefix, counter, []);

        return container.ToArray();
    }

    private static byte[] BuildHeader(int memoryKiB, int iterations, int parallelism, byte[] salt, int chunkSize, byte[] noncePrefix)
    {
        using MemoryStream header = new();
        header.Write("EBK1"u8);
        header.WriteByte(1);
        header.WriteByte(1);
        WriteUInt32LittleEndian(header, (uint)memoryKiB);
        WriteUInt32LittleEndian(header, (uint)iterations);
        WriteUInt32LittleEndian(header, (uint)parallelism);
        header.WriteByte((byte)salt.Length);
        header.Write(salt);
        WriteUInt32LittleEndian(header, (uint)chunkSize);
        header.Write(noncePrefix);

        return header.ToArray();
    }

    private static void WriteChunk(
        Stream container,
        IBlockCipherService aes,
        byte[] key,
        byte[] header,
        byte[] noncePrefix,
        uint counter,
        byte[] plaintext)
    {
        byte[] nonce = [.. noncePrefix, .. BigEndian(counter)];
        byte[] associatedData = [.. header, .. BigEndian(counter)];

        using MemoryStream sealedChunk = new();
        aes.EncryptAsync(
            new MemoryStream(plaintext, writable: false),
            sealedChunk,
            key,
            nonce,
            BlockCipherMode.Gcm,
            PaddingScheme.None,
            TagBits,
            associatedData).GetAwaiter().GetResult();

        if (sealedChunk.Length != plaintext.Length + TagLength)
        {
            throw new InvalidOperationException($"Expected {plaintext.Length + TagLength} sealed bytes, got {sealedChunk.Length}.");
        }

        WriteUInt32LittleEndian(container, (uint)plaintext.Length);
        container.Write(sealedChunk.GetBuffer(), 0, (int)sealedChunk.Length);
    }

    private static byte[] BigEndian(uint value)
    {
        byte[] encoded = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(encoded, value);

        return encoded;
    }

    private static void WriteUInt32LittleEndian(Stream destination, uint value)
    {
        byte[] encoded = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        destination.Write(encoded);
    }
}
