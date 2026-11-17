# FEATURE-63B5 — Release 1.6.0

**Status:** DONE
**Type:** FEATURE
**Branch:** `feature/feature-63b5-release-1-6-0`
**Run:** feature/2026-11-16-centered-pages-release

## Objective

Make the repository release-ready at **1.6.0**, using FEATURE-63DB (1.5.0) as the template and following
`docs/RELEASE.md` §1 and the `dotnet-release` skill. The spec's rule is `x.y.z → x.(y+1).z`, so 1.5.0
becomes 1.6.0. Under SemVer that is a minor release: a new page-based window, nothing breaking, and no
change to the archive or profile formats.

## Context & constraints

- `<Version>1.5.0</Version>` lives in `Directory.Build.props`. Tags are bare (`1.0.1` … `1.5.0`), and
  `1.6.0` does not exist yet.
- **1.6.0 is the first release of FEATURE-95BE** (navigation rail and page-based UI, all five phases
  `DONE`; its plan put the release in "a separate item"). So the notes describe that change first:
  - the rail and its four pages;
  - runs shown in a dialog of their own;
  - the Settings page and About;
  - backing up a saved profile from the Backup page.

  They then describe this run's FEATURE-464A (centred pages) and FEATURE-3310 (About says "Enigma
  Backup"). An item quarantined by the run is described only if it was merged.
- `packaging/windows/buildmsi.ps1` holds no version literal: it reads `<Version>`. Its `$UpgradeCode`
  never changes (`WindowsInstallerTests`).
- `docs/RELEASE.md` §1 lists the pre-release checks: the version, the top `RELEASENOTES.md` section
  (with *Dependencies* and a vulnerability scan), the README callout and **Installing** zip names,
  `SECURITY.md`, a clean Release build, the full suite, the QA checklist matching the UI, the EBK1 byte
  unchanged, and the profile write rule holding.
- The **linux-x64 zip must be built from Linux** (RELEASE.md §2). This host is Windows.
- The Avalonia set (with `Enigma.Avalonia.Desktop`) moves only as a unit.
- RELEASE.md §4–§6 (merge, tag, GitHub release) are the maintainer's. The run never merges into
  `develop` or `main`, never tags, never pushes, and never installs the MSI.

## Steps

1. Check that `git tag --list 1.6.0` is empty.
2. `Directory.Build.props`: `<Version>1.6.0</Version>`.
3. `RELEASENOTES.md`: a new top `1.6.0` section in 1.5.0's shape, with these parts:
   - an intro;
   - *What is new*: the page-based window, the run dialog, Settings/About, the profile backup, centred
     pages and the name in About;
   - *Files*, *Dependencies* (what moved, or "none", plus the scan), *Compatibility*, *Version*.

   Check every path and UI label the notes quote against the files.
4. README: the "What's new in 1.6.0" callout and the **Installing** zip names. Fix any README text that
   describes the pre-1.6.0 window layout (single window, Settings dialog, gear and ⓘ buttons) if it is
   now factually wrong.
5. `SECURITY.md`: 1.6.x is supported, and 1.5.x no longer is.
6. `docs/QA-CHECKLIST.md`:
   - the intro's "Since FEATURE-95BE" notes become "Since 1.6.0", and the "centred column" and
     "Enigma Backup in About" changes are mentioned;
   - 8.13 expects `Version 1.6.0`;
   - a *What was verified for 1.6.0* record is added.
7. Dependency check (`dotnet list package --outdated`, `--vulnerable --include-transitive`), with the
   result logged. Non-coupled patch and minor bumps are applied; the Avalonia set is held back unless it
   moves as a unit.
8. Checks:
   - the Release build has zero warnings;
   - the full suite passes;
   - the EBK1 byte is unchanged (`GoldenArchiveCompatibilityTests` green);
   - the profile write rule holds (`ProfileSerializationTests` green).
9. `./publish.ps1 -Rid win-x64`: `artifacts/Enigma.Backup-1.6.0-win-x64.zip` holds the exe and
   `app.ico`, the exe reports 1.6.0, and About shows `Enigma Backup` and `Version 1.6.0`.
10. Run `buildmsi.bat` in a scratch clone of the run branch with this dev's tree applied. Check that the
    MSI is `Enigma.Backup.1.6.0.msi` and that its tables carry `ProductVersion` 1.6.0 and the unchanged
    `UpgradeCode`. Do not install it.

## Acceptance criteria

- Every place that names the current version says 1.6.0. No stale 1.5.0 is left outside the history
  sections and the records of what 1.5.0 introduced.
- `buildmsi` produces `Enigma.Backup.1.6.0.msi` with `ProductVersion` 1.6.0, without an edit to the
  script.
- The `1.6.0` notes cover FEATURE-95BE and every merged item of the run, every dependency move, and the
  scan result.
- The Release build has zero warnings, and the whole suite has no failure the run's baseline did not
  have. The win-x64 zip is built, holds two files, and its exe carries 1.6.0.
- The completion doc lists what remains for the maintainer: the linux-x64 zip from Linux, the QA
  checklist, `buildmsi` on the merged commit, the merges, the tag and the GitHub release.
- Nothing in the dev runs `git tag` or `git push`, or installs the MSI.

## Out of scope

- Merging, tagging, pushing and publishing the GitHub release: the maintainer's, per RELEASE.md §4–§6
  and the run rules.
- Building the 1.6.0 MSI for distribution (the user runs `buildmsi` on the tagged commit).
- Moving the Avalonia set on its own.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Version | 1.6.0 | The spec's rule (y+1, z stays 0). The UI is reshaped and nothing breaks, so SemVer agrees | 1.5.1; 2.0.0 |
| What the notes cover | FEATURE-95BE first, then this run's two items | 95BE is merged into this run's base and has never been released. Notes that skip it would hide the biggest change of the version | This run's two items only |
| Order in the run | Last | The notes and QA record describe the two UI items, built first | First |
| Shape of the notes, callout and tag | As 1.5.0 | House convention | A single *Changes* list |
| Dependencies | Non-coupled patch/minor bumps applied; the Avalonia set held unless it moves as a unit | `docs/RELEASE.md` §1, `CLAUDE.md` | Bumping everything |
| Zips | win-x64 built and checked here; linux-x64 owed on Linux | RELEASE.md forbids a Linux zip built on Windows | Building it here anyway |
| MSI version | No script edit; a scratch-clone build shows the MSI comes out as 1.6.0 | The script reads `<Version>` from `Directory.Build.props` (FEATURE-63DB precedent) | A version literal in `buildmsi.ps1` |
| Breakdown | One phase | A routine release | Splitting docs from the version |
