# FEATURE-7F99-PHASE02 — Zip archive format

**Branch:** `feature/feature-7f99-phase02-zip-format`
**Status:** DONE

## Summary

A backup can now be written as a plain `.zip` — the file any file manager opens without this
application — with full parity: back up, verify, and restore in-app, plus the usual `.sha256` sidecar.

Four decisions shape the code:

- **Zip is a sibling of the container, not a change to it.** `EBK1`'s version byte, archive naming,
  the sidecar and the golden fixture are all untouched; `VerificationService` needed no change at all,
  because it hashes bytes against the sidecar and only probes the `EBK1` magic to decide whether
  per-chunk authentication is possible.
- **A zip is an archive, not a codec, so it got a seam rather than a hack.** `CompressionStreams`
  models stream transforms and a zip is not one. `BackupService` now writes through an internal
  `IArchiveEntryWriter` — `TarEntryWriter` (today's compressor + `TarWriter` path, moved over
  unchanged) or `ZipEntryWriter` — while the scan, the Skip/Retry/Abort handler, progress and the
  shrank/grew warnings stay one piece of code. The pipeline shape survives:
  `FileStream → HashingWriteStream → ZipArchive`, single pass, no temp files. The hashing stream's
  `CanSeek` being `false` is load-bearing: it makes `ZipArchive` write trailing data descriptors
  instead of seeking back to patch local headers, so no byte the digest already covered is rewritten.
- **A zip can never be encrypted, and everything says so.** Selecting Zip clears the Encrypt toggle
  and disables it, with the card's description explaining why; `BackupService` refuses the combination
  with a `ProfileFormatException` before the output file is even named, which is what catches a
  hand-edited profile.
- **A zip restore is the same restore.** The zip branch feeds `ArchivedEntry` values into the *same*
  path-traversal check, the same conflict question with apply-to-all, the same progress and the same
  deepest-first directory timestamps that the tar path uses. What zip does not carry it does not get:
  no Unix modes, and never a password.

## Files/modules touched

### Created — production

- `src/Enigma.Backup.Core/Archiving/IArchiveEntryWriter.cs` — the seam: `WriteDirectoryAsync` /
  `WriteFileAsync` over a stream it does not own, closing its own layers inside-out on disposal.
- `src/Enigma.Backup.Core/Archiving/TarEntryWriter.cs` — the compressor + PAX `TarWriter` path,
  lifted out of `BackupService` verbatim. Disposes tar then compressor, so tar's end-of-archive marker
  reaches the compressor before the compressor flushes its epilogue.
- `src/Enigma.Backup.Core/Archiving/ZipEntryWriter.cs` — `ZipArchive(Create, leaveOpen: true)` over
  the hashing stream. Deflate `Optimal` for files, `NoCompression` + trailing `/` for directories, and
  each directory entry is opened and closed at once — an entry that is never opened is only written at
  archive disposal, which would move every directory's header past the data of the files after it.
  Its `<remarks>` records the two accepted costs (central-directory memory, DOS timestamp fidelity).

### Created — tests

- `tests/Enigma.Backup.Core.Tests/Archiving/ZipFormatTests.cs` — 16 cases: the produced file's
  extension, `PK\x03\x04` signature and full entry list read back through the *stock*
  `ZipFile.OpenRead`; the sidecar over the zip's own bytes plus a passing verify; timestamps kept to
  the two seconds a DOS date allows; out-of-range mtimes (1970, 2200) clamped to 1980/2107 instead of
  failing the run; an entry-less run writing a zip that opens with `PK\x05\x06` and restores to
  nothing; conflict Skip / Overwrite / Abort and apply-to-all on a zip restore; three crafted
  traversal entry names refused and a rooted one landing inside the destination; a truncated zip
  reported as damaged; a zip+encrypt profile refused before the output directory is created; and a zip
  smuggled inside an `EBK1` container refused rather than guessed at.

### Modified

- `src/Enigma.Backup.Core/Profiles/CompressionAlgorithm.cs` — `Zip`, with the documented impurity
  written down: an archive container living in a codec enum, because what the user picks is "what kind
  of file do I get", which is one control.
- `src/Enigma.Backup.Core/Profiles/ProfileSerializer.cs` — `RequiredFormatVersion` now also returns 2
  for `Compression == Zip`. Zip is a version-2 *value* of a version-1 field, and saying 2 makes an
  older build refuse the profile with its format message rather than an unknown-enum-name JSON error.
- `src/Enigma.Backup.Core/Archiving/ArchiveNaming.cs` — `BuildExtension` returns `.zip` for Zip before
  consulting `CompressionStreams`, and has no `.enc` arm for it: `.zip.enc` is unreachable, not merely
  unwanted.
- `src/Enigma.Backup.Core/Archiving/ArchiveLayer.cs` + `ArchiveFormatDetector.cs` — `ArchiveLayer.Zip`
  on **both** zip signatures, `PK\x03\x04` and the end-of-central-directory `PK\x05\x06`, because a
  zero-entry zip has no local file header at all and an entry-less run is reachable. The
  not-an-archive message names zip too.
- `src/Enigma.Backup.Core/Backups/BackupService.cs` — the zip+encrypt guard (before the password check,
  so a hand-edited profile is told what is actually wrong with it); `CreateEntryWriter` chooses the
  writer; `WriteFileEntryAsync` takes the writer instead of a `TarWriter`; the tar-specific entry code
  moved to `TarEntryWriter`, so `System.Formats.Tar` is gone from this file.
- `src/Enigma.Backup.Core/Restoring/RestoreService.cs` — split into `RestoreFromTarAsync` (the previous
  body, unchanged in behaviour) and `RestoreFromZipAsync`, both feeding a new private `ArchivedEntry`
  into the shared `RestoreDirectory` / `RestoreFileAsync` / `ResolveConflictAsync`. `RestoreState.Counting`
  became `BytesConsumed`, a `Func<long>`: a zip is opened by seeking its central directory at the very
  end, so the file position is already at 100% before anything is restored — zip progress counts the
  compressed size of the entries taken instead. A zip inside `EBK1` now joins nested `EBK1` in being
  refused.
- `src/Enigma.Backup.Desktop/ViewModels/CompressionOption.cs` — the third row, whose description
  carries the fidelity limits ("Cannot be encrypted, and keeps no Unix modes").
- `src/Enigma.Backup.Desktop/ViewModels/ProfileEditorViewModel.cs` — `CanEncrypt` and
  `EncryptDescription`; selecting a format that cannot be encrypted turns `Encrypt` off, and switching
  away re-enables the toggle without turning it back on.
- `src/Enigma.Backup.Desktop/Views/MainWindow.axaml` — the encrypt card's description binds
  `EncryptDescription` and the **toggle** (not the card) binds `IsEnabled` to `CanEncrypt`, so the
  reason stays readable while the control is dead.
- `src/Enigma.Backup.Desktop/Services/FilePicker.cs` — `*.zip` in the archive filter for
  Restore…/Verify….
- Tests: `RoundTripTests.BackupVerifyRestore_ReproducesTheInputTree` gained a `Zip` theory case (the
  same odd-name / unicode / long-path / empty-dir / binary tree); `ProfileSerializationTests` +1 (Zip
  forces version 2); `ProfileEditorViewModelTests` +4 (Zip clears and disables encrypt, switching back
  re-enables without re-arming it, a Zip profile round-trips clean, and a hand-edited zip+encrypt
  profile is shown as it is).
- `docs/roadmap.md`, `docs/plan/FEATURE-7F99.md` — PHASE02 status.

**Untouched:** `src/Enigma.Backup.Core/Encryption/` in its entirety,
`src/Enigma.Backup.Core/Verification/VerificationService.cs`,
`tests/Enigma.Backup.Core.Tests/Fixtures/` (verified by `git status`),
`Archiving/CompressionStreams.cs`, `Directory.Packages.props` (no package added — zip is BCL),
`Directory.Build.props`, `global.json`, `.editorconfig`, `publish.sh`, `publish.ps1`.

## Deviations & follow-ups

- **A zip's timestamps are written in *local* time, which the plan did not spell out.** A DOS date
  carries no time zone, and every tool — Explorer, `unzip`, and `ZipArchiveEntry.LastWriteTime` on the
  way back in — reads one as local. Writing the scan's UTC value verbatim would have made every zip
  show two hours out here and shifted the timestamp on every in-app restore. `ZipEntryWriter.ToZipTime`
  converts to local, and clamps on *that* wall clock: west of UTC, 1980-01-01Z is still 1979, which
  `LastWriteTime` refuses. Confirmed both ways — `unzip -l` and the restored file agree with the source
  to the second.
- **The clamp bounds are 1980-01-01 00:00:00 … 2107-12-31 23:59:58.** `LastWriteTime` validates by
  year only, but the odd upper second is what a DOS date can actually hold (its seconds field is
  halved), so the range is stated honestly rather than as `23:59:59`.
- **`ArchiveNaming` owns `.zip`, not `CompressionStreams.GetExtension`.** The plan said "`ArchiveNaming`
  maps Zip → `.zip`", and keeping it there leaves `CompressionStreams` a table of stream codecs only —
  the same reasoning that produced the writer seam.
- **The nested-container refusal message changed** from "holds another encrypted container instead of a
  compressed archive" to "holds another archive instead of a compressed tar stream", since it now covers
  a nested zip as well as a nested `EBK1`. No test asserted the old wording.
- **A hand-edited zip+encrypt profile is shown as it is, not repaired.** The editor loads
  `Encrypt = true` faithfully (toggle On but disabled, with the description saying why) and the run
  refuses it with a message that names the problem. Silently clearing "encrypt this" is not a decision
  a backup tool gets to make on its own, and the plan asks for exactly this loud failure.
- **`ZipEntryWriter.DisposeAsync` disposes synchronously.** `ZipArchive` has no `IAsyncDisposable`, and
  its `Dispose` is what writes the central directory. The stream below is a hashing stream over a
  buffered file, so this is a memcpy and a file write.
- **The zip round trip does not preserve Unix modes**, by construction — `RoundTripTests.Restore_KeepsTheExecutableBit`
  stays a Zstd-only test. `FileTree.Snapshot` compares paths and content digests, so the shared
  round-trip theory is honest for all three formats.
- **`IArchiveEntryWriter` and the two writers are covered through the services**, not directly: they are
  `internal` and this solution has no `InternalsVisibleTo`, the same position `GlobExcluder` and
  `PatternMatcher` are in.
- **Central-directory memory is accepted, not solved** — roughly 100 bytes per entry held until
  disposal, linear in entry count and independent of archive size. Recorded in the plan's *Risks* and in
  `ZipEntryWriter`'s own `<remarks>`.
- **The UI check used the real config folder**, unlike PHASE01's throwaway `XDG_CONFIG_HOME`: a
  temporary `Shared archive` profile (`formatVersion` 2, `"compression": "Zip"`) was written to
  `~/.config/Enigma.Backup/profiles/` so the app would load it at startup, since there is no
  `xdotool`/`wmctrl` on this machine to synthesise a click on the combo. `settings.json` was copied
  first and restored afterwards, the profile and the `profiles/` folder were removed, and the folder now
  holds exactly what it did before — `logs/` and `settings.json` with `"theme": "System"`. No source
  file was touched for the screenshots.
- **Line endings:** nothing in this phase's diff is CRLF↔LF churn. No action taken — per the workflow
  this is recommendation-only.
- **For PHASE05:** the release notes need zip's fidelity limits stated (DOS timestamps at 2-second
  resolution, no Unix modes, never encrypted), the "profiles asking for Zip are `formatVersion` 2"
  rule, and that no package was added; `README.md` needs zip under Capabilities, in *What a backup run
  writes* and in *Restoring without Enigma.Backup*; `SECURITY.md` needs the sentence that zip archives
  are never encrypted and the app refuses to pretend otherwise; `docs/QA-CHECKLIST.md` needs the zip
  section. All already in the plan's PHASE05 steps.

## Build/test evidence

- **Build (Debug):** `dotnet build Enigma.Backup.slnx` → **0 warnings, 0 errors**.
- **Build (Release):** `dotnet build Enigma.Backup.slnx -c Release --no-incremental` → **0 warnings,
  0 errors**, and `grep -c AVLN` over the full log → **0**. (`TreatWarningsAsErrors` does not promote
  `AVLN*`, so every `.axaml`-touching phase reads the count explicitly.)
- **Tests:** `dotnet test --solution Enigma.Backup.slnx` → **472 passed, 0 failed, 0 skipped** (450
  before; +22). `GoldenArchiveCompatibilityTests` run explicitly → **2 passed**, with
  `git status --porcelain tests/Enigma.Backup.Core.Tests/Fixtures/` empty.
- **Interop, on a real archive from the shipped `BackupService`** (nested tree: unicode and odd names,
  a 40 KB binary, an empty folder, a file stamped `2019-04-03 11:22:34`):
  - `unzip -t` → *"No errors detected in compressed data"*, all 8 entries `OK`.
  - `unzip -l` lists the folders with trailing `/` and shows `a.txt` at **2019-04-03 11:22** — the
    local wall clock, which is what proves the time-zone handling.
  - The sidecar's digest equals `sha256sum` of the file, and `VerificationService` reports
    `Succeeded`.
  - `diff -r` against the source: **identical** for the in-app restore **and** for `unzip -d`.
  - Windows Explorer was not available on this machine; `unzip` (Info-ZIP) is the stock tool used, and
    the archive uses no Zip64 and no encryption, only Deflate with data descriptors.
- **Verified running** on Linux (Wayland + XWayland, `DISPLAY=:1`), Debug build; screenshots with
  `spectacle`, in **Dark** and in **Light**: the Compression card reads *"Zip (.zip)"* with
  *"Opens anywhere without this app. Cannot be encrypted, and keeps no Unix modes."*, and the Encrypt
  card shows *"A zip archive cannot be encrypted. Choose Zstandard or Gzip to protect one with a
  password."* with the toggle visibly greyed out at **Off**. The card's height matches the other two
  options (the description was shortened after a first pass wrapped to three lines and made the row
  taller).

## Acceptance criteria

| Criterion | Met |
|---|---|
| `CompressionAlgorithm.Zip`; the internal entry-writer seam with the tar path moved over verbatim and the zip writer added (Deflate Optimal, dir entries, DOS timestamps); the zip+encrypt guard; `ArchiveNaming` → `.zip`; Zip forces `formatVersion` 2 | ✅ |
| `ArchiveLayer.Zip` on both signatures; `RestoreService` zip branch on the same traversal/conflict/progress path; `VerificationService` unchanged | ✅ |
| Desktop: `CompressionOption` row; `CanEncrypt` off+disabled under Zip with the explanatory description; `FilePicker` filters `*.zip` | ✅ |
| Tests: zip round trip, sidecar, empty-archive run, conflict decisions, traversal refusal, zip+encrypt refusal, both detector signatures; golden fixture untouched and green; Desktop encrypt-toggle and Zip round trip | ✅ |
| Interop check recorded: the `.zip` opens in a stock tool on Linux (`unzip -t`) | ✅ (Windows Explorer unavailable here) |
| Debug **and** Release build clean, zero warnings, zero `AVLN*`; full suite green | ✅ |
| Manually: an unencrypted zip backup of a nested tree restores byte-identically in-app *and* extracts with a stock unzip tool; verify reports the checksum result; the Encrypt toggle is visibly disabled with its explanation under Zip (screenshots both variants) | ✅ |
