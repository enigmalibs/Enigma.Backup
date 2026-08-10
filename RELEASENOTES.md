# Enigma.Backup v1.2.0 Release Notes

Two additions to what a backup can *be*, and two corrections to where the app keeps things. An input
can now be a **rule** rather than a place — a folder plus a glob, worked out again on every run — and
an archive can now be a plain **`.zip`**, the file any file manager opens without this application.
**Restore…** and **Verify…** moved to the top bar, where they belong, and **Settings** learned to open
the folder it writes to and to say which build you are running.

The engine underneath is the one you already have: the `EBK1` container version byte has not moved,
and the real 1.0.0 archive checked into the test suite is still verified and restored byte-for-byte on
every build.

## What is new

- **Pattern inputs — a backup input can be a rule.** **`Add pattern…`** joins `Add files…` and
  `Add folder…`: pick the folder the rule applies to, type the glob, and the row reads
  `folder ▸ pattern`. It is re-resolved on **every** run, so files that appear after you saved the
  profile are picked up rather than missed. `*` matches inside one folder level and `**` at any depth —
  the same language, and the same platform case sensitivity, as the exclude field. One deliberate
  extension in the include direction: **a pattern that matches a folder's name takes that folder with
  everything in it**, which is what `DirPrefix*` is asking for and what strict file-glob semantics would
  silently answer with nothing. Excludes still win over a match, and a pattern that matches nothing is
  reported as a **warning** in the *Last run* panel — the base folder is recorded in the archive anyway,
  so it says where it looked. Silence would leave "my backup is smaller than I thought" as the only
  symptom.
- **A plain `.zip` archive format.** A third entry in *Archive options → Compression* — *"Zip (.zip) —
  Opens anywhere without this app. Cannot be encrypted, and keeps no Unix modes."* It is written in the
  same single pass as the tar formats, with the same `.sha256` sidecar beside it, and **Restore…** and
  **Verify…** open it in-app exactly as they open the others. So does every unzip tool.
- **A zip is never encrypted, and the app says so rather than pretending.** Choosing Zip clears the
  *Encrypt with a password* toggle and disables it, with the reason on the card; a hand-edited profile
  asking for both is refused with a clear message before a single byte is written. The BCL offers no zip
  encryption at all, ZipCrypto is broken, and WinZip-AES would not open in Explorer — every alternative
  either defeats the point of choosing zip or offers something that merely looks like protection. If you
  want encryption, choose Zstandard or Gzip.
- **Restore… and Verify… are in the top bar now**, beside the settings gear, and the bottom bar is the
  accent **Back up** alone. Neither command ever read anything from the form — both are "pick an
  archive, go" — but sitting beside **Back up** they read as its peers, which was exactly wrong. Nothing
  about what they do changed.
- **Settings names the folder it writes to, and opens it.** A **Storage** card carries the
  `%APPDATA%\Enigma.Backup` / `~/.config/Enigma.Backup` path — the folder holding both `settings.json`
  and `profiles/` — with an **Open folder** button that shows it in your file manager, creating it first
  if nothing has been written there yet.
- **Settings says which version you are running.** An **About** section below the cards: the app icon,
  the name, `Version 1.2.0`, the copyright line, and a link to the project page.

## Files

- **A profile is written as the lowest format version that can express it.** A profile carrying at
  least one **pattern input**, or **`Compression: Zip`**, is stamped `formatVersion` **2**. Every other
  profile is still written as `formatVersion` **1**, byte-for-byte as 1.1.0 wrote it. Both are read
  here; what a profile *says* is always what it *is*.
- **A zip run writes the same pair of files as any other run** —
  `<Profile>_yyyy-MM-dd_HHmmss.zip` and `<Profile>_yyyy-MM-dd_HHmmss.zip.sha256` — in the output
  folder, with the naming and the `sha256sum` sidecar format unchanged.
- **Nothing new is stored.** The Storage card and the About section only read what is already there;
  `settings.json` still holds the theme and nothing else.

## Dependencies

- **Nothing was added and nothing moved.** Zip comes from the BCL's `System.IO.Compression`, so a third
  archive format costs no package at all.
- The set is unchanged: Avalonia 12.1.1 with its coupled Fluent/Inter/DiagnosticsSupport set and
  Enigma.Avalonia.Desktop 1.0.0, the three Enigma.Icons packages at 1.0.0, Enigma.Core 1.1.0,
  ZstdSharp.Port 0.8.8, the Microsoft.Extensions packages at 10.0.10, CommunityToolkit.Mvvm 8.4.2 and
  xunit.v3 3.2.2.
- `dotnet list package --outdated` reports no update available for any of the four projects, and
  `--vulnerable --include-transitive` reports no known vulnerability.

## Compatibility

- **The `EBK1` container version byte is unchanged.** Every encrypted archive written by 1.0.0, 1.0.1
  or 1.1.0 opens, verifies and restores here. This is not an assurance: a real 1.0.0 archive is checked
  into the test suite and is verified and restored byte-for-byte on every build.
- **Archive naming and the `.sha256` sidecar format are unchanged**, and the unencrypted `.tar.zst` /
  `.tar.gz` archives are still ordinary tar files any standard tool unpacks.
- **A profile that uses nothing new is still `formatVersion` 1**, and remains readable by 1.0.x and
  1.1.0 exactly as 1.1.0 promised. A test pins that rule, because it is what the promise stands on.
- **A profile that uses a pattern input or Zip is `formatVersion` 2, and 1.0.x / 1.1.0 refuse it — by
  design.** They stop with their existing "different format version" message instead of reading it as
  less than it says. This is forward incompatibility, chosen deliberately: an older build quietly
  backing up fewer files than the profile asks for is the failure a backup tool must not have. Nothing
  already in your hands breaks — an older build simply declines a profile that asks for something it
  does not have.
- **`.zip` archives open in any stock tool** — Explorer, Finder, `unzip`, 7-Zip. They use Deflate with
  trailing data descriptors (the consequence of writing in one pass), no Zip64 and no encryption.
- **What a zip does not carry.** Timestamps are DOS dates: two-second resolution, and a hard
  1980–2107 range that an out-of-range modification time is **clamped** into rather than failing the run
  over. Unix permission bits are not representable in the format and are not written, so an executable
  restored from a zip needs its bit set again. Tar keeps both. This is the price of a file that anything
  can open, and it is stated on the format's own row in the drop-down rather than left to be discovered.
- **Restore and verify behave as before**, including the per-conflict Skip / Overwrite / Abort choice
  and the Skip / Retry / Abort error policy with *apply to all* — the zip restore runs through the same
  path-traversal rejection, the same conflict question and the same progress as the tar one.
- **Windows x64** and **Linux x64**, still shipped as self-contained single-file zips that need no
  installed .NET runtime.

## Version

- Minor release: **1.2.0**. User-visible functionality is added — pattern inputs, a third archive
  format, the relocated commands and the Settings additions — and nothing already released breaks: the
  `EBK1` byte, archive naming, the sidecar format and version-1 profiles are all untouched, so every
  archive and every feature-free profile still works. A patch would misdescribe two new capabilities;
  a major would claim a compatibility break that did not happen. An older build declining a profile that
  asks for a feature it does not have is forward incompatibility — stated above rather than hidden, and
  exactly what a minor is for.

---

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
