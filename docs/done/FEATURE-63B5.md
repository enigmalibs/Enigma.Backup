# FEATURE-63B5 — Release 1.6.0

**Branch:** `feature/feature-63b5-release-1-6-0`
**Run:** feature/2026-11-16-centered-pages-release

## Summary

The repository is release-ready at **1.6.0**. The spec's rule takes 1.5.0 to 1.6.0, a minor release.
It is the first release of FEATURE-95BE (the rail of pages, the run dialog, the Settings page, profile
backups from the Backup page), and it also ships this run's FEATURE-464A (centred pages) and
FEATURE-3310 (About says *Enigma Backup*). Nothing in it breaks.

- `Directory.Build.props`: `<Version>1.6.0</Version>`.
- `Directory.Packages.props`: `xunit.v3` 4.0.2 → 4.0.3, the only move applied.
- `RELEASENOTES.md`: a new top `1.6.0` section in 1.5.0's shape. It has:
  - an intro;
  - *What is new*: the rail, the four pages, the run dialog, drops, centred pages, the name, the
    600-px minimum height;
  - *Files*, *Dependencies*, *Compatibility* and *Version*.
- README: the "What's new in 1.6.0" callout and the **Installing** zip names. Its *Using it* section
  already described the pages, because FEATURE-95BE updated it.
- `SECURITY.md`: 1.6.x is supported, and 1.5.x is not.
- `docs/QA-CHECKLIST.md`:
  - the intro's "Since FEATURE-95BE" notes now say "Since 1.6.0", and the centred column (3.14) and the
    name (8.13) are mentioned;
  - 8.13 expects `Version 1.6.0`;
  - a *What was verified for 1.6.0* record is added.
- `packaging/windows/buildmsi.ps1` is unchanged: it reads the version (FEATURE-63DB precedent).

## Files/modules touched

- Modified:
  - `Directory.Build.props`, `Directory.Packages.props`
  - `RELEASENOTES.md`, `README.md`, `SECURITY.md`, `docs/QA-CHECKLIST.md`
  - `docs/roadmap.md`, `docs/plan/FEATURE-63B5.md`
- Created: `docs/done/FEATURE-63B5.md`

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Microsoft.Extensions.* 10.0.14 → 11.0.0 (`Hosting`, `DependencyInjection`, `.Abstractions`, `FileSystemGlobbing`) | Held back | A major version from the .NET 11 line. The app targets the .NET 10 runtime, whose 10.0.x packages are still serviced. The plan applies only patch and minor bumps |
| Avalonia 12.1.4 → 12.1.6 | Held back as a unit | Enigma.Avalonia.Desktop 1.3.0 is still its latest (`--outdated` lists no update for it) and is built against 12.1.4. `CLAUDE.md`: it moves with the set or not at all |
| xunit.v3 4.0.2 → 4.0.3 | Applied | A patch, test-only, outside any coupled set. The suite is green on it |
| The QA intro's "Two new capabilities" sentence, now under "Since 1.6.0" | Reworded: "the two capabilities 1.2.0 added" | Renaming FEATURE-95BE to 1.6.0 would otherwise have dated pattern inputs and zip (FEATURE-7F99, 1.2.0) to this release |
| How to prove About in the zip | UI Automation on the extracted exe | The workstation was locked during this dev, so screen captures returned the lock screen (discarded) |
| Reading the MSI | The Windows Installer COM API (`WindowsInstaller.Installer`), `Property` table | No extra tool needed. FEATURE-63DB used the vendored DTF assembly to the same effect |

## Deviations & follow-ups

- **The release itself, for the maintainer** (`docs/RELEASE.md` §2–§7):
  - build the **linux-x64 zip on Linux** (`./publish.sh linux-x64`);
  - run **the whole QA checklist** against both zips, every **L** column included. 1.6.0 moves almost
    every control, so §§2–10 cannot be skipped this time;
  - run §11 against the MSI if it ships (11.4 is the 1.5.0 → 1.6.0 upgrade);
  - merge the run branch (and, before it, `feature/2026-11-16-page-based-ui`, which it contains) into
    `develop`, then `develop` into `main`;
  - tag `1.6.0` on `main`;
  - if the release ships the installer, run `packaging\windows\buildmsi.ps1` on the tagged commit. It
    runs `git clean -dxf`, so attach the zips first or build in a separate clone;
  - create the GitHub release with the zips (and the MSI) and the 1.6.0 notes.

  The run did none of these (vibe §5: no writes to `develop`/`main`, no tag, no push).
- `artifacts/Enigma.Backup-1.6.0-win-x64.zip` is on disk and gitignored. It was built before this dev's
  commit, so its exe reports `1.6.0+d5e03bd` (the run-branch tip). Rebuild from the tagged commit for
  the published zip.
- **Not seen by eye:** About in the zip (light or dark). The session was locked; UIA read every line
  instead. The centred layout was seen by eye on a Debug build in FEATURE-464A.
- **Still open:**
  - from FEATURE-9663/63DB: `docs/RELEASE.md` §5 lists `1.0.0, 1.0.1 and 1.1.0` as the existing tags,
    while `git tag` lists 1.0.1 to 1.5.0;
  - from FEATURE-3310: the Core error messages still say `Enigma.Backup`;
  - owed on Linux: a native `install.sh` round trip.

## Build/test evidence

- Baseline: see `docs/done/FEATURE-464A.md` (0 warnings; 590 tests, 578 passed, 12 skipped, 0 failed).
- `git tag --list 1.6.0`: empty.
- `dotnet build Enigma.Backup.slnx -c Release` and Debug: **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Backup.slnx -c Release`: **590 total, 578 passed, 12 skipped, 0 failed**
  (on xunit.v3 4.0.3).
- The checks `docs/RELEASE.md` §1 asks for, each passing:
  - EBK1 byte unchanged: `GoldenArchiveCompatibilityTests` 2/2;
  - the profile write rule: `ProfileSerializationTests` 21/21;
  - `WindowsInstallerTests` 3/3 and `AppIconTests` 1/1.
- `dotnet list package --outdated`:
  - the Avalonia set, 12.1.4 → 12.1.6 (held);
  - Microsoft.Extensions.*, 10.0.14 → 11.0.0 (held);
  - xunit.v3, 4.0.2 → 4.0.3 (applied).
- `--vulnerable --include-transitive`: no vulnerable package in any of the four projects.
- `./publish.ps1 -Rid win-x64` → `artifacts/Enigma.Backup-1.6.0-win-x64.zip` (46.7 MiB):
  - the zip holds exactly `Enigma.Backup.Desktop.exe` (54,605,373 bytes) and `app.ico` (11,382 bytes,
    identical to the asset);
  - the product version is `1.6.0+d5e03bd…`, and the file version `1.6.0.0`.
- Smoke test of the extracted exe through UIA:
  - the title is `Enigma Backup`;
  - Settings → *About Enigma Backup* opens About with `Enigma Backup`, `Version 1.6.0`,
    `Build d5e03bd` and the copyright;
  - the process was stopped by its PID.
- `buildmsi.bat` in a scratch clone of the run branch with this dev's modified files copied in:
  - exit 0 in 28 s, and the clone's `git status` afterwards showed only the copied files;
  - `Enigma.Backup.1.6.0.msi` (70,647,634 bytes) has `ProductName` Enigma Backup, `ProductVersion`
    1.6.0, `Manufacturer` Josué Clément and `UpgradeCode` `{D60F30D6-32D0-4629-BA7D-F98EB9731A91}`.

  The MSI was not installed.
- Fix budget: 0 cycles used.

## Documentation sweep

- Every place that names the current version says 1.6.0: the README callout and zip table,
  `SECURITY.md`, and QA 8.13. A `grep` for 1.5.0 outside `RELEASENOTES.md`'s history finds only:
  - the README callout's mention of what 1.5.0 brought;
  - QA's *Since 1.5.0* note and its *What was verified for 1.5.0* record;
  - the 1.5.0 → 1.6.0 upgrade line.
- `CLAUDE.md` names no app version, and nothing in this run contradicts it. `docs/RELEASE.md` is still
  true ("a release changes nothing for the installer").
