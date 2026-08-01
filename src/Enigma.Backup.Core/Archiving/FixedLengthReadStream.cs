using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.Backup.Core.Archiving;

/// <summary>
/// A read-through stream that always delivers exactly the length it promised, and reports the bytes
/// it hands out as it goes.
/// </summary>
/// <remarks>
/// <para>
/// A tar entry's size is written into its header <em>before</em> its data, so the data has to match
/// the size to the byte. A file that is being written to while the backup runs — a log, a database, a
/// file the user just saved — breaks that promise, and the archive would be silently corrupt. So a
/// source that grew is read only up to the length recorded in the header, and a source that shrank is
/// padded with zeros to it and reports <see cref="SourcePadded"/>, so the run can warn about the
/// file instead of lying about it.
/// </para>
/// <para>This is also where per-file byte progress comes from, which is what keeps a multi-gigabyte
/// file from looking like a frozen progress bar.</para>
/// </remarks>
internal sealed class FixedLengthReadStream : Stream
{
    private readonly Stream _source;
    private readonly long _length;
    private readonly Action<int>? _onBytesRead;
    private readonly bool _leaveOpen;
    private long _position;
    private bool _sourceEnded;

    /// <summary>Creates a fixed-length view over <paramref name="source"/>.</summary>
    /// <param name="source">The stream to read from, positioned at its start.</param>
    /// <param name="length">The number of bytes this stream promises to deliver.</param>
    /// <param name="onBytesRead">Called with the size of every chunk handed out; may be <see langword="null"/>.</param>
    /// <param name="leaveOpen">Whether <paramref name="source"/> survives this stream's disposal.</param>
    public FixedLengthReadStream(Stream source, long length, Action<int>? onBytesRead = null, bool leaveOpen = true)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        _source = source;
        _length = length;
        _onBytesRead = onBytesRead;
        _leaveOpen = leaveOpen;
    }

    /// <summary>Whether the source ran out early, so the tail was padded with zeros.</summary>
    public bool SourcePadded { get; private set; }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => _source.CanSeek;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => _length;

    /// <inheritdoc />
    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
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
        int available = Available(buffer.Length);
        if (available == 0)
        {
            return 0;
        }

        Span<byte> window = buffer[..available];
        int read = _sourceEnded ? 0 : _source.Read(window);

        return Accept(window, read);
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
        int available = Available(buffer.Length);
        if (available == 0)
        {
            return 0;
        }

        Memory<byte> window = buffer[..available];
        int read = _sourceEnded ? 0 : await _source.ReadAsync(window, cancellationToken).ConfigureAwait(false);

        return Accept(window.Span, read);
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        long target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, "Unknown seek origin."),
        };

        ArgumentOutOfRangeException.ThrowIfNegative(target, nameof(offset));

        _source.Seek(Math.Min(target, _length), SeekOrigin.Begin);
        _position = target;
        _sourceEnded = false;

        return _position;
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

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

    private int Available(int requested) => (int)Math.Min(requested, Math.Max(0, _length - _position));

    /// <summary>Turns a source read into what this stream hands out, padding a source that ended early.</summary>
    private int Accept(Span<byte> window, int read)
    {
        if (read == 0)
        {
            // The source is done but the header already promised more: zero-fill the rest so the
            // entry stays exactly the size it claims to be.
            _sourceEnded = true;
            SourcePadded = true;
            window.Clear();
            read = window.Length;
        }

        _position += read;
        _onBytesRead?.Invoke(read);

        return read;
    }
}
