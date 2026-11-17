# FEATURE-95BE-PHASE01 — Runs in a content dialog

## Summary

A backup, restore or verification is now shown in a `ContentDialog` of its own, from its first progress
report to its outcome:

- **While it runs:** progress, the current file and **Cancel**.
- **Once it is over:** the same dialog turns to the outcome — a title and icon by severity, the headline,
  the details, anything left out and any warnings — with **Close**.

What this removes:

- the "Last run" panel;
- the outcome banner (`INotificationService` and the `InfoBar` host);
- the run overlay (`IProgressOverlay` and `ProgressOverlayCard`).

How the dialog works:

- The run dialog has its own host (`HostRunDialog`) below the question host. That is why a mid-run question
  (password, unreadable file, restore conflict) draws above it and can be answered.
- The library's dialog closes itself on any button, Escape or a click on its scrim. While a run is going,
  `RunDialog` reopens it in the same call, so only Cancel stops a run.
- `RunAsync`'s `finally` always calls `run.Finish(...)`, so a run always ends closable.

## Files/modules touched

- Created:
  - `src/Enigma.Backup.Desktop/ViewModels/RunViewModel.cs`
  - `src/Enigma.Backup.Desktop/Services/IRunDialog.cs`
  - `src/Enigma.Backup.Desktop/Services/RunDialog.cs`
  - `src/Enigma.Backup.Desktop/Services/ThemeAssets.cs`
  - `src/Enigma.Backup.Desktop/Views/RunView.axaml`
  - `src/Enigma.Backup.Desktop/Views/RunView.axaml.cs`
  - `tests/Enigma.Backup.Desktop.Tests/TestSupport/FakeRunDialog.cs`
  - `tests/Enigma.Backup.Desktop.Tests/ViewModels/RunViewModelTests.cs`
- Renamed and rewritten: `tests/Enigma.Backup.Desktop.Tests/ViewModels/MainWindowViewModelOverlayTests.cs`
  → `MainWindowViewModelRunDialogTests.cs`
- Modified:
  - `src/Enigma.Backup.Desktop/App.axaml`
  - `src/Enigma.Backup.Desktop/App.axaml.cs`
  - `src/Enigma.Backup.Desktop/Hosting/EnigmaAvaloniaServiceCollectionExtensions.cs`
  - `src/Enigma.Backup.Desktop/Hosting/ServiceCollectionExtensions.cs`
  - `src/Enigma.Backup.Desktop/Services/DialogService.cs`
  - `src/Enigma.Backup.Desktop/ViewModels/MainWindowViewModel.cs`
  - `src/Enigma.Backup.Desktop/Views/MainWindow.axaml`
  - `tests/Enigma.Backup.Desktop.Tests/Hosting/ServiceCollectionExtensionsTests.cs`
  - `tests/Enigma.Backup.Desktop.Tests/Integration/WindowEndToEndTests.cs`
  - `tests/Enigma.Backup.Desktop.Tests/TestSupport/Harness.cs`
  - `tests/Enigma.Backup.Desktop.Tests/ViewModels/MainWindowViewModelRunTests.cs`
  - `README.md`
  - `docs/QA-CHECKLIST.md`
  - `CLAUDE.md`
  - `docs/roadmap.md`
  - `docs/plan/FEATURE-95BE.md`
- Deleted:
  - `src/Enigma.Backup.Desktop/Services/IProgressOverlay.cs`
  - `src/Enigma.Backup.Desktop/Services/ProgressOverlay.cs`
  - `src/Enigma.Backup.Desktop/Services/INotificationService.cs`
  - `src/Enigma.Backup.Desktop/Services/NotificationService.cs`
  - `src/Enigma.Backup.Desktop/Services/NotificationSeverity.cs`
  - `src/Enigma.Backup.Desktop/Views/ProgressOverlayCard.axaml`
  - `src/Enigma.Backup.Desktop/Views/ProgressOverlayCard.cs`
  - `tests/Enigma.Backup.Desktop.Tests/TestSupport/FakeProgressOverlay.cs`
  - `tests/Enigma.Backup.Desktop.Tests/TestSupport/FakeNotificationService.cs`

## Deviations & follow-ups

- **`IRunDialog` has no `CloseAsync`** (plan step 2 listed one). Nothing calls it: the user closes a
  finished run, and a run always finishes. It was dropped rather than shipped unused.
- **`StatusText` stays on `MainWindowViewModel`.** Profile and drop messages still set it, but runs no
  longer do. The following members are gone, because the run's state lives on its `RunViewModel`:
  - `Summary` and `HasSummary`;
  - `ProgressPercent`, `CurrentItem` and `IsProgressIndeterminate`;
  - `CancelCommand`.
- **`ThemeAssets`** (internal) now holds the `Glyph` / `Brush` helpers `DialogService` had privately, so
  `RunDialog` can share them. This was a small extraction, not a behaviour change.
- **Outcome layout fix from the screenshot check.** The details' label column was a fixed 130 px, so
  "Expected SHA-256" and "Encrypted contents" ran into their values. It is now a shared-size `Auto`
  column.
- **`RunDialog` has no unit test.** It drives an Avalonia `ContentDialog`, and the Desktop suite is
  headless view-model tests only. It was checked in the running app (below).
- Follow-up (existing behaviour): a long archive path in the outcome wraps right after `C:`. This is
  `SelectableTextBlock`'s line breaking, as in the old panel.
- Follow-up (existing behaviour): `StatusText` is displayed nowhere. PHASE04/05 move it with the page
  that sets it.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| How a progress report finds its run | The operation lambda receives the `RunViewModel`, and its `DispatchedProgress` reports to that one | A late report from one run can never land in the next run's dialog |
| Dialog title once finished | The old banner's three wordings: "Done", "Finished, with something to report", "Something went wrong" | They already say how to read the headline |
| Icons | `HourglassMedium` while running. Once finished: `CheckCircle`, `Warning` or `WarningCircle`, with the success, warning or error brush | Matches the message dialogs' severity icons |
| A second press on Cancel | The button is greyed out (`IsCloseButtonEnabled = false`), and `RunViewModel` ignores a repeat | The dialog's buttons ignore `CanExecute` |
| A summary that was never set | `RunSummary.Failed(what, "it stopped without saying why.")` | `Finish` must always run with something |

## Build/test evidence

- **Run baseline** (taken on the run branch: `main` @ `48a4736` plus the planning commit):
  - Debug and Release builds: **0 warnings, 0 errors**.
  - `dotnet test --solution Enigma.Backup.slnx`: **552 total, 540 passed, 12 skipped, 0 failed**. The 12
    skips are the existing platform-conditional tests.
- **This dev:**
  - Debug and Release builds: **0 warnings, 0 errors**.
  - Tests: **559 total, 547 passed, 12 skipped, 0 failed**.
- **Tests added:**
  - `RunViewModelTests` (7).
  - `MainWindowViewModelRunDialogTests` (10): these replace the 10 overlay and banner tests — one dialog
    per run, every report reaching it, Cancel, a failure still finished, an abort, headings and
    severities.
  - The DI test `TheRunDialogAppWiresIsTheOneEveryRunIsShownOn`, replacing `TheRunOverlayIsOneInstance`.
- **In the running app** (Debug build on Windows):
  - How it was driven: windows captured with `PrintWindow`, buttons pressed through UI Automation, and the
    native picker answered with `WM_SETTEXT` / `IDOK`. No keystrokes reached the desktop.
  - The "Last run" panel is gone, and the inputs panel spans the window.
  - **Verify…** on a file with no sidecar opens the run dialog on its outcome. It shows "Finished, with
    something to report", the amber warning icon, the headline, the details and **Close**. **Close**
    dismisses it.
  - **Verify…** on a copy of the golden encrypted archive works mid-run:
    - While the password prompt was open, UI Automation saw the run dialog behind it ("Checking — 727
      bytes of 727 bytes" with its **Cancel**), so the prompt drew above the run dialog and could be
      answered.
    - Cancelling the prompt turned the run dialog to the partial outcome.
  - Not seen by eye: the in-progress view (these runs finish instantly), Escape or a scrim click being
    ignored mid-run, and Cancel mid-run. They are covered by the view-model tests and QA rows 4.7, 7.6
    and 7.8.

## Documentation sweep

- `README.md` "Using it", items 7–8: the run and its outcome are in a dialog; no banner and no "Last run"
  panel.
- `docs/QA-CHECKLIST.md`: the preamble, plus rows 3.6, 4.7–4.10, 5.3, 5.4, 5.6, 6.1, 6.3, 6.5, 7.1, 7.2,
  7.6, 7.8 and 9.9 now speak of the run dialog. The history sections are untouched.
- `CLAUDE.md`:
  - The Desktop description no longer mentions the card's dictionary.
  - The view-model seams list `IRunDialog`, and the concrete-type registrations now cover two services.
  - The unused overlay and info-bar services are noted.
  - The host z-order is now `HostRunDialog` → `HostDialog`.
  - The "scrim must come down" invariant is replaced by "a run must always end closable".
  - The `IInfoBarService` invariant is removed.
