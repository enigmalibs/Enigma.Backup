namespace Enigma.Backup.Desktop.Services;

/// <summary>What a password is being asked for, which decides how the prompt behaves.</summary>
public enum PasswordPurpose
{
    /// <summary>
    /// A password that is about to encrypt a new archive. It is typed twice, because a typo here
    /// produces a backup nobody can ever open.
    /// </summary>
    ProtectNewArchive,

    /// <summary>
    /// The password of an archive that already exists. Typed once — a wrong one is caught by the
    /// archive itself, and asking for it twice would help nobody.
    /// </summary>
    OpenArchive,
}
