using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace Enigma.Backup.Core.Tests.TestSupport;

/// <summary>
/// Reads and edits an <c>EBK1</c> container from the outside, following the layout the plan
/// specifies rather than the code that writes it.
/// </summary>
/// <remarks>
/// The container's internals are not visible to the test assembly, and deliberately so: a test that
/// parsed the file with the same parser that wrote it would agree with any bug they shared. This
/// walks the bytes independently, which is what makes "the header records the parameters" and "chunk
/// 3 is at this offset" claims worth asserting — and it is what lets a test damage one precise byte.
/// </remarks>
public static class Ebk1File
{
    /// <summary>Offset of the version byte.</summary>
    public const int VersionOffset = 4;

    /// <summary>Offset of the key-derivation identifier.</summary>
    public const int KdfIdOffset = 5;

    /// <summary>Offset of the Argon2id memory cost, in kibibytes.</summary>
    public const int MemoryKiBOffset = 6;

    /// <summary>Offset of the salt-length byte.</summary>
    public const int SaltLengthOffset = 18;

    /// <summary>Offset of the salt itself.</summary>
    public const int SaltOffset = 19;

    /// <summary>Length of a chunk's <c>plainLen</c> prefix.</summary>
    public const int ChunkLengthPrefix = 4;

    /// <summary>Length of a chunk's GCM tag.</summary>
    public const int TagLength = 16;

    /// <summary>Reads the container's header.</summary>
    /// <param name="path">The archive to read.</param>
    public static Ebk1HeaderView ReadHeader(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        Assert(bytes.Length >= SaltOffset, "The file is too short to hold an EBK1 header.");
        Assert(bytes.AsSpan(0, 4).SequenceEqual("EBK1"u8), "The file does not start with the EBK1 marker.");

        int saltLength = bytes[SaltLengthOffset];
        int chunkSizeOffset = SaltOffset + saltLength;
        Assert(bytes.Length >= chunkSizeOffset + 12, "The file is too short to hold the rest of its header.");

        return new Ebk1HeaderView(
            bytes[VersionOffset],
            bytes[KdfIdOffset],
            (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(MemoryKiBOffset)),
            (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(10)),
            (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(14)),
            bytes[SaltOffset..(SaltOffset + saltLength)],
            (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(chunkSizeOffset)),
            bytes[(chunkSizeOffset + 4)..(chunkSizeOffset + 12)],
            chunkSizeOffset + 12);
    }

    /// <summary>Walks every chunk in the container, the end-of-stream marker included.</summary>
    /// <param name="path">The archive to read.</param>
    public static IReadOnlyList<Ebk1Chunk> ReadChunks(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        List<Ebk1Chunk> chunks = [];

        for (long offset = ReadHeader(path).Length; offset + ChunkLengthPrefix <= bytes.Length;)
        {
            int plainLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan((int)offset));
            Ebk1Chunk chunk = new(offset, plainLength);
            chunks.Add(chunk);

            if (plainLength == 0 || chunk.EndOffset > bytes.Length)
            {
                break;
            }

            offset = chunk.EndOffset;
        }

        return chunks;
    }

    /// <summary>Flips the lowest bit of one byte, in place.</summary>
    /// <param name="path">The file to damage.</param>
    /// <param name="offset">The byte to flip.</param>
    public static void FlipByte(string path, long offset)
    {
        using FileStream file = new(path, FileMode.Open, FileAccess.ReadWrite);
        file.Seek(offset, SeekOrigin.Begin);
        int original = file.ReadByte();
        Assert(original >= 0, "The file is shorter than the byte to flip.");
        file.Seek(offset, SeekOrigin.Begin);
        file.WriteByte((byte)(original ^ 0x01));
    }

    /// <summary>Overwrites one byte, in place.</summary>
    /// <param name="path">The file to edit.</param>
    /// <param name="offset">The byte to overwrite.</param>
    /// <param name="value">Its new value.</param>
    public static void PatchByte(string path, long offset, byte value)
    {
        using FileStream file = new(path, FileMode.Open, FileAccess.Write);
        file.Seek(offset, SeekOrigin.Begin);
        file.WriteByte(value);
    }

    /// <summary>Overwrites one little-endian 32-bit field, in place.</summary>
    /// <param name="path">The file to edit.</param>
    /// <param name="offset">The field to overwrite.</param>
    /// <param name="value">Its new value.</param>
    public static void PatchUInt32(string path, long offset, uint value)
    {
        byte[] encoded = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);

        using FileStream file = new(path, FileMode.Open, FileAccess.Write);
        file.Seek(offset, SeekOrigin.Begin);
        file.Write(encoded);
    }

    /// <summary>Cuts the file short.</summary>
    /// <param name="path">The file to truncate.</param>
    /// <param name="length">Its new length.</param>
    public static void Truncate(string path, long length)
    {
        using FileStream file = new(path, FileMode.Open, FileAccess.Write);
        file.SetLength(length);
    }

    /// <summary>Exchanges two equally sized chunks, leaving every byte of the file otherwise intact.</summary>
    /// <param name="path">The archive to rearrange.</param>
    /// <param name="first">The first chunk.</param>
    /// <param name="second">The second chunk; must be the same size as <paramref name="first"/>.</param>
    public static void SwapChunks(string path, Ebk1Chunk first, Ebk1Chunk second)
    {
        Assert(first.PlainLength == second.PlainLength, "Only equally sized chunks can be swapped.");

        byte[] bytes = File.ReadAllBytes(path);
        int length = (int)(first.EndOffset - first.Offset);
        byte[] held = bytes[(int)first.Offset..((int)first.Offset + length)];

        Array.Copy(bytes, (int)second.Offset, bytes, (int)first.Offset, length);
        held.CopyTo(bytes, (int)second.Offset);

        File.WriteAllBytes(path, bytes);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

/// <summary>An <c>EBK1</c> header, as read from the file itself.</summary>
/// <param name="Version">The container version byte.</param>
/// <param name="KdfId">The key-derivation identifier.</param>
/// <param name="MemoryKiB">The Argon2id memory cost recorded in the file.</param>
/// <param name="Iterations">The Argon2id pass count recorded in the file.</param>
/// <param name="Parallelism">The Argon2id lane count recorded in the file.</param>
/// <param name="Salt">The salt recorded in the file.</param>
/// <param name="ChunkSize">The chunk size recorded in the file.</param>
/// <param name="NoncePrefix">The random nonce prefix recorded in the file.</param>
/// <param name="Length">How many bytes the header occupies; the offset of the first chunk.</param>
public sealed record Ebk1HeaderView(
    byte Version,
    byte KdfId,
    int MemoryKiB,
    int Iterations,
    int Parallelism,
    byte[] Salt,
    int ChunkSize,
    byte[] NoncePrefix,
    int Length);

/// <summary>One chunk's position in the file.</summary>
/// <param name="Offset">Where the chunk's <c>plainLen</c> prefix starts.</param>
/// <param name="PlainLength">The plaintext length the prefix declares; zero for the end-of-stream marker.</param>
public sealed record Ebk1Chunk(long Offset, int PlainLength)
{
    /// <summary>Where the chunk's ciphertext starts.</summary>
    public long CiphertextOffset => Offset + Ebk1File.ChunkLengthPrefix;

    /// <summary>Where the chunk's authentication tag starts.</summary>
    public long TagOffset => CiphertextOffset + PlainLength;

    /// <summary>Where the next chunk starts.</summary>
    public long EndOffset => TagOffset + Ebk1File.TagLength;
}
