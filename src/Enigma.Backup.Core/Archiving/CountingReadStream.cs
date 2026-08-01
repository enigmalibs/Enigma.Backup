using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.Backup.Core.Archiving;

/// <summary>
/// A forward-only read-through stream that counts what it has handed out.
/// </summary>
/// <remarks>
/// A restore cannot know how much data an archive expands to without reading it twice, so its
/// progress is measured on the compressed input instead: bytes pulled out of this stream against the
/// archive's size on disk, which is a real percentage rather than a guess.
/// </remarks>
internal sealed class CountingReadStream : Stream
{
    private readonly Stream _source;
    private readonly bool _leaveOpen;

    /// <summary>Creates a counting stream over <paramref name="source"/>.</summary>
    /// <param name="source">The stream to read from.</param>
    /// <param name="leaveOpen">Whether <paramref name="source"/> survives this stream's disposal.</param>
    public CountingReadStream(Stream source, bool leaveOpen = true)
    {
        ArgumentNullException.ThrowIfNull(source);

        _source = source;
        _leaveOpen = leaveOpen;
    }

    /// <summary>How many bytes have been read so far.</summary>
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
        BytesRead += read;

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
        BytesRead += read;

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
        if (disposing && !_leaveOpen)
        {
            _source.Dispose();
        }

        base.Dispose(disposing);
    }
}
