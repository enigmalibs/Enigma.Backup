# FEATURE-3E11-PHASE03 — Core: tar + compression pipeline & verify

**Completed:** 2026-07-31
**Branch:** `feature/feature-3e11-phase03-archive-pipeline`

## Summary

The core can now actually make a backup, read it back, and prove it is intact. PHASE02 decided *what*
to archive; this phase writes it, in one streaming pass with no temp files and no buffering of the
whole archive:

```
scan → tar (Pax) → compress (zstd | gzip) → SHA-256 → file  +  <archive>.sha256
```

- **`IBackupService`** — timestamped output (`Documents_2026-07-31_234506.tar.zst`), the digest taken
  from the bytes as they are written so the sidecar costs nothing extra, `IProgress<BackupProgress>`
  with real byte-level movement inside large files, cooperative cancellation, and the PHASE02 error
  seam extended to the write side. Anything other than success — a failure, an Abort, a Cancel —
  deletes the partial archive and its sidecar.
- **`IRestoreService`** — streams the archive back out, one entry at a time, asking a
  Skip / Overwrite / Abort conflict handler about every file that is already there, and restoring
  timestamps and Unix modes as well as content.
- **`IVerificationService`** — re-reads an archive and compares its digest with the sidecar, reporting
  a missing or unreadable sidecar as an outcome rather than an exception.

The three services and a `TimeProvider` are registered in `AddEnigmaBackupCore()`.

The archives are plain, standard files: `sha256sum -c` validates the sidecar and GNU `tar` lists and
extracts both formats (verified by hand, see *Build/test evidence*) — which is the "you do not need
this application to get your data back" promise the plan makes for unencrypted output.

## Files/modules touched

**Created — `src/Enigma.Backup.Core/Archiving/`** (the plumbing all three services share)

- `ArchiveFormatException` (public), `ArchiveLayer`, `ArchiveFormatDetector` — what a file *is* comes
  from its leading bytes (`EBK1` / zstd / gzip magic), never from its name.
- `CompressionStreams` — the zstd/gzip compressor and decompressor factory, the `.tar.zst` / `.tar.gz`
  extensions, and `IsCorruptData`, which names the exceptions that mean "these bytes are damaged".
- `ArchiveNaming` — `<SafeProfileName>_yyyy-MM-dd_HHmmss` + extension, with `_2`, `_3`, … when a run
  lands in the same second as a previous one.
- `Sha256Sidecar` — writes and parses the `sha256sum`-compatible sidecar.
- `HashingWriteStream` — digests the output as it is written.
- `FixedLengthReadStream` — delivers exactly the length a tar header promised, and reports bytes read.
- `CountingReadStream` — measures a restore's progress on the archive it consumes.
- `ArchiveEntryPath` — resolves an entry name against the destination, refusing anything that would
  land outside it.
- `UnixPermissions` — reads and applies file modes where the platform has them.

**Created — `src/Enigma.Backup.Core/Backups/`**

`IBackupService`, `BackupService`, `BackupResult`, `BackupProgress`, `BackupPhase`,
`BackupProgressReporter` (internal), `BackupWarning`, `BackupWarningKind`.

**Created — `src/Enigma.Backup.Core/Restoring/`**

`IRestoreService`, `RestoreService`, `RestoreResult`, `RestoreOutcome`, `RestoreProgress`,
`IRestoreConflictHandler`, `RestoreConflict`, `RestoreConflictAction`, `RestoreConflictDecision`,
`RestoreConflictPolicy`.

**Created — `src/Enigma.Backup.Core/Verification/`**

`IVerificationService`, `VerificationService`, `VerificationResult`, `VerificationOutcome`,
`VerificationProgress`.

**Modified**

- `src/Enigma.Backup.Core/Enigma.Backup.Core.csproj` — added the `ZstdSharp.Port` reference (version
  already pinned centrally).
- `src/Enigma.Backup.Core/Hosting/ServiceCollectionExtensions.cs` — registers `TimeProvider.System`
  and the three services with `TryAdd*`.
- `.gitignore` — see *Deviations & follow-ups*.
- `docs/roadmap.md`, `docs/plan/FEATURE-3E11.md` — PHASE03 `TODO` → `IN PROGRESS` → `DONE`, plus a
  note on the plan recording the two deliberate deviations.

**Created — tests (`tests/Enigma.Backup.Core.Tests`)**

- `Archiving/RoundTripTests.cs` — the acceptance criterion, for both algorithms.
- `Backups/BackupServiceTests.cs`, `Restoring/RestoreServiceTests.cs`,
  `Verification/VerificationServiceTests.cs`.
- `TestSupport/` — `Pipeline` (builds the services), `FileTree` (a tree as comparable text),
  `FixedTimeProvider`, `SynchronousProgress<T>` (reports on the reporting thread, which is what lets a
  test cancel a run or change a file mid-entry), `ScriptedConflictHandler`, `AccessDenial`.
- `TestSupport/ScriptedErrorHandler.cs` — gained a `BeforeAnswering` hook, so a scripted Retry can
  repair what failed the way a user would.
- `Hosting/ServiceCollectionExtensionsTests.cs` — extended for the three services and the clock.

## Decisions taken inside the plan's latitude

Each is enforced by a test:

- **A tar entry never lies about its size.** The size goes into the header before the data, so a file
  that is being written to while the backup runs would corrupt the archive. The entry is filled to
  exactly the length that was declared: a source that grew is read only that far, one that shrank is
  padded, and either way the run reports a `BackupWarning` instead of producing a silently bad archive.
  Files are opened `FileShare.ReadWrite | Delete`, so a file another program holds open is still
  backed up rather than skipped.
- **Format detection reads bytes, not names.** A renamed archive still restores, and a file that only
  looks like one by its extension is refused before anything is written.
- **An entry name that cannot stay inside the destination is refused outright**, rather than skipped:
  a leading `/` is dropped (what `tar` does), but `..`, an embedded separator, or a Windows drive ends
  the restore with an `ArchiveFormatException`.
- **Pax format**, so long paths, Unicode names and precise timestamps need no private conventions;
  directory entries get the conventional trailing slash.
- **Timestamps and Unix modes are part of fidelity**, so both are restored. Directory metadata is
  applied last, deepest first — writing into a directory changes its timestamp, and a read-only
  directory cannot be written into at all.
- **Progress is throttled by bytes (1 MiB), not by time**, so a run's reports do not depend on how fast
  the machine is, and the tests are deterministic. A file boundary always reports.
- **A read failure mid-entry ends the run.** Its header is already in the stream, so it cannot be
  skipped; the archive would be inconsistent, and the partial file is deleted. Only failures found
  *before* an entry starts go to the Skip / Retry / Abort handler.
- **One `ErrorDecisionPolicy` spans the scan and the write**, so "apply to all" answered while scanning
  still holds while writing — the user is asked once per run, not once per stage.
- **`Directory.CreateDirectory` for the output folder happens after the scan**, so an aborted scan does
  not leave an empty folder behind.
- **A run whose entries were all skipped still produces a valid (empty) archive** and reports zero
  files, rather than failing.

## Deviations & follow-ups

- **`.gitignore` needed a fix, and it was not cosmetic.** The house VisualStudio `.gitignore` contains
  `Backup*/` (for IDE conversion folders); in a solution named Enigma.Backup that silently excluded the
  new `src/Enigma.Backup.Core/Backups/` and `tests/…/Backups/` folders — the phase's main service would
  simply not have been committed. Two negations (`!src/**/Backup*/`, `!tests/**/Backup*/`) with a
  comment explaining why. Worth knowing for PHASE05, which will want folders with the same name.
- **Encryption is refused, not ignored.** The plan's `password` parameter is on all three services
  already, so PHASE05 can be written against the final signatures, but an encrypting profile and an
  `EBK1` file each raise a clear `NotSupportedException`. PHASE04 replaces exactly those two throw
  sites; the pipeline is built inside-out with the encrypting layer's place marked in
  `BackupService.WriteArchiveAsync`, between the digest and the compressor, which keeps the sidecar
  covering the bytes that are on disk.
- **A restore's Abort is a result, not an exception** (`RestoreResult.Outcome`), unlike a backup's.
  There is no output file to discard, and the user needs to be told what *was* restored before they
  stopped it. Cancellation still throws, per the .NET contract.
- **Restore takes no `IBackupErrorHandler`** — the plan's signature has only the conflict handler, so a
  genuine I/O failure while writing (a locked destination file, a full disk) propagates as an
  exception rather than offering Skip / Retry / Abort. Worth revisiting in PHASE05 if the dialogs make
  it cheap; it would be an additive parameter.
- **`BackupErrorKind` was not extended.** The follow-up noted in PHASE02 suggested a write/output kind;
  in practice every failure the write side reports is still a *read* failure of a source file
  (`MissingInput` / `AccessDenied` / `ReadFailed`), so nothing was added.
- **No `ILogger` in the core yet**, consistent with PHASE02: the results carry warnings and skips, and
  the plan puts `Microsoft.Extensions.Logging` in the Desktop (PHASE05).
- **Verification of an encrypted archive is not blocked** — the sidecar covers the file as it is on
  disk, so the comparison is meaningful without the password. PHASE04 adds the GCM-tag half.
- **Line endings:** the tree remains 100% LF and `.gitattributes` normalizes to LF — nothing to
  recommend, no action taken.

## Build/test evidence

- `dotnet build Enigma.Backup.slnx` — **Build succeeded, 0 Warning(s), 0 Error(s)**.
- `dotnet build Enigma.Backup.slnx -c Release` — **0 Warning(s), 0 Error(s)**.
- `dotnet test --solution Enigma.Backup.slnx` — **total: 173, failed: 0, succeeded: 173, skipped: 0**
  (up from 114 at PHASE02; 59 new tests).
- **Round-trip fidelity** (the phase's acceptance criterion) is proven for **both Zstd and Gzip** on a
  tree containing empty files, empty directories, dotfiles, spaces and quotes in names, Unicode names,
  binary content and a 150-character name that no classic tar header can hold: the restored tree's
  files, sizes and content digests match the source exactly. Also covered: a 5 MiB file (larger than
  every buffer), several inputs each under their own top-level name, timestamps, the executable bit,
  and an archive of nothing but empty directories.
- **Failure paths:** cancellation mid-run and Abort both leave the output directory empty; Skip
  finishes and lists the entry; Retry archives it on the second attempt; "apply to all" is asked
  exactly once across the scan *and* the write. Restore conflicts cover Skip / Overwrite / Abort /
  apply-to-all, a file that would land on a directory, and an entry type that is not restored.
  Verification covers a flipped bit, a tampered sidecar, a missing sidecar, three unreadable sidecars
  and `sha256sum`'s binary-mode format. Damage is covered for both algorithms (truncated archive), as
  are a non-archive file, a file too short to identify, and path traversal.
- The permission-dependent tests degrade to `Assert.Skip` where the platform or the user cannot create
  the condition; on this machine all 173 tests ran (`skipped: 0`), so those paths were genuinely
  exercised.
- **External-tool interop, checked by hand** on archives produced by the real pipeline:
  `sha256sum -c` reports `OK` for both the `.tar.zst` and `.tar.gz` sidecars; `tar -tvf` lists both
  archives (entries, sizes, modes and timestamps as expected); `tar -xf` extracts the zstd archive and
  `diff -r` against the source reports no differences.
