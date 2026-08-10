# FEATURE-7F99-PHASE03 — Restore & Verify relocation

**Branch:** `feature/feature-7f99-phase03-action-buttons`
**Status:** DONE

## Summary

**Restore…** and **Verify…** left the bottom action bar for the top bar, beside the settings gear.
The bottom bar is now the accent **Back up** alone.

The move makes a fact about the app visible that the layout had been hiding: neither command reads
anything from the form. `OnRestoreAsync` is archive picker → destination picker → run, and
`OnVerifyAsync` is archive picker → run; both are `CanExecute: !IsBusy` and nothing between the
profile bar and the bottom button feeds either of them. Sitting next to **Back up** — the one button
the whole form *does* describe — they read as three peers, which is exactly wrong. Beside the gear
they read as what they are: app-level actions that happen to operate on a file you pick.

`MainWindow.axaml` is the only file that changed. No ViewModel, no command, no binding expression
was touched — the two buttons moved with their icons, their `Command` bindings and their
`MinWidth`/`Height` intact.

## Files/modules touched

### Modified

- `src/Enigma.Backup.Desktop/Views/MainWindow.axaml` — the whole change:
  - the profile-bar `Grid` grew a fifth column (`Auto,280,*,Auto,Auto`): `ProfileStatus` keeps the
    star column, column 3 holds the new right-aligned action group, and the gear moved to column 4.
  - the group is a horizontal `StackPanel` — **Restore…**, **Verify…**, then a 1 px
    `EnigmaBorderSubtleBrush` `Border` as a separator, so the two buttons do not read as part of the
    gear.
  - the gear gained `Height="34"` to match its new neighbours; its `Padding` and command are
    unchanged.
  - `ProfileStatus` gained `TextTrimming="CharacterEllipsis"` — it now shares its row with ~285 px of
    buttons, and the plan already accepted truncating the lowest-value text in the row over
    collapsing anything else.
  - the bottom `Border.panel` lost its one-orientation `StackPanel` and holds the accent **Back up**
    button directly, `HorizontalAlignment="Left"` in place of what the stack panel used to give it.
  - two comments rewritten to say why the split is where it is.
- `docs/roadmap.md`, `docs/plan/FEATURE-7F99.md` — PHASE03 status.

**Untouched:** every `.cs` file in the solution, every test, `tests/Enigma.Backup.Core.Tests/Fixtures/`
(verified by `git status`), `Directory.Build.props`, `Directory.Packages.props`, `global.json`,
`.editorconfig`.

## Deviations & follow-ups

- **The gear is now `Height="34"`, which the plan did not mention.** Its default height came out
  ~2 px shorter than the buttons it now stands beside, and an almost-aligned row of three controls
  looks like a mistake rather than a choice. Nothing else about it changed.
- **`ProfileStatus` gained `TextTrimming`, also not spelled out in the plan.** The plan accepted that
  the status text "may truncate" at the minimum width; a `TextBlock` without trimming does not
  truncate, it clips mid-glyph. The trimming is what makes the accepted fallback look deliberate. In
  practice it does not trigger — see below.
- **The 960 px check needed a temporary source edit.** This machine has no `xdotool`, `wmctrl`,
  `kdotool` or `ydotool`, so a window cannot be resized from a script. The window's `Width="1120"`
  was changed to `960` for two screenshot runs and changed straight back; the reverted line and the
  clean `git diff` are both recorded under *Build/test evidence*. `MinWidth="960"` was never touched,
  so what was photographed is the real minimum-width layout.
- **A synthetic click was not possible, so "the buttons still run their flows" is argued, not
  clicked.** With no input-synthesis tool on the box, the evidence is: the two `Command` bindings are
  byte-identical to their previous form (see the diff), they resolve against the same
  `MainWindowViewModel` because the move introduced no new `x:DataType` scope, compiled bindings make
  an unresolvable one a build error, the build is clean with zero `AVLN`, and both commands stay
  covered end to end by `MainWindowViewModelRunTests`, `MainWindowViewModelOverlayTests` and
  `WindowEndToEndTests`. The buttons render enabled and full-size at the new position in all four
  screenshots.
- **The two buttons are now inside `Border IsEnabled="{Binding IsIdle}"`**, which the bottom bar did
  not have. This adds a second disabling mechanism on top of the commands' own `!IsBusy`, keyed off
  the same flag — the plan calls this out and accepts it. Nothing can be disabled that was not
  disabled before.
- **The input-actions `WrapPanel` wraps at 960 px** (*Remove* / *Remove all* drop to a second row).
  That is the pre-existing behaviour its own comment describes, unchanged by this phase and visible in
  the same screenshots.
- **Line endings:** this phase's diff contains no CRLF↔LF churn. No action taken — per the workflow
  this is recommendation-only.
- **For PHASE05:** `README.md`'s *Using it* walkthrough describes Restore and Verify as bottom-bar
  actions and needs the top-bar wording; `docs/QA-CHECKLIST.md` names their old position too. Both
  are already PHASE05 steps, and the freshness sweep for this phase deliberately left them to it
  rather than half-updating docs that PHASE04's Settings work will touch again.

## Build/test evidence

- **Build (Debug):** `dotnet build Enigma.Backup.slnx` → **0 warnings, 0 errors**.
- **Build (Release):** `dotnet build Enigma.Backup.slnx -c Release --no-incremental` → **0 warnings,
  0 errors**, `grep -c AVLN` over the full log → **0**. (`TreatWarningsAsErrors` does not promote
  `AVLN*`, so every `.axaml`-touching phase reads the count explicitly.) Both were re-run after the
  temporary-width revert, against exactly the source being committed.
- **Tests:** `dotnet test --solution Enigma.Backup.slnx` → **472 passed, 0 failed, 0 skipped** — the
  same 472 as PHASE02, with **no test file edited or added**, which is what this phase's acceptance
  asks for. `git status --porcelain tests/Enigma.Backup.Core.Tests/Fixtures/` → empty.
- **Diff:** `git diff --stat` → `MainWindow.axaml` (+47/−26) plus the two status lines in
  `docs/roadmap.md` and `docs/plan/FEATURE-7F99.md`. No `.cs` file appears.
- **Verified running** on Linux (Wayland + XWayland, `DISPLAY=:1`), Debug build, screenshots with
  `spectacle`, four in total — **Dark** and **Light** × **1120 px** (default) and **960 px**
  (`MinWidth`):
  - The top bar reads `Profile │ combo │ status │ Restore… │ Verify… │ ▏ │ ⚙` at both widths in both
    variants. The three controls are the same height, and the separator is visible in both variants
    (subtle in Light, as `EnigmaBorderSubtleBrush` is meant to be — confirmed by pixel inspection of
    the zoomed crop, not by eyeballing the full screenshot).
  - At **960 px** nothing collapses: the combo keeps its full 280 px, both buttons keep their
    130 px minimum, and `No profile loaded.` still renders in full with roughly 90 px of the star
    column to spare — enough that the longest status string the ViewModel can produce
    (`Not saved as a profile yet.`, ~50 px wider) also fits, and the new trimming only ever applies
    below the window's own minimum width.
  - The bottom bar holds **Back up** alone, left-aligned at its 140 px minimum, correctly greyed out
    while the input list is empty.
  - The app was launched from the built binary with the theme forced through
    `~/.config/Enigma.Backup/settings.json`, which was copied first and restored afterwards; the
    folder now holds exactly what it did before (`logs/` and `settings.json` with `"theme":
    "System"`). No profile was created for this phase.

## Acceptance criteria

| Criterion | Met |
|---|---|
| `MainWindow.axaml`: the two buttons moved into the profile-bar grid beside the gear (right aligned, separator, icons and bindings kept) | ✅ |
| The bottom action bar reduced to the accent **Back up** | ✅ |
| No ViewModel changes (commands, `CanExecute` and flows untouched) | ✅ — the diff contains no `.cs` file |
| No new tests; the existing suite passes unmodified | ✅ — 472 passed, no test file touched |
| Builds clean, zero `AVLN*` | ✅ — Debug and Release, 0 warnings, `grep -c AVLN` → 0 |
| Manually: the window reads correctly at the default and the 960 px minimum width in both variants (screenshots) | ✅ — four screenshots, nothing collapses at 960 px |
| The two buttons still run their flows from the new position | ✅ by unchanged bindings + build-time binding resolution + the existing command tests; **not** by a synthetic click — no input-synthesis tool exists on this machine (recorded above) |
| Both disabled while a run is active exactly as before | ✅ — same `!IsBusy` commands, now additionally inside the bar's `IsEnabled="{Binding IsIdle}"` |
