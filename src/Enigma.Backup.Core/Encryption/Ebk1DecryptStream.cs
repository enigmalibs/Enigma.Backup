using System;
using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.Backup.Core.Encryption;

/// <summary>
/// The read half of the <c>EBK1</c> container: reads the header, rebuilds the key from the password,
/// then hands out the payload one authenticated chunk at a time.
/// </summary>
/// <remarks>
/// <para>
/// No byte leaves this stream before the chunk it belongs to has passed its authentication tag, so a
/// caller can never act on data that was tampered with — the failure arrives before the data does.
/// </para>
/// <para>
/// The container is expected to be the whole of the source stream: reaching the end-of-stream marker
/// with bytes still to come is reported as damage, the same as reaching the end of the file without
/// ever seeing the marker.
/// </para>
/// </remarks>
internal sealed class Ebk1DecryptStream : Stream
{
    private readonly Stream _source;
    private readonly Ebk1Cipher _cipher;
    private readonly bool _leaveOpen;
    private readonly byte[] _plain;
    private readonly byte[] _sealedChunk;
    private readonly byte[] _lengthPrefix = new byte[Ebk1Header.ChunkLengthPrefixLength];
    private int _available;
    private int _position;
    private uint _counter;
    private bool _ended;
    private bool _disposed;

    private Ebk1DecryptStream(Stream source, Ebk1Cipher cipher, int chunkSize, bool leaveOpen)
    {
        _source = source;
        _cipher = cipher;
        _plain = new byte[chunkSize];
        _sealedChunk = new byte[chunkSize + Ebk1Header.TagLength];
        _leaveOpen = leaveOpen;
    }

    /// <summary>Opens the container at the current position of <paramref name="source"/>.</summary>
    /// <param name="source">The stream the container is read from, positioned at its first byte.</param>
    /// <param name="password">The password to derive the key from; used for this container only.</param>
    /// <param name="leaveOpen">Whether <paramref name="source"/> survives this stream's disposal.</param>
    /// <param name="cancellationToken">Cancels the header read and the key derivation.</param>
    /// <exception cref="EncryptedArchiveException">The header is cut short, or describes something this build cannot read.</exception>
    public static async Task<Ebk1DecryptStream> CreateAsync(
        Stream source,
        string password,
        bool leaveOpen = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrEmpty(password);

        Ebk1Header header = await Ebk1Header.ReadAsync(source, cancellationToken).ConfigureAwait(false);
        Ebk1Cipher cipher = await Ebk1Cipher.CreateAsync(header, password, cancellationToken).ConfigureAwait(false);

        return new Ebk1DecryptStream(source, cipher, header.ChunkSize, leaveOpen);
    }

    /// <inheritdoc />
    public override bool CanRead => !_disposed;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    /// <remarks>A blocking wrapper over <see cref="ReadAsync(Memory{byte}, CancellationToken)"/>; see <see cref="Blocking"/>.</remarks>
    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);

        Memory<byte> memory = buffer.AsMemory(offset, count);

        return Blocking.Run(() => ReadAsync(memory).AsTask());
    }

    /// <inheritdoc cref="Read(byte[], int, int)" />
    public override int Read(Span<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            return 0;
        }

        // The span cannot cross the asynchronous boundary, so the bytes land in a scratch buffer
        // first. Nothing in the pipeline takes this path — the decompressors read asynchronously.
        byte[] scratch = new byte[buffer.Length];
        int read = Blocking.Run(() => ReadAsync(scratch).AsTask());
        scratch.AsSpan(0, read).CopyTo(buffer);

        return read;
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBufferArguments(buffer, offset, count);

        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        while (_position == _available)
        {
            if (_ended)
            {
                return 0;
            }

            await LoadChunkAsync(cancellationToken).ConfigureAwait(false);
        }

        return Take(buffer.Span);
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            CryptographicOperations.ZeroMemory(_plain);
            _cipher.Dispose();

            if (!_leaveOpen)
            {
                _source.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    /// <summary>Hands out what the current chunk still holds.</summary>
    private int Take(Span<byte> buffer)
    {
        int taken = Math.Min(buffer.Length, _available - _position);
        _plain.AsSpan(_position, taken).CopyTo(buffer);
        _position += taken;

        return taken;
    }

    private async ValueTask LoadChunkAsync(CancellationToken cancellationToken)
    {
        int read = await _source.ReadAtLeastAsync(_lengthPrefix, _lengthPrefix.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
        int total = BeginChunk(read);

        int sealedRead = await _source
            .ReadAtLeastAsync(_sealedChunk.AsMemory(0, total), total, throwOnEndOfStream: false, cancellationToken)
            .ConfigureAwait(false);
        ValidateSealedRead(sealedRead, total);

        int plainLength = await _cipher
            .OpenAsync(_sealedChunk, total, NextCounter(), _plain, cancellationToken)
            .ConfigureAwait(false);

        if (!FinishChunk(plainLength))
        {
            return;
        }

        RejectTrailingData(await _source
            .ReadAtLeastAsync(_lengthPrefix.AsMemory(0, 1), 1, throwOnEndOfStream: false, cancellationToken)
            .ConfigureAwait(false));
    }

    /// <summary>Validates a chunk's length prefix and returns how many sealed bytes follow it.</summary>
    private int BeginChunk(int prefixRead)
    {
        if (prefixRead == 0)
        {
            throw Ebk1Header.Truncated("the payload, without ever reaching the marker that ends it");
        }

        if (prefixRead < _lengthPrefix.Length)
        {
            throw Ebk1Header.Truncated("a chunk header");
        }

        uint plainLength = BinaryPrimitives.ReadUInt32LittleEndian(_lengthPrefix);

        if (plainLength > (uint)_plain.Length)
        {
            throw new EncryptedArchiveException(
                EncryptedArchiveProblem.WrongPasswordOrCorrupted,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"This encrypted archive is damaged: one of its chunks claims to hold {plainLength} bytes, more than the {_plain.Length} the file's own header allows."));
        }

        return (int)plainLength + Ebk1Header.TagLength;
    }

    private static void ValidateSealedRead(int sealedRead, int expected)
    {
        if (sealedRead < expected)
        {
            throw Ebk1Header.Truncated("a chunk's encrypted data");
        }
    }

    /// <summary>Publishes a decrypted chunk, or closes the container when the marker is reached.</summary>
    /// <param name="plainLength">How many plaintext bytes the chunk produced.</param>
    /// <returns><see langword="true"/> when this was the end-of-stream marker.</returns>
    private bool FinishChunk(int plainLength)
    {
        _position = 0;
        _available = plainLength;

        if (plainLength > 0)
        {
            return false;
        }

        _ended = true;

        return true;
    }

    /// <summary>Refuses a container that carries anything after its end-of-stream marker.</summary>
    /// <param name="bytesPastTheEnd">How many bytes a one-byte read past the marker produced.</param>
    private static void RejectTrailingData(int bytesPastTheEnd)
    {
        if (bytesPastTheEnd > 0)
        {
            throw new EncryptedArchiveException(
                EncryptedArchiveProblem.WrongPasswordOrCorrupted,
                "This encrypted archive is damaged: it carries extra data after the marker that ends it.");
        }
    }

    /// <summary>Hands out the next chunk counter; see <see cref="Ebk1EncryptStream"/> for why it cannot wrap.</summary>
    private uint NextCounter()
    {
        if (_counter == uint.MaxValue)
        {
            // No writer can produce this many chunks, so a file that asks for one is not a real container.
            throw Ebk1Cipher.WrongPasswordOrCorrupted();
        }

        return _counter++;
    }
}
