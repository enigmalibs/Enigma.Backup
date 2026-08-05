# FEATURE-17FE-PHASE04 — Theme preference & Settings dialog

**Branch:** `feature/feature-17fe-phase04-settings`
**Status:** DONE

## Summary

The app now has a preference of its own — the theme — and somewhere to keep it and somewhere to
change it.

`System` / `Light` / `Dark`, defaulting to `System`, persisted in
`<config>/Enigma.Backup/settings.json` beside the `profiles/` folder the core already writes. A gear
at the far end of the profile bar opens a Settings `ContentDialog` holding one `SettingsCard` row; a
choice made there repaints the whole app immediately and is written out in the same step, so there is
no OK button and nothing to read back. Startup applies the stored variant before the window is shown.

Three decisions are worth naming, because the shape of the code follows from them:

- **The preference is Desktop-owned.** A theme variant means nothing to a command line, so a settings
  concept in `Enigma.Backup.Core` would be UI leaking inward past the first invariant in `CLAUDE.md`.
  `Enigma.Backup.Core` is untouched by this phase.
- **`System` is a value, not the absence of one.** Two values were rejected at planning because
  "follow the OS" would exist only until the first click and could never be returned to. `System`
  assigns `ThemeVariant.Default`; the other two pin the variant. Returning to `System` is a change
  like any other, and there is a test for exactly that.
- **A preference must never be able to stop the app from opening.** Every way `settings.json` can be
  unreadable — absent, truncated, hand-edited to a theme name this build does not know, written by a
  future format version, not even a JSON object — ends in the defaults and a log line. Where a
  malformed *profile* is an error the user is shown, a malformed preference is not.

The one line of Avalonia this needs (`Application.RequestedThemeVariant`) is behind
`IThemeSwitcher`, so `SettingsDialogViewModel` names no Avalonia type and the whole Desktop suite
stays headless.

## Files/modules touched

### Created — production

- `src/Enigma.Backup.Desktop/Settings/AppTheme.cs` — the three-value enum, with why it is three.
- `src/Enigma.Backup.Desktop/Settings/AppSettings.cs` — the preferences, with a `formatVersion` guard
  in the shape of `BackupProfile`'s.
- `src/Enigma.Backup.Desktop/Settings/IAppSettingsStore.cs` — `FilePath` / `Load()` / `Save()`.
  Synchronous, and the interface says why: the variant has to be applied before the window is shown
  from a method that is not `async`, and a choice is written from the property setter the drop-down is
  bound to, where a fire-and-forget task would be a write nobody can see the end of.
- `src/Enigma.Backup.Desktop/Settings/AppSettingsStore.cs` — the file-backed store. Raw-document
  `formatVersion` check (so a file that omits the field is rejected rather than inheriting the current
  version from the property initializer), themes written as names through
  `JsonStringEnumConverter`, write-then-move so an interrupted save leaves the previous choice intact,
  and neither call able to throw.
- `src/Enigma.Backup.Desktop/Services/IThemeSwitcher.cs` + `ThemeSwitcher.cs` — the seam for
  `Application.RequestedThemeVariant`, `ThemeVariant.Default` for `System`. A no-op when there is no
  `Application`, which is what a headless test has.
- `src/Enigma.Backup.Desktop/ViewModels/ThemeOption.cs` — the drop-down's entries, shaped exactly like
  `CompressionOption` (`Value` / `Label` / `Description`, `All`, `For`).
- `src/Enigma.Backup.Desktop/ViewModels/SettingsDialogViewModel.cs` — loads on construction, and on a
  change applies through `IThemeSwitcher` then saves through `IAppSettingsStore`.
- `src/Enigma.Backup.Desktop/Views/SettingsDialog.axaml(.cs)` — one `SettingsCard` row with the
  Phosphor `Palette` glyph, the selected option's own sentence as the card's description, and a
  selectable line naming the file. Enter closes it, as Escape does; the root takes the focus on attach
  (posted at `DispatcherPriority.Loaded`) per the dialog invariant.

### Created — tests

- `tests/Enigma.Backup.Desktop.Tests/Settings/AppSettingsStoreTests.cs` — 16 cases: the round trip,
  the file's readable shape, folder creation with no `.tmp` left behind, and nine ways a file can be
  unreadable, each of which has to come back as `System`. Also that reading a corrupt file does not
  rewrite it, that a write which cannot happen is dropped rather than thrown, and that the default
  file sits beside `ProfileStore.DefaultDirectory` — which is what pins the "beside `profiles/`"
  decision.
- `tests/Enigma.Backup.Desktop.Tests/ViewModels/SettingsDialogViewModelTests.cs` — 9 cases: opens on
  the stored theme and touches nothing, a choice applies *and* persists exactly once, the theme
  already in use is not a change, every further choice including the return to `System` is applied and
  written in turn, and the gear's command opens the dialog.
- `tests/Enigma.Backup.Desktop.Tests/TestSupport/FakeAppSettingsStore.cs` — copies on both `Load` and
  `Save`, as the file-backed store effectively does; handing out the stored instance would let a view
  model mutate "what is stored" without ever saving, and a test asserting persistence would pass
  either way.
- `tests/Enigma.Backup.Desktop.Tests/TestSupport/FakeThemeSwitcher.cs` — records what was applied.

### Modified

- `src/Enigma.Backup.Desktop/Services/IDialogService.cs` — one method, `ShowSettingsAsync`, returning
  nothing: the choices inside have already applied.
- `src/Enigma.Backup.Desktop/Services/DialogService.cs` — the sixth question, through the same
  `ShowAsync` helper as the other five (gear glyph, "Close" button, no answer read back). Takes
  `IAppSettingsStore` and `IThemeSwitcher` because this is where every dialog's view model is built.
- `src/Enigma.Backup.Desktop/ViewModels/MainWindowViewModel.cs` — `OpenSettingsCommand`, guarded on
  `!IsBusy` like every other command and added to `RefreshCommandStates`. Nothing else changed: the
  window has nothing to do with the answer.
- `src/Enigma.Backup.Desktop/Views/MainWindow.axaml` — a fourth column in the profile bar's top row
  holding the gear.
- `src/Enigma.Backup.Desktop/Hosting/ServiceCollectionExtensions.cs` — `IThemeSwitcher`, and
  `IAppSettingsStore` through a factory (the store's second constructor takes an explicit file and
  cannot be resolved from the container — the same reason `AddEnigmaBackupCore` registers
  `ProfileStore` that way).
- `src/Enigma.Backup.Desktop/App.axaml.cs` — the stored variant applied after the host wiring and
  before the window is shown.
- `tests/Enigma.Backup.Desktop.Tests/TestSupport/FakeDialogService.cs` — a `SettingsRequests` count.
- `tests/Enigma.Backup.Desktop.Tests/Hosting/ServiceCollectionExtensionsTests.cs` — `ThemeSwitcher`
  added to the seams assertion, plus a test that the store resolves onto
  `AppSettingsStore.DefaultPath` as one instance (which is what the factory registration is for).
- `docs/roadmap.md`, `docs/plan/FEATURE-17FE.md` — PHASE04 status.
- `CLAUDE.md`, `README.md` — the documentation freshness sweep. `CLAUDE.md`: `IThemeSwitcher` added to
  the view-model seam list, and a new invariant covering the Desktop-owned preferences (why they are not
  in Core, why the store is synchronous, why neither call throws, and why `System` is a value of its
  own). `README.md`: a theme line under *Capabilities*, the gear named in *Using it* step 1, and
  `settings.json` beside the profile folder in the storage paragraph. The 1.1.0 callout and the
  Installing table are left to PHASE05, which already owns them.

**No existing test's expectations were changed** — the plan names that as its own tripwire. The two
edits to existing test files are additive: a new fake member, and new assertions for new
registrations.

**Untouched:** all of `src/Enigma.Backup.Core/` and `tests/Enigma.Backup.Core.Tests/` (including
`Fixtures/`), `Harness.cs` (the main view model's constructor did not change),
`WindowEndToEndTests.cs`, the five existing dialog Views and their view models, `App.axaml`,
`Directory.Packages.props`, `publish.sh`, `publish.ps1`, `global.json`, `.editorconfig`.

## Deviations & follow-ups

- **The view is `SettingsDialog.axaml`, not `SettingsDialogView.axaml`** as the plan's file list wrote
  it. Its five siblings are `PasswordDialog`, `ProfileNameDialog`, `MessageDialog`,
  `ErrorDecisionDialog`, `RestoreConflictDialog`; a lone `…View` suffix beside them would be the odd
  one out. Same for the class name.
- **`IAppSettingsStore` is synchronous**, where the rest of the app's I/O is `async`. The plan does not
  say which, and both callers argue for sync: startup has to apply the variant before the window is
  shown from a non-`async` method, and the save happens inside a property setter. The file is a few
  hundred bytes. Reasoned in the interface's own `<remarks>`.
- **A missing file logs at `Debug`, not `Warning`.** The plan says "logging at warning level" for
  "a missing, unreadable or malformed file"; a missing file is the normal state until the first
  preference is changed, so warning-per-start would be noise. The two *failure* cases log at
  `Warning`, as planned.
- **The dialog was opened programmatically during verification, not by clicking the gear.** There is no
  `xdotool`/`wmctrl` on this machine, so nothing can synthesise a click. What was exercised is the
  exact call the gear's command makes (`IDialogService.ShowSettingsAsync`) plus, separately, a real
  `SettingsDialogViewModel` over the real store and switcher making a choice — i.e. every line the
  click reaches. The click itself is the one thing not covered; `OpenSettingsCommand` being wired and
  executable is asserted by test. Worth a tick in `docs/QA-CHECKLIST.md` at PHASE05.
- **The temporary verification scaffold is gone.** Opening the dialog at startup and driving one choice
  needed eight lines in `App.axaml.cs`; they were reverted and the file's diff re-read to confirm it,
  and a `grep` over `src/` for `SCAFFOLD` / `DispatcherTimer.RunOnce` / `global::Avalonia` finds
  nothing.
- **The machine was left as it was found:** `~/.config/Enigma.Backup/` holds no `settings.json` (it had
  none before this phase), so the next start follows the OS.
- **Line endings:** nothing in this phase's diff is CRLF↔LF churn. No action taken — per the workflow
  this is recommendation-only.
- **For PHASE05:** the release notes should mention `settings.json` as additive and its absence as
  normal, and `docs/QA-CHECKLIST.md` needs the theme-preference section including the deleted and
  corrupt-file cases. Both are already listed in the plan's PHASE05 steps.

## Build/test evidence

- **Build (Debug):** `dotnet build Enigma.Backup.slnx` → **0 warnings, 0 errors**.
- **Build (Release):** `dotnet build Enigma.Backup.slnx -c Release` → **0 warnings, 0 errors**, and
  `grep -c AVLN` over the full log → **0**. (`TreatWarningsAsErrors` does not promote `AVLN*`, so the
  count is read explicitly, as every `.axaml`-touching phase must.)
- **Tests:** `dotnet test --solution Enigma.Backup.slnx` → **392 passed, 0 failed, 0 skipped** (366
  before; +26, all new).
- **Verified running** on Linux (Wayland + XWayland, `DISPLAY=:1`), Debug build, screenshots taken with
  `spectacle`:
  1. **No `settings.json`** — the app opens following the OS (Dark here), the gear renders in the
     profile bar, and no file is created by starting the app.
  2. **`settings.json` = `Light`** — the whole window opens Light: profile bar, both panels, the
     `TextEditor`, the `SettingsCardExpander` and its two cards, every button and drop-down, and the
     gear. Startup applies the stored variant *before* the window is shown — there is no flash of the
     other variant.
  3. **The Settings dialog** — opens in-window above the scrim, in both variants: gear glyph in the
     header, "Settings", the Theme card with the palette glyph and the selected option's sentence, the
     drop-down, the line naming `/home/jo/.config/Enigma.Backup/settings.json`, and Close.
  4. **A choice repaints immediately** — a real `SettingsDialogViewModel` over the real store and
     switcher set to `Dark` while the dialog was open: the window *and* the open dialog re-coloured in
     one step (every brush is a `DynamicResource`), and `settings.json` on disk became
     `{"formatVersion": 1, "theme": "Dark"}`.
  5. **The choice survives a restart** — the app wrote `Light` itself, was killed, and the next start
     opened Light with no scaffold in the build.
  6. **A hand-corrupted file still starts the app** — `{ "formatVersion": 1, "theme": "Aub` →
     the app opens following the OS, logs
     `warn: …AppSettingsStore[0] The settings file … could not be read; the defaults apply.`, and
     leaves the corrupt file exactly as the user left it.
  7. **Deleting `settings.json` returns the app to following the OS** — confirmed, and the file is not
     recreated.

  (One intermediate full-screen capture showed a stale window while a *sibling* repo's app was also
  running on `DISPLAY=:1`; re-run with an active-window capture and a confirmed-dead process, cases 6
  and 7 are identical, as the shared code path requires.)

## Acceptance criteria

| Criterion | Met |
|---|---|
| `AppSettings` + `IAppSettingsStore`/`AppSettingsStore` under `Settings/`, writing `<config>/Enigma.Backup/settings.json`, `formatVersion` guard in `ProfileSerializer`'s shape | ✅ |
| Load returns defaults for a missing, unreadable or malformed file, logged, never fatal; save is write-then-replace and never throws into the UI | ✅ |
| `SettingsDialogViewModel` + its view (`SettingsCard` row, System/Light/Dark), raised by a gear in the profile bar through `IDialogService` — one new method, one new fake response | ✅ |
| Applying a choice assigns the variant through an interface (no view model names Avalonia) and persists it | ✅ |
| Startup applies the stored variant before the window is shown | ✅ |
| Tests: store round-trip; missing → `System`; corrupt → `System`, no throw; unknown value → `System`; the view model persists exactly once per change and applies through the seam | ✅ |
| Build clean, zero `AVLN*` warnings; the new tests plus the whole suite green | ✅ |
| Manually: the gear opens the dialog, each choice repaints immediately, the choice survives a restart, deleting the file returns to following the OS, a corrupted file still starts the app | ✅ (the gear's *click* not synthesisable — see *Deviations*) |
