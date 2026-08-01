using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.Backup.Core.Encryption;

/// <summary>
/// The write half of the <c>EBK1</c> container: everything written here is cut into fixed-size chunks,
/// each sealed with AES-256-GCM, and framed onto the destination stream.
/// </summary>
/// <remarks>
/// <para>
/// The layout it produces is the header (see <see cref="Ebk1Header"/>) followed by chunks:
/// </para>
/// <code>
/// plainLen     u32 LE   plaintext bytes in this chunk (never more than chunkSize)
/// ciphertext   plainLen bytes
/// tag          16 bytes
/// </code>
/// <para>
/// The last chunk carries <c>plainLen = 0</c>: an authenticated end-of-stream marker whose tag covers
/// nothing but the header and its own counter. Without it a file cut at a chunk boundary would decrypt
/// perfectly and simply stop early; with it, the end of the container is a fact the reader can check.
/// </para>
/// <para>
/// A chunk's nonce and associated data both embed its counter, so a chunk cannot be reordered,
/// duplicated, dropped, or grafted onto another container without failing its tag.
/// </para>
/// </remarks>
internal sealed class Ebk1EncryptStream : Stream
{
    private readonly Stream _destination;
    private readonly Ebk1Cipher _cipher;
    private readonly bool _leaveOpen;
    private readonly byte[] _plain;
    private readonly byte[] _lengthPrefix = new byte[Ebk1Header.ChunkLengthPrefixLength];
    private int _filled;
    private uint _counter;
    private bool _finished;

    private Ebk1EncryptStream(Stream destination, Ebk1Cipher cipher, int chunkSize, bool leaveOpen)
    {
        _destination = destination;
        _cipher = cipher;
        _plain = new byte[chunkSize];
        _leaveOpen = leaveOpen;
    }

    /// <summary>Opens a container on <paramref name="destination"/> and writes its header.</summary>
    /// <param name="destination">The stream the container is written to.</param>
    /// <param name="password">The password to derive the key from; used for this container only.</param>
    /// <param name="options">The parameters the container is written with.</param>
    /// <param name="leaveOpen">Whether <paramref name="destination"/> survives this stream's disposal.</param>
    /// <param name="cancellationToken">Cancels the key derivation and the header write.</param>
    public static async Task<Ebk1EncryptStream> CreateAsync(
        Stream destination,
        string password,
        Ebk1Options options,
        bool leaveOpen = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentException.ThrowIfNullOrEmpty(password);
        ArgumentNullException.ThrowIfNull(options);

        Ebk1Header header = Ebk1Header.Create(options);
        Ebk1Cipher cipher = await Ebk1Cipher.CreateAsync(header, password, cancellationToken).ConfigureAwait(false);

        try
        {
            await destination.WriteAsync(header.Bytes, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            cipher.Dispose();
            throw;
        }

        return new Ebk1EncryptStream(destination, cipher, header.ChunkSize, leaveOpen);
    }

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => !_finished;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    /// <remarks>A blocking wrapper over <see cref="WriteAsync(ReadOnlyMemory{byte}, CancellationToken)"/>; see <see cref="Blocking"/>.</remarks>
    public override void Write(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);

        ReadOnlyMemory<byte> memory = buffer.AsMemory(offset, count);
        Blocking.Run(() => WriteAsync(memory).AsTask());
    }

    /// <inheritdoc cref="Write(byte[], int, int)" />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        // The span cannot cross the asynchronous boundary, so it is copied. Nothing in the pipeline
        // takes this path — the compressors write asynchronously — so the copy costs nothing in
        // practice, and one shared implementation of the chunking is worth more than saving it.
        byte[] copy = buffer.ToArray();
        Blocking.Run(() => WriteAsync(copy).AsTask());
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBufferArguments(buffer, offset, count);

        return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_finished, this);

        while (!buffer.IsEmpty)
        {
            int taken = Fill(buffer.Span);
            buffer = buffer[taken..];

            if (_filled == _plain.Length)
            {
                await EmitChunkAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Pushes what the destination already holds; the current chunk keeps filling.</summary>
    /// <remarks>
    /// A chunk boundary is cut when a chunk fills or when the container closes, never on a flush: a
    /// compressor that flushed often would otherwise pepper the file with tiny chunks, each paying its
    /// own 20 bytes of framing and its own cipher setup, for no benefit — the container is only
    /// meaningful once it is complete.
    /// </remarks>
    public override void Flush() => _destination.Flush();

    /// <inheritdoc cref="Flush" />
    public override Task FlushAsync(CancellationToken cancellationToken) => _destination.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    /// <remarks>Closing the stream is what completes the container, so this is not optional bookkeeping.</remarks>
    public override async ValueTask DisposeAsync()
    {
        try
        {
            // Set before the await, not after: a failure part-way through must not be retried by the
            // synchronous disposal that follows.
            if (!_finished)
            {
                _finished = true;
                await FinishAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            Dispose();
        }
    }

    /// <inheritdoc cref="DisposeAsync" />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                if (!_finished)
                {
                    _finished = true;
                    Blocking.Run(() => FinishAsync(CancellationToken.None).AsTask());
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(_plain);
                _cipher.Dispose();

                if (!_leaveOpen)
                {
                    _destination.Dispose();
                }
            }
        }

        base.Dispose(disposing);
    }

    /// <summary>Writes what is left of the payload, then the marker that closes the container.</summary>
    private async ValueTask FinishAsync(CancellationToken cancellationToken)
    {
        await EmitChunkAsync(cancellationToken).ConfigureAwait(false);
        await EmitTerminatorAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Copies as much of <paramref name="buffer"/> into the pending chunk as it has room for.</summary>
    private int Fill(ReadOnlySpan<byte> buffer)
    {
        int taken = Math.Min(buffer.Length, _plain.Length - _filled);
        buffer[..taken].CopyTo(_plain.AsSpan(_filled));
        _filled += taken;

        return taken;
    }

    private async ValueTask EmitChunkAsync(CancellationToken cancellationToken)
    {
        if (_filled == 0)
        {
            return;
        }

        int count = _filled;
        _filled = 0;

        ReadOnlyMemory<byte> sealedChunk = await _cipher.SealAsync(_plain, count, NextCounter(), cancellationToken).ConfigureAwait(false);
        WriteLengthPrefix(count);
        await _destination.WriteAsync(_lengthPrefix, cancellationToken).ConfigureAwait(false);
        await _destination.WriteAsync(sealedChunk, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask EmitTerminatorAsync(CancellationToken cancellationToken)
    {
        ReadOnlyMemory<byte> sealedChunk = await _cipher.SealAsync(_plain, 0, NextCounter(), cancellationToken).ConfigureAwait(false);
        WriteLengthPrefix(0);
        await _destination.WriteAsync(_lengthPrefix, cancellationToken).ConfigureAwait(false);
        await _destination.WriteAsync(sealedChunk, cancellationToken).ConfigureAwait(false);
    }

    private void WriteLengthPrefix(int plainLength) => BinaryPrimitives.WriteUInt32LittleEndian(_lengthPrefix, (uint)plainLength);

    /// <summary>Hands out the next chunk counter, refusing to reuse one.</summary>
    /// <remarks>
    /// A repeated (key, nonce) pair is the one way to break GCM outright, and the counter is what keeps
    /// the pair unique. Exhausting a 32-bit counter takes 4 PiB of payload at the default chunk size,
    /// so this cannot happen in practice — but "cannot happen" is not a reason to carry on if it does.
    /// </remarks>
    private uint NextCounter()
    {
        if (_counter == uint.MaxValue)
        {
            throw new InvalidOperationException("This archive has reached the maximum number of encrypted chunks a single EBK1 container can hold.");
        }

        return _counter++;
    }
}
