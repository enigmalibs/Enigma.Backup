using System;

namespace Enigma.Backup.Core.Profiles;

/// <summary>Thrown when the store has no profile under the requested name.</summary>
public sealed class ProfileNotFoundException : Exception
{
    /// <summary>Creates an exception for a profile that is not in the store.</summary>
    /// <param name="name">The requested profile name.</param>
    public ProfileNotFoundException(string name)
        : base($"There is no profile named '{name}'.")
    {
        Name = name;
    }

    /// <summary>Creates an exception for a profile that could not be found, wrapping the cause.</summary>
    /// <param name="name">The requested profile name.</param>
    /// <param name="innerException">The failure that was caught.</param>
    public ProfileNotFoundException(string name, Exception innerException)
        : base($"There is no profile named '{name}'.", innerException)
    {
        Name = name;
    }

    /// <summary>The profile name that was looked up.</summary>
    public string Name { get; }
}
