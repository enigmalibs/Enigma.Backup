# FEATURE-93D8 — Release 1.3.0

**Status:** DONE
**Type:** FEATURE
**Branch:** `feature/feature-93d8-release-1-3-0`
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

Make the repository release-ready at **1.3.0**, which closes this run. The spec's rule is
`x.y.z → x.(y+1).0`, so 1.2.0 becomes 1.3.0. Under SemVer that is a minor release: visible additions
(About dialog, error dialog, new icon, Linux install, publish scripts), nothing breaking, no archive or
profile format change.

## Context & constraints

- `<Version>1.2.0</Version>` lives in `Directory.Build.props`. Tags are bare (`1.0.0`, `1.0.1`, `1.1.0`,
  `1.2.0`).
- `docs/RELEASE.md` §1 lists the pre-release checks:
  - Version;
  - the top `RELEASENOTES.md` section, with *Dependencies* (`old → new`) and a vulnerability scan;
  - the README "what's new" callout and its **Installing** zip names;
  - the `SECURITY.md` supported-versions table;
  - a clean Release build;
  - the full suite;
  - `docs/QA-CHECKLIST.md` matching the UI;
  - the EBK1 version byte unchanged;
  - the profile write rule holding.
- §2 builds the zips with `publish.ps1` / `publish.sh`. The **linux-x64 zip must be built from Linux**
  (`Compress-Archive` drops the exec bit). This host is Windows.
- §4–§6 (merge, tag, GitHub release) are the maintainer's. The run never merges into `develop` or `main`,
  never tags, never pushes.
- The run's devs, which this release describes:
  - FEATURE-0725 packages;
  - FEATURE-9755 icons;
  - FEATURE-4573 About;
  - FEATURE-5D75 `app.ico`;
  - FEATURE-AD48 safety net;
  - FEATURE-61C4 Linux install;
  - FEATURE-251A publish scripts.

  A quarantined one is described only if it was merged (it was not), so the notes cover what the run
  branch actually holds.

## Spec coverage (items the run did not build)

- **Remove splash screen:** not applicable. The app has no splash screen; `Program.Main` starts the
  classic desktop lifetime and `App` shows `MainWindow` directly.
- **Remove NET8:** not applicable. Every project (`Enigma.Backup.Core`, `.Desktop` and both test projects)
  targets `net10.0` alone. There is no `net8.0` target and no `netstandard` target to keep.

## Steps

1. `Directory.Build.props`: `<Version>1.3.0</Version>`.
2. `RELEASENOTES.md`: a new top `1.3.0` section in the house voice, with:
   - what is new;
   - *Files*: nothing new is stored, `settings.json` and profiles are unchanged, the EBK1 byte is unchanged;
   - *Dependencies*: every `old → new` from FEATURE-0725's completion doc, plus a clean
     `dotnet list package --vulnerable --include-transitive`;
   - *Compatibility*.
3. README: the "what's new" callout, the **Installing** zip names (1.3.0), and anything else the run's
   devs left pointing at 1.2.0.
4. `SECURITY.md`: the supported-versions table covers `1.3.x`.
5. `docs/QA-CHECKLIST.md`: checked against the UI as it now is (About button, error dialog, Settings
   without About).
6. Checks: Release build with zero warnings; the full suite; the EBK1 version byte unchanged (golden test
   green); the profile write rule (serializer test green).
7. `./publish.ps1 -Rid win-x64`: the zip is named `Enigma.Backup-1.3.0-win-x64.zip`, and its staging
   folder holds the exe and `app.ico`. Smoke-launch the extracted exe. The linux-x64 zip is recorded as
   owed on Linux.
8. CLAUDE.md: any 1.2.0 mention that should now say 1.3.0.

## Acceptance criteria

- Every place that names the current version says 1.3.0. No stale 1.2.0 remains outside the history
  sections.
- The `1.3.0` notes list every merged dev of the run, every dependency move, and the scan result.
- Release build has zero warnings and the whole suite is green. The win-x64 zip is built and launches.
- What remains for the maintainer is listed in the completion doc: the linux-x64 zip from Linux, the QA
  checklist on both platforms, the merges, the tag and the GitHub release.

## Out of scope

- Merging, tagging, pushing, publishing the GitHub release (the maintainer's, per RELEASE.md §4–§6 and the
  run rules).

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Version | 1.3.0 | The spec's rule. Additive and nothing breaks, so SemVer agrees | 2.0.0 (a major of Enigma.Core is not a break of this app) |
| Splash screen / net8 items | Recorded as not applicable, no dev | Nothing to remove. A dev with no change would be noise | Empty devs |
| Zips | win-x64 built and smoke-tested here; linux-x64 owed on Linux | RELEASE.md forbids a Windows-built Linux zip | Building it here anyway |
| Merge and tag | Left to the maintainer | The run never writes `develop`/`main` or tags (vibe §5) | — |
