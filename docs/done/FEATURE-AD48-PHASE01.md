# FEATURE-AD48-PHASE01 — Hooks, policy and logging

## Summary

Exceptions that nothing else catches no longer end the app silently. Three process-wide hooks hand them
to a safety net that logs them with where they were caught:

- `AppDomain.UnhandledException`;
- `TaskScheduler.UnobservedTaskException`;
- Avalonia's `Dispatcher.UIThread.UnhandledException`, which is where a throwing command lands.

The parts are Enigma.GitClient's, scaled down to one window and no credentials:

- `UnhandledExceptionHooks` (static):
  - `Install` is the first line of `Main`, and `InstallDispatcher` the first line of `App`'s composition;
  - what is reported before the reporter exists is buffered (at most 32) and traced;
  - `Attach`, once the main window has opened, flushes the buffer to the reporter;
  - `Detach` runs on `Exit`, before the host stops.
- `UnhandledErrorReporter`: logs every error at `Error` with its source. A cancellation someone asked
  for, or any cancellation once shutdown has started, is logged at `Debug` only.
- `UnhandledErrorPolicy` and `UnhandledErrorBatch`: the decisions PHASE02's dialog will use. In order:
  - the same instance offered twice → ignored;
  - a requested cancellation → ignored;
  - an error arriving while a batch is open → merged into it;
  - the same signature within a minute of being closed → suppressed;
  - anything else → queued as a new batch.

  The batch also builds the message (seeing through single-inner wrappers) and the details (type,
  message, stack, inner exceptions to depth 8, never `Exception.Data`). Both are tested on their own here.

## Files/modules touched

- Created:
  - `src/Enigma.Backup.Desktop/Services/UnhandledErrorSource.cs`
  - `src/Enigma.Backup.Desktop/Services/UnhandledErrorDecision.cs`
  - `src/Enigma.Backup.Desktop/Services/UnhandledErrorBatch.cs`
  - `src/Enigma.Backup.Desktop/Services/UnhandledErrorPolicy.cs`
  - `src/Enigma.Backup.Desktop/Services/IUnhandledErrorReporter.cs`
  - `src/Enigma.Backup.Desktop/Services/UnhandledErrorReporter.cs`
  - `src/Enigma.Backup.Desktop/Services/UnhandledExceptionHooks.cs`
  - `tests/Enigma.Backup.Desktop.Tests/Services/UnhandledErrorPolicyTests.cs`
  - `tests/Enigma.Backup.Desktop.Tests/Services/UnhandledErrorBatchTests.cs`
  - `tests/Enigma.Backup.Desktop.Tests/Services/UnhandledExceptionHooksTests.cs`
  - `tests/Enigma.Backup.Desktop.Tests/Services/UnhandledErrorReporterTests.cs`
  - `tests/Enigma.Backup.Desktop.Tests/TestSupport/ManualTimeProvider.cs`
  - `tests/Enigma.Backup.Desktop.Tests/TestSupport/RecordingLogger.cs`
  - `tests/Enigma.Backup.Desktop.Tests/TestSupport/RecordingReporter.cs`
  - `tests/Enigma.Backup.Desktop.Tests/TestSupport/Thrown.cs`
  - `docs/done/FEATURE-AD48-PHASE01.md`
- Modified:
  - `src/Enigma.Backup.Desktop/Program.cs`
  - `src/Enigma.Backup.Desktop/App.axaml.cs`
  - `src/Enigma.Backup.Desktop/Hosting/ServiceCollectionExtensions.cs`
  - `tests/Enigma.Backup.Desktop.Tests/Hosting/ServiceCollectionExtensionsTests.cs`
  - `docs/roadmap.md`
  - `docs/plan/FEATURE-AD48.md`

## Deviations & follow-ups

- **`Detach` runs in `App`'s `Exit` handler, not after the lifetime returns in `Main`.** The `Exit`
  handler is where the host is stopped, so detaching there, just before `StopAsync`, is what keeps a late
  report away from a logger that is shutting down.
- **The reporter consults the policy only for cancellations in this phase.** The queue, merge and
  suppress decisions show nothing yet, so wiring them in now would have kept batches nobody presents.
  PHASE02 adds them together with the dialog. The policy itself is complete and tested here.
- The app logs to console and debug sinks only, so in a normal (non-console) launch the log is not
  persisted anywhere. This is unchanged and out of scope. PHASE02's dialog is what the user sees.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Static field names in the hooks | `Gate`, `Early` (PascalCase) for `static readonly`, `_reporter` for mutable | The repo's `static readonly` fields are PascalCase (`ArchiveFormatDetector`); the naming rule is a suggestion |
| `EarlyLimit` visibility | `public const` | The hooks test asserts the cap without repeating the number |
| Hooks tests | A `DisableParallelization` collection; each test drains the static buffer first; `Install` never called | The hooks are process-wide; `Install` in a test would subscribe the whole test run to real events |
| Testing "the same error again" | `Thrown.Error` / `Thrown.ErrorElsewhere`: real throws at two fixed sites | Signatures include the stack trace, so the tests need real ones |
| Manual check's throwing path | `OnOpenAboutAsync` made `async` and throwing, in the working tree only | A realistic faulting `AsyncRelayCommand`. It was reverted before staging, and `git diff` on the file is empty |

## Documentation sweep

- Nothing became wrong in this phase. CLAUDE.md, the README and the QA checklist describe the safety net
  in PHASE02, when it becomes visible.

## Build/test evidence

- `dotnet build Enigma.Backup.slnx -c Release`: **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Backup.slnx -c Release`: **536 total, 524 passed, 12 skipped, 0 failed**
  (up from 505). New tests:
  - 13 policy tests;
  - 9 batch tests;
  - 5 hooks tests;
  - 3 reporter tests;
  - 1 DI test (`TheSafetyNetIsOneInstance`).
- Manual run (Release build, stdout captured):
  - the About command was temporarily made to throw `InvalidOperationException("QA: temporary throw from About")`;
  - invoking it through UI Automation left the app **running**;
  - the log shows `fail: …UnhandledErrorReporter — An unhandled exception reached the safety net (Dispatcher).`
    with the exception and its stack;
  - the throw was then reverted and the suite re-run, with the same counts.
