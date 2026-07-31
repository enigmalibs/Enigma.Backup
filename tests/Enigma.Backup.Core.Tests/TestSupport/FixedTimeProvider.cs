using System;

namespace Enigma.Backup.Core.Tests.TestSupport;

/// <summary>
/// A <see cref="TimeProvider"/> frozen at one instant, so an archive's timestamped file name is
/// something a test can assert on rather than guess at.
/// </summary>
public sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;

    /// <summary>Creates a provider frozen at <paramref name="now"/>.</summary>
    /// <param name="now">The instant every call reports.</param>
    public FixedTimeProvider(DateTimeOffset now)
    {
        _now = now;
    }

    /// <inheritdoc />
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _now;
}
