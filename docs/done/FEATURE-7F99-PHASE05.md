# FEATURE-7F99-PHASE05 — Release readiness: 1.2.0

**Branch:** `feature/feature-7f99-phase05-release`
**Status:** DONE

## Summary

The repository is release-ready at **1.2.0**. No production code changed: this phase is the version
bump and the documents that have to agree with it.

Four phases built four user-facing things and each deliberately deferred its release prose here, so
this is where the story is told once, in one voice:

- **`<Version>` is 1.2.0**, and that is the only place it is written. It reaches the About section on
  its own (`AppInfo` reads the assembly attribute, not a literal) and it names the zips `publish.sh`
  will produce, which is why the README's *Installing* table cannot disagree with the binary inside.
- **`RELEASENOTES.md` gains the 1.2.0 entry** in the house shape. The part that took the most care is
  *Compatibility*, because this release has an honest asymmetry to state: nothing already in people's
  hands breaks, but a profile that uses a **pattern input** or **Zip** is `formatVersion` 2 and older
  builds refuse it. That is forward incompatibility, chosen deliberately — an older build quietly
  backing up fewer files than the profile asks for is the failure a backup tool must not have — and
  the notes say so rather than leaving it to be discovered.
- **`README.md`, `SECURITY.md` and `docs/QA-CHECKLIST.md`** now describe the app as it is: patterns and
  zip among the capabilities, zip in *What a backup run writes* and in *Restoring without
  Enigma.Backup*, Restore/Verify in the top bar, `1.2.x` supported, and two new manual QA sections for
  the two new features.
- **The zip fidelity limits are stated everywhere they matter** — release notes, README, the format's
  own row in the drop-down, and QA check 10.9, which asks the tester to confirm a restored executable
  has *lost* its bit. A cost that is only discovered is a defect; a cost that is written down is a
  trade.

`EBK1` is untouched, as it has been all five phases, and the golden fixture was never regenerated.

## Files/modules touched

### Created

- `docs/done/FEATURE-7F99-PHASE05.md` — this file.

### Modified

- `Directory.Build.props` — `<Version>` 1.1.0 → **1.2.0**. The only version literal in the solution.
- `RELEASENOTES.md` — the 1.2.0 section on top, newest-first: *What is new* (pattern inputs, the zip
  format, the never-encrypted rule, the relocated commands, Storage and About), *Files* (the
  lowest-version write rule; the zip's own pair of output files; nothing new stored), *Dependencies*
  (nothing added, nothing moved — zip is BCL — with both scans recorded), *Compatibility* (the `EBK1`
  byte, the golden fixture, naming and sidecar, v1 profiles both ways, **v2 profiles refused by older
  builds by design**, stock-tool interop, and what a zip does not carry), *Version* (why a minor).
- `README.md` — the *What's new* callout rewritten for 1.2.0; a zip bullet under *Capabilities* and
  `.zip` added to *Full backups*; the *Installing* table at 1.2.0; *Using it* step 5 gains Zip with its
  costs, step 6 says the toggle greys out under Zip, step 7 is **Back up** alone and a **new step 8**
  puts Restore…/Verify… in the top bar with the reason; *What a backup run writes* names the `.zip`
  pair; *Restoring without Enigma.Backup* gains an `unzip` line, is retitled from "ordinary tar files"
  to "ordinary files — a tar, or a zip", and the Windows sentence now leads with Explorer opening a
  zip by double-click.
- `SECURITY.md` — supported versions `1.2.x` ✅ (1.1.x dropped to ❌); a paragraph in *Scope* stating
  that a `.zip` is never encrypted and the app refuses to pretend otherwise, why every alternative was
  worse, and where encryption does live.
- `docs/QA-CHECKLIST.md` — the throwaway tree gains a `logs/` fixture for the pattern checks; a
  "Since 1.2.0" note about the relocated buttons; §3 gains 3.9–3.11 (the `Add pattern…` button, the
  top-bar layout, and all three top-bar controls disabling during a run); §8 is retitled *Settings:
  theme, storage and About* and gains 8.10–8.15; **new §9 Pattern inputs** (12 checks) and **new §10
  Zip archives** (11 checks); and a *What was verified for 1.2.0* record at the front of the trailer.
- `docs/RELEASE.md` — §5's tag list now names `1.1.0` too, and §1 gains a box for the profile write
  rule (a feature-free profile must still save as version 1, and any new feature forcing a higher
  version must be named in the notes as refused by older builds).
- `docs/roadmap.md`, `docs/plan/FEATURE-7F99.md` — PHASE05 and the item itself to DONE.

**Untouched:** every file under `src/` and `tests/` — this phase changed no production or test code
(`git diff --stat` names none); `tests/Enigma.Backup.Core.Tests/Fixtures/` (verified by `git status`);
`Directory.Packages.props`, `global.json`, `.editorconfig`, `publish.sh`, `publish.ps1`, `CLAUDE.md`
(already current — PHASE02 and PHASE04's sweeps added the zip-beside-`EBK1` and profile-v2 facts and
the `IShellLauncher` seam).

## Deviations & follow-ups

- **The QA checklist was extended, not renumbered.** Pattern inputs belong beside §3 and zip beside
  §4, but the historical *What was verified for…* tables cite section numbers (`3.7`, `8.6`, `2.3/2.4`)
  and renumbering would silently invalidate every one of them. The new features are therefore §9 and
  §10 at the end, with a pointer to them in the header note. The alternative — rewriting three release
  records to keep two sections adjacent — costs more truth than it buys tidiness.
- **`docs/RELEASE.md` §1 gained a box the plan did not ask for.** Step 6 said to touch the runbook
  "only if a step stopped matching reality", and the tag list had (1.1.0 was missing). While there, the
  profile write rule got a checkbox of its own: as of this release the compatibility claim is
  version-dependent, so "did a new feature raise the written version, and did we say so?" is now a
  real release-time question rather than a fact about the code. It mirrors the `EBK1` box beside it.
- **About showing `Version 1.2.0` is evidenced by mechanism, not by a photograph.** Opening the
  Settings dialog needs a click, and this machine still has no `xdotool` / `wmctrl` / `kdotool` /
  `ydotool`. Rather than re-introduce the temporary startup hook PHASE04 used and reverted, the claim
  rests on a chain that is fully checkable: the Release assembly's `AssemblyInformationalVersion` reads
  `1.2.0+6a71212…` (verified against the built DLL), `AppInfo.ShortVersion` strips at the `+` (pinned
  by `AppInfoTests`), and PHASE04 photographed the section rendering whatever that property returned.
  **No source file was modified for verification in this phase** — the diff contains no `.cs` at all.
- **`dotnet msbuild -getProperty:Version` does not work against the `.slnx`** (MSB1063 — properties
  cannot be read from a solution file). This is not a regression: `publish.sh` already reads the
  property from the *project*, which returns `1.2.0` correctly. Worth knowing before anyone tries the
  solution form during a release.
- **No zips, no tag, no push** — as the plan's step 8 requires. §2 onward of `docs/RELEASE.md` is the
  maintainer's, and the runbook is printed to the console instead of executed.
- **`RELEASENOTES.md` records no size figure for 1.2.0.** 1.1.0 quoted the published zip sizes because
  it added 4.1 MB of packages; this release adds no package at all, and quoting a size would mean
  building artifacts this phase is explicitly not building.
- **Line endings:** this phase's diff contains no CRLF↔LF churn. No action taken — per the workflow
  this is recommendation-only.

## Build/test evidence

- **Build (Debug):** `dotnet build Enigma.Backup.slnx --no-incremental` → **0 warnings, 0 errors**.
- **Build (Release):** `dotnet build Enigma.Backup.slnx -c Release --no-incremental -v n` → **0
  warnings, 0 errors**, and `grep -c AVLN` over the full verbose log → **0**.
- **Tests:** `dotnet test --solution Enigma.Backup.slnx` → **489 passed, 0 failed, 0 skipped** — the
  same 489 as PHASE04, with no test file added or edited, which is what a docs-and-version phase
  should produce. `git status --porcelain tests/Enigma.Backup.Core.Tests/Fixtures/` → empty.
- **The bump reaches the binary:** the Release `Enigma.Backup.Desktop.dll` carries
  `1.2.0+6a71212385c9d9cf5e76960f6fb9a16f2f8f1356` as its informational version and `1.2.0.0` as its
  assembly version; `dotnet msbuild src/Enigma.Backup.Desktop/Enigma.Backup.Desktop.csproj
  -getProperty:Version` → `1.2.0`, which is exactly the read `publish.sh` uses to name the zips.
- **Dependency scans**, both clean and both recorded in `RELEASENOTES.md`:
  - `dotnet list package --outdated` → *"has no updates given the current sources"* for all four
    projects.
  - `dotnet list package --vulnerable --include-transitive` → *"has no vulnerable packages given the
    current sources"* for all four projects.
- **No stale version string:** `grep -rn "1\.[0-9]\.[0-9x]"` over `README.md`, `SECURITY.md`,
  `docs/RELEASE.md`, `docs/QA-CHECKLIST.md` and `CLAUDE.md` returns only 1.2.0 (README callout +
  Installing table, SECURITY's supported row) and deliberately historical references (the tag list,
  the earlier release records, CLAUDE.md's compatibility narrative).
- **Smoke run at 1.2.0:** the Release build was launched on Linux (Wayland + XWayland, `DISPLAY=:1`)
  against a throwaway `XDG_CONFIG_HOME` and screenshotted. The window opens titled `Enigma.Backup` with
  the top bar reading `Profile │ combo │ status │ Restore… │ Verify… │ ▏ │ ⚙`, `Add pattern…` beside
  `Add files… / Add folder…`, the two *Archive options* cards, and **Back up** alone at the bottom,
  correctly greyed out on an empty editor. Killed by PID afterwards; `~/.config/Enigma.Backup/` still
  holds exactly what it did before (`logs/` and `settings.json`).

## `docs/RELEASE.md` §1 — pre-release checks

| Box | State |
|---|---|
| `<Version>1.2.0</Version>` in `Directory.Build.props` | ✅ |
| `RELEASENOTES.md` has a top `1.2.0` section | ✅ |
| README's *What's new* callout and *Installing* zip names at 1.2.0 | ✅ |
| `SECURITY.md` supported-versions table covers `1.2.x` | ✅ |
| Clean, warning-free Release build | ✅ — 0 warnings, 0 errors, 0 `AVLN` |
| Full test suite green | ✅ — 489/489 |
| Dependency check, findings logged under *Dependencies* | ✅ — no update available, no known vulnerability |
| `docs/QA-CHECKLIST.md` still describes the UI as it is | ✅ — relocated buttons noted, §§9–10 added, §8 retitled |
| The `EBK1` container version byte unchanged | ✅ — untouched since 1.0.0; `GoldenArchiveCompatibilityTests` green with the fixture unmodified |
| The profile write rule still holds (new box) | ✅ — a feature-free profile still writes 1, pinned by test; the v2-refused-by-older-builds consequence is stated in the notes |

§§2–7 (zips, smoke test on the artifacts, the two merges, the tag, the GitHub release, post-release
verification) are **deliberately deferred to the maintainer** — the plan's step 8 says the artifacts
stay with the user, so nothing was published, tagged or pushed.

## Acceptance criteria

| Criterion | Met |
|---|---|
| `<Version>1.2.0</Version>` in `Directory.Build.props` | ✅ |
| `RELEASENOTES.md` 1.2.0 entry in the house shape (*What is new*, *Files*, *Dependencies*, *Compatibility*, *Version*) | ✅ |
| `README.md`: what's-new callout, Capabilities, *Using it* walkthrough, Installing at 1.2.0, *What a backup run writes*, *Restoring without Enigma.Backup* | ✅ |
| `SECURITY.md`: `1.2.x` row + the zip-is-never-encrypted sentence | ✅ |
| `docs/QA-CHECKLIST.md`: pattern inputs, zip, the relocated buttons, Settings storage + About | ✅ — §§9–10 new, §3 and §8 extended |
| `docs/RELEASE.md` updated only where it stopped matching reality | ✅ — tag list, plus one new §1 box (see *Deviations*) |
| `dotnet list package --outdated` / `--vulnerable --include-transitive`, findings recorded | ✅ — both clean, in *Dependencies* |
| The merge/tag/publish runbook printed; no zips, no tag, no push | ✅ |
| Release build clean and suite green at 1.2.0 | ✅ — 0 warnings, 489/489 |
| Every `docs/RELEASE.md` §1 box ticked or explicitly deferred with a reason | ✅ — table above |
| No stale version string in the docs | ✅ — grep recorded above |
</content>
</invoke>
