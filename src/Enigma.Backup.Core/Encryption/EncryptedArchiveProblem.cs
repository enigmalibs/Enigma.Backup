namespace Enigma.Backup.Core.Encryption;

/// <summary>Why an encrypted archive could not be read.</summary>
public enum EncryptedArchiveProblem
{
    /// <summary>The archive is encrypted, but no password was supplied.</summary>
    PasswordRequired,

    /// <summary>
    /// A chunk failed its authentication tag. The password is wrong, or the file has been altered
    /// since it was written — GCM cannot tell those two apart, and neither can this application.
    /// </summary>
    WrongPasswordOrCorrupted,

    /// <summary>The file ends before the container does: it was cut short or never finished writing.</summary>
    Truncated,

    /// <summary>The container declares a format version this build does not know how to read.</summary>
    UnsupportedVersion,

    /// <summary>The container declares a key-derivation function this build does not know.</summary>
    UnsupportedKeyDerivation,

    /// <summary>
    /// A header field is present but impossible — a zero salt length, a chunk size beyond what any
    /// writer produces — so the file is not a container this build will act on.
    /// </summary>
    MalformedHeader,
}
