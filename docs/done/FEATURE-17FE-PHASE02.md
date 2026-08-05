# FEATURE-17FE-PHASE02 — Dialogs & pickers on the library services

**Status:** DONE · phase 2 of 5 · branch `feature/feature-17fe-phase02-dialogs-pickers`

## Summary

The app no longer opens a window to ask a question. All five dialogs are now `UserControl`s shown in
the single `ContentDialog` host that sits in `MainWindow`'s root `Panel`, and both file pickers go
through the control library's `IFileDialogService` / `IFolderDialogService`. `IDialogService` and
`IFilePicker` — the interfaces the view models see — did not change, which is why not one ViewModel
test was touched.

What moved:

- **`Hosting/EnigmaAvaloniaServiceCollectionExtensions.cs`** registers the library's six singletons
  (the package ships no such extension) and `AddEnigmaBackupDesktop` calls it.
- **`MainWindow.axaml`** wraps the app's `Grid` in a `Panel` and ends with
  `Overlay` → `ContentDialog` → `InfoBar`, in that order, with the reason written next to them. The
  dialog host carries the six size properties, because the service does not reset them between
  dialogs.
- **`App.axaml.cs`** hands over the three hosts and the window's storage provider before the window is
  shown, and `MainWindowProvider` is gone: nothing needs the window any more.
- **`DialogService`** raises each question through `IContentDialogService.ShowAsync(configure)`, maps
  the answers onto the dialog's own Primary/Secondary/Close buttons, and reads the result off the view
  model exactly as before — so `DialogResult.None` from Escape or a scrim click needs no new logic.
  Password fields are cleared once the result has been read.
- **`FilePicker`** uses the string-path extension overloads, keeping both `FilePickerFileType`
  filters, the export suggestion and extension, the overwrite prompt and the start-location hint.

Two things the plan did not foresee, both found by driving the real UI (see *Deviations*): a dialog
with nothing to type in was not taking the keyboard focus, and the dialog's own buttons ignore a
command's `CanExecute`.

## Files/modules touched

**Created**

- `src/Enigma.Backup.Desktop/Hosting/EnigmaAvaloniaServiceCollectionExtensions.cs` —
  `AddEnigmaAvaloniaDesktop()`, `TryAddSingleton` for each of the library's six services.
- `docs/done/FEATURE-17FE-PHASE02.md` — this file.

**Modified**

- `src/Enigma.Backup.Desktop/Hosting/ServiceCollectionExtensions.cs` — calls the new extension; the
  two `MainWindowProvider` registrations are gone.
- `src/Enigma.Backup.Desktop/App.axaml.cs` — the three `RegisterHost` and two `SetStorageProvider`
  calls, with a comment on why they cannot be skipped.
- `src/Enigma.Backup.Desktop/Views/MainWindow.axaml` — the root `Panel`, the three hosts, the z-order
  comment. The app's `Grid` was re-indented by two spaces when it moved inside the `Panel`; the real
  change is 30 lines (`git diff -w`).
- `src/Enigma.Backup.Desktop/Services/DialogService.cs` — rewritten over `IContentDialogService`.
- `src/Enigma.Backup.Desktop/Services/FilePicker.cs` — rewritten over the two picker services.
- `Views/PasswordDialog.axaml(.cs)`, `Views/ProfileNameDialog.axaml(.cs)`,
  `Views/ErrorDecisionDialog.axaml(.cs)`, `Views/RestoreConflictDialog.axaml(.cs)`,
  `Views/MessageDialog.axaml(.cs)` — `Window` → `UserControl`, buttons removed from the content (they
  are the dialog's now), an Enter `KeyBinding` on each content root, and focus taken on attach.
  Wording, bindings and `x:DataType` otherwise unchanged.
- `tests/Enigma.Backup.Desktop.Tests/Hosting/ServiceCollectionExtensionsTests.cs` — the window-provider
  test is replaced by one asserting the library's six services resolve to one instance each, and one
  asserting nothing holds the main window any more.
- `docs/roadmap.md`, `docs/plan/FEATURE-17FE.md` — statuses.
- `CLAUDE.md` — from the documentation freshness sweep: the Desktop seam list now records that the
  library's services are consumed only from `Desktop/Services/` and wired once in `App`, that the host
  z-order is load-bearing, and that an in-window dialog has to be given the focus. The
  `docs/QA-CHECKLIST.md` rows that still describe a title-bar ✕ were offered and deliberately left to
  PHASE05, which rewrites that file.

**Deleted**

- `src/Enigma.Backup.Desktop/Services/IMainWindowProvider.cs`,
  `src/Enigma.Backup.Desktop/Services/MainWindowProvider.cs`.

**Untouched, as the plan expected** — all of `src/Enigma.Backup.Core/`, all of
`tests/Enigma.Backup.Core.Tests/` (including `Fixtures/`), every Desktop **ViewModel** test,
`Harness.cs`, `FakeDialogService.cs`, `FakeFilePicker.cs`, `publish.sh`, `publish.ps1`, `global.json`,
`.editorconfig`.

## Deviations & follow-ups

- **A dialog with no input box was not taking the focus — found, and fixed.** `ContentDialog` handles
  Escape in its own `OnKeyDown`, and the content's Enter `KeyBinding` runs when the event bubbles
  through it; both need the keyboard focus to be *inside* the dialog. The password and profile-name
  dialogs focus their box, so they were fine, but the error, conflict and message dialogs left the
  focus wherever the main window had it — so Enter and Escape did nothing, and worse, keys would have
  reached the window **behind the scrim**. A real `Window` gave this for free; an in-window dialog does
  not. Those three roots are now `Focusable="True"` and take the focus on attach, posted at
  `DispatcherPriority.Loaded` because the content is attached while the host is still collapsed and an
  invisible control cannot be focused. Both keys are asserted per dialog in the verification run.
- **The dialog's buttons ignore `CanExecute`.** `OnPrimaryButtonClick` is
  `PrimaryButtonCommand?.Execute(null)` — no `CanExecute` check — and it closes the dialog either way.
  The two dialogs that validate what was typed would therefore have accepted an invalid answer from a
  button that still looked enabled. `DialogService` now drives `IsPrimaryButtonEnabled` from the view
  model's `CanAccept` (subscribed for the life of the dialog, not bound, so nothing outlives it), and
  the command wrapper checks `CanExecute` as a second line of defence.
- **Closing the dialog twice had to be prevented explicitly.** A button click runs its command *then*
  calls `CloseDialog`, and the view models raise `CloseRequested` from those commands. Letting that
  event also call `HideAsync()` would close the dialog, resolve the awaiting question, let the caller
  open the *next* dialog — and only then would the first click's `CloseDialog` run, handing the new
  dialog the old result. The `closingItself` flag in `DialogService.ShowAsync` is what stops it; the
  comment says so. The cancellation registration is guarded the same way, because the host is shared.
- **Button order and layout are the library's.** The template lays out Primary → Secondary → Close from
  left to right and styles Primary `accent`, so *OK* now sits left of *Cancel* (it was the other way
  round), and *Skip* / *Keep the file* lead their rows. Wording is unchanged.
- **Two headings lost their colour until PHASE03.** `MessageDialog` and `ErrorDecisionDialog` used to
  repeat their window title inside the content, in `EnigmaErrorBrush`/`EnigmaWarningBrush`; that
  duplicate is gone now the dialog has a real `Title`, so the severity is currently carried by wording
  alone. PHASE03 step 3 puts warning/error icons on the dialog headers, which is where that signal
  belongs. `RestoreConflictDialog` keeps its coloured heading, because it says something the title does
  not ("This file is already on disk" vs "The file already exists"), and
  `MessageDialogViewModel.IsError` / `.IsWarning` are kept for that phase.
- **`MessageDialog`'s `MaxHeight="320"` is gone**, replaced by the host's `DialogMaxHeight="560"`: the
  card bounds and scrolls its own content, and two competing limits would fight.
- **The five dialog Views keep their file names** (`PasswordDialog.axaml`, …) even though they are no
  longer windows — the plan lists them as modified, not replaced, and renaming would have buried the
  real diff. Note that PHASE04 introduces `SettingsDialogView`, so the suffix will be inconsistent;
  worth a rename pass in PHASE03 or PHASE04 if it grates.
- **`INavigationService` is registered and unused.** The plan says six services; this app has no
  `NavigationView` and never will (single window, by decision). It costs one descriptor and the
  comment in the extension says why it is there.
- **No line-ending problems.** Every file touched is LF throughout; the diff carries no CRLF↔LF churn.
- **The one criterion no tooling here can answer:** clicking *Open* inside the platform's own file
  picker. This machine has no `xdotool`/`wmctrl`/`ydotool`, so the picker was opened for real and
  photographed (below), and everything on our side of it — options, filters, titles, the returned
  paths — was checked against the real storage provider. The click itself is left for the PHASE05 QA
  pass.

## Build/test evidence

- **Debug build:** `dotnet build Enigma.Backup.slnx -c Debug --no-incremental` → `Build succeeded`,
  **0 warnings, 0 errors**.
- **Release build:** same with `-c Release` → `Build succeeded`, **0 warnings, 0 errors**.
- **`AVLN*` warnings: 0** in both configurations, counted by grepping the captured logs — a green
  summary line does not rule them out.
- **Tests:** `dotnet test --solution Enigma.Backup.slnx` → **355 passed, 0 failed, 0 skipped** (354
  before; the window-provider test became two). **The only test file touched is the DI one** — no
  ViewModel test was edited, which was the acceptance criterion and the plan's stated tripwire.
- **The manual criteria were driven, not eyeballed.** A throwaway harness in the session scratchpad
  (not part of the solution, no repository file added) references the **built
  `Enigma.Backup.Desktop` assembly** and runs the real `App`, the real `MainWindow` with its three
  hosts, the real `DialogService` and the real `FilePicker`, with the same five wiring calls
  `App.axaml.cs` makes. It runs in two modes because no single one can do both jobs:
  - **Headless (`Avalonia.Headless` 12.1.1 + Skia), driving Avalonia's real input pipeline —
    12/12 checks:** the three hosts are the last children of one `Panel` in overlay → dialog → infoBar
    order and are invisible while closed; the password dialog opens in-window with its box focused and
    *OK* greyed until a password is typed; **Enter** accepts (and the plaintext is gone from the visual
    tree afterwards); **Escape** and a **click on the scrim** both cancel; the dialog's own **OK** and
    **Cancel** buttons answer (real clicks, hit-tested before they are sent); a new password waits for
    the confirmation to match and says so; the profile-name box starts selected and refuses a name
    already taken, then accepts once it is free; Skip/Retry/Abort map onto the error dialog's three
    buttons with the ticked box carried, and a dismissal aborts; the conflict dialog keeps the file on
    Enter, overwrites with the box ticked, and aborts on a scrim click; a message has one button and a
    confirmation two, with a dismissal declining; **cancelling the run closes the question that is open
    and it aborts**; and **a question raised while the run overlay is open is above the scrim and its
    button is clickable** — the plan's headline z-order risk, exercised rather than assumed.
  - **X11 on `DISPLAY=:1`, for the pickers and the screenshots — 5/5 checks:** the platform hands out
    real storage items; adding files asks for many and comes back with usable paths; the folder picker
    opens at the destination and **drops a relative hint instead of throwing** (Avalonia's own
    `TryGetFolderFromPathAsync` refuses one, and the destination box holds whatever was typed); the
    archive picker keeps all five patterns plus *All files*; the profile pickers keep the JSON filter,
    MIME type, suggested name, `json` extension and overwrite prompt; and a picker asked before the
    window exists answers nothing instead of throwing.
- **Screenshots, both variants** (`shots/` in the scratchpad): the window itself, then the password
  prompt, the profile-name prompt, the unreadable-file question, the restore-conflict question and a
  confirmation — each in **Dark** and **Light**. All five draw as in-window cards over the scrim, with
  the app's panels visible behind them; the password box shows its focus ring; the conflict heading
  reads `EnigmaWarningBrush` in both variants; the window itself is unchanged from PHASE01 because the
  three hosts are invisible while closed.
- **The real platform picker was opened through the shipping `IFilePicker`** and photographed
  (`dark-archive-picker.png`): the KDE portal dialog is titled *Choose the archive to restore* with
  *File type: Enigma.Backup archives* — the app's own title and `FilePickerFileType` reaching the
  platform through the library's service.
- **The user's real configuration was not touched.** The harness registers its own `IProfileStore` in a
  temp directory and runs under a redirected `XDG_CONFIG_HOME`; `~/.config/Enigma.Backup` still shows
  its 2026-08-01 timestamp and July log. No stray process was left behind.

Acceptance criteria for PHASE02 — build clean with zero `AVLN*` warnings, the full suite green with no
existing ViewModel test changed, the DI test asserting the six library services and the retired
provider, and in the running app every dialog opening in-window with OK/Cancel, Enter, Escape, the
scrim, mid-run cancellation and all five pickers behaving — are met, with the one exception recorded
above (the click inside the platform picker, deferred to the PHASE05 QA pass).
