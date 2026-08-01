using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.Backup.Core.Archiving;

/// <summary>
/// A write-through stream that feeds every byte into a running SHA-256 as it passes.
/// </summary>
/// <remarks>
/// This is what makes the sidecar free: the digest of the finished file is known the moment the last
/// byte is written, so the archive is never read a second time. It is the outermost layer of the
/// pipeline — closest to the file — so the digest covers the bytes on disk, encryption included.
/// </remarks>
internal sealed class HashingWriteStream : Stream
{
    private readonly Stream _destination;
    private readonly bool _leaveOpen;
    private IncrementalHash? _hash;
    private byte[]? _finalHash;

    /// <summary>Creates a hashing stream in front of <paramref name="destination"/>.</summary>
    /// <param name="destination">The stream bytes are forwarded to.</param>
    /// <param name="leaveOpen">Whether <paramref name="destination"/> survives this stream's disposal.</param>
    public HashingWriteStream(Stream destination, bool leaveOpen = true)
    {
        ArgumentNullException.ThrowIfNull(destination);

        _destination = destination;
        _leaveOpen = leaveOpen;
        _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    }

    /// <summary>How many bytes have passed through.</summary>
    public long BytesWritten { get; private set; }

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => BytesWritten;
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// Finishes the digest and returns it as lowercase hex. Idempotent: further calls return the same
    /// value, so it is safe to ask for the digest and then dispose.
    /// </summary>
    public string GetHashHex()
    {
        if (_finalHash is null)
        {
            IncrementalHash hash = _hash ?? throw new ObjectDisposedException(nameof(HashingWriteStream));
            _finalHash = hash.GetHashAndReset();
        }

        return Convert.ToHexStringLower(_finalHash);
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);

        Write(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        AppendData(buffer);
        _destination.Write(buffer);
        BytesWritten += buffer.Length;
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
        AppendData(buffer.Span);
        await _destination.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        BytesWritten += buffer.Length;
    }

    /// <inheritdoc />
    public override void Flush() => _destination.Flush();

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => _destination.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // The digest is kept: a caller that disposes the pipeline before asking still gets it.
            if (_finalHash is null && _hash is not null)
            {
                _finalHash = _hash.GetHashAndReset();
            }

            _hash?.Dispose();
            _hash = null;

            if (!_leaveOpen)
            {
                _destination.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    private void AppendData(ReadOnlySpan<byte> buffer)
    {
        IncrementalHash hash = _hash ?? throw new ObjectDisposedException(nameof(HashingWriteStream));
        hash.AppendData(buffer);
    }
}
