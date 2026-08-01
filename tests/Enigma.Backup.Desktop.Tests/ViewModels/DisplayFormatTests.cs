using System;
using Enigma.Backup.Desktop.ViewModels;
using Xunit;

namespace Enigma.Backup.Desktop.Tests.ViewModels;

public sealed class DisplayFormatTests
{
    [Theory]
    [InlineData(0, "0 bytes")]
    [InlineData(1, "1 byte")]
    [InlineData(812, "812 bytes")]
    [InlineData(1023, "1023 bytes")]
    [InlineData(1024, "1 KiB")]
    [InlineData(1536, "1.5 KiB")]
    [InlineData(1_048_576, "1 MiB")]
    [InlineData(16_881_254, "16.1 MiB")]
    [InlineData(3_221_225_472, "3 GiB")]
    public void BytesAreShownInBinaryUnits(long value, string expected) =>
        Assert.Equal(expected, DisplayFormat.Bytes(value));

    [Theory]
    [InlineData(0.8, "0.8 s")]
    [InlineData(42.14, "42.1 s")]
    [InlineData(185, "3 min 05 s")]
    [InlineData(3_840, "1 h 04 min")]
    public void DurationsGrowUnitsAsTheyGrow(double seconds, string expected) =>
        Assert.Equal(expected, DisplayFormat.Duration(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void ANegativeDurationIsShownAsNone() =>
        Assert.Equal("0.0 s", DisplayFormat.Duration(TimeSpan.FromSeconds(-5)));

    [Theory]
    [InlineData(0, "0 files")]
    [InlineData(1, "1 file")]
    [InlineData(12, "12 files")]
    [InlineData(1234, "1,234 files")]
    public void CountsTakeTheRightNoun(long value, string expected) =>
        Assert.Equal(expected, DisplayFormat.Count(value, "file", "files"));

    [Fact]
    public void TimestampsAreShownInLocalTime() =>
        Assert.Equal(
            "2026-08-01 14:32",
            DisplayFormat.Timestamp(new DateTimeOffset(new DateTime(2026, 8, 1, 14, 32, 0, DateTimeKind.Local))));
}
