# FEATURE-3E11 — Enigma.Backup v1 — cross-platform backup app

**Status:** IN PROGRESS
**Type:** multi-phase feature (6 phases)
**Branches (created by `/build`, one per phase):** `feature/feature-3e11-phaseNN-<slug>`

## Objective

A cross-platform desktop application (Windows + Linux) that backs up multiple user-selected
input paths (files and directories) into a single compressed — optionally password-encrypted —
archive file in an output directory, with reusable profiles, in-app restore, integrity
verification, and a streaming pipeline that handles backups of any size in constant memory.

## Validated decisions (interview 2026-07-30)

- **Scope:** backup **and** restore in v1; the plan covers A to Z — repo init through release.
  No CLI app is planned, but the core must stay CLI-ready (zero UI dependencies; interactive
  decisions enter through interfaces).
- **Backup type:** full backup each run; no incremental/differential, no retention management
  (timestamped outputs accumulate; deletion is manual — possible later work item).
- **Archive format:** streaming pipeline `scan → tar → compress → [encrypt] → output file`,
  single pass, no temp files. Unencrypted output is a standard `.tar.zst` / `.tar.gz`.
- **Compression:** Zstandard (default; ZstdSharp.Port 0.8.8) and Gzip (built-in `GZipStream`),
  selectable per profile. No Brotli.
- **Encryption:** AES-256-GCM via Enigma.Core 1.0.0, chunked segments (container spec below);
  **Argon2id only** as KDF, parameters + salt recorded in the file header.
- **Password:** never stored — prompted at backup time (with confirmation field on entry) and
  at restore/verify time. Not remembered across runs.
- **Output naming:** `<SafeProfileName>_yyyy-MM-dd_HHmmss.tar.zst|.tar.gz` (+ `.enc` when
  encrypted) + a `.sha256` sidecar per run (sha256sum-compatible format).
- **Profiles:** managed JSON files in the per-user config folder
  (`%APPDATA%/Enigma.Backup/profiles` · `~/.config/Enigma.Backup/profiles`), auto-listed in the
  UI, with Save / Save As / Rename / Delete **and Import/Export** to arbitrary locations.
  Profile JSON carries a `formatVersion` field.
- **Excludes:** optional glob exclusion patterns per profile (`**/bin/**`, `*.log`, …) applied
  during the scan (Microsoft.Extensions.FileSystemGlobbing).
- **Failure policy:** per-error dialog **Skip / Retry / Abort + "apply to all further errors"**;
  Abort (and Cancel) deletes the partial output file; all skipped entries appear in the
  end-of-run summary. Missing input paths at run time go through the same policy.
- **Verification:** SHA-256 sidecar written while streaming each backup + a manual **Verify**
  action (re-read vs sidecar; for encrypted files also validates every GCM tag).
- **Reporting:** live progress (current file, files/bytes done, overall %) with Cancel; final
  summary (files/bytes, duration, output path, prominent skipped/failed list);
  Microsoft.Extensions.Logging with console/debug providers only.
- **UI:** plain Avalonia 12.1.1 Fluent theme, stock controls only — **no Carbon.Avalonia.Desktop,
  no PhosphorIconsAvalonia**. **Hardcoded English** strings (explicit user decision — deviates
  from the HardCopy `.resx` precedent).
- **Packaging:** self-contained single-file publish for `win-x64` and `linux-x64`, zipped;
  no installer, no CI.
- **License:** MIT.
- **Tests:** xUnit v3 — `Enigma.Backup.Core.Tests` + `Enigma.Backup.Desktop.Tests`
  (headless ViewModel tests, no UI automation).
- **Repo:** git init + house `.gitignore` / `.gitattributes` / `.editorconfig` done at planning
  time (2026-07-30), before this plan was written.

### Assumptions accepted at validation

- Project naming: `src/Enigma.Backup.Core`, `src/Enigma.Backup.Desktop`,
  `tests/Enigma.Backup.Core.Tests`, `tests/Enigma.Backup.Desktop.Tests`.
- Core library targets `net10.0` (not `netstandard2.0`) because it uses `System.Formats.Tar`;
  reason documented in the csproj.
- Archive layout: each input root becomes a top-level entry under its own name; duplicate names
  disambiguated with a numeric suffix (`Photos`, `Photos_2`); paths stored relative with POSIX
  separators.
- Scan safety: inputs canonicalized and deduplicated (nested inputs merged); the output
  directory is always excluded from the scan; directory symlinks are not followed (recorded as
  warnings); hidden files are included.
- Restore conflicts: same dialog pattern — Skip / Overwrite / Abort + apply-to-all.
- Argon2id defaults: 64 MiB memory, 3 iterations, parallelism 4, 16-byte salt.
- UI shape: single main window — profile bar (dropdown + manage buttons), inputs list (add
  file / add folder / remove, drag-and-drop), output folder picker, compression combo,
  encryption toggle, Backup / Restore / Verify actions, progress area with Cancel, summary
  panel. Backup action disabled while the profile has no inputs or no output directory.
- Placeholder app icon wired per the avalonia skill (`<ApplicationIcon>` + window `Icon`);
  final `.ico` art supplied by the user later.
- No scheduling/automation in v1.
- House conventions apply throughout: `.slnx`, CPM (`Directory.Packages.props`),
  `Directory.Build.props` (LangVersion 14, nullable, TreatWarningsAsErrors, no implicit
  usings), full house `.editorconfig`, `global.json` (SDK 10 pin + MTP test runner), IHost
  bootstrap, explicit CommunityToolkit.Mvvm 8.4.2 style (no source generators), xUnit v3 3.2.2.

## Architecture

```
Enigma.Backup.Core          net10.0 library — all functionality, zero UI dependencies
Enigma.Backup.Desktop       net10.0 Avalonia 12.1.1 app — thin MVVM layer over Core
Enigma.Backup.Core.Tests    net10.0 xunit.v3
Enigma.Backup.Desktop.Tests net10.0 xunit.v3 (ViewModel tests)
```

Pipeline (single pass, streaming, constant memory):

```
scan(profile) → entries
entries → TarWriter → CompressionStream (Zstd|Gzip) → [Ebk1EncryptStream (AES-256-GCM)]
        → FileStream(output) ; SHA-256 computed on the final output bytes while writing
        → <output>.sha256 sidecar
```

Core public surface (sketch — the build contract, names refinable):

- `BackupProfile` — `FormatVersion`, `Name`, `Inputs[]`, `OutputDirectory`,
  `Compression` (enum `Zstd`/`Gzip`), `Encrypt` (bool), `ExcludeGlobs[]`.
- `IProfileStore` — managed store: `ListAsync` / `LoadAsync` / `SaveAsync` / `RenameAsync` /
  `DeleteAsync` / `ImportAsync(path)` / `ExportAsync(name, path)`.
- `IBackupService.RunAsync(profile, password?, errorHandler, progress, ct)` → `BackupResult`
  (files/bytes processed, duration, output path, skipped entries with reasons).
- `IRestoreService.RestoreAsync(archivePath, destination, password?, conflictHandler, progress, ct)`.
- `IVerificationService.VerifyAsync(archivePath, password?, progress, ct)` → sidecar match +
  (encrypted) GCM tag validation result.
- `IBackupErrorHandler.HandleAsync(BackupError)` → `Skip | Retry | Abort` (+ apply-to-all flag);
  the Desktop implements it with the dialog, tests implement it programmatically — this is the
  CLI-readiness seam.
- Password crosses the API as a parameter (`ReadOnlyMemory<char>`/string), never persisted.
- DI: `services.AddEnigmaBackupCore()` extension registers all core services;
  Desktop resolves ViewModels and services from the IHost container.

### Encrypted container format — `EBK1` (v1 spec)

Written after compression (the encrypted payload is the compressed tar stream):

```
Header:
  magic        "EBK1" (4 ASCII bytes)
  version      u8 = 1
  kdfId        u8 = 1 (Argon2id)
  memoryKiB    u32 LE (default 65536)
  iterations   u32 LE (default 3)
  parallelism  u32 LE (default 4)
  saltLen      u8 (default 16) + salt bytes
  chunkSize    u32 LE (default 64 MiB)
  noncePrefix  8 random bytes
Per chunk:
  plainLen     u32 LE (≤ chunkSize; last chunk may be smaller)
  ciphertext   plainLen bytes + 16-byte GCM tag
  nonce        = noncePrefix ‖ u32 BE chunk counter (not stored per chunk)
  AAD          = full header bytes ‖ u32 BE chunk counter
```

Key = Argon2id(password, salt, params) → 32 bytes. Wrong password or tampering fails the first
affected chunk's GCM tag → a specific, user-friendly error ("wrong password or corrupted file");
truncation → incomplete final chunk → error. Encrypted output file extension: `.enc` appended to
the compressed name. The build may refine field details **within** version 1 before first
release; any post-release change bumps the version byte.

### Profile JSON (v1)

```json
{
  "formatVersion": 1,
  "name": "Documents",
  "inputs": ["C:/Users/jo/Documents", "C:/Users/jo/keepass.kdbx"],
  "outputDirectory": "D:/Backups",
  "compression": "Zstd",
  "encrypt": true,
  "excludeGlobs": ["**/bin/**", "**/obj/**", "*.tmp"]
}
```

Profiles referencing paths from the other OS simply surface missing paths at run time through
the failure policy.

## Central package versions (CPM, bumped as coupled sets)

Avalonia + Avalonia.Desktop + Avalonia.Themes.Fluent + Avalonia.Fonts.Inter 12.1.1 ·
AvaloniaUI.DiagnosticsSupport 2.2.3 (Debug only) · CommunityToolkit.Mvvm 8.4.2 ·
Enigma.Core 1.0.0 · ZstdSharp.Port 0.8.8 · Microsoft.Extensions.Hosting ·
Microsoft.Extensions.FileSystemGlobbing · xunit.v3 3.2.2.

## Phases

### PHASE01 — Repo & solution scaffolding (DONE)

Branch: `feature/feature-3e11-phase01-scaffolding`

1. `Enigma.Backup.slnx`; `src/`, `tests/` layout (docs/ exists).
2. Solution root config: `Directory.Build.props` (from dotnet-solution-config template,
   authors/year filled), `Directory.Packages.props` (CPM, versions above),
   `global.json` (SDK 10 pin, `rollForward: latestFeature`, MTP test runner).
3. Projects: Core library (net10.0 + csproj comment justifying the TFM), Desktop (Avalonia
   12.1.1 per the avalonia skill — IHost bootstrap, Fluent theme, Inter font, DiagnosticsSupport,
   placeholder icon wired both ways), both test projects (xunit.v3, one placeholder test each).
4. `LICENSE` (MIT), `README.md` skeleton (name, purpose, build instructions).

**Acceptance:** `dotnet build` zero warnings; `dotnet test` green; Desktop starts and shows an
empty main window; solution opens cleanly.

> **Correction (build 2026-07-31):** the *Validated decisions* claim that `git init` and the house
> `.gitignore` / `.gitattributes` / `.editorconfig` were done at planning time did not hold — the
> repository contained only `docs/`. The repo was initialized and those three files were created as
> part of this phase (agreed with the user before building). See
> `docs/done/FEATURE-3E11-PHASE01.md`.

### PHASE02 — Core: profiles & input scanning (DONE)

Branch: `feature/feature-3e11-phase02-profiles-scanning`

1. `BackupProfile` + JSON serialization (`System.Text.Json`, `formatVersion` guard).
2. `IProfileStore` managed store (per-user config dir) + Import/Export; safe file naming;
   rename/delete semantics.
3. Scanner: enumerate inputs → entry list with archive-relative paths; canonicalization,
   dedup of nested inputs, output-directory exclusion, glob excludes, symlink-dir skip
   (warning), missing-path routing through `IBackupErrorHandler`, root-name disambiguation.
4. Tests: profile round-trip/IO/import-export/version guard; scanner fixtures covering dedup,
   excludes, missing paths, output-inside-input, name collisions.

**Acceptance:** all listed behaviors covered by green tests; zero-warning build.

> **Note (build 2026-07-31):** the plan's public surface named `IBackupErrorHandler` under PHASE03,
> but PHASE02 step 3 routes missing paths through it, so the whole error seam
> (`IBackupErrorHandler`, `BackupError`, `BackupErrorDecision`, `ErrorDecisionPolicy` with the
> apply-to-all semantics, `BackupAbortedException`, `SkippedEntry`) landed here and PHASE03 consumes
> it as-is. See `docs/done/FEATURE-3E11-PHASE02.md`.

### PHASE03 — Core: tar + compression pipeline & verify (TODO)

Branch: `feature/feature-3e11-phase03-archive-pipeline`

1. Tar writing/reading via `System.Formats.Tar`; compression stream factory (ZstdSharp / GZipStream).
2. `IBackupService`: full run — timestamped output, streaming SHA-256 → `.sha256` sidecar,
   `IProgress<BackupProgress>`, cooperative cancellation (partial output deleted),
   error-handler loop (Skip/Retry/Abort + apply-to-all semantics owned by the caller state).
3. `IRestoreService`: stream extract with conflict handler (Skip/Overwrite/Abort + apply-to-all).
4. `IVerificationService`: sidecar re-hash compare (unencrypted path).
5. Tests: backup→verify→restore round-trips (both algorithms, content equality), cancellation
   cleanup, skip/retry/abort flows, sidecar tamper detection, empty dirs & odd names.

**Acceptance:** round-trip fidelity proven by tests for Zstd and Gzip; zero-warning build.

### PHASE04 — Core: encryption container (Enigma.Core) (TODO)

Branch: `feature/feature-3e11-phase04-encryption`

1. Argon2id key derivation via Enigma.Core (defaults above), header read/write.
2. `EBK1` chunked AES-256-GCM encrypt/decrypt streams (spec above) integrated into the backup,
   restore, and verify pipelines (verify validates every GCM tag).
3. Specific error types: wrong password/corrupted, truncated, unsupported version/KDF.
4. Tests: encrypted round-trips (both compressions), wrong password, per-chunk tampering,
   truncation, header field validation, multi-chunk files (small chunkSize in tests),
   KDF parameter round-trip from header.

**Acceptance:** encrypted backup→verify→restore proven by tests; tampering/wrong password
produce the specific errors; zero-warning build.

### PHASE05 — Avalonia desktop app (TODO)

Branch: `feature/feature-3e11-phase05-desktop-app`

1. Main window per the validated UI shape (profile bar, inputs list with add file/folder,
   remove, drag-and-drop; output picker; compression combo; encrypt toggle; Backup / Restore /
   Verify; progress + Cancel; summary panel with prominent skipped/failed list).
2. Dialogs: password entry (confirmation field on create), per-error Skip/Retry/Abort +
   apply-to-all, restore conflict Skip/Overwrite/Abort + apply-to-all.
3. Explicit MVVM (no generators), compiled bindings with `x:DataType`, ViewModels + services
   from DI, async per dotnet-async (UI marshaling via `Dispatcher.UIThread`), M.E.Logging
   console/debug.
4. Desktop.Tests: command enablement (no inputs/output → Backup disabled), profile list state,
   error-decision flow, summary population, password dialog validation logic.

**Acceptance:** full backup/restore/verify usable end-to-end from the UI on Windows + Linux;
ViewModel tests green; zero-warning build.

### PHASE06 — Release readiness (TODO)

Branch: `feature/feature-3e11-phase06-release`

1. Publish scripts (`publish.ps1` + `publish.sh`): self-contained single-file `win-x64` +
   `linux-x64`, zipped as `Enigma.Backup-<version>-<rid>.zip`.
2. Version set to 1.0.0; README completed (usage walkthrough, restore-without-the-app note for
   unencrypted archives, build/publish instructions); icon status confirmed (placeholder or
   final art if supplied).
3. Manual QA checklist executed on both platforms (backup, restore, verify, error dialog,
   cancel, profile import/export); release runbook printed per the dotnet-release skill
   (git tag `v1.0.0`; no NuGet push — app zips only).

**Acceptance:** both zips produced from the scripts and smoke-tested; README accurate;
Definition of Done for the item as a whole met.

## Overall acceptance criteria (v1)

1. A profile with mixed file/directory inputs, glob excludes, Zstd or Gzip, with or without
   encryption, produces a single timestamped output file + `.sha256` sidecar; restore
   reproduces the input content byte-for-byte (proven by tests).
2. Encrypted outputs are unreadable without the password; wrong password and tampering are
   detected and reported with specific errors.
3. Unreadable/missing entries raise the Skip/Retry/Abort dialog with apply-to-all; the final
   summary lists everything skipped; Abort/Cancel leaves no partial output file.
4. Profiles persist, reload, import, and export across sessions on both OSes.
5. Verify detects sidecar mismatch (and GCM tag failure for encrypted files).
6. Zero-warning builds; full test suite green; app runs on Windows and Linux from the
   self-contained zips.
