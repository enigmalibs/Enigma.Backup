# FEATURE-1AA8-PHASE01 — Core error messages

**Branch:** `feature/feature-1aa8-phase01-core-messages`
**Run:** feature/2026-11-16-display-name-everywhere

## Summary

The three Core error messages that reach the run dialog now name the app **Enigma Backup**:

- `ArchiveFormatDetector`: "The file is not an Enigma Backup archive: …", when Verify or Restore is
  given a file that is no archive;
- `Ebk1Header`: "… this version of Enigma Backup only reads version …" and "… which this version of
  Enigma Backup does not know …", for an encrypted archive from a newer format.

The tests that pin the first message (`Ebk1ContainerTests`, `RestoreServiceTests`) now expect the new
spelling. The unsupported-version and unsupported-key-derivation tests now assert their message's
wording as well; the key-derivation test asserted no message before.

`RELEASENOTES.md`: the 1.6.0 bullet "Enigma Backup everywhere the app names itself" now covers these
errors.

## Files/modules touched

- Modified:
  - `src/Enigma.Backup.Core/Archiving/ArchiveFormatDetector.cs`
  - `src/Enigma.Backup.Core/Encryption/Ebk1Header.cs`
  - `tests/Enigma.Backup.Core.Tests/Encryption/Ebk1ContainerTests.cs`
  - `tests/Enigma.Backup.Core.Tests/Restoring/RestoreServiceTests.cs`
  - `RELEASENOTES.md`, `docs/roadmap.md`, `docs/plan/FEATURE-1AA8.md`
- Created: `docs/done/FEATURE-1AA8-PHASE01.md`

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| `ArchiveFormatException`'s doc comment ("an Enigma.Backup archive") | Left as it is | A code comment, which no user sees. There the dotted name names the project, as the namespaces do |
| What the new assertions match | The sentence fragment around the name ("this version of Enigma Backup only reads"), beside the existing "newer version of the application" | Pins the name in context without freezing the whole message |

## Deviations & follow-ups

- None against the plan.

## Build/test evidence

- **Run baseline** (run branch `feature/2026-11-16-display-name-everywhere` @ `a601225`, before this dev):
  - `dotnet build Enigma.Backup.slnx` and `-c Release`: 0 warnings, 0 errors;
  - `dotnet test --solution Enigma.Backup.slnx`: 590 total, 578 passed, 12 skipped, 0 failed;
  - no pre-existing warnings or failures.
- After the change:
  - Debug and Release builds: **0 warnings, 0 errors**;
  - `dotnet test --solution Enigma.Backup.slnx`: **590 total, 578 passed, 12 skipped, 0 failed**.
- `grep` of the string literals in `src/Enigma.Backup.Core`: the only one that names `Enigma.Backup` is
  the config folder's path segment in `ProfileStore`, which is on purpose.
- Fix budget: 0 cycles used.

## Documentation sweep

- `RELEASENOTES.md` 1.6.0 updated (above).
- README, `CLAUDE.md` and the QA checklist quote none of these messages, so none of them changes.
