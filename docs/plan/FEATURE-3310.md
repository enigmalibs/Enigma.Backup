# FEATURE-3310 — About dialog shows Enigma Backup

**Status:** DONE
**Type:** FEATURE
**Branch:** `feature/feature-3310-app-display-name`
**Run:** feature/2026-11-16-centered-pages-release

## Objective

The About dialog names the app **Enigma Backup**, as the main window's title, the Settings page's
About card and the MSI already do, instead of `Enigma.Backup`.

## Context & constraints

- `AppInfo.Name` (`src/Enigma.Backup.Desktop/AppInfo.cs`) is the constant `"Enigma.Backup"`. Its
  doc comment says it is "what the app is called, on screen and in the window title". Its only reader
  is `AboutDialogViewModel.DisplayName`, which `AboutDialog.axaml` binds as the product name.
- The main window's title has been `Enigma Backup` since FEATURE-95BE. So `AppInfoTests` has a test
  named `TheNameIsTheOneTheWindowUses`, and it asserts the dotted name the window no longer uses.
- Two other on-screen strings still name the app `Enigma.Backup`:
  - the title of `StartupErrorWindow.axaml`, the window shown when the start fails;
  - the archive filter label in `Services/FilePicker.cs` (`"Enigma.Backup archives"`).
- **The dotted name is also an identifier, and those uses must not change:**
  - the config folder `<config>/Enigma.Backup/` (`ProfileStore`, `AppSettingsStore`). Renaming it would
    orphan every saved profile and preference;
  - the project, assembly and executable names, the zip and MSI file names, and the docs' prose.

## Steps

1. `AppInfo.Name` → `"Enigma Backup"`. Update its doc comment if it no longer reads true.
2. `StartupErrorWindow.axaml`: `Title="Enigma Backup"`.
3. `FilePicker.cs`: the archive filter label becomes `"Enigma Backup archives"`.
4. Tests:
   - `AppInfoTests.TheNameIsTheOneTheWindowUses` asserts `"Enigma Backup"`;
   - a new `AboutDialogViewModelTests` fact asserts that `DisplayName` is `"Enigma Backup"`.
5. `docs/QA-CHECKLIST.md`: 8.13 expects `Enigma Backup` in About; 7.12 expects an `Enigma Backup`
   window. The history records of earlier releases stay as they are.
6. Build in Debug and Release, run the whole suite, open About in the running app, and take a screenshot.

## Acceptance criteria

- The About dialog shows `Enigma Backup` (screenshot).
- No user-visible string in `src/` names the app `Enigma.Backup`. A `grep` finds the dotted name only in
  namespaces, paths, the config folder, and file and project names.
- The config folder is still `Enigma.Backup`: `ProfileStore` and `AppSettingsStore` are unchanged.
- `AppInfoTests` and `AboutDialogViewModelTests` pin the new name.
- Debug and Release builds have zero warnings. The whole suite passes against the run's baseline.

## Out of scope

- Renaming the config folder, the assemblies, the executable, the zips or the MSI file.
- Rewriting `Enigma.Backup` in docs prose (README, release notes): there it names the project.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Where to change the name | `AppInfo.Name` | It is the About dialog's source, and the constant whose doc comment and test already say it is "the one the window uses" | A literal in `AboutDialog.axaml` (two sources of the name) |
| Other on-screen `Enigma.Backup` strings | Change both: the startup-error window title and the archive picker label | The request is about the name the app shows. Left as they are, these would keep the inconsistency in plain view in the two places the app still names itself. Both changes are one line and safe | Changing About only, and noting the other two as follow-ups |
| Identifiers (config folder, exe, zip, MSI file) | Unchanged | Renaming the config folder loses users' profiles. The file names are build and packaging contracts, not display text | Renaming everything |
| Tests | Update the existing name test, and add one on `AboutDialogViewModel.DisplayName` | The view model is what the dialog binds, and the house tests are view-model tests | Only the `AppInfo` test |
