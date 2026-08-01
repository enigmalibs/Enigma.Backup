using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.Backup.Core.Archiving;

/// <summary>
/// A read-through stream that feeds every byte into a running SHA-256 as it passes — the mirror of
/// <see cref="HashingWriteStream"/>.
/// </summary>
/// <remarks>
/// It is what lets a verification check an encrypted archive in one pass: the decryption reads the
/// file <em>through</em> this stream, so the digest that proves the file has not changed and the tags
/// that prove it decrypts are both produced by the same single read of the disk.
/// </remarks>
internal sealed class HashingReadStream : Stream
{
    private readonly Stream _source;
    private readonly bool _leaveOpen;
    private readonly Action<long>? _onBytesRead;
    private IncrementalHash? _hash;
    private byte[]? _finalHash;

    /// <summary>Creates a hashing stream over <paramref name="source"/>.</summary>
    /// <param name="source">The stream bytes are read from.</param>
    /// <param name="onBytesRead">Called with the running total after every read; may be <see langword="null"/>.</param>
    /// <param name="leaveOpen">Whether <paramref name="source"/> survives this stream's disposal.</param>
    public HashingReadStream(Stream source, Action<long>? onBytesRead = null, bool leaveOpen = true)
    {
        ArgumentNullException.ThrowIfNull(source);

        _source = source;
        _onBytesRead = onBytesRead;
        _leaveOpen = leaveOpen;
        _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    }

    /// <summary>How many bytes have passed through.</summary>
    public long BytesRead { get; private set; }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => BytesRead;
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// Finishes the digest and returns it as lowercase hex. Idempotent, and safe to call before
    /// disposal or after it.
    /// </summary>
    public string GetHashHex()
    {
        if (_finalHash is null)
        {
            IncrementalHash hash = _hash ?? throw new ObjectDisposedException(nameof(HashingReadStream));
            _finalHash = hash.GetHashAndReset();
        }

        return Convert.ToHexStringLower(_finalHash);
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);

        return Read(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        int read = _source.Read(buffer);
        Accept(buffer[..read]);

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
        int read = await _source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        Accept(buffer.Span[..read]);

        return read;
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
        if (disposing)
        {
            // The digest is kept: a caller that disposes before asking still gets it.
            if (_finalHash is null && _hash is not null)
            {
                _finalHash = _hash.GetHashAndReset();
            }

            _hash?.Dispose();
            _hash = null;

            if (!_leaveOpen)
            {
                _source.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    private void Accept(ReadOnlySpan<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            return;
        }

        IncrementalHash hash = _hash ?? throw new ObjectDisposedException(nameof(HashingReadStream));
        hash.AppendData(buffer);
        BytesRead += buffer.Length;
        _onBytesRead?.Invoke(BytesRead);
    }
}
