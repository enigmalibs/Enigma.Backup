using System;

namespace Enigma.Backup.Core;

/// <summary>
/// Validation and sanitization for names that must be usable as a file or directory name on
/// <em>every</em> supported operating system. Windows is the strict one — its invalid-character
/// set, reserved device names and trailing-dot rule are applied on Linux too, so profiles and
/// archives stay portable between the two.
/// </summary>
internal static class PortableFileName
{
    /// <summary>Longest name accepted, leaving ample room inside the platform path limits.</summary>
    public const int MaxLength = 128;

    /// <summary>Length a sanitized name is truncated to, leaving room for a `_2` style suffix.</summary>
    public const int SanitizedMaxLength = 100;

    private static readonly char[] InvalidCharacters = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    private static readonly string[] ReservedStems =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    ];

    /// <summary>Tells whether <paramref name="name"/> is usable verbatim as a file name anywhere.</summary>
    public static bool IsValid(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxLength)
        {
            return false;
        }

        if (name != name.Trim() || name.EndsWith('.'))
        {
            return false;
        }

        if (name is "." or "..")
        {
            return false;
        }

        foreach (char character in name)
        {
            if (char.IsControl(character) || Array.IndexOf(InvalidCharacters, character) >= 0)
            {
                return false;
            }
        }

        return !IsReserved(name);
    }

    /// <summary>
    /// Turns arbitrary text into a name <see cref="IsValid"/> accepts: invalid and control
    /// characters become underscores, surrounding whitespace and trailing dots are removed, and the
    /// result is truncated. Falls back to <paramref name="fallback"/> when nothing usable remains.
    /// </summary>
    /// <param name="name">The text to sanitize; may be <see langword="null"/>.</param>
    /// <param name="fallback">A name that is itself valid, used when the result would be empty.</param>
    public static string Sanitize(string? name, string fallback)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return fallback;
        }

        char[] buffer = name.ToCharArray();
        for (int index = 0; index < buffer.Length; index++)
        {
            char character = buffer[index];
            if (char.IsControl(character) || Array.IndexOf(InvalidCharacters, character) >= 0)
            {
                buffer[index] = '_';
            }
        }

        string sanitized = new string(buffer).Trim();
        if (sanitized.Length > SanitizedMaxLength)
        {
            sanitized = sanitized[..SanitizedMaxLength].TrimEnd();
        }

        sanitized = sanitized.TrimEnd('.').TrimEnd();

        if (sanitized.Length == 0 || sanitized is "." or "..")
        {
            return fallback;
        }

        // A reserved device name is still unusable on Windows even though every character is legal.
        return IsReserved(sanitized) ? "_" + sanitized : sanitized;
    }

    private static bool IsReserved(string name)
    {
        int dot = name.IndexOf('.');
        string stem = dot < 0 ? name : name[..dot];

        return Array.Exists(ReservedStems, reserved => string.Equals(stem, reserved, StringComparison.OrdinalIgnoreCase));
    }
}
