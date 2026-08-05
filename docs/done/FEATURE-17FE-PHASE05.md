# FEATURE-17FE-PHASE05 — Release readiness: 1.1.0

**Status:** DONE · phase 5 of 5 (the item's last) · branch `feature/feature-17fe-phase05-release`

## Summary

The repository is release-ready at **1.1.0**. No production code changed in this phase — the version
string moved and the release documentation caught up with the four phases that redrew the app.

- **`<Version>` 1.0.1 → 1.1.0**, once, in `Directory.Build.props`, which is also where
  `publish.sh` / `publish.ps1` read the zip names from.
- **`RELEASENOTES.md` gains a 1.1.0 section**: what the UI now is (theme preference, in-window
  dialogs, the run card, the outcome banner, the restyled window), the new `settings.json` as an
  additive file whose absence is normal, the four new packages with their measured size, and a
  compatibility statement that names what did *not* move.
- **`README.md`** — the what's-new callout and both zip names are at 1.1.0, and a new **Built on**
  table lists what the app stands on, including Phosphor's upstream MIT attribution.
- **`SECURITY.md`** — `1.1.x` supported, `1.0.x` no longer.
- **`docs/QA-CHECKLIST.md`** — rewritten where the UI moved, plus a new §8 for the theme preference
  and a *What was verified for 1.1.0* record that is explicit about the artifacts not existing yet.
- **`docs/RELEASE.md`** — two stale spots fixed, and the dependency check now names the vulnerability
  scan the plan actually asks for.

The zips, the tag and the GitHub release stay with the user; the runbook for them was printed to the
console rather than executed.

**The strongest fact this phase established** is that `src/Enigma.Backup.Core/` and
`tests/Enigma.Backup.Core.Tests/` are **byte-for-byte identical to the `1.0.1` tag**
(`git diff --stat 1.0.1..HEAD -- …` is empty). Everything the release notes claim about archive
compatibility rests on that plus the golden-archive test, not on an assurance.

## Files/modules touched

**Created**

- `docs/done/FEATURE-17FE-PHASE05.md` — this file.

**Modified**

- `Directory.Build.props` — `<Version>` `1.0.1` → `1.1.0`. Nothing else in the file.
- `RELEASENOTES.md` — a `v1.1.0` section above the untouched 1.0.1 and 1.0.0 ones, in five parts:
  *What is new* (the five user-visible changes), *Files* (`settings.json`, additive, absent is
  normal), *Dependencies* (the four packages, the ~4.1 MB they add, the Enigma.Core 1.0.0 → 1.1.0
  resolution, and that nothing else moved), *Compatibility* (the core is byte-identical to 1.0.1; the
  `EBK1` byte, profile JSON, archive naming and the sidecar all unchanged), and *Version* (why a
  minor).
- `README.md` — the what's-new callout rewritten for 1.1.0; the Installing table's two zip names;
  and a new **Built on** section between *Layout* and *Building*.
- `SECURITY.md` — the supported-versions table is now `1.1.x` ✅ / `1.0.x` ❌.
- `docs/QA-CHECKLIST.md` — see below.
- `docs/RELEASE.md` — §1's dependency box gains `dotnet list package --vulnerable
  --include-transitive` and a note that `Enigma.Avalonia.Desktop` belongs to the Avalonia-coupled set;
  a new §1 box asks whether the QA checklist still describes the UI; and §5's claim that "the
  repository had no tags before 1.0.1" is replaced by what `git tag` actually shows (`1.0.0` and
  `1.0.1` both exist).
- `docs/roadmap.md`, `docs/plan/FEATURE-17FE.md` — PHASE05 and the item itself to `DONE`.
- `CLAUDE.md` — from the documentation freshness sweep. The `EBK1` invariant said field details "may
  be refined *within* version 1 before the first release"; that window closed at 1.0.0, so it now says
  version 1 is frozen, that any layout change bumps the version byte and is called out as breaking,
  and that the golden-archive test is what backs the opposite claim every release since has made.
  (A second candidate — the intro pointing a newcomer at `docs/plan/FEATURE-3E11.md` alone, when the
  UI architecture now lives in `FEATURE-17FE.md` — was offered and declined.)

**What changed in the QA checklist.** A preamble explaining that questions are now asked *inside* the
window, so Escape or a click on the dimmed area is what cancels one — there is no title-bar ✕ any
more. Then: §2 gains the in-window wording, the Enter/Escape/scrim check and the header-icon check
(2.9, 2.10) and 2.5 notes the greyed-out OK; §3 gains the *Archive options* cards and a
minimum-size layout check (3.7, 3.8) and 3.4 mentions Enter; §4's single "progress" row becomes four
(the run card, the card coming down, the banner, the *Last run* panel); §5 gains the conflict dialog
appearing *above* the card and the card's own heading and Cancel; §6 gains the banner severities and
the previous banner being taken away; §7.6 moves Cancel onto the card, §7.7 replaces the title-bar ✕
with Escape / the dimmed area, and 7.8 checks no scrim is left behind; and §8 is new — nine rows for
the theme, including the deleted, corrupted and unknown-value files.

**Untouched** — all of `src/`, all of `tests/`, `Directory.Packages.props`, `publish.sh`,
`publish.ps1`, `global.json`, `.editorconfig`, `CLAUDE.md`, `LICENSE.md`.

## Deviations & follow-ups

- **The README had no dependency list to extend.** The plan's step 3 says "the dependency list
  extended with the library and the icon packs"; there was none — the only such list lived in the
  1.0.0 release notes. A **Built on** table was written instead, placed between *Layout* (what is in
  the repository) and *Building* (how to build it), carrying the Phosphor MIT attribution the plan
  asks for.
- **`SECURITY.md` marks `1.0.x` unsupported rather than dropping the row.** The file's own sentence is
  "security fixes are provided for the latest released version", so a table listing only the current
  one says less than a table saying what happened to the previous one.
- **`docs/RELEASE.md` was edited more than "only if a step no longer matches reality".** Three edits:
  one is squarely that (the tag claim was false), one adds the `--vulnerable` scan the plan's step 7
  requires of every release but the runbook never mentioned, and one adds a box asking whether the QA
  checklist still matches the UI — which is exactly the drift this phase had to repair by hand.
- **The size figure is measured, not estimated.** The plan's finding said "roughly 4 MB"; the four
  assemblies in `bin/Release/net10.0/` total **4,329,472 bytes** (`Enigma.Icons.Phosphor.dll` is
  4,064,256 of it). The release notes give that as ~4.1 MB against the published *build*, and quote
  1.0.1's zip sizes for reference rather than predicting 1.1.0's — the zips do not exist yet.
- **Nothing in §1 of the QA checklist is ticked, and the 1.1.0 record says so in its first line.**
  Publishing is out of this phase's scope, so there is no artifact to check; the running-app
  verification below used `bin/Release/net10.0/`, not an extracted zip.
- **The app had to be launched with `DOTNET_ROOT=/home/jo/.dotnet`.** The framework-dependent apphost
  in `bin/` cannot find the SDK on this machine, which installs it under `~/.dotnet` rather than
  `/usr/share/dotnet`. This affects running the *build* only — it says nothing about the published
  artifact, which is self-contained and carries its own runtime.
- **Line endings:** every file touched is LF throughout; the diff carries no CRLF↔LF churn. No action
  taken — per the workflow this is recommendation-only.
- **Left to the user, by design:** `./publish.sh` and the win-x64 build on Windows, the two zips,
  the `feature → develop → main` merges, `git tag 1.1.0`, the push and the GitHub release. The
  runbook was printed to the console. Also left to the user: running the full QA checklist against
  the extracted zips and filling in its 1.1.0 record.

## Build/test evidence

- **Release build:** `dotnet build Enigma.Backup.slnx -c Release -t:Rebuild` → `Build succeeded`,
  **0 warnings, 0 errors**.
- **Debug build:** `dotnet build Enigma.Backup.slnx -t:Rebuild` → `Build succeeded`, **0 warnings,
  0 errors**.
- **`AVLN*` warnings: 0** in both configurations, counted by grepping the captured logs
  (`TreatWarningsAsErrors` does not promote them, so a green summary line does not rule them out).
- **Tests:** `dotnet test --solution Enigma.Backup.slnx` → **392 passed, 0 failed, 0 skipped** — the
  same count PHASE04 left, as a docs-and-version phase should leave it.
- **Version resolves:** `dotnet msbuild -getProperty:Version` in
  `src/Enigma.Backup.Desktop` → `1.1.0`, which is what `publish.sh` reads to name the zips.
- **Dependencies:** `dotnet list package --outdated` → *"has no updates given the current sources"*
  for all four projects. `dotnet list package --vulnerable --include-transitive` → *"has no
  vulnerable packages"* for all four. Nothing was bumped; the Avalonia set stays at 12.1.1 as a unit.
- **The core is unchanged since the last release:**
  `git diff --stat 1.0.1..HEAD -- src/Enigma.Backup.Core tests/Enigma.Backup.Core.Tests` → empty.
- **Verified running** on Linux (Wayland + XWayland, `DISPLAY=:1`), from the **1.1.0 Release build**,
  with `XDG_CONFIG_HOME` redirected to a scratch folder holding a seeded profile. Screenshotted with
  `spectacle` in both variants and stopped by PID afterwards:
  1. **Light** (`settings.json` = `Light`) — the window opens Light with no flash of the other
     variant: profile bar with the gear, both framed lists, the `Save backups in` editor with its
     *Browse…* button, the exclude editor, *Archive options* expanded with the Compression and
     *Encrypt with a password* cards, the toggle reading **On**, and the three action buttons.
  2. **Dark** (`settings.json` = `Dark`) — identical layout, every `Enigma*` brush resolved for the
     dark variant, the accent *Back up* button and the toggle both still legible.
  3. The seeded profile loaded in both: name in the drop-down, `Saved.`, two inputs, both exclude
     globs, the output folder and Zstandard.
- **No stray process and no real data touched.** Both launches were killed by PID and confirmed gone;
  `~/.config/Enigma.Backup/` still holds only its July `logs/` folder — no `profiles/`, no
  `settings.json`.

## Acceptance criteria

| Criterion | Met |
|---|---|
| `<Version>1.1.0</Version>` in `Directory.Build.props` | ✅ |
| `RELEASENOTES.md` 1.1.0 section: the UI, the four packages with the size note, `settings.json` as additive, an explicit compatibility statement | ✅ |
| `README.md` what's-new callout, Installing table at 1.1.0, dependencies extended with the library and the icon packs (Phosphor MIT attribution) | ✅ (as a new *Built on* table — see *Deviations*) |
| `SECURITY.md` supported-versions row covers `1.1.x` | ✅ |
| `docs/QA-CHECKLIST.md` rewritten where the UI moved, plus a theme section with the deleted/corrupt cases | ✅ |
| `docs/RELEASE.md` corrected where a step no longer matched reality | ✅ |
| `dotnet list package --outdated` / `--vulnerable --include-transitive`, findings in the notes | ✅ — both clean, recorded under *Dependencies* |
| The merge/tag/publish runbook printed; no zips, no tag, no push | ✅ |
| Release build clean and suite green at 1.1.0 | ✅ — 0 warnings, 392 passed |
| Every box in `docs/RELEASE.md` §1 ticked or explicitly deferred with a reason | ✅ — all nine ticked (§§2–7 are the user's) |
| No unreleased-version string left in the docs | ✅ — every remaining `1.0.1` is a historical record |

### The item's overall acceptance criteria (FEATURE-17FE)

| # | Criterion | Met |
|---|---|---|
| 1 | The app uses the library's theme, `ContentDialog`, `Overlay`, `InfoBar`, pickers, `SettingsCard` family and typed editors; no `App*` brush and no dialog `Window` remains | ✅ (PHASE01–03) |
| 2 | Core byte-for-byte unchanged; no ViewModel names an Avalonia or library type; library services consumed only from `Desktop/Services/` | ✅ — the `git diff` against the `1.0.1` tag is empty |
| 3 | Debug and Release clean, zero `AVLN*`, full suite green, pre-existing ViewModel tests unmodified | ✅ |
| 4 | Backup, restore and verify work end to end, including encryption, cancellation, Skip/Retry/Abort with apply-to-all, and restore conflicts | ✅ (PHASE02–03, plus the end-to-end suite) |
| 5 | The theme preference persists, defaults to following the OS, survives a missing or corrupt file | ✅ (PHASE04) |
| 6 | Verified running on Linux with screenshots in both variants, per phase | ✅ — all five phases |
| 7 | Release-ready at 1.1.0 per `docs/RELEASE.md` §1, artifact steps left to the user | ✅ |
