using System;

namespace Enigma.Backup.Core.Profiles;

/// <summary>
/// Thrown when a profile file cannot be understood: malformed JSON, a missing
/// <c>formatVersion</c>, or a format version this build does not read.
/// </summary>
public sealed class ProfileFormatException : Exception
{
    /// <summary>Creates a profile-format exception with a default message.</summary>
    public ProfileFormatException()
        : base("The profile file could not be read.")
    {
    }

    /// <summary>Creates a profile-format exception.</summary>
    /// <param name="message">What is wrong with the file.</param>
    public ProfileFormatException(string message)
        : base(message)
    {
    }

    /// <summary>Creates a profile-format exception wrapping the underlying parse failure.</summary>
    /// <param name="message">What is wrong with the file.</param>
    /// <param name="innerException">The parse failure that was caught.</param>
    public ProfileFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
