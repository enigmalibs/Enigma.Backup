using System;
using System.Globalization;

namespace Enigma.Backup.Desktop.ViewModels;

/// <summary>
/// Turns the numbers the core reports into the short strings the window shows.
/// </summary>
/// <remarks>
/// Sizes are binary — a "MiB" here is 1 048 576 bytes, which is what a backup's own buffers and
/// chunk sizes are measured in, so the summary and the container spec agree. The invariant culture
/// is used deliberately: the app's strings are hardcoded English, so its numbers are too.
/// </remarks>
public static class DisplayFormat
{
    private static readonly string[] Units = ["KiB", "MiB", "GiB", "TiB", "PiB"];

    /// <summary>Formats a byte count, e.g. <c>0 bytes</c>, <c>812 bytes</c>, <c>16.1 MiB</c>.</summary>
    /// <param name="value">The number of bytes.</param>
    public static string Bytes(long value)
    {
        if (value < 1024)
        {
            return value == 1 ? "1 byte" : string.Create(CultureInfo.InvariantCulture, $"{value} bytes");
        }

        double scaled = value;
        int unit = -1;

        while (scaled >= 1024 && unit < Units.Length - 1)
        {
            scaled /= 1024;
            unit++;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{scaled:0.#} {Units[unit]}");
    }

    /// <summary>Formats an elapsed time, e.g. <c>0.8 s</c>, <c>42.1 s</c>, <c>3 min 05 s</c>, <c>1 h 04 min</c>.</summary>
    /// <param name="value">How long something took.</param>
    public static string Duration(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        if (value.TotalSeconds < 60)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{value.TotalSeconds:0.0} s");
        }

        if (value.TotalHours < 1)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{(int)value.TotalMinutes} min {value.Seconds:00} s");
        }

        return string.Create(CultureInfo.InvariantCulture, $"{(int)value.TotalHours} h {value.Minutes:00} min");
    }

    /// <summary>Formats a timestamp in local time, e.g. <c>2026-08-01 14:32</c>.</summary>
    /// <param name="value">The instant to show.</param>
    public static string Timestamp(DateTimeOffset value) =>
        value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Formats a count with the right singular or plural noun, e.g. <c>1 file</c>, <c>27 files</c>.</summary>
    /// <param name="value">How many.</param>
    /// <param name="singular">The noun for one, e.g. "file".</param>
    /// <param name="plural">The noun for any other number, e.g. "files".</param>
    public static string Count(long value, string singular, string plural) =>
        string.Create(CultureInfo.InvariantCulture, $"{value:N0} {(value == 1 ? singular : plural)}");
}
