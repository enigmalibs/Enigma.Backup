using System;

namespace Enigma.Backup.Core.Profiles;

/// <summary>
/// Thrown when an operation would overwrite a different, existing profile — renaming onto an
/// occupied name, for instance. Saving a profile under its own name is an ordinary overwrite and
/// does not raise this.
/// </summary>
public sealed class ProfileAlreadyExistsException : Exception
{
    /// <summary>Creates an exception for an occupied profile name.</summary>
    /// <param name="name">The name that is already taken.</param>
    public ProfileAlreadyExistsException(string name)
        : base($"A profile named '{name}' already exists.")
    {
        Name = name;
    }

    /// <summary>Creates an exception for an occupied profile name, wrapping the cause.</summary>
    /// <param name="name">The name that is already taken.</param>
    /// <param name="innerException">The failure that was caught.</param>
    public ProfileAlreadyExistsException(string name, Exception innerException)
        : base($"A profile named '{name}' already exists.", innerException)
    {
        Name = name;
    }

    /// <summary>The profile name that is already taken.</summary>
    public string Name { get; }
}
