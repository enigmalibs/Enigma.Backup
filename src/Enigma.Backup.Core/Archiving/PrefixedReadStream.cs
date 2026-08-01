using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.Backup.Core.Archiving;

/// <summary>
/// Hands back a few bytes that have already been read, then gets out of the way.
/// </summary>
/// <remarks>
/// A decrypted payload cannot be rewound — the bytes were produced, not stored — but its first four
/// bytes still have to be inspected to tell a zstd frame from a gzip member. This replays them in
/// front of the rest of the stream, so the decompressor below receives the payload whole.
/// </remarks>
internal sealed class PrefixedReadStream : Stream
{
    private readonly byte[] _prefix;
    private readonly Stream _rest;
    private readonly bool _leaveOpen;
    private int _prefixPosition;

    /// <summary>Creates a stream that yields <paramref name="prefix"/> and then <paramref name="rest"/>.</summary>
    /// <param name="prefix">The bytes already taken from <paramref name="rest"/>, in order.</param>
    /// <param name="rest">The stream the remaining bytes come from.</param>
    /// <param name="leaveOpen">Whether <paramref name="rest"/> survives this stream's disposal.</param>
    public PrefixedReadStream(byte[] prefix, Stream rest, bool leaveOpen = true)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(rest);

        _prefix = prefix;
        _rest = rest;
        _leaveOpen = leaveOpen;
    }

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
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
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
        int taken = TakePrefix(buffer);

        return taken > 0 || buffer.IsEmpty ? taken : _rest.Read(buffer);
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
        int taken = TakePrefix(buffer.Span);

        return taken > 0 || buffer.IsEmpty ? taken : await _rest.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
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
        if (disposing && !_leaveOpen)
        {
            _rest.Dispose();
        }

        base.Dispose(disposing);
    }

    private int TakePrefix(Span<byte> buffer)
    {
        int remaining = _prefix.Length - _prefixPosition;
        if (remaining <= 0)
        {
            return 0;
        }

        int taken = Math.Min(buffer.Length, remaining);
        _prefix.AsSpan(_prefixPosition, taken).CopyTo(buffer);
        _prefixPosition += taken;

        return taken;
    }
}
