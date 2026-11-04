# FEATURE-63DB — Release 1.5.0

**Branch:** `feature/feature-63db-release-1-5-0`
**Run:** feature/2026-11-04-icons-release

## Summary

The repository is release-ready at **1.5.0**. The spec's rule takes 1.4.0 to 1.5.0, a minor release: the
icon that fills its frame (FEATURE-76FB), and nothing that breaks.

- `Directory.Build.props`: `<Version>1.5.0</Version>`.
- **`packaging/windows/buildmsi.ps1` is unchanged, on purpose.** The spec asked to "update the new version
  number in the buildmsi script", but the script has no version literal: it reads `<Version>` from
  `Directory.Build.props`. An MSI built from this tree is `Enigma.Backup.1.5.0.msi` with
  `ProductVersion` 1.5.0, as shown below.
- `RELEASENOTES.md`: a new top `1.5.0` section in 1.4.0's shape. It covers:
  - the larger icon, everywhere it appears, the MSI included;
  - *Files* (zips and MSI unchanged in shape);
  - *Dependencies* (no package moved; the Avalonia set held back; the scan);
  - *Compatibility* and *Version*.
- README: the "What's new in 1.5.0" callout and the **Installing** zip names.
- `SECURITY.md`: 1.5.x is supported, and 1.4.x is not.
- `docs/QA-CHECKLIST.md`:
  - a *Since 1.5.0* note;
  - 8.13 expects `Version 1.5.0`;
  - a *What was verified for 1.5.0* record.

## Files/modules touched

- Modified:
  - `Directory.Build.props`
  - `RELEASENOTES.md`, `README.md`, `SECURITY.md`, `docs/QA-CHECKLIST.md`
  - `docs/roadmap.md`, `docs/plan/FEATURE-63DB.md`
- Created: `docs/done/FEATURE-63DB.md`

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Avalonia 12.1.4 → 12.1.5 | Held back as a unit | Enigma.Avalonia.Desktop 1.3.0 is still its latest and is built against 12.1.4. `CLAUDE.md`: it moves with the set or not at all |
| Proving the MSI's version without editing `buildmsi.ps1` | `buildmsi.bat` in a scratch clone of the dev branch with this dev's modified files copied in; its `Property`, `Icon` and `Shortcut` tables read through the vendored DTF assembly | `git clean -dxf` leaves modified tracked files alone, so the clone built exactly this tree. It never runs in the working repository (`CLAUDE.md`). The MSI was not installed |
| The MSI's icon | Checked as well: both `Icon` rows compared with the asset | The spec's "installers" covers the MSI's shortcuts and *Apps & features* entry, which take `app.ico` |

## Deviations & follow-ups

- **The release itself, for the maintainer** (`docs/RELEASE.md` §2–§7):
  - build the **linux-x64 zip on Linux** (`./publish.sh linux-x64`);
  - run the QA checklist against both zips, every **L** column, and §11 against the MSI if it ships
    (11.4 is the first real 1.4.0 → 1.5.0 upgrade);
  - merge the run branch into `develop`, then `develop` into `main`;
  - tag `1.5.0` on `main`;
  - if the release ships the installer, run `packaging\windows\buildmsi.ps1` on the tagged commit. It runs
    `git clean -dxf`, so attach the zips first or build in a separate clone;
  - create the GitHub release with the zips (and the MSI) and the 1.5.0 notes.

  The run did none of these (vibe §5: no writes to `develop`/`main`, no tag, no push).
- `artifacts/Enigma.Backup-1.5.0-win-x64.zip` is on disk, gitignored. It was built before this dev's
  commit, so its exe reports `1.5.0+a017af3` (the run-branch tip). Rebuild from the tagged commit for the
  published zip.
- **Owed on Linux**, carried over from 1.3.0 and 1.4.0: a native `install.sh` round trip, now with the
  larger icon in the launcher.
- Still open from FEATURE-9663: `docs/RELEASE.md` §5 lists `1.0.0, 1.0.1 and 1.1.0` as the existing tags.
  `git tag` lists 1.0.1 to 1.4.0. Left alone because the section says to run `git tag` first.

## Build/test evidence

- Baseline: see `docs/done/FEATURE-76FB.md` (0 warnings; 552 tests, 0 failed).
- `git tag --list 1.5.0`: empty.
- `dotnet build Enigma.Backup.slnx -c Release`: **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Backup.slnx -c Release`: **552 total, 540 passed, 12 skipped, 0 failed**.
- The checks `docs/RELEASE.md` §1 asks for:
  - EBK1 byte unchanged: `GoldenArchiveCompatibilityTests` 2/2;
  - the profile write rule: `ProfileSerializationTests` 21/21;
  - `WindowsInstallerTests` and `AppIconTests` 4/4.
- `dotnet list package --outdated`: only the Avalonia set (12.1.4 → 12.1.5), held back.
  `--vulnerable --include-transitive`: no vulnerable package in any of the four projects.
- `./publish.ps1 -Rid win-x64` → `artifacts/Enigma.Backup-1.5.0-win-x64.zip` (46.7 MiB):
  - the zip holds exactly `Enigma.Backup.Desktop.exe` and `app.ico`;
  - `app.ico` is byte-identical to the new asset;
  - the product version is `1.5.0+a017af3…`, and the file version `1.5.0.0`.
- Smoke test of the extracted exe: it starts (`Enigma.Backup`), and **About** shows the larger icon,
  `Version 1.5.0`, `Build a017af3` and the copyright (screenshot and UIA). It was stopped by its PID.
- `buildmsi.bat` in the scratch clone:
  - exit 0 in 31 s, and the clone's `git status` showed only the copied files afterwards;
  - `Enigma.Backup.1.5.0.msi` (70,553,426 bytes) has `ProductName` Enigma Backup, `ProductVersion` 1.5.0,
    `Manufacturer` Josué Clément and `UpgradeCode` `{D60F30D6-32D0-4629-BA7D-F98EB9731A91}`;
  - its `Icon` rows `app_icon.ico` (`ARPPRODUCTICON`) and `IconFile1_app.ico` (both shortcuts) are each
    byte for byte the new `.ico`.
- Fix budget: 0 cycles used.

## Documentation sweep

- Every place that names the current version says 1.5.0: the README callout and zip table,
  `SECURITY.md`, and QA 8.13. A `grep` for 1.4.0 outside the history sections finds only:
  - text that says what 1.4.0 introduced (README callout, QA *Since 1.4.0* and §11 intro);
  - the upgrade code "drawn for the first MSI (1.4.0)" in `docs/RELEASE.md` and `WindowsInstallerTests`.
- `CLAUDE.md` names no app version, so it needs no change. `docs/RELEASE.md`'s installer section ("a
  release changes nothing for the installer") is still true.
