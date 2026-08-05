# Enigma.Backup v1.1.0 Release Notes

The app is redrawn. Enigma.Backup is now built on **Enigma.Avalonia.Desktop**, the house control
library — its Fluent theme, its in-window dialogs, its overlay and its notification bar — and it has
a preference of its own for the first time: the theme. Nothing about a backup changed. The pipeline,
the container, the profile format and the file names are the same, so an archive written by any 1.0.x
build opens here exactly as it did.

## What is new

- **A theme preference — `System`, `Light` or `Dark`.** The gear at the end of the profile bar opens
  **Settings**; the choice repaints the app the moment you make it and is remembered afterwards. The
  default is `System`, which follows the desktop rather than pinning a variant — and it is a value you
  can return to, not just the state before your first click.
- **Dialogs open in the window, not beside it.** Every question the app asks — the password, a profile
  name, a confirmation, the Skip / Retry / Abort decision, a restore conflict — is now raised inside
  the main window over a dimmed background instead of as a separate window. Enter and Escape work as
  they did, and dismissing a decision still aborts rather than silently skipping.
- **A run reports itself on a card.** Backup, restore and verify each raise a card over the window
  carrying the phase, the current file, the percentage and **Cancel**. The status bar and progress bar
  at the bottom of the window are gone; the action row is just the three buttons now.
- **The outcome arrives as a banner.** When a run ends, a bar at the top of the window says how it
  went — success, something left out, or a failure — and the **Last run** panel still holds the full
  detail: the archive path, the sizes, the durations and the per-file skipped and warning lists.
- **The window itself is restyled.** The output folder and the exclude-glob field are typed editors
  with their action buttons inline, compression and encryption are grouped as setting cards under
  *Archive options*, and every command and section heading carries a Phosphor icon.

## Files

- **New: `settings.json`**, beside the `profiles/` folder that already exists —
  `%APPDATA%\Enigma.Backup\settings.json` on Windows, `~/.config/Enigma.Backup/settings.json` on
  Linux. It holds the theme preference and nothing else. It is written the first time you change one,
  so **its absence is normal**; deleting it simply returns the app to following the system, and a file
  that has been hand-edited into something unreadable is logged and ignored rather than being allowed
  to stop the app from opening.
- Profiles are untouched, in the same folder and the same format as before.

## Dependencies

Four packages are added, all at 1.0.0, all in the Avalonia-coupled group:

- **Enigma.Avalonia.Desktop** — the control library: its Fluent theme, the `ContentDialog` / `Overlay`
  / `InfoBar` hosts, the file and folder picker services, the `SettingsCard` family and the typed
  editors.
- **Enigma.Icons**, **Enigma.Icons.Phosphor**, **Enigma.Icons.Avalonia** — the icon set used across
  the restyled window and the dialog headers. The glyphs are [Phosphor
  Icons](https://phosphoricons.com/), MIT-licensed upstream.
- **Size.** Together these add about **4.1 MB** of assemblies to the published build, nearly all of it
  `Enigma.Icons.Phosphor.dll` (4.0 MB — the whole glyph set). The zips grow accordingly; for
  reference, 1.0.1's were 45 MB (linux-x64) and 47 MB (win-x64).
- Enigma.Avalonia.Desktop depends on **Enigma.Core 1.0.0**, which loses to the **1.1.0** this solution
  already pins — so the cryptography under an archive is the same build 1.0.1 shipped, and
  `GoldenArchiveCompatibilityTests` is what proves that resolution held.
- Nothing else moved. Avalonia stays at 12.1.1 with its coupled Fluent/Inter/DiagnosticsSupport set,
  ZstdSharp.Port at 0.8.8, the Microsoft.Extensions packages at 10.0.10, CommunityToolkit.Mvvm at
  8.4.2 and xunit.v3 at 3.2.2. `dotnet list package --outdated` reports no update available for any
  project, and `--vulnerable --include-transitive` reports no known vulnerability.

## Compatibility

- **The engine did not change at all.** `Enigma.Backup.Core` — scanning, tar, compression, the `EBK1`
  container, restore and verification — is byte-for-byte identical to the 1.0.1 tag. This release is
  the app around it.
- **The `EBK1` container version byte is unchanged.** Every encrypted archive written by 1.0.0 or
  1.0.1 opens, verifies and restores here, and the 1.0.0 archive checked into the test suite is
  verified and restored byte-for-byte on every build.
- **Profile JSON is unchanged** — same `formatVersion`, same fields, same folder. A profile written by
  1.0.x loads unchanged, and one written here is still readable by 1.0.x.
- **Archive naming and the `.sha256` sidecar format are unchanged**, as are the unencrypted
  `.tar.zst` / `.tar.gz` archives, which remain ordinary tar files any standard tool can unpack.
- **Restore and verify behave as before**, including the per-conflict Skip / Overwrite / Abort choice
  and the Skip / Retry / Abort error policy with *apply to all*.
- The only new file on disk is `settings.json`, and it is additive: 1.0.x ignores it, and this release
  works without it.
- **Windows x64** and **Linux x64**, still shipped as self-contained single-file zips that need no
  installed .NET runtime.

## Version

- Minor release: **1.1.0**. User-visible functionality is added — a theme preference, in-window
  dialogs, the run card and the outcome banner — and nothing breaks: no format, no file name and no
  stored data changed, so every 1.0.x archive and profile still works. A patch would misdescribe a
  redrawn app; a major would claim a compatibility break that did not happen.

---

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
