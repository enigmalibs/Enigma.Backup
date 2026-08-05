# FEATURE-17FE-PHASE01 — Package, theme & icon foundation

**Status:** DONE · phase 1 of 5 · branch `feature/feature-17fe-phase01-theme-foundation`

## Summary

The Desktop app now draws itself with **Enigma.Avalonia.Desktop 1.0.0**'s theme instead of six
hand-rolled brushes, and the **Phosphor** icon pack is wired and proven. This is the foundation
phase: no control was replaced, no service moved, no dialog changed shape — the window is the same
255 lines of `Border.panel` sections it was, painted from the library's palette.

Four packages were pinned in CPM (inside the version-coupled Desktop group, since
`Enigma.Avalonia.Desktop` pins Avalonia 12.1.1 itself) and referenced from the Desktop project.
`App.axaml` keeps `<FluentTheme />` in `Application.Styles` — the library themes only the controls it
ships, plus `TextBox`/`ComboBox` via FluentTheme's own brush keys — and merges
`avares://Enigma.Avalonia.Desktop/Themes/Fluent.axaml` into `Application.Resources` as a
`ResourceInclude`. The `ThemeDictionaries` block holding `AppDangerBrush`, `AppWarningBrush`,
`AppSuccessBrush`, `AppMutedBrush`, `AppPanelBrush` and `AppPanelBorderBrush` is gone; all 22 usages
across `MainWindow.axaml` and the five dialog Views now name the `Enigma*` equivalents, every one
through `DynamicResource`. `MainWindow` and each dialog window carry
`Background="{DynamicResource EnigmaBackgroundBrush}"`, because the theme paints controls and not
windows.

Two findings from the plan were confirmed rather than assumed:

- **`Enigma.Core` resolves to 1.1.0, not the library's 1.0.0.** `dotnet list package
  --include-transitive` reports `Enigma.Core 1.1.0` for the Desktop project, and
  `GoldenArchiveCompatibilityTests` is green against it — so the golden archive written by the 1.0.0
  build still verifies and restores under the resolution this phase introduces.
- **Zero `AVLN*` warnings.** Counted explicitly in both configurations rather than inferred from
  `Build succeeded`, since `TreatWarningsAsErrors` does not promote them.

## Files/modules touched

**Created**

- `docs/done/FEATURE-17FE-PHASE01.md` — this file.

**Modified**

- `Directory.Packages.props` — `Enigma.Avalonia.Desktop` 1.0.0 added to the Desktop group with a
  comment on why it belongs to the Avalonia-coupled set and on the `Enigma.Core` 1.0.0 → 1.1.0
  resolution; `Enigma.Icons`, `Enigma.Icons.Phosphor` and `Enigma.Icons.Avalonia` at 1.0.0. All three
  icon packages are pinned even though the Avalonia one would pull the other two transitively, so a
  bump is always a visible edit.
- `src/Enigma.Backup.Desktop/Enigma.Backup.Desktop.csproj` — four version-less `PackageReference`s.
- `src/Enigma.Backup.Desktop/App.axaml` — the `ResourceInclude` merge replaces the `ThemeDictionaries`
  block; comments record why `FluentTheme` stays and why an override of an `Enigma*` key would have to
  be merged *after* the library dictionary.
- `src/Enigma.Backup.Desktop/Views/MainWindow.axaml` — window background; `xmlns:ei`; the six brush
  swaps in `Window.Styles` plus the inline skipped-header warning brush; the *Back up* button's
  content becomes an icon + label.
- `src/Enigma.Backup.Desktop/Views/ErrorDecisionDialog.axaml`,
  `MessageDialog.axaml`, `PasswordDialog.axaml`, `ProfileNameDialog.axaml`,
  `RestoreConflictDialog.axaml` — brush swaps and the window background. Wording, layout, bindings and
  `x:DataType` untouched.
- `docs/roadmap.md`, `docs/plan/FEATURE-17FE.md` — statuses.

**Untouched, as the plan expected** — all of `src/Enigma.Backup.Core/`, all of `tests/` (no test was
added or edited this phase), `publish.sh`, `publish.ps1`, `global.json`, `.editorconfig`.

## Deviations & follow-ups

- **The one icon is hosted in a `PathIcon`, not a bare `Path`.** The plan's step 5 asks for
  `{ei:IconGeometry …}` on the *Back up* button, and that is what ships —
  `<PathIcon Data="{ei:IconGeometry Archive}" Width="16" Height="16" />`. The wrapper matters: a
  markup extension is evaluated once at load time and can never follow a brush, and this button is
  `Classes="accent"`, so a fixed `Fill` would have been visibly wrong. `PathIcon` binds its fill to
  its inherited `Foreground`, so the extension supplies only the geometry while the colour stays
  dynamic. Verified both ways — the glyph is grey with the button disabled and white on accent blue
  (see *Build/test evidence*). PHASE03, which spreads icons across the window, should prefer
  `<ei:Icon>` wherever a glyph is themed or bound, per the `enigma-icons` skill.
- **The `Archive` glyph was chosen after enumerating the real enum.** `PhosphorIcon` has 1,512
  members and the markup extensions **throw at load time** on an unknown name (unlike the `Icon`
  control, which silently paints nothing), so the name was confirmed by reflection over the packaged
  assembly rather than guessed. Note that `strings` on the DLL is *not* a reliable way to check a
  short name — .NET's metadata string heap suffix-shares, so `Archive` is stored inside `FileArchive`
  and never appears on its own.
- **No line-ending problems.** Every file this phase touched is LF throughout and the diff carries no
  CRLF↔LF churn, so there is nothing to recommend.
- **Follow-up for PHASE03, not a defect now:** `MainWindow.axaml`'s two `ListBox`es still show
  FluentTheme's own fill (sampled `#f2f2f2` in Light) against the panel's `EnigmaSurfaceBrush`
  (`#ebedf0`). The plan already schedules their `EnigmaSurface`/`EnigmaBorderSubtle` framing in
  PHASE03 step 2; recorded here only so the slight mismatch in this phase's screenshots is understood
  as expected rather than missed.
- **Publish size:** `Enigma.Icons.Phosphor.dll` lands at 4.06 MB in the output, confirming the
  plan's ~4 MB per-zip growth estimate. Nothing to do until PHASE05 records it in the release notes.

## Build/test evidence

- **Debug build:** `dotnet build Enigma.Backup.slnx -c Debug --no-incremental` → `Build succeeded`,
  **0 warnings, 0 errors**.
- **Release build:** same with `-c Release` → `Build succeeded`, **0 warnings, 0 errors**. Checked
  because `DiagnosticsSupport` / `#if DEBUG` paths differ.
- **`AVLN*` warnings: 0** in both configurations, counted by grepping the captured build logs rather
  than trusting the summary line.
- **Tests:** `dotnet test --solution Enigma.Backup.slnx` → **354 passed, 0 failed, 0 skipped**, the
  same 354 as before this phase. **No test file was added, edited or deleted** — the acceptance
  criterion was "full suite green with no test changes".
- **No `App*` brush key remains:** `grep` over `src/` and `tests/` for all six retired keys returns
  nothing. Independently confirmed at runtime — a harness resolving each retired key against the
  merged theme reports `resolves=False` for all six.
- **All seven new keys resolve, in both variants.** Resolved against the merged dictionary:
  `EnigmaBackgroundBrush`, `EnigmaSurfaceBrush`, `EnigmaBorderSubtleBrush`,
  `EnigmaForegroundSecondaryBrush`, `EnigmaSuccessBrush`, `EnigmaWarningBrush`, `EnigmaErrorBrush` —
  Light `#f7f8fa` / `#ebedf0` / `#e0e2e6` / `#6f737a` / `#3b8c4b` / `#c48832` / `#db3b3b`, Dark
  `#1e1f22` / `#2b2d30` / `#43454a` / `#6f737a` / `#59a869` / `#e8a33d` / `#f75464`. (The library
  ships **one brush instance per key** whose `Color` follows the active variant, so the per-variant
  colours come from two runs rather than from querying one.)
- **The app was run on Linux (`DISPLAY=:1`) and screenshotted in both variants**, with
  `RequestedThemeVariant` temporarily pinned to `Dark` then `Light` and **reverted to `Default`**
  afterwards (the persisted preference is PHASE04's job). Startup logged no warning, error or
  exception in either run.
  - **Dark, empty state:** window on `EnigmaBackgroundBrush`, all five panels on `EnigmaSurfaceBrush`
    with `EnigmaBorderSubtleBrush` borders, muted captions legible, `TextBox`/`ComboBox` restyled by
    the library's FluentTheme key overrides.
  - **Light, with a profile loaded:** driven from a seeded profile under an isolated
    `XDG_CONFIG_HOME` — inputs, exclude globs, destination and *Encrypt with a password* all
    populated, so *Back up* was **enabled**. The user's real `~/.config/Enigma.Backup` was confirmed
    untouched.
  - **The icon:** the Phosphor `Archive` glyph renders left of the *Back up* label, grey beside the
    disabled label in the Dark run and white on accent blue in the Light run — the proof that the
    geometry comes from the markup extension while the colour follows `Foreground`.
  - **All five dialogs, both variants:** staged through a throwaway harness that references the
    **built `Enigma.Backup.Desktop` assembly**, so it shows the shipped Views and the shipped theme
    merge, not a copy. There is no click automation on this machine (`xdotool`/`wmctrl` absent), so
    this was the only way to get the dialogs on screen. Error headlines and both validation messages
    read `EnigmaErrorBrush`; the restore-conflict headline reads `EnigmaWarningBrush`; secondary
    labels read `EnigmaForegroundSecondaryBrush`. Pixel-sampled to remove any doubt about the
    per-variant swap: conflict headline `#c48832` on `#f7f8fa` in Light, `#e8a33d` on `#1e1f22` in
    Dark — exactly the documented pair.
  - The harness and the seeded profile live in the session scratchpad and are **not** part of the
    solution; no repository file was added for them.

Acceptance criteria for PHASE01 — Debug and Release clean with zero warnings and zero `AVLN*`, full
suite green with no test changes, every panel/dialog/status colour correct in both variants, no `App*`
brush key left in the solution — are all met.
