# FEATURE-AD48 — Catch unhandled exceptions

**Status:** IN PROGRESS
**Type:** FEATURE (multi-phase — 3 phases)
**Branch (per phase, at build time):** `feature/feature-ad48-phaseNN-<slug>`, one branch per phase, cut from the run-branch tip.
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

Every exception that nothing else catches is logged and shown in an error **content dialog**, which says
the application is still running. This holds from the first line of `Main` until the process exits,
including while the app is starting. It is the safety net every Enigma app carries. The reference is
Enigma.GitClient's (`Services/UnhandledExceptionHooks.cs`, `UnhandledErrorPolicy.cs`,
`UnhandledErrorReporter.cs`, `Views/Dialogs/UnhandledErrorView.axaml`), scaled down to a single-window app
with no credentials.

## Context & constraints

- **Today nothing is hooked.** No handler on `AppDomain.UnhandledException`,
  `TaskScheduler.UnobservedTaskException` or Avalonia's `Dispatcher.UIThread.UnhandledException`.
  A command that throws ends the process.
- **Known failures are handled where they happen.** Run failures become the outcome banner and a
  `RunSummary`; profile I/O gets message dialogs. The net is a safety net, not the error handling, and that
  existing handling is untouched.
- **Where exceptions surface.**
  - CommunityToolkit's `AsyncRelayCommand` rethrows a faulted execution on the UI
    `SynchronizationContext`, which lands in `Dispatcher.UIThread.UnhandledException`.
  - An exception reaching `AppDomain.UnhandledException` ends the process whatever any handler does. It
    can only be logged.
- **Logging.** Through `ILogger` (console and debug sinks; this app has no file logging, and that stays).
- **The dialog host.**
  - There is one `ContentDialog` host (`MainWindow.HostDialog`). `IContentDialogService.ShowAsync` resets
    and reconfigures it, so showing a dialog while another is open would replace that one's content.
  - Every dialog goes through `DialogService`.
  - The host z-order invariant (Overlay → ContentDialog → InfoBar) already puts a dialog above the run
    scrim.
- **CLAUDE.md invariants.**
  - View models never name Avalonia.
  - The library's services are consumed only inside `Desktop/Services/`.
  - Desktop tests are headless view-model tests: no Avalonia platform.
  - Dialog content takes the focus on attach.
- **Reference styling.**
  - Title "Something went wrong", Phosphor `Warning` icon, one `Close` button (default).
  - Content `MinWidth 480`, in this order: the lead sentence; the message (SemiBold); "N more errors
    occurred." (secondary); a collapsed **Details** expander holding a read-only monospace `TextBox`
    (max 220 px high) and a **Copy details** button.
  - Icon brush: the reference hard-codes `#E8A33D`. This app's dialogs use `EnigmaWarningBrush`, which
    follows the theme. See *Decisions*.
- **Startup.** `App.OnFrameworkInitializationCompleted` builds the host and resolves the window. If that
  throws, there is no dialog host.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Shape | GitClient's chain: static hooks → reporter → policy → dialog | Uniform across apps, and each part testable on its own | A try/catch in `Main` (catches nothing on the UI thread) |
| Scaling down | No multi-window tracking, no token recovery, no redaction | One window, and no credentials ever travel in this app's exceptions (passwords are parameters) | Copying GitClient verbatim |
| How the reporter shows the dialog | Through a new `IDialogService.ShowUnhandledErrorAsync`, with the reporter itself free of Avalonia | The reporter stays testable in the headless Desktop suite, and dialogs keep one gateway | The reporter calling `IContentDialogService` directly (untestable here) |
| Busy host | `DialogService` shows one dialog at a time: a dialog asked for while another is open waits for it to close | Showing would replace the open question. One gate covers every dialog, not just this one | Interrupting the open dialog; a reporter-only `IsOpen` probe |
| What is shown | Dispatcher and unobserved-task exceptions get the dialog. AppDomain exceptions are logged only | The runtime allows nothing more for AppDomain | Pretending to recover |
| Floods | One dialog at a time. Later errors are counted into it. An identical error closed less than 1 min ago is logged only | A failing loop must not open a dialog per failure | A dialog per exception |
| Cancellations | Ignored when their own token was cancelled, and during shutdown | A stop someone asked for is not an error. A run's Cancel already ends in the banner | Showing them |
| Errors before the window | Buffered (at most 32) and handed over once the main window has opened | The dialog needs a host on screen | Dropping them |
| Startup failure | A small `StartupErrorWindow` that hosts its own `ContentDialog` and shows the same dialog. Closing it ends the app | "All" includes the start, and MarkdownEditor did the same | Logging only |
| Icon brush | `EnigmaWarningBrush` | Every warning in this app uses it, and it follows a theme switch. The reference's literal does not | `#E8A33D` hard-coded |
| Copy details | The view's code-behind writes to `TopLevel.Clipboard` | View-only behaviour, so no new seam for one click (as in Enigma.Msi) | An `IClipboard` service |
| Error details | Type, message and stack for the exception and its inners (depth ≤ 8). Never `Exception.Data` or `ToString()` | That is what a bug report needs, without what an exception might smuggle | `ToString()` |
| Phasing | PHASE01 hooks + policy + logging; PHASE02 dialog + wiring; PHASE03 startup window | Three reviewable units. PHASE01 alone already stops the crashes | One dev |

---

## PHASE01 — Hooks, policy and logging

**Status:** DONE

### Steps

1. **Enums** (`Services/`): `UnhandledErrorSource` (`Dispatcher`, `UnobservedTask`, `AppDomain`,
   `Startup`) and `UnhandledErrorDecision` (`Ignore`, `Suppress`, `Queue`, `Merge`).
2. **`Services/UnhandledErrorBatch.cs`**
   - Holds the first error and the later ones.
   - `Message`: the meaningful message, unwrapped through a single-inner `AggregateException` or
     `TargetInvocationException`, falling back to the type name.
   - `More`: how many errors came after the first.
   - `Details`: type, message and stack, with inner exceptions to depth 8, and "Error i of n" when there
     are several.
   - A `Changed` event.
3. **`Services/UnhandledErrorPolicy.cs`** (driven by a `TimeProvider`)
   - `IsShuttingDown` and `IsIgnoredCancellation`.
   - `Offer`, decided in this order:
     - the same instance twice → `Ignore`;
     - a requested cancellation → `Ignore`;
     - an open batch → `Merge`;
     - the same signature closed less than `RepeatWindow` (1 min) ago → `Suppress`;
     - anything else → `Queue`.
   - `BeginShow` and `EndShow`.
4. **`Services/IUnhandledErrorReporter.cs`** with `Report(exception, source)` and `ShutdownStarted()`.
5. **`Services/UnhandledExceptionHooks.cs`** (static)
   - `Install()` hooks AppDomain and unobserved tasks (calling `SetObserved`).
   - `InstallDispatcher()` hooks the dispatcher and sets `Handled = true`.
   - `Attach`, `Detach` and `Report`. Early reports are buffered (at most 32) and traced (type and message only).
6. **`Services/UnhandledErrorReporter.cs`** (this phase logs only)
   - `LogError` with the source; `LogDebug` for an ignored cancellation.
   - The policy decision is taken on the UI thread through `IUiDispatcher.Post`.
   - Register it in `AddEnigmaBackupDesktop`, with `TimeProvider.System` registered via `TryAdd`.
7. **Wiring**
   - `Program.Main` calls `Install()` first and `Detach()` after the lifetime returns.
   - `App.OnFrameworkInitializationCompleted` calls `InstallDispatcher()` first and attaches the reporter
     once the window has opened (`Opened`).
   - `desktop.ShutdownRequested` calls `ShutdownStarted()`.
8. **Tests**: plain `[Fact]` / `[Theory]` with a manual `TimeProvider`.
   - Policy: ignore, merge, suppress, repeat-window expiry, shutdown.
   - Batch: message unwrapping, details shape, depth cap, `Changed`.
   - Hooks: buffered before attach and flushed on attach, the cap of 32, detach. In a non-parallel
     collection, because the class is static.
   - Reporter: logs with the source; an ignored cancellation is not an error.
   - DI: the reporter and `TimeProvider` resolve.

### Acceptance criteria

- A command that throws no longer ends the process, and the exception is logged with its source.
- Release build has zero warnings. Whole suite green, new tests included.

## PHASE02 — The error dialog

**Status:** DONE

### Steps

1. **`DialogService` serializes dialogs.** A gate awaited on the UI thread inside `ShowAsync`: a second
   dialog waits for the first to close. Cancellation while waiting returns the view model's default, as
   it does today.
2. **`ViewModels/UnhandledErrorDialogViewModel.cs`** (`DialogViewModel`)
   - `Lead`, `Message`, `HasMore` / `MoreText` (singular and plural), and `Details`.
   - It follows the batch's `Changed` event.
   - A `CloseCommand` for Enter.
3. **`Views/UnhandledErrorDialog.axaml(.cs)`**
   - The reference's markup: named elements and automation names.
   - Focus taken on attach.
   - **Copy details** writes `Details` to the clipboard from code-behind.
4. **`IDialogService.ShowUnhandledErrorAsync(UnhandledErrorBatch, CancellationToken)`** and its
   `DialogService` implementation: title "Something went wrong", `Warning` glyph in `EnigmaWarningBrush`,
   Close (default).
5. **`UnhandledErrorReporter` presents batches.**
   - On `Queue`: `BeginShow`, then await the dialog, then `EndShow`, then show the next pending batch if
     there is one.
   - If the dialog itself fails: log it, and switch the net to log-only for the rest of the session.
6. **Tests**
   - `UnhandledErrorDialogViewModelTests`: texts, singular and plural, follows `Changed`.
   - Reporter tests with `FakeDialogService`:
     - a queued error is shown;
     - a merge while showing counts into it;
     - a repeat within the window is not shown again;
     - after `EndShow` a new error is shown;
     - a dialog failure turns the net log-only.
   - `FakeDialogService` gains the method.
7. **Manual run** with a temporary throwing path (e.g. a debug-only throw behind a button). Recorded,
   then removed, so it never lands in a commit.
8. **Docs**: README (a short "if something goes wrong" line), `docs/QA-CHECKLIST.md`, and CLAUDE.md (the
   one-dialog-at-a-time gate and the safety net).

### Acceptance criteria

- A throwing command shows "Something went wrong" with its message, and the app keeps running.
- Two errors in a row give one dialog that says "1 more error occurred.". The same error again within a
  minute is logged only.
- A dialog asked for while another is open waits instead of replacing it.
- Release build has zero warnings. Whole suite green, new tests included.

## PHASE03 — Startup failure window

**Status:** TODO

### Steps

1. **`Views/StartupErrorWindow.axaml(.cs)`**
   - A small, centred window on `EnigmaBackgroundBrush`, titled `AppInfo.Name`, with the app icon.
   - It carries only a `ContentDialog` host, and on open shows the PHASE02 dialog content for the
     exception (same title, glyph, brush and Close).
   - Closing the dialog closes the window.
2. **`App.OnFrameworkInitializationCompleted`** wraps the composition in a `try/catch`. On an exception it:
   - reports it through `UnhandledExceptionHooks.Report(…, Startup)`, which traces it and logs it if the
     reporter is attached;
   - disposes a half-built host;
   - sets `desktop.MainWindow` to a `StartupErrorWindow`.

   Closing that window ends the process, because it is the only window. A failure inside the fallback
   window itself propagates, since that path has no host left to try.
3. **Tests**: the part that is not Avalonia, i.e. building the batch and view model for a startup
   exception, is a tested static helper.
4. **Manual run** with a temporary throw in composition. Recorded, then removed.

### Acceptance criteria

- A throw during startup shows a window with the "Something went wrong" dialog, and closing it ends the app.
- Release build has zero warnings. Whole suite green.

## Out of scope

- Crash reporting to any service. Restarting after an AppDomain-level failure.
- File logging.
- Changing the existing, specific error handling (run outcome banner, profile I/O dialogs).
