using System;

namespace Enigma.Backup.Desktop.Tests.TestSupport;

/// <summary>A <see cref="TimeProvider"/> whose clock moves only when a test says so.</summary>
/// <remarks>
/// The safety net measures "how long ago was this error closed" with timestamps, not wall-clock dates,
/// so it is the timestamp this provider controls.
/// </remarks>
internal sealed class ManualTimeProvider : TimeProvider
{
    private long _ticks;

    /// <inheritdoc />
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc />
    public override long GetTimestamp() => _ticks;

    /// <summary>Moves the clock forward.</summary>
    /// <param name="by">How far.</param>
    public void Advance(TimeSpan by) => _ticks += by.Ticks;
}
