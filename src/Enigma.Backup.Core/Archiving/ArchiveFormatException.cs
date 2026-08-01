using System;

namespace Enigma.Backup.Core.Archiving;

/// <summary>
/// Thrown when a file cannot be read as an Enigma.Backup archive: it is not one, it is truncated,
/// or it contains an entry a restore refuses to write.
/// </summary>
public sealed class ArchiveFormatException : Exception
{
    /// <summary>Creates a format exception with a default message.</summary>
    public ArchiveFormatException()
        : base("The file is not a readable archive.")
    {
    }

    /// <summary>Creates a format exception.</summary>
    /// <param name="message">A ready-to-display explanation.</param>
    public ArchiveFormatException(string message)
        : base(message)
    {
    }

    /// <summary>Creates a format exception with an underlying cause.</summary>
    /// <param name="message">A ready-to-display explanation.</param>
    /// <param name="innerException">The exception that revealed the problem.</param>
    public ArchiveFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
