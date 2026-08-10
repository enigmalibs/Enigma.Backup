# Security Policy

Enigma.Backup writes people's backups, and encrypts them with a password that is never stored
anywhere. A defect in the archive pipeline can silently leave data out of a backup; a defect in the
`EBK1` container can make an encrypted archive unreadable — or readable by someone who should not be
able to read it — and in both cases the damage is only discovered when the backup is needed.
Vulnerability reports are taken seriously and handled with priority.

## Supported versions

Security fixes are provided for the latest released version. Enigma.Backup follows
[Semantic Versioning](https://semver.org/), and users are encouraged to stay current with the
newest release.

| Version | Supported          |
|---------|--------------------|
| 1.2.x   | :white_check_mark: |
| 1.1.x   | :x:                |
| 1.0.x   | :x:                |

## Reporting a vulnerability

**Please do not report security vulnerabilities through public GitHub issues, discussions, or pull
requests.** Public disclosure before a fix is available puts every user at risk.

Instead, use **GitHub's private vulnerability reporting**:

1. Go to the repository's **Security** tab.
2. Select **Report a vulnerability** to open a private advisory.
3. Include as much detail as you can — the affected version, the component involved, a description
   of the issue, and, where possible, a minimal reproduction and its impact.

This keeps the report private between you and the maintainers while it is triaged and fixed.

**Never attach a real backup archive or a real password to a report.** If a reproduction needs an
archive, build one from throwaway files with a throwaway password, and say which it is.

## What to expect

- Your report will be acknowledged and triaged as promptly as possible.
- The issue will be investigated and, once confirmed, a fix prepared and released.
- Coordinated disclosure is preferred: please allow a reasonable period for a fix to ship before
  any public discussion of the vulnerability.
- Your contribution will be credited in the resulting advisory unless you ask to remain anonymous.

## Scope

In scope: the archive pipeline (scanning, tar, compression, restore, verification), the `EBK1`
encryption container and its key derivation, profile storage and import/export, and the handling of
passwords in the desktop app.

Because the cryptography is provided by [Enigma.Core](https://www.nuget.org/packages/Enigma.Core),
which in turn builds on BouncyCastle, an issue rooted in the AES-256-GCM or Argon2id primitives
themselves should also be reported upstream to those projects. How Enigma.Backup *uses* them — the
container framing, the nonce and AAD construction, the KDF parameters — is in scope here.

**A `.zip` archive is never encrypted, and the app refuses to pretend otherwise.** Zip exists as a
format that opens anywhere, which is incompatible with the protection this app offers: choosing it
clears and disables the encryption toggle with the reason on the card, and a profile hand-edited to
ask for both fails the run before a single byte is written. The BCL provides no zip encryption at all,
ZipCrypto is broken, and WinZip-AES would not open in the very tools that make zip worth choosing — so
the honest answer is no, rather than something that merely looks like a password. Encryption lives in
the `EBK1` container: choose Zstandard or Gzip for a backup that needs it.

Out of scope: the strength of a password you choose, and recovery of an archive whose password has
been lost. There is deliberately no recovery path, and adding one would be the vulnerability.
