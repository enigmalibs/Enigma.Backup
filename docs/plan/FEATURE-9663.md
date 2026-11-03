# FEATURE-9663 — Release 1.4.0

**Status:** TODO
**Type:** FEATURE
**Branch:** `feature/feature-9663-release-1-4-0`
**Run:** feature/2026-11-03-icons-msi-release

## Objective

Make the repository release-ready at **1.4.0**, using FEATURE-93D8 (1.3.0) as the template and following
`docs/RELEASE.md` §1 and the `dotnet-release` skill. The spec's rule is `x.y.z → x.(y+1).0`, so 1.3.0
becomes 1.4.0. Under SemVer that is a minor release: a new icon and a new Windows installer, nothing
breaking, no archive or profile format change.

## Context & constraints

- `<Version>1.3.0</Version>` lives in `Directory.Build.props`. Tags are bare (`1.0.0` … `1.3.0`).
- `docs/RELEASE.md` §1 lists the pre-release checks: the version; the top `RELEASENOTES.md` section, with
  *Dependencies* (`old → new`) and a vulnerability scan; the README callout and **Installing** zip names;
  the `SECURITY.md` supported-versions table; a clean Release build; the full suite; the QA checklist
  matching the UI; the EBK1 byte unchanged; the profile write rule holding.
- **1.4.0 carries** this run's items, `DONE` on the run branch before this dev starts:
  - FEATURE-F207: the new icon set without the tile;
  - FEATURE-A834: the vendored Enigma.Msi worker and `buildmsi.ps1` / `buildmsi.bat`.
  An item that ended quarantined is described only if it was merged.
- The **linux-x64 zip must be built from Linux** (RELEASE.md §2). This host is Windows.
- The Avalonia set (with `Enigma.Avalonia.Desktop`) moves only as a unit.
- §4–§6 (merge, tag, GitHub release) are the maintainer's. The run never merges into `develop` or
  `main`, never tags, never pushes, never installs the MSI.

## Steps

1. Check that `git tag --list 1.4.0` is empty.
2. `Directory.Build.props`: `<Version>1.4.0</Version>`.
3. `RELEASENOTES.md`: a new top `1.4.0` section in 1.3.0's shape: what is new (the icon, the Windows
   installer), *Files*, *Dependencies* (the outdated check's moves, or "none", plus the vulnerability
   scan), *Compatibility*, *Version*. Every path the notes quote is checked against the files.
4. README: the "what's new" callout names 1.4.0; the **Installing** zip names say 1.4.0; anything else
   that names 1.3.0 as the current version.
5. `SECURITY.md`: 1.4.x is supported, 1.3.x no longer is.
6. `docs/QA-CHECKLIST.md`: a *Since 1.4.0* note for the icon and the MSI, and a *What was verified for
   1.4.0* record.
7. Dependency check (`dotnet list package --outdated`, `--vulnerable --include-transitive`), logged.
   Non-coupled patch and minor bumps applied; the Avalonia set held back unless it moves as a unit.
8. Checks: Release build with zero warnings; the full suite; the EBK1 version byte unchanged
   (`GoldenArchiveCompatibilityTests` green); the profile write rule (`ProfileSerializationTests` green).
9. `./publish.ps1 -Rid win-x64`: `artifacts/Enigma.Backup-1.4.0-win-x64.zip` holds the exe and `app.ico`
   (the new icon); the exe reports 1.4.0.

## Acceptance criteria

- Every place that names the current version says 1.4.0. No stale 1.3.0 remains outside the history
  sections.
- The `1.4.0` notes cover every merged item of the run, every dependency move, and the scan result.
- The Release build has zero warnings and the whole suite shows no new failure against the run's
  baseline. The win-x64 zip is built, holds two files, and its exe carries 1.4.0.
- The completion doc lists what remains for the maintainer: the linux-x64 zip from Linux, the QA
  checklist, `buildmsi` on the merged commit, the merges, the tag and the GitHub release.
- Nothing in the dev runs `git tag`, `git push`, or installs the MSI.

## Out of scope

- Merging, tagging, pushing, publishing the GitHub release (the maintainer's, per RELEASE.md §4–§6 and
  the run rules).
- Building the 1.4.0 MSI for distribution (the user runs `buildmsi` on the tagged commit).
- Moving the Avalonia set on its own.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Version | 1.4.0 | The spec's rule (y+1, z already 0). Additive, so SemVer agrees | 1.3.1; 2.0.0 |
| Order in the run | Last | The notes describe the icon and the installer, built first | First |
| Shape of the notes, callout, tag | As 1.3.0 | House convention | A single *Changes* list |
| A committed MSI profile for 1.4.0 | None | FEATURE-A834's script generates the profile per build from the version | `msiProfiles/<App>.1.4.0.msipkg.json` (the `dotnet-release` default) |
| Dependencies | Non-coupled patch/minor bumps applied; the Avalonia set held unless it moves as a unit | `docs/RELEASE.md` §1 | Bumping everything |
| Zips | win-x64 built and checked here; linux-x64 owed on Linux | RELEASE.md forbids a Windows-built Linux zip | Building it here anyway |
| Breakdown | One phase | A routine release | Splitting docs from the version |
