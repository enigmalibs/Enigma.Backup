# FEATURE-3310 — About dialog shows Enigma Backup

**Branch:** `feature/feature-3310-app-display-name`
**Run:** feature/2026-11-16-centered-pages-release

## Summary

The About dialog now names the app **Enigma Backup**, as the main window's title, the Settings page's
About card and the MSI already did.

- `AppInfo.Name` is now `"Enigma Backup"`. It is what `AboutDialogViewModel.DisplayName` returns, so it
  is the About dialog's product line. Its doc comment now says why the dotted name lives on elsewhere:
  it is the project's name and the config folder's.
- Two other strings the Desktop shows as the app's name changed as well:
  - the title of the startup-failure window (`StartupErrorWindow.axaml`);
  - the archive filter in the file picker (`Enigma Backup archives`).
- These are untouched, on purpose:
  - the config folder `<config>/Enigma.Backup/` (`ProfileStore`, `AppSettingsStore`), so that every
    saved profile and preference is still found;
  - the project, assembly, executable, zip and MSI file names.
- Tests: `AppInfoTests.TheNameIsTheOneTheWindowUses` and `AboutDialogViewModelTests.ItSaysWhatThisBuildIs`
  now expect `Enigma Backup`.
- `docs/QA-CHECKLIST.md`: 8.13 expects `Enigma Backup` in About, and 7.12 an `Enigma Backup` window.

## Files/modules touched

- Modified:
  - `src/Enigma.Backup.Desktop/AppInfo.cs`
  - `src/Enigma.Backup.Desktop/Views/StartupErrorWindow.axaml`
  - `src/Enigma.Backup.Desktop/Services/FilePicker.cs`
  - `tests/Enigma.Backup.Desktop.Tests/AppInfoTests.cs`
  - `tests/Enigma.Backup.Desktop.Tests/ViewModels/AboutDialogViewModelTests.cs`
  - `docs/QA-CHECKLIST.md`, `docs/roadmap.md`, `docs/plan/FEATURE-3310.md`
- Created: `docs/done/FEATURE-3310.md`

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| A new `DisplayName` test, as the plan said | Update the existing fact instead | `ItSaysWhatThisBuildIs` already asserted `DisplayName` (as `Enigma.Backup`). A second fact asserting the same value would duplicate it |
| Core's error messages that say `Enigma.Backup` (`ArchiveFormatDetector`: "not an Enigma.Backup archive"; `Ebk1Header`: "this version of Enigma.Backup …") | Left as they are | They are Core prose that names the project, as the README and release notes do. The Core is the CLI-ready layer, and two Core tests pin the wording. The request was about the About dialog; renaming the product in error text is a separate call, listed below |
| Proving the About dialog | UI Automation on the Debug build: Settings → *About Enigma Backup* invoked by pattern, then every text in the window listed | The workstation was locked during this dev, so a screen capture returned the lock screen (discarded). UIA still reads the app's own tree |

## Deviations & follow-ups

- **Acceptance criterion narrowed.** The plan said "no user-visible string in `src/` names the app
  `Enigma.Backup`". That is true of the Desktop. It is not true of the three Core error messages above,
  which a run dialog can show (an unreadable archive, an encrypted archive from a newer version).
  **Follow-up for the user:** say if those should read `Enigma Backup` too. It is a three-line change in
  Core plus the two test assertions (`Ebk1ContainerTests`, `RestoreServiceTests`).
- **No screenshot of About.** Taking one was impossible while the session was locked. The UIA reading
  below stands in for it, and QA 8.13 covers the visual check.

## Build/test evidence

- Baseline: see `docs/done/FEATURE-464A.md` (0 warnings; 590 tests, 578 passed, 12 skipped, 0 failed).
- `dotnet build Enigma.Backup.slnx` and `-c Release`: **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Backup.slnx`: **590 total, 578 passed, 12 skipped, 0 failed**.
- UIA on the running Debug build:
  - the window title is `Enigma Backup`;
  - with About open, the texts are `Enigma Backup`, `Version`, `1.5.0`, `Build`, `d2d4cf7`, and
    `Copyright © 2026 Josué Clément`;
  - no `Enigma.Backup` text is left anywhere in the window.
- `grep` of `src/` for `Enigma.Backup` finds, besides the three Core messages above, only:
  - namespaces and XAML class names;
  - doc comments and the config-folder paths.
- Fix budget: 0 cycles used.

## Documentation sweep

- `docs/QA-CHECKLIST.md` 7.12 and 8.13 updated (in this commit). The *What was verified for 1.x* records
  keep the name those releases showed.
- `README.md` names `Enigma.Backup` only in file names and the config path, which did not change.
  `CLAUDE.md` does not name the display string.
