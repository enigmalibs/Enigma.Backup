namespace Enigma.Backup.Core.Verification;

/// <summary>What a verification found.</summary>
public enum VerificationOutcome
{
    /// <summary>The archive's digest matches its sidecar: the file on disk is intact.</summary>
    Succeeded,

    /// <summary>The archive's digest does not match its sidecar: the file has changed since it was written.</summary>
    HashMismatch,

    /// <summary>There is no <c>.sha256</c> sidecar beside the archive, so there is nothing to compare against.</summary>
    SidecarMissing,

    /// <summary>The sidecar exists but holds no readable SHA-256 digest.</summary>
    SidecarUnreadable,

    /// <summary>
    /// The archive is encrypted and no password was supplied, so only the file itself could be
    /// checked — its contents were not opened.
    /// </summary>
    PasswordRequired,

    /// <summary>
    /// A chunk of an encrypted archive failed its authentication tag: the password is wrong, or the
    /// payload has been altered.
    /// </summary>
    AuthenticationFailed,
}
