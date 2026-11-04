# FEATURE-63DB — Release 1.5.0

**Status:** TODO
**Type:** FEATURE
**Branch:** `feature/feature-63db-release-1-5-0`
**Run:** feature/2026-11-04-icons-release

## Objective

Make the repository release-ready at **1.5.0**, with FEATURE-9663 (1.4.0) as the template, following
`docs/RELEASE.md` §1 and the `dotnet-release` skill. The spec's rule is `x.y.z → x.(y+1).z`, so 1.4.0
becomes 1.5.0, a minor release under SemVer: a new icon, nothing breaking, no archive or profile format
change. The spec also asks for "the new version number in the buildmsi script": the MSI has to come out
as 1.5.0.

## Context & constraints

- `<Version>1.4.0</Version>` lives in `Directory.Build.props`. Tags are bare (`1.0.1` … `1.4.0`).
- **`packaging/windows/buildmsi.ps1` holds no version literal.** It reads `<Version>` from
  `Directory.Build.props` and names the MSI, its `version` and its profile after it. `docs/RELEASE.md`
  (*The Windows installer*) says so: "a release changes nothing for the installer". Its `$UpgradeCode`
  never changes (`WindowsInstallerTests`).
- `docs/RELEASE.md` §1 lists the pre-release checks: the version; the top `RELEASENOTES.md` section, with
  *Dependencies* (`old → new`) and a vulnerability scan; the README callout and **Installing** zip names;
  the `SECURITY.md` supported-versions table; a clean Release build; the full suite; the QA checklist
  matching the UI; the EBK1 byte unchanged; the profile write rule holding.
- **1.5.0 carries** this run's FEATURE-76FB (the icon set that fills its frame), `DONE` on the run branch
  before this dev starts. If it ended quarantined, the notes describe it only if it was merged.
- The **linux-x64 zip must be built from Linux** (RELEASE.md §2). This host is Windows.
- The Avalonia set (with `Enigma.Avalonia.Desktop`) moves only as a unit.
- §4–§6 (merge, tag, GitHub release) are the maintainer's. The run never merges into `develop` or
  `main`, never tags, never pushes, never installs the MSI.

## Steps

1. Check that `git tag --list 1.5.0` is empty.
2. `Directory.Build.props`: `<Version>1.5.0</Version>`.
3. `packaging/windows/buildmsi.ps1`: check that it still takes the version from `Directory.Build.props`
   and has no literal to update; leave it unchanged.
4. `RELEASENOTES.md`: a new top `1.5.0` section in 1.4.0's shape: what is new (the icon), *Files*,
   *Dependencies* (what the outdated check moved, or "none", plus the vulnerability scan),
   *Compatibility*, *Version*. Check every path the notes quote against the files.
5. README: the "what's new" callout names 1.5.0; the **Installing** zip names say 1.5.0; anything else
   that names 1.4.0 as the current version.
6. `SECURITY.md`: 1.5.x is supported, 1.4.x no longer is.
7. `docs/QA-CHECKLIST.md`: a *Since 1.5.0* note for the icon, 8.13 expects `Version 1.5.0`, and a *What
   was verified for 1.5.0* record.
8. Dependency check (`dotnet list package --outdated`, `--vulnerable --include-transitive`), logged.
   Non-coupled patch and minor bumps applied; the Avalonia set held back unless it moves as a unit.
9. Checks: Release build with zero warnings; the full suite; the EBK1 version byte unchanged
   (`GoldenArchiveCompatibilityTests` green); the profile write rule (`ProfileSerializationTests` green).
10. `./publish.ps1 -Rid win-x64`: `artifacts/Enigma.Backup-1.5.0-win-x64.zip` holds the exe and `app.ico`
    (the new icon), and the exe reports 1.5.0.
11. `buildmsi.bat` in a scratch clone of the run branch with this dev's tree applied: the MSI is
    `Enigma.Backup.1.5.0.msi`, and its tables carry `ProductVersion` 1.5.0 and the unchanged
    `UpgradeCode`. Not installed.

## Acceptance criteria

- Every place that names the current version says 1.5.0. No stale 1.4.0 is left outside the history
  sections and the records of what 1.4.0 introduced.
- `buildmsi` produces `Enigma.Backup.1.5.0.msi` with `ProductVersion` 1.5.0, without an edit to the script.
- The `1.5.0` notes cover every merged item of the run, every dependency move, and the scan result.
- The Release build has zero warnings, and the whole suite has no failure the run's baseline did not
  have. The win-x64 zip is built, holds two files, and its exe carries 1.5.0.
- The completion doc lists what remains for the maintainer: the linux-x64 zip from Linux, the QA
  checklist, `buildmsi` on the merged commit, the merges, the tag and the GitHub release.
- Nothing in the dev runs `git tag`, `git push`, or installs the MSI.

## Out of scope

- Merging, tagging, pushing, publishing the GitHub release (the maintainer's, per RELEASE.md §4–§6 and
  the run rules).
- Building the 1.5.0 MSI for distribution (the user runs `buildmsi` on the tagged commit).
- Moving the Avalonia set on its own.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Version | 1.5.0 | The spec's rule (y+1, z stays 0). Additive, so SemVer agrees | 1.4.1; 2.0.0 |
| "Update the version in the buildmsi script" | No edit; show with a scratch-clone MSI build that it comes out as 1.5.0 | The script already reads `<Version>` from `Directory.Build.props`, the one place the version lives. A literal in the script would be a second copy, wrong at the next release unless someone remembers it | A `$Version = '1.5.0'` constant in `buildmsi.ps1`; a committed `msiProfiles/…1.5.0.msipkg.json` (FEATURE-A834 declined both) |
| Order in the run | Last | The notes describe the icon, built first | First |
| Shape of the notes, callout, tag | As 1.4.0 | House convention | A single *Changes* list |
| Dependencies | Non-coupled patch/minor bumps applied; the Avalonia set held unless it moves as a unit | `docs/RELEASE.md` §1 | Bumping everything |
| Zips | win-x64 built and checked here; linux-x64 owed on Linux | RELEASE.md forbids a Linux zip built on Windows | Building it here anyway |
| Breakdown | One phase | A routine release | Splitting docs from the version |
