# FEATURE-9663 — Release 1.4.0

**Branch:** `feature/feature-9663-release-1-4-0`
**Run:** feature/2026-11-03-icons-msi-release

## Summary

The repository is release-ready at **1.4.0**. The spec's rule is 1.3.0 → 1.4.0, a minor release: a new
icon and a Windows installer, nothing that breaks.

- `Directory.Build.props`: `<Version>1.4.0</Version>`.
- `RELEASENOTES.md`: a new top `1.4.0` section in 1.3.0's shape. It covers:
  - the tile-less icon (FEATURE-F207);
  - the Windows installer (FEATURE-A834);
  - *Files*, *Dependencies* (no package moved; the Avalonia set held back; the scan), *Compatibility*
    and *Version*.
- README: the "What's new in 1.4.0" callout and the **Installing** zip names.
- `SECURITY.md`: 1.4.x is supported and 1.3.x is not.
- `docs/QA-CHECKLIST.md`:
  - a *Since 1.4.0* note;
  - 8.13 expects `Version 1.4.0`;
  - a new §11 *Windows installer* (install, shortcuts, *Apps & features*, upgrade, uninstall);
  - a *What was verified for 1.4.0* record.

## Files/modules touched

- Modified:
  - `Directory.Build.props`
  - `RELEASENOTES.md`, `README.md`, `SECURITY.md`, `docs/QA-CHECKLIST.md`
  - `docs/roadmap.md`, `docs/plan/FEATURE-9663.md`
- Created: `docs/done/FEATURE-9663.md`

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Avalonia 12.1.4 → 12.1.5 | Held back as a unit | Enigma.Avalonia.Desktop 1.3.0 is still its latest and is built against 12.1.4. `CLAUDE.md`: it moves with the set or not at all |
| A QA section for the MSI | §11, Windows only, five checks | The QA checklist must describe what a release ships, and RELEASE.md §6 can now attach an MSI |
| Checking the 1.4.0 MSI | Built in a scratch clone and its tables read; not installed | Shows the version flows into the MSI; installing is outward-facing |

## Deviations & follow-ups

- **The release itself, for the maintainer** (`docs/RELEASE.md` §2–§7):
  - build the **linux-x64 zip on Linux** (`./publish.sh linux-x64`);
  - run the QA checklist against both zips, every **L** column, and §11 against the MSI if it ships;
  - merge the run branch into `develop`, then `develop` into `main`;
  - tag `1.4.0` on `main`;
  - if the release ships the installer, run `packaging\windows\buildmsi.ps1` on the tagged commit,
    after attaching the zips or in a separate clone, because it runs `git clean -dxf`;
  - create the GitHub release with the zips (and the MSI) and the 1.4.0 notes.

  The run did none of these (vibe §5: no writes to `develop`/`main`, no tag, no push).
- `artifacts/Enigma.Backup-1.4.0-win-x64.zip` is on disk, gitignored. It was built before this dev's
  commit, so its exe reports `1.4.0+9f3c163` (the run-branch tip). Rebuild from the tagged commit for the
  published zip.
- **Owed on Linux**, carried over from FEATURE-93D8: a native `install.sh` round trip, now with the
  tile-less icon in the launcher.
- `docs/RELEASE.md` §5 lists `1.0.0, 1.0.1 and 1.1.0` as the existing tags; `git tag` here lists 1.0.1 to
  1.3.0 (1.0.0 is not a local tag). The section says to run `git tag` first, so it was left alone.

## Build/test evidence

- Baseline: see `docs/done/FEATURE-F207.md` (0 warnings; 549 tests, 0 failed).
- `git tag --list 1.4.0`: empty.
- `dotnet build Enigma.Backup.slnx -c Release`: **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Backup.slnx -c Release`: **552 total, 540 passed, 12 skipped, 0 failed**.
- The checks `docs/RELEASE.md` §1 asks for:
  - EBK1 byte unchanged: `GoldenArchiveCompatibilityTests` 2/2;
  - the profile write rule: `ProfileSerializationTests` 21/21.
- `dotnet list package --outdated`: only the Avalonia set (12.1.4 → 12.1.5), held back.
  `--vulnerable --include-transitive`: no vulnerable package in any of the four projects.
- `./publish.ps1 -Rid win-x64` → `artifacts/Enigma.Backup-1.4.0-win-x64.zip` (46.7 MiB):
  - the staging folder holds exactly `Enigma.Backup.Desktop.exe` and `app.ico`;
  - `app.ico` is byte-identical to the new asset;
  - the product version is `1.4.0+9f3c163…`.
- Smoke test of the extracted exe: it starts (`Enigma.Backup`), and **About** shows the tile-less icon,
  `Version 1.4.0`, `Build 9f3c163` and the copyright (screenshot and UIA). It was stopped by its PID.
- `buildmsi.bat` in a scratch clone of this branch with this dev's tree committed:
  - exit 0 in 18 s, and `git status` was clean afterwards;
  - `Enigma.Backup.1.4.0.msi` has `ProductName` Enigma Backup, `ProductVersion` 1.4.0, and `UpgradeCode`
    `{D60F30D6-32D0-4629-BA7D-F98EB9731A91}`.
- Fix budget: 0 cycles used.

## Documentation sweep

- Every place that names the current version says 1.4.0: README callout and zip table, `SECURITY.md`,
  QA 8.13. `grep 1.3.0` outside the history sections finds only:
  - `Enigma.Avalonia.Desktop` 1.3.0 (a package version);
  - the illustrative `1.3.0+<git sha>` in `AppInfo.cs`'s doc comment, left as is;
  - the callout's line about what 1.3.0 brought.
- `CLAUDE.md` names no app version, so nothing to change there.
