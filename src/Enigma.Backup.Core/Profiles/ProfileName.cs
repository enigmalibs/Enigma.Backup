using System;
using System.Runtime.CompilerServices;

namespace Enigma.Backup.Core.Profiles;

/// <summary>
/// The naming rules for profiles. A profile's name doubles as its file name in the store, so it is
/// validated against the strictest supported platform (Windows) rather than sanitized behind the
/// user's back — and validated <em>before</em> it is ever combined into a path, which is what stops
/// a name such as <c>../evil</c> from escaping the store directory.
/// </summary>
public static class ProfileName
{
    /// <summary>The longest accepted profile name.</summary>
    public const int MaxLength = PortableFileName.MaxLength;

    /// <summary>Name used when nothing usable can be derived from an imported file.</summary>
    public const string Fallback = "profile";

    /// <summary>Tells whether <paramref name="name"/> may be used as a profile name.</summary>
    /// <param name="name">The candidate name.</param>
    /// <returns><see langword="true"/> when the name is usable on Windows and Linux alike.</returns>
    public static bool IsValid(string? name) => PortableFileName.IsValid(name);

    /// <summary>Returns <paramref name="name"/> when it is valid, and throws otherwise.</summary>
    /// <param name="name">The candidate name.</param>
    /// <param name="parameterName">Filled in by the compiler; names the offending argument.</param>
    /// <returns>The validated name.</returns>
    /// <exception cref="ArgumentException">The name is not a usable profile name.</exception>
    public static string EnsureValid(
        string? name,
        [CallerArgumentExpression(nameof(name))] string? parameterName = null)
    {
        if (!IsValid(name))
        {
            throw new ArgumentException(
                $"'{name}' is not a valid profile name. Use up to {MaxLength} characters, without " +
                @"< > : "" / \ | ? *, without leading or trailing spaces or a trailing dot.",
                parameterName);
        }

        return name!;
    }

    /// <summary>
    /// Turns arbitrary text — an imported file's stem, say — into a valid profile name, replacing
    /// what cannot be used and falling back to <see cref="Fallback"/> when nothing remains.
    /// </summary>
    /// <param name="name">The text to turn into a profile name.</param>
    /// <returns>A name <see cref="IsValid(string?)"/> accepts.</returns>
    public static string Sanitize(string? name) => PortableFileName.Sanitize(name, Fallback);
}
