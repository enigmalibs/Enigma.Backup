# FEATURE-7F99-PHASE04 — Settings folder & About

**Branch:** `feature/feature-7f99-phase04-settings-about`
**Status:** DONE

## Summary

The Settings dialog grew the two things it was missing: a way to **reach** the app's files, and a way
to know **what build you are running**.

- A **Storage** card names `<config>/Enigma.Backup` — the folder holding both `settings.json` and
  `profiles/` — and puts an **Open folder** button beside it. The button makes the folder first if
  nothing has been written there yet, because "show me where my files go" is a reasonable question on
  a fresh install and an error dialog is an absurd answer to it. This absorbs the old `StorageNote`
  `SelectableTextBlock`, which named the file and left copying the path to the user.
- An **About** section below the cards: the app icon, the name, the version, the copyright line, and a
  link to the project page.

Opening either one needs the platform, and the only API for that hangs off the window — so it enters
through **`IShellLauncher`** (`OpenFolderAsync` / `OpenUriAsync`) in `Desktop/Services/`, implemented
over `TopLevel.Launcher` and handed the window once in `App`, exactly like the picker services'
storage provider. The interface names no Avalonia type, which is what lets the headless suite fake it
and keeps `SettingsDialogViewModel` free of Avalonia as before.

Version and copyright come from **`AppInfo`**, which reads the running assembly's own
`AssemblyInformationalVersionAttribute` (stripped at the `+` the SDK appends) and
`AssemblyCopyrightAttribute` — both fed by `Directory.Build.props`. Nothing is hardcoded, so the
PHASE05 version bump reaches the About section on its own.

## Files/modules touched

### Created

- `src/Enigma.Backup.Desktop/Services/IShellLauncher.cs` — the seam: `OpenFolderAsync(string)`,
  `OpenUriAsync(Uri)`. Neither reports success, and the reasoning is in the doc comment: there is
  nothing useful to say when a desktop has no file manager, and an exception must never travel out of
  a button.
- `src/Enigma.Backup.Desktop/Services/ShellLauncher.cs` — the implementation over `TopLevel.Launcher`
  (`LaunchDirectoryInfoAsync` / `LaunchUriAsync`). `SetTopLevel` is on the concrete class, not the
  interface; a `false` return or a throw ends in a log line.
- `src/Enigma.Backup.Desktop/AppInfo.cs` — `Name`, `Version`, `Copyright`, `ProjectPage`, plus the
  public pure `ShortVersion(string?)` the version property is built on.
- `src/Enigma.Backup.Desktop/Assets/enigma-backup.png` — the 256 px frame of the existing `.ico`,
  still placeholder art (see *Deviations*).
- `tests/Enigma.Backup.Desktop.Tests/AppInfoTests.cs` — 8 tests.
- `tests/Enigma.Backup.Desktop.Tests/TestSupport/FakeShellLauncher.cs` — records folders and URIs.

### Modified

- `src/Enigma.Backup.Desktop/ViewModels/SettingsDialogViewModel.cs` — takes `IShellLauncher`; gains
  `StorageFolder`, `StorageDescription` (replacing `StorageNote`), `ApplicationName`, `VersionLabel`,
  `Copyright`, `ProjectPageLabel`, `OpenStorageFolderCommand`, `OpenProjectPageCommand`.
- `src/Enigma.Backup.Desktop/Views/SettingsDialog.axaml` — the Storage card, the About section, and a
  local `Button.link` style (three states, each written against the template's `ContentPresenter` —
  see *Deviations*).
- `src/Enigma.Backup.Desktop/Services/DialogService.cs` — holds `IShellLauncher` and passes it to the
  Settings view model, for the same reason it already holds the store and the theme switcher.
- `src/Enigma.Backup.Desktop/Hosting/ServiceCollectionExtensions.cs` — `ShellLauncher` registered
  under its own type *and* forwarded to `IShellLauncher` through a factory, so both resolve to one
  instance.
- `src/Enigma.Backup.Desktop/App.axaml.cs` — `ShellLauncher.SetTopLevel(window)` beside the picker
  wiring, before the window is shown.
- `tests/Enigma.Backup.Desktop.Tests/ViewModels/SettingsDialogViewModelTests.cs` — the nine existing
  tests take the new dependency; four new ones cover the folder, the two commands and About.
- `tests/Enigma.Backup.Desktop.Tests/Hosting/ServiceCollectionExtensionsTests.cs` — `IShellLauncher`
  joins the UI-seams assertion, plus a test that the concrete and interface registrations are the same
  object.
- `tests/Enigma.Backup.Desktop.Tests/Integration/WindowEndToEndTests.cs` — the fixture's fake roster
  gains the launcher.
- `tests/Enigma.Backup.Desktop.Tests/TestSupport/FakeAppSettingsStore.cs` — `FilePath` became
  `{ get; init; }` so a test can point it at a temporary folder.
- `docs/roadmap.md`, `docs/plan/FEATURE-7F99.md` — PHASE04 status.
- `CLAUDE.md`, `README.md` — the documentation freshness sweep (accepted). `CLAUDE.md`'s "view models
  never name Avalonia" invariant enumerated six seams and now names `IShellLauncher` as the seventh,
  with the one thing about it that differs (registered under its concrete type too, so `App` can hand
  it the `TopLevel`). `README.md` said in three places that Settings is where "the theme lives" and
  that the preferences are "currently just the theme"; all three now name the storage folder and the
  About section. The 1.1.0 *What's new* callout was left alone — PHASE05 step 3 replaces it wholesale.

**Untouched:** `src/Enigma.Backup.Core/` in its entirety,
`tests/Enigma.Backup.Core.Tests/Fixtures/` (verified by `git status`), `Directory.Build.props`,
`Directory.Packages.props`, `global.json`, `.editorconfig`, `publish.sh`, `publish.ps1`,
`MainWindow.axaml`.

## Deviations & follow-ups

- **The `.ico`'s frames are PNG-encoded, so the asset is an extraction, not a conversion.** The plan's
  findings said all four frames were BMP and that "no frame can be *extracted* as one" — in fact every
  frame carries a PNG signature. `enigma-backup.png` is therefore the 256 px frame's **1 513 bytes
  copied verbatim**, with no decode/re-encode step and no re-encode loss. Strictly better than what
  was planned; the art is still the placeholder CLAUDE.md describes.
- **About shows "Version 1.1.0", not "Version 1.2.0".** PHASE04's acceptance names 1.2.0, but
  `<Version>` in `Directory.Build.props` is bumped by **PHASE05 step 1**, which has not run. `AppInfo`
  reads the attribute rather than a literal, so the section will read 1.2.0 the moment that phase
  lands, with no edit here. What was verified is the mechanism: the `+<git sha>` suffix is stripped,
  and the screenshots show `Version 1.1.0` — the correct answer for this build.
- **`StorageNote` was renamed to `StorageDescription`, not just re-homed.** The plan has the card's
  description absorb the note; the note named the *file*, the description names the *folder* (which is
  what the button opens and what holds `profiles/` too). Keeping the old name would have left a
  property called "note" that is a card description about something else. The existing test that
  asserted the file path appears was updated to assert the folder path and the `settings.json`
  sentence.
- **`ShellLauncher` is registered twice — concrete and interface — resolving to one instance.** The
  plan says `App` hands it the `TopLevel`, and `IShellLauncher` names no Avalonia type; those two
  cannot both hold unless `App` can reach the concrete class. A dedicated test pins that the object
  `App` wires is the object the dialogs call, because a second instance would throw on every click.
- **`ShellLauncher.SetTopLevel` throws when skipped, but `OpenFolderAsync` swallows platform
  failures.** The first is a programming error (`App` always wires it before the window shows) and
  matches how the library's services behave; the second is the user's desktop declining, which is not
  something this app can fix or usefully report from inside a preferences dialog. Both end up in the
  log.
- **The `Button.link` style is written against the template's `ContentPresenter`, not the `Button`.**
  Setting `Foreground`/`Background` on the button alone works until the pointer enters it — FluentTheme's
  own `:pointerover /template/ ContentPresenter` setters repaint over it. All three states
  (normal/hover/pressed) are therefore template-targeted.
- **The manual checks needed two temporary source edits, both reverted.** This machine still has no
  `xdotool` / `wmctrl` / `kdotool` / `ydotool`, so neither the gear nor the two new buttons can be
  clicked from a script. An env-var-guarded hook in `MainWindow.OnOpened` opened the Settings dialog
  for the screenshots, and a second in `SettingsDialog.OnAttachedToVisualTree` executed the two
  commands for the launcher check. Both were removed and
  `git diff src/…/MainWindow.axaml.cs src/…/SettingsDialog.axaml.cs` is **empty**; the builds and the
  test run recorded below were re-run afterwards, against exactly the source being committed. The
  commands were driven through the real production path (view model → `ShellLauncher` →
  `TopLevel.Launcher`), which is what a click does — only the press itself was synthetic.
- **Line endings:** this phase's diff contains no CRLF↔LF churn. No action taken — per the workflow
  this is recommendation-only.
- **For PHASE05:** `README.md` and `docs/QA-CHECKLIST.md` both need the Settings dialog's new Storage
  and About sections, alongside the top-bar Restore/Verify wording PHASE03 already deferred there.
  Both are already PHASE05 steps.

## Build/test evidence

- **Build (Debug):** `dotnet build Enigma.Backup.slnx --no-incremental` → **0 warnings, 0 errors**.
- **Build (Release):** `dotnet build Enigma.Backup.slnx -c Release --no-incremental` → **0 warnings,
  0 errors**. `grep -c AVLN` over both full logs → **0** and **0**. (`TreatWarningsAsErrors` does not
  promote `AVLN*`, so every `.axaml`-touching phase reads the count explicitly.)
- **Tests:** `dotnet test --solution Enigma.Backup.slnx` → **489 passed, 0 failed, 0 skipped**
  (PHASE03 ended at 472; **+17**: 8 `AppInfoTests`, 4 new `SettingsDialogViewModelTests`, 1 new
  `ServiceCollectionExtensionsTests`, and 4 from the parameterised `ShortVersion` theories).
  `git status --porcelain tests/Enigma.Backup.Core.Tests/Fixtures/` → empty.
- **Verified running** on Linux (Wayland + XWayland, `DISPLAY=:1`), Debug build, screenshots with
  `spectacle`:
  - **Dark and Light**, Settings open: three sections in order — Theme card, Storage card with the
    **Open folder** button, then a `EnigmaBorderSubtleBrush` rule and the About block (48 px icon,
    **Enigma.Backup**, `Version 1.1.0`, `Copyright © 2026 Josué Clément`, and the accent-blue
    underlined `github.com/enigmalibs/Enigma.Backup` link). The link colour follows the variant, as a
    `DynamicResource` must.
  - **The grown dialog fits the shared 680×560 host cap comfortably** — it measures roughly
    676 × 430 including the title and the Close button, so About is ~130 px clear of the ceiling in
    both variants. Nothing scrolls, nothing is clipped.
  - **Open folder** launched **Dolphin** on `Home ▸ .config ▸ Enigma.Backup`, showing `logs/` and
    `settings.json` (screenshot); no "declined"/"could not open" warning was logged.
  - **Open folder on a fresh config**: `~/.config/Enigma.Backup` was moved aside, the folder was
    confirmed absent, and the button **created it** (empty) and opened it. The real folder was moved
    back afterwards and its `settings.json` restored byte-for-byte to `{"formatVersion": 1, "theme":
    "System"}` — the machine holds exactly what it did before this phase.
  - **The project link** reached the browser: no launcher warning, and Firefox's `places.sqlite`
    records a visit to `https://github.com/enigmalibs/Enigma.Backup` at **23:51:14**, the second the
    command ran. (Firefox was already open, so this added a tab rather than starting anything.)

## Acceptance criteria

| Criterion | Met |
|---|---|
| `IShellLauncher`/`ShellLauncher` in `Desktop/Services/`, registered in `AddEnigmaBackupDesktop`, `TopLevel` handed over in `App.OnFrameworkInitializationCompleted`, fake for the test harness | ✅ |
| `AppInfo` (informational version stripped at `+`, copyright) + `Assets/enigma-backup.png` from the ico's 256 px frame | ✅ — extraction rather than re-encode, see *Deviations* |
| `SettingsDialogViewModel`: `OpenStorageFolderCommand` (create-if-missing, then launch), `OpenProjectPageCommand`, version/copyright properties | ✅ |
| `SettingsDialog.axaml`: Storage card absorbing the storage note, About section (icon, name, version, copyright, link-styled button) | ✅ |
| Tests: `AppInfo` strips the `+sha` and never throws on a missing attribute; both commands hit the fake launcher with the right path/URI; the storage-folder command creates the folder when absent; DI registration covers `IShellLauncher` | ✅ — 17 new tests |
| The e2e Desktop test keeps passing, its fake roster gaining the launcher | ✅ |
| Builds clean, zero `AVLN*` | ✅ — Debug and Release, 0 warnings, `grep -c AVLN` → 0 |
| Suite green | ✅ — 489 passed |
| Manually: Open folder opens the real `~/.config/Enigma.Backup` in the file manager (and creates it on a fresh config) | ✅ — Dolphin, both cases, screenshot |
| Manually: the project link opens the browser | ✅ — Firefox history records the visit at 23:51:14 |
| Manually: About shows the version, the icon renders | ✅ — shows `Version 1.1.0`; 1.2.0 arrives with PHASE05's bump, see *Deviations* |
| Manually: the grown dialog fits the 680×560 host cap in both variants (screenshots) | ✅ — ~676 × 430, ~130 px of headroom |
