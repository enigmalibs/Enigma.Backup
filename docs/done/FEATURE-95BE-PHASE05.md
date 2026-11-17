# FEATURE-95BE-PHASE05 — Backup with a profile

## Summary

The Backup page now backs up one of two things, chosen by two radio buttons at the top of **Input**:

- **Files and folders**: the one-off backup from PHASE04, edited on the page.
- **A profile**:
  - a drop-down of the saved profiles (the shared `ProfileCatalog`);
  - a summary of what the chosen one backs up, as last saved: a lead line, each path, each pattern as
    "`pattern` in `folder`", and "Leaves out …";
  - **Output** shows that profile's folder and options, greyed out, under "Set by the profile — change it
    on the Profiles page."

**Back up** writes `<Profile>_…` archives from the profile exactly as saved. An unsaved edit on the
Profiles page is not included.

**Which mode the page opens in.** The first store load picks **A profile** when there is at least one
profile, and selects the first one; otherwise it picks **Files and folders**. If the user has already
chosen a mode, the load leaves it alone.

**Following the store.** A reload re-shows the chosen profile as it was just saved. If that profile is
gone, the selection is dropped and the read-only editor is emptied.

**The line beside the disabled button** covers profile mode too:
- there is no profile yet;
- no profile is chosen;
- the profile backs up nothing;
- the profile has no output folder.

**Drops** land on the Backup page only for a one-off; with a profile chosen they are refused.

## Files/modules touched

- Created: `tests/Enigma.Backup.Desktop.Tests/ViewModels/BackupPageViewModelProfileModeTests.cs`
- Modified:
  - `src/Enigma.Backup.Desktop/ViewModels/BackupPageViewModel.cs`
  - `src/Enigma.Backup.Desktop/ViewModels/MainWindowViewModel.cs`
  - `src/Enigma.Backup.Desktop/Views/Pages/BackupPage.axaml`
  - tests `ServiceCollectionExtensionsTests`, `WindowEndToEndTests`, `FakeProfileStore`, `Harness`,
    `MainWindowViewModelTests`
  - `README.md`, `docs/QA-CHECKLIST.md`, `docs/roadmap.md`, `docs/plan/FEATURE-95BE.md`

## Deviations & follow-ups

- **A profile also needs at least one input before Back up enables.** The plan asked only for a selected
  profile with an output folder. A profile with nothing to back up gets the same treatment as an empty
  one-off: the button stays disabled and the line beside it says why.
- **Choosing "A profile" with nothing selected selects the first profile**, as the Profiles page does
  when it opens. The plan described this only for the first load.
- **Test-fake fix:** `FakeProfileStore.Clone` dropped `PatternInputs`, so a seeded profile with patterns
  came back without them. The summary test exposed it, and the fake now copies them. Production code was
  never affected; the real store was always used end to end.
- **QA rows 3.12 and 3.13 were added** for profile mode. A new mode with no rows would leave the checklist
  claiming coverage it does not have. They are added to the existing §3 table, not a new section.
- **Not pressed in the running app:** **Back up** in profile mode. It would have written into the user's
  own `C:\Temp\Backup` and asked for that profile's password. The flow runs in `WindowEndToEndTests`
  against the real pipeline (`Documents_…` archive, verified and restored byte for byte).

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Radio buttons ↔ view model | `UseProfile` and its mirror `IsSingleMode`; the mirror's setter acts only on `true` | A radio button being unchecked says nothing on its own; its sibling being checked does |
| Default mode without overriding the user | The first `Reloaded` calls `DefaultModeTo(HasProfiles)`, which sets the mode and leaves `_modeChosen` false; any setter call marks the mode as chosen | A choice the user made is kept |
| The editor shown in profile mode | A second, read-only `ProfileEditorViewModel` (`FromProfile`), loaded from the stored profile. Nothing chosen resets it | Output can show the profile's folder and options with the same view; a stale folder would be a lie |
| What a profile backup runs | `FromProfile.ToProfile(name)` (plan step 1) | `FromProfile` is the stored profile, loaded as is |
| Bindings that change the `DataContext` | A wrapper `Panel` carries `IsVisible` / `IsEnabled` | A binding on the same element would resolve against the editor it is given |
| Guarding one editor for both jobs | The constructor refuses the same instance twice (`ArgumentException`) | Both would show the one-off and the profile at once |

## Build/test evidence

- Run baseline: see `FEATURE-95BE-PHASE01.md`.
- Debug and Release builds: **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Backup.slnx`: **590 total, 578 passed, 12 skipped, 0 failed**.
- Tests added:
  - `BackupPageViewModelProfileModeTests` (14): mode default with and without profiles, a chosen mode
    kept, summary contents, an empty profile, no profile, nothing chosen, no output folder, the stored
    profile handed over under its name, the password asked for in its name, switching back, a reload
    after a save, a reload after a delete, and one editor refused for both jobs.
  - `MainWindowViewModelTests.ADrop_OnTheBackupPage_IsRefusedWhileItBacksUpAProfile`.
  - DI: the page's two editors are distinct.
- `WindowEndToEndTests.AProfileIsSaved_BackedUp_Verified_AndRestoredByteForByte`: after the profile is
  saved on the Profiles page, the Backup page is on **A profile** with "Documents" chosen. The archive is
  `Documents_…` and round-trips byte for byte, in all four compression/encryption cases.
- In the running app (Debug build on Windows, via `PrintWindow` and UI Automation, against the user's
  two real profiles):
  - The page opened on **A profile** with "Notes with pass" chosen, its one-item summary, a greyed-out
    Output (`C:\Temp\Backup`, Gzip, encryption on) under the profile note, and **Back up** enabled.
  - Selecting **Files and folders** showed the one-off editor, an empty editable Output, and the
    disabled **Back up** with its hint.

## Documentation sweep

- `README.md` "Using it": the mode choice at the top of Input, the profile summary, the read-only
  Output, the default mode, and Profiles "to run again from the Backup page".
- `docs/QA-CHECKLIST.md`: row 3.2 (drops refused in profile mode), new rows 3.12 and 3.13.
- `CLAUDE.md`: nothing this phase made wrong.
