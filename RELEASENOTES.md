# Enigma.Backup v1.0.1 Release Notes

A maintenance release. It moves the cryptography dependency forward and adds the regression guard
that makes moving it forward safe to do again. There is no change to the application, to the archive
format, or to anything you interact with — an archive written by 1.0.0 is opened by 1.0.1 exactly as
it always was.

## Dependencies

- **Enigma.Core 1.0.0 → 1.1.0** — the package providing Argon2id and AES-256-GCM. It is a
  dependency-only release upstream: its public API is unchanged, and the change it carries is the one
  below.
- **BouncyCastle.Cryptography 2.6.2 → 2.7.0**, transitively — the primitives sitting directly under
  every encrypted archive.
- Every other package was current at release time and is unchanged: Avalonia 12.1.1 (and its coupled
  Fluent/Inter/DiagnosticsSupport set), ZstdSharp.Port 0.8.8, the Microsoft.Extensions 10.0.10
  packages, CommunityToolkit.Mvvm 8.4.2 and xunit.v3 3.2.2. No dependency reports a known
  vulnerability.

## Compatibility

- **The `EBK1` container version byte is unchanged**, and archives written by 1.0.0 stay readable.
- This is no longer only an assurance. A real 1.0.0 archive — produced by the actual backup pipeline,
  at the shipped Argon2id parameters — is checked into the test suite, and the build verifies it
  against its sidecar and restores it byte-for-byte on every run. A change in Argon2id or AES-256-GCM
  arriving through a future dependency bump fails the build instead of quietly making people's
  backups unopenable, which is a class of failure no round-trip test can detect: a round trip
  encrypts and decrypts with the same code, so it agrees with itself whatever it does.
- Encryption throughput is unchanged, measured directly against 1.0.0 on the same machine.
- Profiles, output filenames, the sidecar format and the unencrypted `.tar.zst` / `.tar.gz` archives
  are all untouched.

## Version

- Maintenance release: **1.0.1**. No API change, no behaviour change and no format change, so this is
  a patch — even though the dependency it carries numbered the same change a minor for itself.

---

# Enigma.Backup v1.0.0 Release Notes

The first release of **Enigma.Backup** — a cross-platform desktop app that packs selected files and
folders into a single compressed, optionally encrypted archive, and restores and verifies it again.
The whole product is one streaming pipeline, `scan → tar → compress → [encrypt] → file`, run in a
single pass with no temporary files, so a backup's size is bounded by the disk rather than by memory.
Everything the user is asked during a run — a password, an unreadable file, a restore conflict —
enters the engine through an interface, which is what keeps the core library free of any UI.

## Feature overview

- **Profiles** — a named set of input paths, output folder, compression choice, encryption flag and
  exclude globs, stored as one JSON file per profile in the per-user config folder. Save, Save as,
  Rename, Delete, plus Import/Export to arbitrary locations for moving a profile between machines.
- **Input scanning** — files and folders in any mix, canonicalized and deduplicated so overlapping
  inputs are merged rather than archived twice; the output folder is always excluded from its own
  backup; glob exclusion patterns (`**/bin/**`, `*.log`) are applied during the scan; hidden files
  are included and directory symlinks are recorded as warnings rather than followed.
- **Archives** — one timestamped output per run, `<Profile>_yyyy-MM-dd_HHmmss.tar.zst` or `.tar.gz`,
  written beside a `.sha256` sidecar in `sha256sum` format that is computed on the output bytes as
  they are written. Unencrypted archives are ordinary tar files, restorable with standard tools.
- **Encryption** — optional AES-256-GCM in chunked segments (the `EBK1` container), with the key
  derived by Argon2id at 64 MiB / 3 iterations / parallelism 4 and the parameters and salt recorded
  in the file header. The password is a parameter, prompted per run and never stored, logged or
  cached. Wrong passwords, tampering and truncation are all detected and reported distinctly.
- **Restore and verify** — restore streams an archive back to a destination with a per-conflict
  Skip / Overwrite / Abort choice; verify re-reads an archive against its sidecar and, when
  encrypted, authenticates every GCM tag.
- **Failure handling** — an unreadable or missing entry raises Skip / Retry / Abort with an
  *apply to all further errors* option; everything skipped is listed in the end-of-run summary.
  Abort and Cancel both delete the partial archive, so no half-written backup survives a run.
- **Cross-cutting** — every long operation reports live progress (current file, bytes, overall
  percentage), is cancellable, and cannot take the window down: a cancellation, an abort and an
  outright failure each end as a summary rather than an exception.

## Compatibility

- **Windows x64** and **Linux x64**, shipped as self-contained single-file zips that need no
  installed .NET runtime.
- Built on **.NET 10** with **Avalonia 12.1.1**; the core library targets `net10.0` because the
  archive pipeline is built on `System.Formats.Tar`.
- Encryption is provided by **Enigma.Core 1.0.0** (Argon2id + AES-256-GCM), compression by
  **ZstdSharp.Port 0.8.8** and the BCL's `GZipStream`.
- The `EBK1` container is at version 1. Field details may be refined within version 1; any change
  after this release bumps the version byte, so archives written by 1.0.0 stay readable.

## Version

- Initial release: **1.0.0**.
