# FEATURE-AD48-PHASE03 — Startup failure window

## Summary

A failure while the app is starting — before there is a container or a main window — no longer ends the
process with nothing on screen.

- `App.OnFrameworkInitializationCompleted` now runs the composition in `Compose()`, inside a `try/catch`,
  filtered to a classic desktop lifetime. On an exception it:
  - reports it as `UnhandledErrorSource.Startup` (traced and buffered by the hooks: the reporter is not
    attached yet);
  - disposes the half-built host;
  - makes a `StartupErrorWindow` the main window.
- `StartupErrorWindow`:
  - a small `Enigma.Backup` window on `EnigmaBackgroundBrush`, with the app icon, carrying only a
    `ContentDialog` host bounded like the main window's;
  - on open, it raises the safety net's dialog on that host, configured exactly as `DialogService` does it
    (title, `Warning` glyph in `EnigmaWarningBrush`, one default `Close`), because `DialogService` may be
    what failed;
  - closing the dialog closes the window, and with it the app.
- `UnhandledErrorDialogViewModel.ForStartupFailure` gives the dialog a lead that does not claim the app is
  still running. It says the app cannot open, and that closing ends it.

## Files/modules touched

- Created:
  - `src/Enigma.Backup.Desktop/Views/StartupErrorWindow.axaml`
  - `src/Enigma.Backup.Desktop/Views/StartupErrorWindow.axaml.cs`
  - `docs/done/FEATURE-AD48-PHASE03.md`
- Modified:
  - `src/Enigma.Backup.Desktop/App.axaml.cs`
  - `src/Enigma.Backup.Desktop/ViewModels/UnhandledErrorDialogViewModel.cs`
  - `tests/Enigma.Backup.Desktop.Tests/ViewModels/UnhandledErrorDialogViewModelTests.cs`
  - `README.md`
  - `docs/QA-CHECKLIST.md`
  - `CLAUDE.md`
  - `docs/roadmap.md`
  - `docs/plan/FEATURE-AD48.md`

## Deviations & follow-ups

- **The tested helper is `UnhandledErrorDialogViewModel.ForStartupFailure`**, not a separate static
  class. It is the only non-Avalonia part. The window itself is view code, which is outside the headless
  suite.
- A failure inside the fallback window (its `async void OnOpened`) is not caught again. It reaches the
  dispatcher hook and is traced, as the plan accepts ("a failure in the fallback has nowhere left to go").
- Not seen by eye: the session was locked, so the check ran through UI Automation (see evidence). QA 7.12
  covers the visual check.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Catch scope | `catch (Exception) when (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)` | It catches only when there is a window to show the failure in. The designer and any other lifetime still see the exception |
| Composition extracted | Into `Compose()`, body unchanged | The `try` wraps one call rather than sixty lines |
| Lead for a failed start | A second, private constructor plus `ForStartupFailure` | Keeps the public constructor's meaning, "app still running", unchanged for the safety net |
| Window size | 720×560 (min 560×420), host bounds copied from `MainWindow` | Room for the dialog with its details expanded, and the same bounds as every other dialog |

## Documentation sweep

- README: the safety-net paragraph now covers a failed start.
- `docs/QA-CHECKLIST.md`: 7.12 added.
- CLAUDE.md: the safety-net invariant describes the startup fallback.

## Build/test evidence

- `dotnet build Enigma.Backup.slnx -c Release`: **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Backup.slnx -c Release`: **549 total, 537 passed, 12 skipped, 0 failed**
  (new: `AFailedStartIsNotCalledARunningApp`).
- Manual run (Release build, with `throw new InvalidOperationException("QA: temporary startup failure")`
  temporarily added to `Compose()` right after `_host.Start()`):
  - the process stayed up, with a window titled `Enigma.Backup`;
  - UI Automation found the message, the startup lead ("…while the application was starting, so it
    cannot open…"), *Details* and `Close`;
  - invoking `Close` ended the process.

  The throw was then removed; `git diff` shows no trace of it.
- After the revert, a normal launch opened the main window: UI Automation finds `About Enigma.Backup` and
  `Settings`, and no error dialog.
- The first two attempts at the temporary throw did not compile (CS0162 unreachable code, then CS8602).
  They were only scaffolding for the manual check, not fix cycles of this dev.
