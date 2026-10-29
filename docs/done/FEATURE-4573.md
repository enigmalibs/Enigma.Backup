# FEATURE-4573 — Uniform About dialog

## Summary

Enigma.Backup now has the About dialog every Enigma app shows, built from Enigma.GitClient's
`AboutView.axaml`. The layout, sizes, type ramp, brushes and dialog chrome are the reference's:

- content 380 px wide;
- the 256-px logo drawn at 96 px;
- the name at 18 SemiBold;
- `Version X.Y.Z` in the secondary brush;
- `Build <7-char sha>` at 11 in the tertiary brush, selectable, and hidden when the build has no revision;
- the copyright at 11;
- no title, no header icon, and one default `Close` button.

It opens from a new ⓘ button beside the settings gear. The button is disabled during a run, like the rest
of the top bar.

The Settings dialog loses its About section and the project-page link. Settings is itself a dialog on the
one host, so About could not open from inside it. The reference shows no link, so keeping one would have
broken the uniformity. With the link gone, `IShellLauncher.OpenUriAsync` and `AppInfo.ProjectPage` had no
caller left, and both were removed. `AppInfo` gains `BuildSha` and the testable `ShortBuildSha`.

## Files/modules touched

- Created:
  - `src/Enigma.Backup.Desktop/ViewModels/AboutDialogViewModel.cs`
  - `src/Enigma.Backup.Desktop/Views/AboutDialog.axaml`
  - `src/Enigma.Backup.Desktop/Views/AboutDialog.axaml.cs`
  - `tests/Enigma.Backup.Desktop.Tests/ViewModels/AboutDialogViewModelTests.cs`
  - `docs/done/FEATURE-4573.md`
- Modified:
  - `src/Enigma.Backup.Desktop/AppInfo.cs`
  - `src/Enigma.Backup.Desktop/Services/IDialogService.cs`
  - `src/Enigma.Backup.Desktop/Services/DialogService.cs`
  - `src/Enigma.Backup.Desktop/Services/IShellLauncher.cs`
  - `src/Enigma.Backup.Desktop/Services/ShellLauncher.cs`
  - `src/Enigma.Backup.Desktop/ViewModels/MainWindowViewModel.cs`
  - `src/Enigma.Backup.Desktop/ViewModels/SettingsDialogViewModel.cs`
  - `src/Enigma.Backup.Desktop/Views/MainWindow.axaml`
  - `src/Enigma.Backup.Desktop/Views/SettingsDialog.axaml`
  - `tests/Enigma.Backup.Desktop.Tests/AppInfoTests.cs`
  - `tests/Enigma.Backup.Desktop.Tests/TestSupport/FakeDialogService.cs`
  - `tests/Enigma.Backup.Desktop.Tests/TestSupport/FakeShellLauncher.cs`
  - `tests/Enigma.Backup.Desktop.Tests/ViewModels/SettingsDialogViewModelTests.cs`
  - `README.md`
  - `docs/QA-CHECKLIST.md`
  - `CLAUDE.md`
  - `docs/roadmap.md`
  - `docs/plan/FEATURE-4573.md`

## Deviations & follow-ups

- None from the plan.
- `docs/QA-CHECKLIST.md`'s *What was verified for 1.2.0* section is history and still mentions the old
  About section. The release item (FEATURE-93D8) replaces it with the 1.3.0 record.
- `SettingsDialogViewModelTests.cs` still carries `using Enigma.Backup.Desktop;`, which is redundant
  inside a nested namespace. It is pre-existing, the build does not flag it, and it was left alone.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Enter key | A `KeyBinding` to `CloseCommand`, as in Settings | The house invariant: every dialog's content takes the focus on attach and binds Enter |
| Gear's automation name | `AutomationProperties.Name="Settings"` added beside the new button's | UI Automation needs a name to find the gear, and it makes the two buttons symmetrical for screen readers |
| Button spacing | `Margin="6,0,0,0"` on ⓘ | The same gap as the `Spacing` between Restore… and Verify… |
| Tests for the window wiring | In `AboutDialogViewModelTests`, mirroring `SettingsDialogViewModelTests`' gear test | Keeps "the button opens the dialog" next to the dialog's own tests |

## Documentation sweep

- README *Using it* §1: Settings holds theme and storage, and ⓘ opens About.
- `docs/QA-CHECKLIST.md`:
  - 3.10 and 3.11 now include ⓘ;
  - §8's About rows rewritten for the dialog (8.13–8.15).
- CLAUDE.md: `IShellLauncher` opens "a folder" (no longer "or a link").

## Build/test evidence

- `dotnet build Enigma.Backup.slnx -c Release`: **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Backup.slnx -c Release`: **504 total, 492 passed, 12 skipped, 0 failed**,
  up from 489. New: 7 `AboutDialogViewModelTests`, plus the `ShortBuildSha` theories (9 cases) and
  `TheRunningBuildsShaIsShort` in `AppInfoTests`. Removed: the project-page test and two Settings About
  tests.
- Release launch, driven through UI Automation (`About Enigma.Backup` invoked):
  - dark theme: the dialog matches the reference, reading `Version 1.2.0` / `Build b25126e` and the
    copyright;
  - light theme: the same, via a temporary `settings.json` (`formatVersion` 1, `Light`) that was deleted
    right after, so the user's config folder is back to holding `profiles/` only;
  - Settings: shows Theme and Storage only.
  - Screenshots are in the session scratchpad.
