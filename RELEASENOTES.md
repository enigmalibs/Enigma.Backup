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
