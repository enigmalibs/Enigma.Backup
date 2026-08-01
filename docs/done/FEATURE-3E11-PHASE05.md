# FEATURE-3E11-PHASE05 — Avalonia desktop app

**Completed:** 2026-08-01
**Branch:** `feature/feature-3e11-phase05-desktop-app`

## Summary

The app got its user interface. PHASE01–04 built a core that can do everything and a window that said
"the user interface arrives in PHASE05"; this phase is that window — and, more to the point, the three
seams that let it exist without the view models ever naming Avalonia:

```
IFilePicker      the system file and folder pickers, in and out as plain paths
IDialogService   every question that needs a modal answer — password, profile name,
                 Skip/Retry/Abort, Skip/Overwrite/Abort, message, confirmation
IUiDispatcher    the UI thread, for the progress reports the core sends from its own
```

The core already had the interfaces for the two *decisions* (`IBackupErrorHandler`,
`IRestoreConflictHandler`); this phase supplies the dialogs behind them and adds the seams for
everything else the desktop needs. The result is that a test can run a whole backup, answer its error
dialog, and read the summary with no window in existence — which is what the 141 new tests do.

- **One window.** A profile bar (drop-down + New / Save / Save as / Rename / Delete / Import /
  Export), the inputs list with add-file / add-folder / remove / remove-all and drag-and-drop, an
  exclude-pattern editor, the output folder, the compression drop-down and the encryption toggle,
  Back up / Restore / Verify with Cancel, a progress bar with a status line, and the summary panel
  with the skipped list given its own prominent heading.
- **Five dialogs**, each a window over a view model that holds its own answer — so a dialog closed
  with the title-bar button is not a special case. Every decision dialog defaults to the cautious
  answer: a dismissed error prompt aborts rather than quietly dropping a file, and a dismissed
  conflict prompt neither overwrites nor skips.
- **The profile is a working copy.** The store is read on demand and written only when asked; the
  title carries a `*` and the profile bar says "Unsaved changes." Every profile is loaded once on
  refresh and kept, so choosing one in the drop-down is instant and synchronous.
- **A run cannot take the window down.** Cancellation, an Abort answer and an outright failure each
  become a summary rather than an exception, and the editor is disabled for the duration.

## Files/modules touched

**Created — `src/Enigma.Backup.Desktop/Services/`** (the UI seams and their Avalonia implementations)

- `IUiDispatcher` / `AvaloniaUiDispatcher` — the UI thread as something a view model can ask for.
  Work already on it runs inline rather than queued.
- `IFilePicker` / `FilePicker` — the pickers, over `IStorageProvider`; anything without a local path
  is dropped, since the core works on the file system.
- `IDialogService` / `DialogService` — every modal question. Builds the view model, shows a window
  over the main one, reads the answer back off the view model. Marshals to the UI thread itself, so
  the core can ask from wherever it is.
- `IMainWindowProvider` / `MainWindowProvider` — the main window, handed over by `App` once resolved.
  It cannot be injected: the window is built from the view model that depends on the services that
  need it.
- `DialogBackupErrorHandler` / `DialogRestoreConflictHandler` — the core's decision seams, in terms of
  dialogs. Per-run adapters, not services: the "apply to all" memory belongs to the core's own
  `ErrorDecisionPolicy` / `RestoreConflictPolicy`, which wrap these for the length of a run.
- `DispatchedProgress<T>` — `IProgress<T>` through the dispatcher. `Progress<T>` would depend on which
  thread it happened to be constructed on, which is invisible at the point of use and wrong in a test.
- `PasswordPurpose`, `MessageSeverity`, `ProfileNameRequest` — the small types those take.

**Created — `src/Enigma.Backup.Desktop/ViewModels/`**

- `ProfileEditorViewModel` — the working copy of a profile: inputs, excludes, output, compression,
  encryption, dirty tracking, and `AddInputs` (shared by the pickers and drag-and-drop).
- `DialogViewModel` — the one thing every dialog shares: the ability to say it is finished.
- `PasswordDialogViewModel`, `ProfileNameDialogViewModel`, `ErrorDecisionDialogViewModel`,
  `RestoreConflictDialogViewModel`, `MessageDialogViewModel`.
- `RunSummary` + `SummaryDetail` + `RunSummaryKind` — the end-of-run panel, immutable and built by
  static factories from the core's results, which is what makes "did the summary come out right?" a
  test with no window in it.
- `CompressionOption` — the compression drop-down's entries, so no converter is needed.
- `DisplayFormat` — bytes in binary units, durations, counts with the right noun, local timestamps.

**Created — `src/Enigma.Backup.Desktop/Views/`**

- `PasswordDialog`, `ProfileNameDialog`, `ErrorDecisionDialog`, `RestoreConflictDialog`,
  `MessageDialog` — each `.axaml` + a three-line code-behind that binds the view model and closes on
  its request.

**Modified**

- `Views/MainWindow.axaml` — the whole window, replacing the placeholder. Compiled bindings with
  `x:DataType` on the root and on every `DataTemplate`.
- `Views/MainWindow.axaml.cs` — the two things a view model cannot hold: dropped files (platform
  storage items in, plain paths out) and the first read of the profile store, deferred to `OnOpened`
  so a slow disk cannot delay the window appearing.
- `ViewModels/MainWindowViewModel.cs` — rewritten from the placeholder into the window's state and
  every action.
- `App.axaml` — theme dictionaries for the app's own accents and panel surfaces.
- `App.axaml.cs` — hands the resolved main window to `MainWindowProvider`.
- `Hosting/ServiceCollectionExtensions.cs` — registers the seams (singleton) and the views and view
  models (transient).
- `docs/roadmap.md`, `docs/plan/FEATURE-3E11.md` — PHASE05 `TODO` → `IN PROGRESS` → `DONE`, plus a
  note on the phase.

**Created — tests (`tests/Enigma.Backup.Desktop.Tests`)**

- `Integration/WindowEndToEndTests.cs` — the acceptance criterion, minus the pixels: the shipping
  container, the real store, the real streaming pipeline, real files on disk, with only the three
  window-opening seams replaced.
- `ViewModels/` — `MainWindowViewModelProfileTests`, `MainWindowViewModelRunTests`,
  `ProfileEditorViewModelTests`, `PasswordDialogViewModelTests`, `ProfileNameDialogViewModelTests`,
  `DecisionDialogViewModelTests`, `RunSummaryTests`, `DisplayFormatTests`.
- `Hosting/ServiceCollectionExtensionsTests.cs` — the composition root, without starting Avalonia.
- `TestSupport/` — `Harness`, `FakeProfileStore`, `FakeDialogService`, `FakeFilePicker`,
  `FakeRunServices`, `Results`, `ImmediateUiDispatcher`, `TempDirectory`.

**Deleted — tests**

- `ViewModels/MainWindowViewModelTests.cs` — the PHASE01 placeholder (it asserted a settable `Title`),
  replaced by the files above.

## Decisions taken inside the plan's latitude

- **An exclude-pattern editor, which the plan's UI sketch did not list.** Per-profile glob excludes are
  a validated v1 decision and PHASE02 implemented them, but the sketched window had no way to set one
  — and PHASE06 adds no UI, so leaving it out would have shipped an unreachable feature. It is a text
  box, an Add button (Enter works too), a list and a Remove button, beside the inputs.
- **Three new UI seams rather than Avalonia in the view models.** The plan asks for view-model tests
  with no UI automation; that is only possible if file pickers, dialogs and thread marshaling are
  interfaces. It is the same argument the core already makes for `IBackupErrorHandler`, one layer up.
- **Every profile is loaded on refresh, not on selection.** Selecting one from the drop-down is then
  synchronous, which avoids an `async` operation started from a property setter. A profile file that
  cannot be read is left out of the list and reported once, rather than hiding every other profile.
- **Switching profiles discards unsaved edits without asking.** A confirmation would have to be
  awaited from that same synchronous setter. Instead the state is never invisible — the title carries
  a `*`, the profile bar says "Unsaved changes." — and the one genuinely irreversible action, Delete,
  is the one that confirms. The rule is: confirm what cannot be undone, signpost what can be redone.
- **Restore and verify find out about encryption from the archive, not its name.** Both run once with
  no password; the core answers `EncryptedArchiveProblem.PasswordRequired` or
  `VerificationOutcome.PasswordRequired`, and only then is a password asked for and the operation
  retried. This reuses the core's own detection instead of the desktop reading magic bytes, and it
  costs nothing, because the core refuses before it writes anything. The retry is guarded so a wrong
  password is reported rather than looping.
- **A dismissed decision dialog is the cautious answer.** Abort for both the error and the conflict
  prompt. Skipping is the one answer that quietly leaves data out of a backup, so it is never the one
  nobody chose.
- **Retry is never made sticky, and the dialog says so** — matching `ErrorDecisionPolicy`, which
  refuses it because a permanent failure answered with "retry, always" would never stop.
- **The progress bar keeps where it stopped.** Only a failure resets it to zero: a cancelled run
  stopped where it stopped, and saying so beats snapping to either end.
- **The window's own accent and panel brushes, in `App.axaml`.** The Fluent theme's
  `LayerFillColorDefaultBrush` / `CardStrokeColorDefaultBrush` did not resolve at runtime — the panels
  rendered with no border at all — so the app defines translucent ones per theme variant instead.
- **A backup from an unsaved profile is named `Backup`**, which the core then sanitizes into the
  archive's file name like any other profile name.

## Deviations & follow-ups

- **The click itself is untested.** No input-automation tool (`xdotool`, `ydotool`, `wmctrl`) is
  installed on this machine and the session is Wayland, so the UI was verified by running the app,
  loading real profile files from the per-user config folder, and screenshotting the result — every
  control renders, the bindings show the stored profile, and command enablement is visibly correct.
  What is *behind* the buttons is covered by `WindowEndToEndTests`, which drives the real container
  and the real pipeline through the same view model the window binds to. The gap is the button press.
- **Windows has not been exercised.** The plan's acceptance says "on Windows + Linux"; only Linux is
  available here. Nothing in the desktop layer is platform-specific beyond Avalonia itself, and path
  comparison follows the OS, but the manual QA on Windows belongs to PHASE06's checklist.
- **`ProfileEditorViewModel.AddInputs` compares paths, it does not canonicalize them.** It drops a
  trailing separator and compares case-insensitively on Windows and macOS, which stops the obvious
  duplicate; genuinely overlapping inputs (`/a` and `/a/b`) are still merged by the core's scan, with
  a warning in the summary, exactly as PHASE02 designed.
- **Export writes the *stored* profile, not the editor.** That is what `IProfileStore.ExportAsync`
  does; the status line says "Exported the saved profile 'X'" so an editor that has moved on is not
  mistaken for what was written.
- **No multi-selection in the inputs list.** Remove works on one entry, with Remove all beside it;
  two-way binding to `SelectedItems` in Avalonia was not worth the complexity for this.
- **The `DialogService`'s window-showing path has no automated test** — it is the one class that
  cannot run without Avalonia, and the solution deliberately has no UI-automation harness. Its logic
  (which view model, which default, which marshaling) is a straight-line mapping, and both ends of it
  are tested: the view models it builds, and the `IDialogService` contract its fakes implement.
- **Line endings:** the tree remains 100% LF and `.gitattributes` normalizes to LF — nothing to
  recommend, no action taken.

## Build/test evidence

- `dotnet build Enigma.Backup.slnx` — **Build succeeded, 0 Warning(s), 0 Error(s)**.
- `dotnet build Enigma.Backup.slnx -c Release` — **0 Warning(s), 0 Error(s)**.
- `dotnet test --solution Enigma.Backup.slnx` — **total: 351, failed: 0, succeeded: 351, skipped: 0**
  (up from 210 at PHASE04; 141 new tests).
- **The acceptance criterion, end to end through the real container** (`WindowEndToEndTests`): a
  profile built in the editor, saved through the real `ProfileStore`, backed up, verified and restored
  — for **Zstd and Gzip, encrypted and not**, four combinations. Each asserts the archive's name and
  extension (`.tar.zst` / `.tar.gz`, `+ .enc`), that the `.sha256` sidecar is beside it, that the
  verification succeeded (and, when encrypted, that every block authenticated), and that the restored
  tree matches the source byte-for-byte — with the profile's `**/build/**` exclude honoured and an
  empty directory preserved.
- **Encryption, end to end**: an archive written with one password reports `IsFailure` and "the
  password is wrong" when verified with another, and a restore with the wrong password writes nothing
  into the destination.
- **Profiles across sessions**: saved by one window, reopened by a second over the same folder with
  every field intact, exported to an arbitrary path, and imported into a fresh store.
- **Command enablement**: Backup disabled with no inputs, with no output directory, and with an
  output directory of only spaces; enabled with both; disabled again when the inputs are cleared, and
  for the whole duration of a run. Rename, Delete and Export disabled without a stored profile.
- **Profile list state**: names listed case-insensitively in order, the first opened onto, the
  selection preserved across a refresh, a profile whose file cannot be read left out and reported,
  Save-as offering every existing name as taken, Rename leaving the profile's own name available,
  Delete confirming first and doing nothing when declined, a store failure reported with the core's
  message and changing nothing.
- **The error-decision flow**: the core's handler reaches the dialog, the answer (Skip, apply to all)
  reaches the core, and the skipped entry reaches the summary with the core's own reason text.
  Restore's conflict flow likewise, with Overwrite + apply-to-all.
- **Progress and cancellation**: indeterminate while scanning, 25% at 512 of 2048 bytes with the
  current file shown, busy state and disabled commands during, all cleared after; Cancel from inside
  a run produces "The backup was cancelled; the partial archive was deleted."
- **Password dialog validation**: two fields for a new archive and one for an existing one, empty
  refused without being called an error, a mismatch refused only once the second field has been
  started, a password of spaces accepted (matching the core), and a dismissed dialog handing back
  nothing.
- **The composition root**: `MainWindowViewModel` resolves with all nine of its dependencies from
  `AddEnigmaBackupCore()` + `AddEnigmaBackupDesktop()`, the seams resolve to their Avalonia
  implementations, the window provider is one shared instance, views and view models are transient,
  and registering twice changes nothing.
- **By hand, on this machine** (Wayland + XWayland, `DISPLAY=:1`): the app launched against two real
  profile files in `~/.config/Enigma.Backup/profiles`, opened onto the first, and rendered every
  control — the drop-down showing the stored profile with "Saved.", the inputs list, the exclude
  editor, the output path, "Gzip (.tar.gz)" with its description, the ticked encryption box, the
  action row with Cancel disabled, and the empty summary panel with its placeholder. Confirmed in
  both the Debug and the final build; the test profiles were removed afterwards.
