# FEATURE-AD48-PHASE02 — The error dialog

## Summary

An exception that nothing else caught now opens the safety net's **Something went wrong** dialog, which
says the app is still running. It is GitClient's `UnhandledErrorView`, in this app's dialog shape:

- a lead sentence;
- the message, in SemiBold;
- "N more errors occurred.";
- a collapsed *Details* expander holding a read-only monospace box and **Copy details**;
- the `Warning` glyph in `EnigmaWarningBrush` and a single default `Close`.

The parts:

- `UnhandledErrorReporter` decides what is shown on the UI thread through `UnhandledErrorPolicy`. It shows
  a batch through the new `IDialogService.ShowUnhandledErrorAsync`, then any batch that queued meanwhile.
  If the dialog itself fails, the net goes log-only for the rest of the session.
- An exception reaching two hooks (dispatcher, then unobserved task at GC) is now logged as **one** error
  plus a debug line, and shown once.
- **`DialogService` shows one dialog at a time.** A one-slot gate, held from raise to close, makes any
  dialog asked for while another is open wait instead of replacing it. A run cancelled while its question
  waits gets the same cautious answer as one cancelled before asking. In practice only the safety net can
  arrive mid-question, but the gate covers every dialog.

## Files/modules touched

- Created:
  - `src/Enigma.Backup.Desktop/ViewModels/UnhandledErrorDialogViewModel.cs`
  - `src/Enigma.Backup.Desktop/Views/UnhandledErrorDialog.axaml`
  - `src/Enigma.Backup.Desktop/Views/UnhandledErrorDialog.axaml.cs`
  - `tests/Enigma.Backup.Desktop.Tests/ViewModels/UnhandledErrorDialogViewModelTests.cs`
  - `docs/done/FEATURE-AD48-PHASE02.md`
- Modified:
  - `src/Enigma.Backup.Desktop/Services/DialogService.cs`
  - `src/Enigma.Backup.Desktop/Services/IDialogService.cs`
  - `src/Enigma.Backup.Desktop/Services/IUnhandledErrorReporter.cs`
  - `src/Enigma.Backup.Desktop/Services/UnhandledErrorReporter.cs`
  - `tests/Enigma.Backup.Desktop.Tests/Services/UnhandledErrorReporterTests.cs`
  - `tests/Enigma.Backup.Desktop.Tests/TestSupport/FakeDialogService.cs`
  - `README.md`
  - `docs/QA-CHECKLIST.md`
  - `CLAUDE.md`
  - `docs/roadmap.md`
  - `docs/plan/FEATURE-AD48.md`

## Deviations & follow-ups

- **Step 4 of the plan (attach once the window has opened) was already done in PHASE01**, so no change was
  needed here.
- **Added beyond the plan:** de-duplicating the log for one exception that reaches two hooks. The manual
  run showed each faulted command logged twice: `Dispatcher`, then `UnobservedTask` when the GC finalised
  its task. A weak, thread-safe table in the reporter turns the second arrival into a debug line.
- **Not observed by eye.** The workstation's session locked during this phase, so screenshots captured
  only the lock screen. The dialog was verified through UI Automation instead (see evidence). A visual
  check of the dialog, both themes, belongs to QA 7.9–7.11.
- **Gate behaviour behind an open dialog (QA 7.11)** is covered by the code and its reasoning, not by an
  automated test. `DialogService` names Avalonia types, so it is outside the headless suite.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Gate primitive | `SemaphoreSlim(1, 1)`, awaited inside the UI-thread `InvokeAsync`, released in `finally` | Correct cancellation semantics for a waiting question. The service is an app-lifetime singleton, so the semaphore is never disposed |
| Gate structure | The body moved into a private `ShowOnHostAsync`, behaviour unchanged | Keeps the turn-taking visible in one place without nesting the whole body in another try |
| Details font | `FontFamily="monospace"` | What this repo's other dialogs use for paths. The reference's `MonospaceFontFamily` is a GitClient resource |
| Copy-details button | A plain `Button` | The reference's `toolbar` class is a GitClient style this app does not have |
| Duplicate arrivals | Logged once as an error, then at debug | One failure is one error in the log, and the reason for the second arrival is in the debug line |

## Documentation sweep

- README *Using it*: a paragraph on the **Something went wrong** dialog.
- `docs/QA-CHECKLIST.md` §7: 7.9–7.11 (dialog, merge plus details plus copy, waiting behind an open dialog).
- CLAUDE.md: two new invariants, *The dialogs take turns* and *Unhandled exceptions end in the safety net*.

## Build/test evidence

- `dotnet build Enigma.Backup.slnx -c Release`: **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Backup.slnx -c Release`: **548 total, 536 passed, 12 skipped, 0 failed**
  (up from 536 total).
  - New view-model tests: 4 in `UnhandledErrorDialogViewModelTests`.
  - `UnhandledErrorReporterTests` went from 3 to 11 tests: shown, merged while open, suppressed within the
    window, shown again after it, a different error, dialog failure → log-only, the same exception at two
    hooks, decisions on the UI thread.
  - One intermediate run had **1 failure** (`TheButtonBesideTheGearOpensTheDialog`). The cause was the
    manual check's temporary throw, still in `OnOpenAboutAsync`. Reverting it made the suite green; no fix
    cycle was involved.
- Manual run (Release build, About temporarily made to throw, then reverted; `git diff` on the file is
  empty):
  - after one invoke the app is alive, and UI Automation finds the message
    `QA: temporary throw from About`, the *Details* expander (expanded through UIA), the details box,
    **Copy details** and **Close**;
  - after two invokes, UIA finds `1 more error occurred.` and the lead sentence.
