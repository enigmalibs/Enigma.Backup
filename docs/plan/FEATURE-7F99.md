# FEATURE-7F99 — Pattern inputs, zip format & window polish — 1.2.0 release

**Status:** IN PROGRESS
**Type:** multi-phase feature (5 phases)
**Branches (created by `/build`, each from `HEAD`):** one per phase, see *Phases*

## Objective

Ship **1.2.0**: four user-facing improvements planned together because they release together.

1. **Pattern inputs** — a backup input can be a *(base folder, glob)* pair such as `C:\Logs` +
   `*.log` or `DirPrefix*`, re-evaluated on every run, alongside the existing file and folder
   inputs.
2. **A plain `.zip` archive format** — for users who want an archive Explorer, Finder or any unzip
   tool opens, with full in-app parity: back up, verify *and* restore.
3. **Restore… and Verify… move out of the form's visual flow** — to the top bar beside the settings
   gear. They have never depended on the form (`MainWindowViewModel.OnRestoreAsync` /
   `OnVerifyAsync` are file-picker-driven end to end); the move makes that fact visible. The bottom
   action bar keeps a single accent **Back up**.
4. **The Settings dialog grows** — a *Storage* card with an **Open folder** button on the
   `<config>/Enigma.Backup` folder (which holds `settings.json` and `profiles/`), and an **About**
   section: app icon, name, version, copyright, and a link to the project page.

`EBK1` is untouched by every phase: the container version byte stays 1, the golden fixture is never
regenerated, and every compatibility claim in `RELEASENOTES.md` keeps holding. The zip format is a
**sibling of** the container, not a change to it.

## Validated decisions (interview 2026-08-10)

- **Zip is a full-parity output format, not a codec.** Backing up with Zip writes
  `<stem>.zip` (Deflate) plus the usual `.sha256` sidecar; Restore… and Verify… open it in-app
  exactly like the tar formats. "Zip as a compression inside EBK1/tar" was offered and ruled out —
  the output would still be `.tar.*`/`.enc` and Explorer could not open it, which is the whole
  motive.
- **Zip cannot be encrypted, and says so.** Selecting Zip forces the *Encrypt with a password*
  toggle off and disables it, with the card explaining why; a hand-edited profile carrying both
  fails the run with a clear error before anything is written. Rationale: `System.IO.Compression`
  has no zip encryption at all, ZipCrypto is broken, and WinZip-AES (a new third-party dependency in
  the crypto-sensitive path) still would not open in Explorer — every alternative either defeats
  zip's purpose or offers false security. Passwords remain parameters, never state.
- **A pattern input is a (base folder, pattern) pair**, entered through a new **Add pattern…**
  dialog: pick the base folder, type the glob, with validation as you type. A single combined
  string (`C:\Logs\*.log`) was offered and declined — parsing the static prefix out of a typed path
  has edge cases and no picker help.
- **Standard glob depth, one rulebook per window.** `*` matches within one path segment, `**` at
  any depth, `?` unsupported — exactly the exclude field's `Microsoft.Extensions.FileSystemGlobbing`
  semantics, with platform-driven case sensitivity (`PathUtilities.Comparison`). `*.log` therefore
  matches only directly under the base folder; the pattern dialog documents `**` with an example.
- **One documented extension: a pattern that matches a folder's name takes the whole folder.**
  `DirPrefix*` includes every directory under the base whose name matches — with its entire
  contents — plus any files named like that. This is what the request's own example means; strict
  file-glob semantics would silently match nothing when only directories carry the prefix. The
  exclude field keeps strict semantics; the difference is documented in the pattern dialog.
- **Profile `formatVersion` 2, written only when needed.** A profile is saved as version 2 **iff**
  it uses a 1.2.0-only feature (at least one pattern input, or `Compression: Zip`); 1.0.x/1.1.0
  builds then refuse it loudly with their existing "different format version" error. A profile
  using no new feature keeps version 1 and remains readable in both directions, preserving the
  1.1.0 release-notes claim. Alternatives rejected: a version-1 sibling field (old builds would
  silently back up less than the profile promises, then drop the field on save — silent-data-loss
  behaviour in a backup tool), and always-version-2 (gratuitous break for untouched profiles).
- **Zip appears as a third entry in the existing Compression combo** — `CompressionAlgorithm.Zip`,
  serialized by name like `Zstd`/`Gzip`, presented as "Zip (.zip) — opens anywhere; cannot be
  encrypted". One control, no second profile field; the documented impurity is that a container
  format sits in a codec enum. A separate *Archive format* card was offered and declined.
- **Restore…/Verify… go to the top bar, grouped beside the gear** — the window's existing
  "app-level, form-independent" corner — chosen over a right-aligned bottom-bar group and over a
  Tools menu flyout. The bottom bar keeps only the accent **Back up**, making the form → Back up
  relationship unmistakable.
- **About shows: version, copyright, and a project-page link** (plus the icon and name that anchor
  the section). Version comes from the assembly's informational version with the `+sha` suffix
  stripped; copyright from the assembly attributes fed by `Directory.Build.props`; the link is
  `https://github.com/enigmalibs/Enigma.Backup` (the repo's `origin`), opened through the same
  launcher seam as the Open-folder button — never via a view-level `NavigateUri`, so the action is
  testable and the ViewModel stays Avalonia-free.
- **One work item, five phases**, mirroring how FEATURE-17FE shipped 1.1.0: features first, release
  readiness last, each phase independently buildable and reviewable on its own branch.
- **Version 1.2.0 — a minor.** Functionality is added (patterns, zip, the dialog's new section) and
  nothing released breaks: the `EBK1` byte, archive naming, the sidecar and v1 profiles are all
  unchanged. Old builds refusing *new-feature* profiles is forward incompatibility, stated in the
  notes — not a break of anything already in people's hands.

### Findings established before planning

- **Restore and Verify already read nothing from the form.** `OnRestoreAsync`
  (`MainWindowViewModel.cs:645-685`) is archive picker → destination picker → run, with a lazy
  password prompt on `EncryptedArchiveException`; `OnVerifyAsync` (:687-713) is archive picker →
  run, re-verifying with a password only when the result says one is required. Both are
  `CanExecute: !IsBusy` only. PHASE03 is therefore XAML-only.
- **An input is a bare `string` end to end** — `BackupProfile.Inputs : IList<string>`
  (`BackupProfile.cs:26-27`), `ObservableCollection<string>` in `ProfileEditorViewModel`, and the
  scanner canonicalizes every input with `Path.GetFullPath` and routes non-existent ones to the
  Skip/Retry/Abort handler (`InputScanner.cs:76,168-190`). A pattern smuggled into that list would
  be reported as a missing path — patterns need their own typed representation and their own
  resolve path.
- **The globbing machinery already exists.** `GlobExcluder` (`Scanning/GlobExcluder.cs`) wraps
  `Microsoft.Extensions.FileSystemGlobbing.Matcher` with `PathUtilities.Comparison`
  (OrdinalIgnoreCase on Windows/macOS, Ordinal on Linux) and already carries the
  directory-node trick (`**/bin/**` also matches `bin` itself). Pattern inputs reuse the library
  and the comparison, in the include direction.
- **The profile format guard is strict and total.** `ProfileSerializer` reads `formatVersion` from
  the raw document and rejects any value ≠ 1 on read *and* write (`ProfileSerializer.cs:36-41,
  70-81`); unknown enum names throw `JsonException` → `ProfileFormatException` (:88-91); unknown
  fields are silently ignored and dropped on the next save. This is exactly why version-2-when-
  needed is the safe encoding: the old build's failure is loud and immediate.
- **`ZipArchive(ZipArchiveMode.Create)` writes single-pass to a non-seekable stream** — local
  headers with bit 3 + trailing data descriptors, which Explorer reads fine. The streaming pipeline
  shape survives unchanged: `FileStream → HashingWriteStream → ZipArchive`. One honest nuance: the
  central directory's per-entry records stay in memory until dispose (~100 bytes/entry — linear in
  entry *count*, still independent of archive *size*; recorded under *Risks*).
- **Compression has no seam today.** The compressor is a `switch` in internal static
  `CompressionStreams.CreateCompressor` (`CompressionStreams.cs:30-42`); `BackupService` names
  `TarWriter` directly (:174) and `RestoreService` names `TarReader`. Zip does not fit the
  stream-codec seam at all (it is an archive, not a stream transform) — PHASE02 introduces a small
  internal archive-writer/reader seam instead of forcing zip through `CompressionStreams`.
- **Format detection is magic-byte based and filename-blind** (`ArchiveFormatDetector.cs:12-22`):
  gzip `1F 8B`, zstd `28 B5 2F FD`, `EBK1`. Zip joins the table so *restore* names the format
  instead of failing with "not an Enigma.Backup archive" — that error is reached only from
  `RestoreService`; verify never calls the detector (it deliberately hashes and reports *any*
  file, `VerificationService.cs:217-221`, so a `.zip` with a sidecar verifies already today).
  One trap: a **zero-entry** zip has no local file header at all and opens with the
  end-of-central-directory signature `50 4B 05 06`, not `50 4B 03 04` — and an empty backup is
  reachable (every input skipped as missing), so the detector learns **both** signatures. The
  EBK1 container records no compression field, so nothing in the container changes.
- **Verify is already format-agnostic on its checksum half** — it hashes bytes against the sidecar
  and only probes the `EBK1` magic to decide whether per-chunk authentication is possible
  (`VerificationService.cs:82,177-229`). A zip verifies via the sidecar; there is no encrypted half
  to authenticate, by construction.
- **No open-in-file-manager capability exists anywhere** — not in the app (zero hits for
  `Process.Start`/`ILauncher`/`TopLevel.Launcher`) and not in `Enigma.Avalonia.Desktop`'s service
  roster. Avalonia 12's `TopLevel.Launcher` (`ILauncher.LaunchDirectoryInfoAsync` /
  `LaunchUriAsync`) is the primitive; per the invariants it enters through a new `Desktop/Services/`
  interface whose implementation is handed the `TopLevel` once in
  `App.OnFrameworkInitializationCompleted`, like the pickers.
- **Nothing reads a version at runtime yet**, and `<Version>1.2.0</Version>` flows into
  `AssemblyInformationalVersion` as `1.2.0+<git-sha>` via the SDK — About strips the `+` suffix.
  `Directory.Build.props` also carries the Authors/Copyright the About line needs.
- **The only art is `Assets/enigma-backup.ico`** (placeholder; four BMP-encoded frames
  16/32/48/256 — none is a PNG, so no frame can be *extracted* as one); no standalone PNG exists
  in the repo. About gets a PNG **converted** from the 256 px frame (decode, re-encode) — still
  placeholder; final art comes from the user later (CLAUDE.md).
- **The settings folder is already composed in two places** —
  `AppSettingsStore.ResolveDefaultPath` and `ProfileStore.ResolveDefaultDirectory` both build on
  `Environment.SpecialFolder.ApplicationData` + `"Enigma.Backup"`. The Open-folder button opens
  that shared parent, `Path.GetDirectoryName(AppSettingsStore.DefaultPath)` — the folder holding
  both `settings.json` and `profiles/`.
- **The shared `ContentDialog` host caps every dialog at 680×560** (`MainWindow.axaml:418-421`).
  The grown Settings dialog (theme card + storage card + About) must fit; checked visually.

## Design

### Pattern inputs (Core)

- **Model.** `PatternInput` record in `Core/Profiles/` — `BaseDirectory` (absolute path) +
  `Pattern` (relative glob, `/` separators, `\` normalized on read like excludes). New profile
  property `PatternInputs : IList<PatternInput>`, JSON `"patternInputs": [{"baseDirectory": …,
  "pattern": …}]`, camelCase like everything else.
- **Format version.** `BackupProfile.CurrentFormatVersion` becomes 2, with a
  *minimum-necessary-version* rule in `ProfileSerializer`: write `2` iff
  `PatternInputs.Count > 0 || Compression == Zip`, else `1`; accept `1` and `2` on read; keep
  rejecting anything else on read and write. A test pins that saving a feature-free profile still
  writes `1` — that line is what the release-notes compatibility claim stands on.
- **Scanning.** `InputScanner` resolves each pattern input independently of the path-input
  merge/overlap logic (patterns never merge; a base folder that duplicates a plain input is handled
  by the existing root-name collision suffixing, `AllocateRootName`). Per pattern input:
  - missing/blank base directory → the existing `MissingInput` Skip/Retry/Abort flow;
  - walk the base folder's subtree exactly like a folder input (same ordering, same symlink and
    output-dir rules), matching each entry's base-relative path with a new internal
    `PatternMatcher` built on `Matcher` + `PathUtilities.Comparison`:
    a **file** is included when it matches the pattern; a **directory** is included — with its
    entire subtree — when its own relative path matches (the `DirPrefix*` rule); directories on the
    path *to* a match are emitted as directory entries so the archive keeps its structure, but an
    unmatched, match-free directory never appears;
  - exclude globs then apply on top, against the same base-relative path, exactly as for folder
    inputs (excludes win over pattern matches);
  - the entries' `ArchivePath` root is the base folder's allocated root name — a pattern input
    archives as "the matching part of that folder";
  - a pattern input whose walk produces **zero** entries emits a new scan warning
    (`PatternMatchedNothing`, carrying base + pattern) — a legitimate outcome (no logs today), not
    an error; the base folder's root directory entry is still emitted so the archive records where
    it looked.
- **Progress/summary.** Pattern inputs count into the existing scan totals; no new progress shape.

### Pattern inputs (Desktop)

- `ProfileEditorViewModel.Inputs` becomes a collection of a small immutable input descriptor
  (path-or-pattern discriminated: `InputItem.Path(string)` / `InputItem.Pattern(base, glob)`),
  which `ToProfile`/`LoadProfile` split into `Inputs` + `PatternInputs`. Dedup: paths compare as
  today (`PathComparison`); patterns compare as (base, pattern) pairs — base with `PathComparison`,
  pattern with the same comparison the matcher uses. Drag-drop and both pickers keep producing path
  items unchanged.
- The `ListBox` item template renders paths exactly as today and patterns as
  `C:\Logs ▸ *.log` (full pair in the tooltip), so a pattern is visually distinguishable at a
  glance.
- **Add pattern…** button joins *Add files… / Add folder…*. It raises a new dialog through
  `IDialogService` (one new method + one new `FakeDialogService` response):
  `PatternInputDialogViewModel` + `UserControl` view — a folder row (picker via the existing
  `IFilePicker.PickFolderAsync`, path shown read-only) and a pattern `TextEditor`, with two lines of
  inline help (`*` one level, `**` any depth, a folder-name match takes the folder). Validation
  drives `IsPrimaryButtonEnabled` from the ViewModel (the dialog's buttons ignore `CanExecute` —
  CLAUDE.md): base folder chosen, pattern non-blank, pattern not rooted (`/`-leading or
  drive-qualified patterns rejected). The content takes keyboard focus on attach at
  `DispatcherPriority.Loaded`, and Enter submits via `KeyBinding`, like every other dialog.
- `Editor.CanRun` counts any input item, path or pattern.

### Zip format (Core)

- `CompressionAlgorithm.Zip` third enum member (name-serialized; old builds refuse the name
  loudly). `CompressionOption` (Desktop) gains the matching row.
- **A small internal archive seam instead of a zip-shaped hack.** `CompressionStreams` models
  stream codecs; zip is an archive. `BackupService.WriteArchiveAsync` gets an internal
  entry-writer seam (tar+codec implementation wrapping today's `TarWriter` code path verbatim; zip
  implementation over `ZipArchive(ZipArchiveMode.Create, leaveOpen: true)` writing to the hashing
  stream — `FileStream → HashingWriteStream → ZipArchive`, single pass, no temp files). Deflate at
  `CompressionLevel.Optimal`, matching the gzip convention. Directory entries are written with
  trailing `/`; file entries carry `LastWriteTime` clamped to the representable DOS range,
  **1980-01-01 … 2107-12-31** — `ZipArchiveEntry.LastWriteTime` throws outside it on *both*
  sides, so a forged far-future mtime must clamp, not abort the run mid-write. Unix modes are not
  representable and not written — a documented fidelity limit, not compensated.
- **Guard.** `Encrypt == true && Compression == Zip` throws a clear, actionable
  `ProfileFormatException`-family error out of `BackupService` before any byte is written (the UI
  prevents the combination; the guard catches hand-edited profiles).
- **Naming.** `ArchiveNaming` maps Zip → `.zip` (no `.enc` arm — unreachable by the guard);
  sidecar `<archive>.zip.sha256` via the unchanged `Sha256Sidecar`.
- **Detection.** `ArchiveLayer.Zip` on **two** signatures in `ArchiveFormatDetector`:
  `50 4B 03 04` and the end-of-central-directory `50 4B 05 06` (a zero-entry zip has no local
  file header and starts with the latter). A zip nested inside EBK1 is rejected like nested EBK1
  is today (nothing writes it; refusing beats guessing).
- **Restore.** `RestoreService` branches on the detected layer: `ArchiveLayer.Zip` opens
  `ZipArchive(ZipArchiveMode.Read)` over the already-seekable source `FileStream` and walks entries
  through the **same** decision path as tar: identical path-traversal rejection (rooted or
  `..`-escaping names), the same Skip/Overwrite/Abort conflict handler with apply-to-all, the same
  progress reporting, directory timestamps applied deepest-first where zip provides them, file
  `LastWriteTime` applied from the entry. An encrypted-zip password prompt never occurs (zip is
  never encrypted).
- **Verify.** Unchanged — `VerificationService` never calls the detector: it deliberately hashes
  and reports *any* file against its sidecar, so a `.zip` verifies today already, and the
  friendlier format naming benefits **restore** (the only place "not an Enigma.Backup archive" is
  raised). A password supplied for a zip is silently ignored exactly as it is for an unencrypted
  tar — and the UI flow can never supply one, since it prompts only on `PasswordRequired`, which
  an unencrypted file never returns.

### Zip format (Desktop)

- Compression combo: "Zip (.zip) — Opens anywhere; cannot be encrypted." Selecting it sets
  `Encrypt = false` and disables the toggle (new `CanEncrypt` property on
  `ProfileEditorViewModel`, bound to the SettingsCard content's `IsEnabled`; the card's description
  explains why when Zip is active). Switching away re-enables the toggle (previous state not
  restored — off is the honest default after a format that forbade it).
- `FilePicker` archive filter patterns gain `*.zip` for Restore…/Verify….

### Restore… / Verify… relocation

`MainWindow.axaml` only. The profile-bar `Grid` (`ColumnDefinitions="Auto,280,*,Auto"`) grows a
column: the `ProfileStatus` text keeps the star column, then a right-aligned `glyphrow`-style
group — **Restore…**, **Verify…**, a thin vertical separator (`EnigmaBorderSubtleBrush`), then the
existing gear. Buttons keep their icons (`ArrowCounterClockwise`, `SealCheck`), bindings and
`MinWidth`/`Height` from the bottom bar; being inside the `IsEnabled="{Binding IsIdle}"` border is
correct (both commands are `!IsBusy` anyway). The bottom `Border.panel` keeps only the accent
**Back up** button. Checked visually at the 960 px minimum width — the status text may truncate
(it is `muted`, lowest-value content in the row) but the buttons and combo must not collapse.

### Settings dialog: Storage card, About section

- **Launcher seam.** `IShellLauncher` in `Desktop/Services/` — `Task OpenFolderAsync(string path)`,
  `Task OpenUriAsync(Uri uri)`. Implementation `ShellLauncher` wraps Avalonia's
  `TopLevel.Launcher` (`LaunchDirectoryInfoAsync` / `LaunchUriAsync`); the `TopLevel` is handed to
  it once in `App.OnFrameworkInitializationCompleted` beside the picker wiring; calling it
  unregistered throws `InvalidOperationException` like the library services do. Registered in
  `AddEnigmaBackupDesktop`; headless tests register a fake. No ViewModel names Avalonia.
- **Storage card.** A second `SettingsCard` (Header *Storage*, folder icon): description carries
  the `<config>/Enigma.Backup` path and the existing "delete settings.json and the app follows the
  system again" sentence (absorbing today's `StorageNote` `SelectableTextBlock`, which is removed);
  content is an **Open folder** button bound to a new `OpenStorageFolderCommand` on
  `SettingsDialogViewModel` → `IShellLauncher.OpenFolderAsync`. The folder is created if missing
  before opening (first-run: nothing has been written yet — opening an error dialog instead of the
  folder would be absurd).
- **About section.** Below the cards, visually separated: the app icon (new
  `Assets/enigma-backup.png`, converted from the placeholder ico's 256 px frame — still
  placeholder art), "Enigma.Backup", "Version 1.2.0", the copyright line, and a **project page**
  link-styled button → `OpenProjectPageCommand` → `IShellLauncher.OpenUriAsync
  (https://github.com/enigmalibs/Enigma.Backup)`. Version and copyright come from a small
  `AppInfo` static (Desktop) reading `AssemblyInformationalVersionAttribute` (stripped at `+`) and
  `AssemblyCopyrightAttribute` — no Avalonia types, so the ViewModel uses it directly and headless
  tests assert the values.
- `SettingsDialogViewModel` gains the `IShellLauncher` dependency; the whole dialog must fit the
  host's 680×560 cap — checked visually in both variants.

## Phases

### PHASE01 — Pattern inputs (DONE)

Branch: `feature/feature-7f99-phase01-pattern-inputs`

1. Core model: `PatternInput`, `BackupProfile.PatternInputs`, serializer round-trip;
   `CurrentFormatVersion = 2` with the minimum-necessary-version write rule and 1-or-2 read rule.
2. Core scanning: `PatternMatcher` (include-direction matching with the folder-subtree rule),
   pattern resolution in `InputScanner` (missing-base flow, exclude interaction, root naming,
   structure-only directories, `PatternMatchedNothing` warning).
3. Desktop: input descriptor collection in `ProfileEditorViewModel` (+ `ToProfile`/`LoadProfile`,
   dedup, `CanRun`), list template with the `base ▸ pattern` rendering, **Add pattern…** command.
4. Desktop: `PatternInputDialogViewModel` + view + `IDialogService.RequestPatternInputAsync` +
   `FakeDialogService` response; validation drives `IsPrimaryButtonEnabled`; focus-on-attach and
   Enter `KeyBinding` per the dialog invariants.
5. Tests — Core: matcher semantics (one-level `*`, `**`, folder-name inclusion, per-platform case
   sensitivity), scanner integration (structure dirs, excludes win, empty match warns, missing base
   routes to the handler, root-name collision with a plain input), serializer (v1 stays v1;
   patterns force v2; v2 reads back; v3 refused; old-shape v1 profiles load). Desktop: editor
   round-trip with mixed inputs, dialog validation states, `CanRun` with patterns only.

**Acceptance:** Debug and Release build clean, zero warnings and zero `AVLN*` warnings; full suite
green; a feature-free profile saved by this build is byte-identical in `formatVersion` to 1.1.0's
output; manually — add `*.log` and `DirPrefix*` patterns against a prepared folder, run a backup,
and confirm the archive holds exactly the expected entries, the empty-match warning appears in the
Last-run panel when nothing matches, and both variants render the new list items and dialog
correctly (screenshots in the completion doc).

### PHASE02 — Zip archive format (DONE)

Branch: `feature/feature-7f99-phase02-zip-format`

1. Core: `CompressionAlgorithm.Zip`; the internal archive entry-writer seam with the tar path
   moved over verbatim and the zip writer added (`ZipArchive` over the hashing stream, Deflate
   Optimal, dir entries, DOS timestamps); the zip+encrypt guard; `ArchiveNaming` → `.zip`;
   serializer: Zip forces `formatVersion` 2 (rule from PHASE01).
2. Core: `ArchiveLayer.Zip` detection on both zip signatures (`PK\x03\x04`, and the zero-entry
   zip's `PK\x05\x06`); `RestoreService` zip branch on the same traversal/conflict/progress path.
   `VerificationService` needs no change.
3. Desktop: `CompressionOption` row; `CanEncrypt` off+disabled under Zip with the explanatory card
   description; `FilePicker` archive filters `*.zip`.
4. Tests — Core: zip backup → verify → restore round trip (content, structure, timestamps within
   DOS resolution, out-of-range mtimes clamped not thrown); sidecar correctness; empty-archive
   run (a zero-entry zip detects via `PK\x05\x06` and restores to nothing, like the empty tar
   formats); conflict Skip/Overwrite/Abort on zip restore; traversal-attack zip (crafted
   rooted/`..` entry names) refused; zip+encrypt profile refused with the clear error; detector
   recognizes both zip signatures; `GoldenArchiveCompatibilityTests` untouched and green.
   Desktop: selecting Zip clears+disables encrypt, switching back re-enables; profile round-trip
   with Zip.
5. Interop check (manual, recorded): the produced `.zip` opens in a stock tool on Linux (`unzip
   -t`) and, when available, Windows Explorer.

**Acceptance:** builds clean as above; full suite green with the golden fixture untouched;
manually — an unencrypted zip backup of a nested tree restores byte-identically in-app *and*
extracts with a stock unzip tool; verify reports the checksum result; the Encrypt toggle is
visibly disabled with its explanation under Zip (screenshots both variants).

### PHASE03 — Restore & Verify relocation (TODO)

Branch: `feature/feature-7f99-phase03-action-buttons`

1. `MainWindow.axaml`: move the two buttons into the profile-bar grid beside the gear (right
   aligned, separator, icons and bindings kept); reduce the bottom action bar to the accent
   **Back up**.
2. No ViewModel changes (commands, `CanExecute` and flows are untouched).
3. Tests: none new (a headless suite cannot see placement); the existing suite must pass
   unmodified.

**Acceptance:** builds clean, zero `AVLN*`; suite green with no test edits; manually — the window
reads correctly at the default and the 960 px minimum width in both variants (screenshots), the
two buttons still run their flows from the new position, and both are disabled while a run is
active exactly as before.

### PHASE04 — Settings folder & About (TODO)

Branch: `feature/feature-7f99-phase04-settings-about`

1. `IShellLauncher` / `ShellLauncher` in `Desktop/Services/`, registered in
   `AddEnigmaBackupDesktop`, `TopLevel` handed over in `App.OnFrameworkInitializationCompleted`;
   fake for the test harness.
2. `AppInfo` (informational version stripped at `+`, copyright) + `Assets/enigma-backup.png`
   converted from the ico's 256 px frame — the frames are BMP-encoded, so this is a
   decode-and-re-encode, not an extraction (placeholder, like the ico).
3. `SettingsDialogViewModel`: `OpenStorageFolderCommand` (create-if-missing, then launch),
   `OpenProjectPageCommand`, version/copyright properties; `SettingsDialog.axaml`: Storage card
   absorbing the storage note, About section (icon, name, version, copyright, link-styled button).
4. Tests: `AppInfo` strips the `+sha` and never throws on a missing attribute; the two commands hit
   the fake launcher with the right path/URI; the storage-folder command creates the folder when
   absent; DI registration test covers `IShellLauncher`.
5. The e2e Desktop test keeps passing — its fake roster gains the launcher.

**Acceptance:** builds clean, zero `AVLN*`; suite green; manually — Open folder opens the real
`~/.config/Enigma.Backup` in the file manager on the Linux box (and creates it on a fresh
config), the project link opens the browser, the About section shows "Version 1.2.0", the icon
renders, and the grown dialog fits the 680×560 host cap in both variants (screenshots).

### PHASE05 — Release readiness: 1.2.0 (TODO)

Branch: `feature/feature-7f99-phase05-release`

1. `<Version>1.2.0</Version>` in `Directory.Build.props`.
2. `RELEASENOTES.md` 1.2.0 entry in the house shape — *What is new* (patterns, zip, the moved
   buttons, the Settings additions), *Files* (profiles using new features are `formatVersion` 2;
   all others stay 1), *Dependencies* (nothing added — zip is BCL; nothing moved), *Compatibility*
   (EBK1 byte unchanged, golden fixture proves 1.0.0 archives still open; v1 profiles readable
   both ways; **v2 profiles are refused by 1.0.x/1.1.0 with a clear error — by design**; `.zip`
   archives open in any stock tool; zip fidelity limits stated), *Version* (minor, justified).
3. `README.md`: what's-new callout, Capabilities (patterns, zip), *Using it* walkthrough
   (top-bar Restore/Verify, Add pattern…, zip option, Settings storage/About), Installing zip
   names at 1.2.0, *What a backup run writes* (`.zip` + sidecar), *Restoring without
   Enigma.Backup* (zip = any unzip tool).
4. `SECURITY.md`: supported-versions row for `1.2.x`; a sentence that zip archives are never
   encrypted and the app refuses to pretend otherwise.
5. `docs/QA-CHECKLIST.md`: new/updated sections — pattern inputs (match, empty match, missing
   base), zip (backup/restore/verify, Explorer interop, encrypt disabled), the relocated buttons,
   Settings storage + About.
6. `docs/RELEASE.md`: only if a step stopped matching reality.
7. `dotnet list package --outdated` / `--vulnerable --include-transitive`, findings recorded.
8. Print the merge/tag/publish runbook. No zips, no tag, no push — the artifacts stay with the
   user.

**Acceptance:** Release build clean and suite green at 1.2.0; every `docs/RELEASE.md` §1 box
ticked or explicitly deferred with a reason; no stale version string in the docs; the runbook
printed.

## Files expected to change

**Created** — `Core/Profiles/PatternInput.cs`; `Core/Scanning/PatternMatcher.cs`; the internal
archive writer/reader seam files in `Core/Backups/` / `Core/Archiving/` / `Core/Restoring/` (names
at the builder's discretion); `Desktop/ViewModels/PatternInputDialogViewModel.cs` + view;
`Desktop/ViewModels/InputItem.cs` (or equivalent descriptor); `Desktop/Services/IShellLauncher.cs`
+ `ShellLauncher.cs`; `Desktop/AppInfo.cs`; `Desktop/Assets/enigma-backup.png`; new Core and
Desktop test files per phase; `docs/done/FEATURE-7F99-PHASE01..05.md`.

**Modified** — `Core/Profiles/BackupProfile.cs`, `ProfileSerializer.cs`,
`CompressionAlgorithm.cs`; `Core/Scanning/InputScanner.cs` (+ warning kinds);
`Core/Archiving/ArchiveNaming.cs`, `ArchiveFormatDetector.cs`, `ArchiveLayer.cs`;
`Core/Backups/BackupService.cs`; `Core/Restoring/RestoreService.cs`;
`Desktop/ViewModels/ProfileEditorViewModel.cs`, `MainWindowViewModel.cs` (dialog call),
`CompressionOption.cs`; `Desktop/Services/IDialogService.cs`, `DialogService.cs`,
`FilePicker.cs`; `Desktop/Settings/`-adjacent `SettingsDialogViewModel.cs` + `SettingsDialog.axaml`;
`Desktop/Views/MainWindow.axaml`; `Desktop/Hosting/ServiceCollectionExtensions.cs`;
`App.axaml.cs`; `Directory.Build.props` (version, PHASE05); test harness fakes; `README.md`,
`RELEASENOTES.md`, `SECURITY.md`, `docs/QA-CHECKLIST.md`, `docs/roadmap.md`, this file, and
`CLAUDE.md` (the zip-beside-EBK1 and profile-v2 facts) via the per-phase documentation freshness
sweep.

**Expected untouched** — `Core/Encryption/` in its entirety, `tests/Enigma.Backup.Core.Tests/
Fixtures/` (never regenerated), `publish.sh`, `publish.ps1`, `global.json`, `.editorconfig`,
`Directory.Packages.props` (no package is added or moved).

## Risks

- **The profile version rule is the compatibility claim.** If a feature-free profile is ever
  written as version 2, the 1.1.0 release-notes promise ("one written here is still readable by
  1.0.x") silently breaks. Pinned by a dedicated serializer test, not by convention.
- **Silent under-matching would be worse than failing.** The folder-subtree rule and the
  one-level-`*` default are both places where a user's mental model can differ from the code. The
  pattern dialog's inline help and the `PatternMatchedNothing` warning are the mitigations; both
  are acceptance criteria.
- **A zip restore is a fresh walk over hostile input.** The traversal-rejection tests (crafted
  entry names) are written against the zip branch specifically — the tar checks do not cover it.
- **Central-directory memory.** Streamed zip keeps ~100 bytes per entry until dispose — linear in
  entry count, not archive size. Accepted and recorded here; a million-file backup costs tens of
  MB, which matches the spirit but not the letter of the constant-memory invariant.
- **DOS timestamps (2-second resolution, hard 1980–2107 range) and absent Unix modes** in zip are
  fidelity losses against tar. Documented in the release notes and the format's own UI description
  rather than compensated; out-of-range mtimes clamp instead of aborting the run.
- **The top-bar row gains ~280 px.** At the 960 px minimum width the muted status text may
  truncate; the PHASE03 screenshot check is the gate, and the fallback (accepted in the interview)
  is that truncation of the lowest-value text in the row is tolerable.
- **The Settings dialog approaches the shared host cap** (680×560, not per-dialog). The About
  section must stay compact; the PHASE04 screenshot check is the gate.
- **`AVLN*` warnings hide behind a green build** (`TreatWarningsAsErrors` does not promote them) —
  every phase that touches `.axaml` reads the count explicitly, as in FEATURE-17FE.

## Overall acceptance criteria

1. A profile can mix file, folder and pattern inputs; patterns re-evaluate per run with the agreed
   semantics (one-level `*`, `**`, folder-name subtree inclusion, excludes on top, empty match
   warns, missing base asks).
2. Zip is a first-class format: written single-pass with a sidecar, verified, restored in-app, and
   openable by stock tools; it can never be combined with encryption, and the UI says why.
3. Restore… and Verify… sit in the top bar beside the gear; the bottom bar is the accent Back up
   alone; no command behaviour changed.
4. Settings offers Open folder (working on both platforms' file managers) and an About section
   with icon, name, version 1.2.0, copyright and a working project link.
5. Profile compatibility: feature-free profiles round-trip as `formatVersion` 1 both ways;
   new-feature profiles are version 2 and refused loudly by older builds; the `EBK1` byte, archive
   naming, sidecar format and golden fixture are untouched.
6. No ViewModel references an Avalonia type; every new UI capability enters through a
   `Desktop/Services/` interface with a fake in the headless suite.
7. Debug and Release builds clean — zero warnings, zero `AVLN*` — full suite green every phase,
   with the app verified running on Linux (screenshots, both variants) for every UI-touching
   phase.
8. The repository is release-ready at 1.2.0 per `docs/RELEASE.md` §1, artifacts left to the user.
