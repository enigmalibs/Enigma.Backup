# FEATURE-3E11-PHASE02 — Core: profiles & input scanning

**Completed:** 2026-07-31
**Branch:** `feature/feature-3e11-phase02-profiles-scanning`

## Summary

Filled the empty core library with the two things the archive pipeline needs before it can write a
byte: the **profile** (what to back up, persisted as JSON in the per-user config folder) and the
**scan** (turning a profile's inputs into the exact, ordered list of entries to archive).

Everything a run has to *decide* now happens up front, in one place, with no UI anywhere near it:

- `BackupProfile` + `IProfileStore`/`ProfileStore` — list / load / save / rename / delete plus
  import and export to arbitrary paths, atomic writes, and a `formatVersion` guard that rejects a
  file this build does not understand instead of guessing at it.
- `IInputScanner`/`InputScanner` — canonicalizes inputs, merges overlapping ones, excludes the
  output directory subtree, applies glob excludes, refuses to follow directory symlinks, routes
  missing and unreadable paths through the error handler, and gives every input a portable, unique
  top-level name.
- The **error seam** the whole app hangs off: `IBackupErrorHandler` (Skip / Retry / Abort) and
  `ErrorDecisionPolicy`, which owns the "apply to all further errors" behaviour so PHASE03's
  pipeline and PHASE05's dialog do not each reinvent it.

Both new services are registered in `AddEnigmaBackupCore()`, so nothing news up a service.

## Files/modules touched

**Created — `src/Enigma.Backup.Core`**

- `PortableFileName.cs` — the shared "usable as a file name on Windows *and* Linux" rules
  (invalid characters, control characters, reserved device names, trailing dots, length), with a
  sanitizer that always produces a name the validator accepts.
- `Errors/` — `BackupErrorKind`, `BackupErrorAction`, `BackupErrorDecision` (readonly record struct
  with Skip/Retry/Abort factories), `BackupError`, `IBackupErrorHandler`, `ErrorDecisionPolicy`,
  `BackupAbortedException`, `SkippedEntry`.
- `Profiles/` — `BackupProfile` (+ `CurrentFormatVersion`), `CompressionAlgorithm` (JSON-named),
  `ProfileName` (validation / `EnsureValid` / `Sanitize`), `IProfileStore`, `ProfileStore`,
  `ProfileSerializer` (internal), `ProfileFormatException`, `ProfileNotFoundException`,
  `ProfileAlreadyExistsException`.
- `Scanning/` — `IInputScanner`, `InputScanner`, `ScanEntry`, `ScanEntryKind`, `ScanResult`,
  `ScanWarning`, `ScanWarningKind`, `GlobExcluder` (internal), `PathUtilities` (internal).

**Modified**

- `src/Enigma.Backup.Core/Enigma.Backup.Core.csproj` — added the
  `Microsoft.Extensions.FileSystemGlobbing` reference (version already pinned centrally).
- `src/Enigma.Backup.Core/Hosting/ServiceCollectionExtensions.cs` — registers `IProfileStore`
  (singleton, via factory) and `IInputScanner` (singleton) with `TryAdd*`.
- `docs/roadmap.md`, `docs/plan/FEATURE-3E11.md` — PHASE02 `TODO` → `IN PROGRESS` → `DONE`, plus a
  note on the plan recording where the error seam actually landed.

**Created — tests (`tests/Enigma.Backup.Core.Tests`)**

- `TestSupport/TempDirectory.cs`, `TestSupport/ScriptedErrorHandler.cs`.
- `Profiles/ProfileNameTests.cs`, `Profiles/ProfileStoreTests.cs`, `Profiles/ProfileSerializationTests.cs`.
- `Scanning/InputScannerTests.cs`.
- `Hosting/ServiceCollectionExtensionsTests.cs` — extended with resolution, lifetime and
  "does not stomp an existing registration" tests.

## Decisions taken inside the plan's latitude

These were left open by the plan; each is enforced by a test:

- **Profile names are validated, not sanitized.** A name is the file name, so it is checked against
  the strictest platform *before* it is combined into a path — which is what stops `../evil` from
  escaping the store. Sanitization exists (`ProfileName.Sanitize`) but is only used where the user
  did not choose the name: importing a foreign file.
- **The file name is the identity inside the store.** `LoadAsync` returns the profile with `Name`
  set from the file, and `RenameAsync` rewrites rather than moves, so the `name` inside the JSON
  never drifts from the file it lives in.
- **Import never overwrites.** A colliding import becomes `Documents_2`, `Documents_3`, ….
- **`Save` overwrites, `Rename` does not.** Save is what both "Save" and "Save As" call; renaming
  onto an occupied name raises `ProfileAlreadyExistsException`.
- **Exclude globs match the path relative to the input they were found under.** So `*.log` catches
  logs at the top of an input, `**/*.log` catches them everywhere — the standard
  `FileSystemGlobbing` semantics, not a private dialect. A pattern ending in `/**` additionally
  excludes the directory it names, so `**/bin/**` does not leave an empty `bin` in the archive.
- **The output directory's whole subtree is excluded**, not just the directory itself — that is what
  actually prevents a backup from containing yesterday's archives, its own sidecar, or itself.
- **Directory symlinks are never followed, including when one *is* an input** — a single rule, no
  cycles, and each occurrence is a warning in the summary rather than a silent omission.
- **`Retry` is never made sticky by "apply to all"** — a permanent failure answered that way would
  loop forever.
- **Entries are emitted in a deterministic, ordinal-sorted, parent-before-child walk**, so PHASE03
  can stream straight into the tar writer and tests can assert exact archive layouts.
- **Collision suffixes go before the extension** (`config.json` → `config_2.json`).
- **`ScanResult` carries warnings and skips**, not an `ILogger`. The run summary is the user-facing
  channel the plan asked for; core logging can be added in a later phase without changing this.

## Deviations & follow-ups

- **The error seam moved from PHASE03 to PHASE02.** The plan sketches `IBackupErrorHandler` under
  the pipeline phase, but PHASE02's own step 3 requires "missing-path routing through
  `IBackupErrorHandler`" — the interface cannot be later than its first consumer. `ErrorDecisionPolicy`
  (apply-to-all) came with it, so PHASE03 inherits the semantics instead of re-deriving them. Noted
  in the plan file.
- **`IProfileStore` gained a `DirectoryPath` property** beyond the plan's sketch, so the UI can show
  where profiles live and tests can assert the store's location.
- **A malformed input path is not rejected up front.** It is kept verbatim, fails the existence
  check, and reaches the user through the ordinary missing-input dialog — one route for "this path
  is not usable" instead of two.
- **The `Enigma.Core` and `ZstdSharp.Port` package versions stay unused** until PHASE04/PHASE03; only
  `Microsoft.Extensions.FileSystemGlobbing` was consumed here.
- **Follow-up for PHASE03:** `BackupErrorKind` has the three kinds the scan can raise
  (`MissingInput`, `AccessDenied`, `ReadFailed`); the write side will want at least a write/output
  kind — adding an enum member is additive.
- **Follow-up for PHASE05:** the desktop app should pre-validate profile names with
  `ProfileName.IsValid` so the user sees a field-level message rather than an exception.
- **Line endings:** the tree remains 100% LF and `.gitattributes` normalizes to LF — nothing to
  recommend, no action taken.

## Build/test evidence

- `dotnet build Enigma.Backup.slnx` — **Build succeeded, 0 Warning(s), 0 Error(s)**.
- `dotnet build Enigma.Backup.slnx -c Release` — **0 Warning(s), 0 Error(s)**.
- `dotnet test --solution Enigma.Backup.slnx` — **total: 114, failed: 0, succeeded: 114, skipped: 0**
  (`Enigma.Backup.Core.Tests` 111, `Enigma.Backup.Desktop.Tests` 3), up from 5 at PHASE01.
- Scanner coverage runs against real files in a temp tree: walk order and archive paths, empty
  directories, hidden files, duplicate and nested inputs (in both listing orders), sibling paths with
  a shared prefix, top-level name collisions (2 and 3 deep, directories and files), non-portable root
  names, `**/bin/**` pruning, root-relative vs recursive globs, blank globs, output-dir-inside-input
  and input-inside-output-dir, missing inputs answered Skip / Retry / Abort, apply-to-all asking
  exactly once, "retry + apply to all" deliberately not sticking, real directory symlinks (as a child
  and as an input), real permission-denied directories answered Skip and Abort, cancellation, and
  argument validation.
- The symlink and permission tests degrade to `Assert.Skip` where the platform or the user cannot
  create the condition; on this machine all 114 tests ran (`skipped: 0`), so both paths were
  genuinely exercised.
