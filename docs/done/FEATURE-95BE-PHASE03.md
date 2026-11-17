# FEATURE-95BE-PHASE03 — Settings page

## Summary

Settings is now a page at the foot of the rail, laid out like Enigma.Launchpad's. It has a header, then
three sections of cards in a column capped at 900 px:

- **Appearance**: the theme drop-down. A choice applies and is saved at once, as before.
- **Storage**: the profiles-and-preferences folder, with **Open folder**.
- **About**: "Enigma Backup", `Version x`, and an **About Enigma Backup** button. It opens the same About
  dialog as before, and this is now the only way to reach it.

What was removed:

- the Settings dialog: `SettingsDialog(.axaml)`, `SettingsDialogViewModel`,
  `IDialogService.ShowSettingsAsync` and the `DialogService` code behind it;
- the gear and ⓘ buttons in the Backup page's profile bar, with their commands.

`DialogService` no longer needs `IAppSettingsStore`, `IThemeSwitcher` or `IShellLauncher`.

## Files/modules touched

- Renamed and reworked:
  - `ViewModels/SettingsDialogViewModel.cs` → `ViewModels/SettingsPageViewModel.cs`
  - `Views/SettingsDialog.axaml(.cs)` → `Views/Pages/SettingsPage.axaml(.cs)`
  - tests `SettingsDialogViewModelTests` → `SettingsPageViewModelTests`
- Modified:
  - `src/Enigma.Backup.Desktop/Hosting/ServiceCollectionExtensions.cs`
  - `src/Enigma.Backup.Desktop/Services/DialogService.cs`
  - `src/Enigma.Backup.Desktop/Services/IDialogService.cs`
  - `src/Enigma.Backup.Desktop/Services/IShellLauncher.cs` (doc comment only)
  - `src/Enigma.Backup.Desktop/ViewModels/BackupPageViewModel.cs`
  - `src/Enigma.Backup.Desktop/ViewModels/MainWindowViewModel.cs`
  - `src/Enigma.Backup.Desktop/Views/MainWindow.axaml(.cs)`
  - `src/Enigma.Backup.Desktop/Views/Pages/BackupPage.axaml`
  - tests `ServiceCollectionExtensionsTests`, `FakeDialogService`, `Harness`, `AboutDialogViewModelTests`,
    `MainWindowViewModelTests`
  - `README.md`, `docs/QA-CHECKLIST.md`, `docs/roadmap.md`, `docs/plan/FEATURE-95BE.md`

## Deviations & follow-ups

- **Two About tests were removed, not moved.** `TheButtonBesideTheGearOpensTheDialog` and
  `TheButtonIsUnavailableWhileARunIsInProgress` tested a button that no longer exists.
  - The first is replaced by `SettingsPageViewModelTests.AboutOpensFromThisPage`.
  - The second has no counterpart: during a run, the run dialog's scrim covers the whole window, the
    Settings page included, so About cannot be reached.
- **The settings file is read once, when the page is built**, not each time it is shown (the dialog read
  it on every open). The page is the only thing that writes that file while the app runs. A hand edit
  made while the app is open shows at the next start, which is also when it would take effect.
- Follow-up (existing behaviour): the About dialog still names the app `Enigma.Backup` (`AppInfo.Name`),
  while the title, the Settings card and the button say "Enigma Backup". Making the two agree is a
  product-naming decision outside this item.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Storage card header | "Profiles and preferences" | It names what is in the folder. The old dialog's "Storage" is now the section heading |
| About card | Header "Enigma Backup", description "Released under the MIT license.", `Version x` and an "About Enigma Backup" button | Launchpad's card, with this app's name as the window title spells it; `LICENSE.md` is MIT |
| `OpenAboutCommand` availability | Always executable | Nothing on this page can be reached during a run; the run dialog covers it |
| Settings rail icon | `Gear` | TextEditor and Launchpad use it |

## Build/test evidence

- Run baseline: see `FEATURE-95BE-PHASE01.md`.
- Debug and Release builds: **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Backup.slnx`: **566 total, 554 passed, 12 skipped, 0 failed**.
  - Removed: the settings dialog's close test, the gear test, and the two About-button tests.
  - Added: `AboutOpensFromThisPage`, `TheAboutCardShowsTheRunningVersion`, and
    `MainWindowViewModelTests.TheSettingsPage_IsOneOfThePages`.
  - The DI tests cover the Settings page as a singleton.
- In the running app (Debug build on Windows, via `PrintWindow` and UI Automation):
  - The rail's foot shows the gear, highlighted when selected.
  - The Settings page shows its three sections, with the current theme and the real config folder.
  - **About Enigma Backup** opens the About dialog, and **Close** dismisses it.
  - Back on Backup, UI Automation finds no "Settings" or "About" button left there.
  - The theme switch was **not** exercised live, because it would rewrite the user's real
    `settings.json`. It is unchanged code under the existing tests.

## Documentation sweep

- `README.md`: the theme feature bullet and "Using it" item 1 point to the Settings page at the foot of
  the rail, and to About on it.
- `docs/QA-CHECKLIST.md`:
  - the preamble mentions the Settings page, and the 1.3.0 About note now says where About opens;
  - row 3.11's profile bar no longer has ⚙ or ⓘ, and row 7.11's example is About;
  - §8 rows 8.1, 8.2, 8.4, 8.13 and 8.15 describe the page.
  - The over-long preamble line left by PHASE02 is rewrapped.
- `src/.../IShellLauncher.cs` and a DI test remark: "Settings dialog" → "Settings page".
