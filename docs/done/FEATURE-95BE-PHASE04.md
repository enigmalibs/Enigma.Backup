# FEATURE-95BE-PHASE04 — Profiles page and single backup

## Summary

**The Profiles page** (`ProfilesPageViewModel`, rail icon `FileText`) now holds profile management:
- the profile drop-down on a card whose description is its status ("Saved." / "Unsaved changes.");
- all seven buttons: New, Save, Save as…, Rename…, Delete…, Import…, Export…;
- the profile's **Input** and **Output** sections.

**The Backup page** no longer manages profiles. It runs a one-off backup from its own editor
(`BackupPageViewModel.Single`, archives named `Backup_…`):
- two `SettingsCardExpander`s, **Input** ("Files, folders and patterns to back up" and the exclude
  patterns) and **Output** (output folder, compression, encryption);
- an action bar at its foot that keeps **Back up** in view. While the button is disabled, the bar says what
  is missing.

**Shared pieces**
- **`InputsEditor`** and **`OutputEditor`** are views over `ProfileEditorViewModel`, used by both pages.
  The editor now owns **Add pattern…** (it takes `IDialogService`).
- **`ProfileCatalog`** is a singleton list of saved profiles. The Profiles page reloads it after every
  write, and any page that lists profiles reads it. Its `Names` collection is updated in place, so a
  drop-down whose profile is still there keeps its selection.

**Drops** go to the Backup or the Profiles page, whichever is shown, and are refused elsewhere. The
window's first store read now loads the catalog through the Profiles page.

## Files/modules touched

- Created:
  - `src/Enigma.Backup.Desktop/ViewModels/ProfileCatalog.cs`
  - `src/Enigma.Backup.Desktop/ViewModels/BackupPageViewModel.cs` (new, one-off only)
  - `src/Enigma.Backup.Desktop/Views/InputsEditor.axaml(.cs)`
  - `src/Enigma.Backup.Desktop/Views/OutputEditor.axaml(.cs)`
  - `src/Enigma.Backup.Desktop/Views/Pages/ProfilesPage.axaml(.cs)`
  - `tests/Enigma.Backup.Desktop.Tests/ViewModels/ProfileCatalogTests.cs`
- Renamed:
  - `ViewModels/BackupPageViewModel.cs` → `ViewModels/ProfilesPageViewModel.cs` (the profile logic moved
    with its history)
  - tests `BackupPageViewModelProfileTests` → `ProfilesPageViewModelTests`
- Modified:
  - `src/Enigma.Backup.Desktop/Hosting/ServiceCollectionExtensions.cs`
  - `src/Enigma.Backup.Desktop/ViewModels/MainWindowViewModel.cs`
  - `src/Enigma.Backup.Desktop/ViewModels/ProfileEditorViewModel.cs`
  - `src/Enigma.Backup.Desktop/Views/MainWindow.axaml(.cs)`
  - `src/Enigma.Backup.Desktop/Views/Pages/BackupPage.axaml`
  - tests `ServiceCollectionExtensionsTests`, `WindowEndToEndTests`, `FakeProfileStore`, `Harness`,
    `BackupPageViewModelRunTests`, `MainWindowViewModelTests`, `ProfileEditorViewModelTests`
  - `README.md`, `docs/QA-CHECKLIST.md`, `docs/roadmap.md`, `docs/plan/FEATURE-95BE.md`

## Deviations & follow-ups

- **The "Add pattern" status lines are gone.** "Added the pattern…" and "Nothing was added — that pattern
  is already…" were `StatusText` messages, which nothing displays. Moving the command onto the editor left
  them no owner, so they were dropped. The two tests now check the inputs (added once) and the number of
  prompts instead.
- **Profile commands no longer check the busy state.** During a run, the run dialog's scrim covers every
  page, so they cannot be pressed. The one-off **Back up** and drops still check it.
- **The window's minimum size** dropped from 960×780 to 820×600, because every page now scrolls. That
  change was planned for this phase.
- **The two archive-option cards are now stacked** inside **Output**. They used to sit side by side; with
  room to scroll, stacking reads like the Settings page.
- **The unused `Border.panel` style was removed** from the window.
- **QA rows fixed late:** 1.4 (title `Enigma Backup`) and 2.1/2.2 (the `*` in the title bar) were made
  wrong by PHASE02's fixed title, but that phase's sweep missed them. They are corrected here.
- **Not exercised in the running app:** filling the one-off backup through the native folder picker. A
  typed path plus OK makes the picker open that folder instead of returning it. The app was stopped,
  which closed the picker, and nothing was written. The one-off backup runs end to end in
  `WindowEndToEndTests` on the real pipeline.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| How the shared name list changes on reload | In place: remove the names that went, insert or move the rest into order. A case-only rename counts as a new name | Rebuilding would make every bound drop-down lose its selection |
| A selected profile that leaves the catalog | `ProfilesPage` clears its selection under its `_reloading` guard and keeps the editor | The old behaviour, now also correct when another page triggered the reload |
| Where Back up lives | An action bar docked at the page's foot, outside the scroll, aligned with the 900-px column | Always visible, however far Input is scrolled |
| What the disabled button says | `MissingForBackup`: one of three sentences, or `null` | A greyed-out button that says nothing reads as a bug; this was the "empty state" item in the beyond-the-draft check |
| Inputs list height | `MinHeight 110` / `MaxHeight 260`, scrolling inside | The page scrolls, so the list cannot take a `*` row any more |
| Drop message on Backup | "…already in the list." | There is no profile on that page |
| Names in the e2e fixture | `BackupEditor` / `ProfileEditor` (and `harness.FillProfileEditor()`) | Two editors now exist, and each test says which one it fills |

## Build/test evidence

- Run baseline: see `FEATURE-95BE-PHASE01.md`.
- Debug and Release builds: **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Backup.slnx`: **575 total, 563 passed, 12 skipped, 0 failed**.
- Tests added:
  - `ProfileCatalogTests` (5): order and lookup, an unreadable file, an unlistable store, in-place
    changes, a case-only rename.
  - `ProfilesPageViewModelTests.AProfileThatLeavesTheStore_IsDeselected_AndTheEditorIsKept`.
  - `BackupPageViewModelRunTests.Backup_SaysWhatItStillNeeds` and
    `DroppedPaths_LandInTheOneOffInputsAndAreCounted`.
  - `MainWindowViewModelTests.ADrop_OnTheProfilesPage_AddsToTheProfile`.
  - The DI tests cover the Profiles page and the catalog as singletons, and the two editors being
    distinct.
- `WindowEndToEndTests`:
  - The profile is saved on the Profiles page.
  - The backup runs as a one-off (`Backup_…` archive) and is verified and restored byte for byte.
  - Save, reopen, export and import go through the Profiles page.
  - The pattern test saves the profile, then backs up the same rules as a one-off.
- In the running app (Debug build on Windows, via `PrintWindow` and UI Automation):
  - The Backup page shows its header, the **Input** and **Output** sections, and the foot bar with
    "Add something to back up, and choose where to save the archive." beside a disabled **Back up**.
  - The Profiles page shows the profile card ("Notes with pass", "Saved."), the seven buttons, and that
    profile's Input and Output.

## Documentation sweep

- `README.md` "Using it": the Backup page's two sections, the always-visible **Back up** and its hint,
  Profiles as a page of its own, and Settings as item 9.
- `docs/QA-CHECKLIST.md`: rows 1.4, 2.1, 2.2, 2.8, 3.1, 3.2, 3.5, 3.7, 3.8, 3.10 and 3.11.
- `CLAUDE.md`: nothing this phase made wrong.
