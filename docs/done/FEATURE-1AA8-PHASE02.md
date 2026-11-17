# FEATURE-1AA8-PHASE02 — The Windows exe's metadata

**Branch:** `feature/feature-1aa8-phase02-exe-metadata`
**Run:** feature/2026-11-16-display-name-everywhere

## Summary

On Windows, the running app and its exe are now named **Enigma Backup**. Before, they were named
`Enigma.Backup.Desktop`. This covers Task Manager's *Processes* tab, the exe's *Properties → Details*,
and the other places Windows reads a file description.

- `Enigma.Backup.Desktop.csproj`: `<AssemblyTitle>Enigma Backup</AssemblyTitle>` and
  `<Product>Enigma Backup</Product>`. They become the exe's `FileDescription` and `ProductName`.
  They live in the Desktop project only, so the Core and test assemblies keep their own titles.
- The assembly name, and so the exe's file name (`Enigma.Backup.Desktop.exe`), is unchanged, as are
  the version, the copyright and the MSI.
- New test `AppInfoTests.TheExecutableCarriesTheProductsName`. It pins:
  - the title and the product to `AppInfo.Name`;
  - the assembly name to `Enigma.Backup.Desktop`.
- `docs/QA-CHECKLIST.md`: new row 1.7 (Windows). `RELEASENOTES.md` 1.6.0: the name bullet mentions Task
  Manager and the exe's Properties.

## Files/modules touched

- Modified:
  - `src/Enigma.Backup.Desktop/Enigma.Backup.Desktop.csproj`
  - `tests/Enigma.Backup.Desktop.Tests/AppInfoTests.cs`
  - `RELEASENOTES.md`, `docs/QA-CHECKLIST.md`, `docs/roadmap.md`, `docs/plan/FEATURE-1AA8.md`
- Created: `docs/done/FEATURE-1AA8-PHASE02.md`

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Also pin the assembly name in the test | Yes | The file name is a contract: `buildmsi.ps1`'s shortcut target, the Linux launcher's `Exec`, the zips. The test says the rename stops at the display name |
| How to read the real exe's metadata | `./publish.ps1 -Rid win-x64`, then `FileVersionInfo` and `Get-Process` on the extracted single-file exe | The release build is what users get, and the single-file apphost carries its own copy of the version resource |

## Deviations & follow-ups

- `artifacts/Enigma.Backup-1.6.0-win-x64.zip` was rebuilt by this check, from the run branch at
  `ea75725`, and replaced FEATURE-63B5's copy. It is gitignored and not the release artifact: rebuild it
  from the tagged commit.
- Task Manager's *Details* tab still lists the file `Enigma.Backup.Desktop.exe`, on purpose: that column
  is the file name.

## Build/test evidence

- Baseline: see `docs/done/FEATURE-1AA8-PHASE01.md` (0 warnings; 590 tests, 578 passed, 12 skipped,
  0 failed).
- Debug and Release builds: **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Backup.slnx`: **591 total, 579 passed, 12 skipped, 0 failed**. The one
  extra test is the new one.
- The extracted single-file exe from `./publish.ps1 -Rid win-x64`:
  - `FileDescription` and `ProductName` are `Enigma Backup`;
  - `ProductVersion` is `1.6.0+ea75725…`, `FileVersion` `1.6.0.0`, and `LegalCopyright`
    `Copyright © 2026 Josué Clément`, all unchanged.
- The same exe, launched:
  - `Get-Process` reports `Description: Enigma Backup` and `Product: Enigma Backup`, and the window
    title is `Enigma Backup`;
  - About, read through UIA, still shows `Version 1.6.0`, `Build ea75725` and the copyright;
  - the process was stopped by its PID.
- Fix budget: 0 cycles used.

## Documentation sweep

- `RELEASENOTES.md` 1.6.0 and `docs/QA-CHECKLIST.md` 1.7 updated (above).
- README's zip table names the exe file, which did not change. `CLAUDE.md` does not mention the
  assembly's metadata.
