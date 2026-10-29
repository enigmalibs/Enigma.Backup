# FEATURE-93D8 — Release 1.3.0

## Summary

The repository is release-ready at **1.3.0**. The spec's rule is 1.2.0 → 1.3.0, a minor release:
user-visible additions, nothing that breaks.

- `Directory.Build.props`: `<Version>1.3.0</Version>`.
- `RELEASENOTES.md`: a new top `1.3.0` section in the house voice, covering:
  - the final icon;
  - the uniform About dialog, with Settings trimmed to Theme and Storage;
  - the safety net and the startup window;
  - `app.ico` beside the exe;
  - the Linux installer and the publish scripts;
  - *Files*, *Dependencies* with every move from FEATURE-0725 and both scans, *Compatibility*, and
    *Version*.
- README: the "What's new in 1.3.0" callout, the **Installing** zip names, and the stale *Capabilities*
  bullet that still put the version in Settings.
- `SECURITY.md`: 1.3.x is supported and 1.2.x is not.
- `docs/QA-CHECKLIST.md`: a *Since 1.3.0* note in the introduction, and a *What was verified for 1.3.0*
  record.

**Spec items that needed no dev:**

- *Remove splash screen*: the app has none. `Program.Main` starts the desktop lifetime, and `App` shows
  `MainWindow` directly.
- *Remove NET8*: every project targets `net10.0` alone. There was no `net8.0` target, and no
  `netstandard` target either.

## Files/modules touched

- Modified:
  - `Directory.Build.props`
  - `RELEASENOTES.md`
  - `README.md`
  - `SECURITY.md`
  - `docs/QA-CHECKLIST.md`
  - `docs/roadmap.md`
  - `docs/plan/FEATURE-93D8.md`
- Created:
  - `docs/done/FEATURE-93D8.md`

## Deviations & follow-ups

- **README *Capabilities* fixed here, not in FEATURE-4573.** Its last bullet said Settings "says which
  version you are running". That sweep missed it, and this release's sweep caught it.
- **The release itself, for the maintainer** (`docs/RELEASE.md` §2–§7):
  - build the **linux-x64 zip on Linux** (`./publish.sh linux-x64`);
  - run the QA checklist against both zips, in particular **every L column**, 7.9–7.12 and 8.13 by eye
    in both variants, and §1.3 on a machine with **no .NET runtime**;
  - merge the run branch into `develop`, then `develop` into `main`;
  - tag `1.3.0` on `main`;
  - create the GitHub release with both zips and the 1.3.0 notes.

  The run did none of these (vibe §5: no writes to `develop`/`main`, no tag, no push).
- **Owed on Linux**, carried over from FEATURE-61C4 / FEATURE-251A: a native `install.sh` (build and
  `--from`), the launcher entry on a real desktop, a native symlink, the root refusal, and
  `packaging/linux/publish.sh`.
- `artifacts/Enigma.Backup-1.3.0-win-x64.zip` is on disk, gitignored. It was built from the release
  branch before the version-bump commit, so its exe reports `1.3.0+9be9970` (the run-branch tip it was
  built on). Rebuild from the tagged commit for the published zip.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| The stale 1.2.0 zip in `artifacts/` | Deleted | This run's FEATURE-5D75 check produced it, in an ignored folder. A 1.2.0-named zip with 1.3.0-era content would mislead |
| QA record scope | Windows zip through UI Automation, with what was not exercised spelled out | The session was locked, so screenshots were impossible. The record says "by automation" wherever it was not by eye |
| Release-notes "Removed" line | Named the project-page link explicitly | It was a 1.2.0 feature, and its removal is user-visible |

## Documentation sweep

- Every place that names the current version says 1.3.0. `grep 1.2.0` over README, SECURITY, CLAUDE.md
  and RELEASE.md finds nothing; the QA checklist and the notes keep 1.2.0 only in their history sections.

## Build/test evidence

- `dotnet build Enigma.Backup.slnx -c Release`: **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Backup.slnx -c Release`: **549 total, 537 passed, 12 skipped, 0 failed**.
- The checks `docs/RELEASE.md` §1 asks for:
  - EBK1 byte unchanged: `GoldenArchiveCompatibilityTests` 2/2;
  - the profile write rule: `ProfileSerializationTests` 21/21.
- `dotnet list package --outdated`: no updates for any project. `--vulnerable --include-transitive`: no
  vulnerable package.
- `./publish.ps1 -Rid win-x64` → `artifacts/Enigma.Backup-1.3.0-win-x64.zip` (46.7 MiB).
  - It holds exactly `Enigma.Backup.Desktop.exe` and `app.ico`, and `app.ico` is byte-identical to the
    asset.
  - Product version `1.3.0+9be9970…`.
- Smoke test of the extracted exe, through UI Automation (the session was locked):
  - it starts and shows the main window;
  - **About** shows `Enigma.Backup`, `Version`, `1.3.0`, a `Build` line and `Close`;
  - **Settings** shows `Open folder` and no project-page link.
