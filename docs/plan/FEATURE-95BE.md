# FEATURE-95BE — Navigation rail and page-based UI

**Status:** TODO
**Type:** FEATURE (multi-phase — 5 phases)
**Branch (per phase, at build time):** `feature/feature-95be-phaseNN-<slug>`, one branch per phase, cut from the run-branch tip.
**Run:** feature/2026-11-16-page-based-ui

## Objective

The current single window is hard to understand for someone opening it for the first time. It is replaced by
a small navigation rail on the left, as in Enigma.TextEditor, with four pages:

- **Backup** — either a one-off backup (files, folders and patterns chosen on the page) or a backup that uses
  a saved profile (a profile picker with a summary of what it backs up). Two sections, as
  `SettingsCardExpander`s: **Input** and **Output** (output folder and archive options). No profile
  management buttons.
- **Verify & Restore** — deliberately simple: one card per action.
- **Profiles** — profile management: every button currently in the window's top bar, plus the profile editor.
- **Settings** (at the bottom of the rail) — a page like Enigma.Launchpad's. The About dialog opens from here
  and only from here.

The "Last run" panel disappears. A run (backup, restore or verify) is shown in a **content dialog** from
start to finish. The window title is just "Enigma Backup".

## Context & constraints

- **Today.**
  - `MainWindowViewModel` (≈ 920 lines) owns the profile bar and store, the editor
    (`ProfileEditorViewModel`), the three runs, Settings and About.
  - `MainWindow.axaml` has a profile bar (Restore… / Verify… / gear / ⓘ on the right), a two-column body
    (inputs + "Last run") and the output editor plus archive options. The Back up button is at the bottom.
  - A run shows a library `Overlay` with our `ProgressOverlayCard`, through `IProgressOverlay`. Its outcome
    is an `InfoBar` banner (through `INotificationService`) plus the "Last run" panel.
- **The dialog host.**
  - There is one `ContentDialog` host, and `DialogService` holds a one-slot gate while a dialog is open.
  - Mid-run questions are raised **while** the run is on screen: unreadable file, restore conflict, the
    password for an encrypted archive. Raised on the same host, they would wait on the gate forever.
- **The library's `ContentDialog` (1.3.0).**
  - It closes itself on any footer button, on Escape and on a click on its scrim: `CloseDialog` sets
    `IsOpen = false`, then raises `Closed`.
  - The control has its own `ShowAsync()` and `HideAsync()`. Its button texts and commands are styled
    properties, so they can change while it is open.
- **The rail.**
  - Enigma.TextEditor uses `NavigationView` with an icon-only `NavigationItem` `ControlTheme` (PaneSize 44,
    tooltip = header). It shows and hides its pages rather than having `INavigationService` rebuild them.
  - `NavigationItem` is an Avalonia `TemplatedControl`.
- **The Settings page.** Enigma.Launchpad's has a page header (icon tile, title, description), then section
  headers above `SettingsCard`s: Appearance, then data, then About with the version and an "About …" button.
- **CLAUDE.md invariants that move with this work.**
  - Host z-order.
  - "The scrim must always come down".
  - Never await `IInfoBarService.ShowAsync`.
  - `ShellLauncher` is the only service registered under its concrete type too.
  - Every other invariant holds:
    - view models never name Avalonia;
    - the library's services stay inside `Desktop/Services`;
    - dialogs take the focus;
    - the dialogs take turns;
    - passwords are parameters.
- Desktop tests are headless view-model tests only.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| How a run is "displayed in a content dialog" | A second `ContentDialog` host (`HostRunDialog`), driven by a new `IRunDialog` seam (implementation in `Desktop/Services`, handed the control by `App` like `ShellLauncher`), drawn **below** the question host | Mid-run questions must draw above the run and must not wait on the gate the run would hold. It is still the library's dialog, with the same look | The run on the one `HostDialog` (deadlocks with mid-run questions); keeping the overlay card restyled as a dialog (not what was asked) |
| Run dialog lifecycle | Opens at start (progress, current file, percentage, **Cancel**), then switches in place to the outcome (headline, details, left-out entries, warnings, **Close**) | One place for the whole run replaces both the card and "Last run" | Closing at the end and opening a second outcome dialog |
| Closing the dialog while running | Escape and a scrim click are ignored: the seam reopens the dialog in the same call stack, so nothing is drawn in between. Cancel cancels and the dialog stays, showing "Cancelling…" then the cancelled outcome | A stray click must neither abandon a long backup's progress nor cancel it | Treating Escape or the scrim as Cancel |
| The outcome banner | Dropped. `INotificationService`, `NotificationService`, `NotificationSeverity` and the `InfoBar` host are removed, because nothing else uses them. `IInfoBarService` and `IOverlayService` stay registered as part of the library's set, as `INavigationService` already is | The dialog *is* the outcome now. A dead seam rots | Keeping a redundant banner; keeping the seam unused |
| How pages are switched | Show and hide (TextEditor's pattern). The view model exposes a plain `AppPage CurrentPage`. `MainWindow`'s code-behind builds the four `NavigationItem`s and syncs the rail's selection with `CurrentPage` | Page state (the profile being edited, the one-off inputs) survives navigation. `NavigationItem` is an Avalonia control, which no view model may name | `INavigationService` building pages (loses state); `NavigationItem`s in the view model (breaks the invariant) |
| Rail look | TextEditor's icon-only `ControlTheme`, PaneSize 44, tooltip and `AutomationProperties.Name` = the page name. The library's selection highlight is kept. Icons `Archive`, `ClockCounterClockwise`, `FileText`, `Gear` | "Like done in Enigma.TextEditor". The highlight says which page is shown | The library's labelled 90-px rail |
| Page headers | Launchpad's header on every page: icon tile, title, one-sentence description | Each page says what it is for, which is what a newcomer needs | No header |
| Backup modes | Two radio buttons, "Files and folders" and "A profile". The page starts on "A profile" when the store has at least one profile at the first load and the user has not picked yet; otherwise on "Files and folders" | Someone who made profiles means to use them, and someone who has none needs the editor | Always one fixed default; persisting the mode in settings.json |
| Output in profile mode | Shows the profile's output folder and archive options **read-only**, with a hint that they are changed on the Profiles page | Narrowest reading: "a backup using a profile" runs the profile as saved | Editable per-run override (a behaviour nobody asked for, and easy to mistake for editing the profile) |
| One-off backup state | In memory for the session, not persisted. Archives are named `Backup_…` as today | A one-off is a one-off | Persisting it |
| Profile summary | Lead sentence, then each path, each pattern as "`pattern` in `folder`", then the exclude patterns, as last saved | "A summary of what the profile will back up" | A single run-on sentence |
| Unsaved profile edits when leaving the Profiles page | Kept as they are (the page is hidden, not destroyed), with no prompt. The Backup page always runs the saved profile | Nothing is lost, and nothing is run that was not saved | A leave-page prompt |
| Dropped files | Go to the visible page's input list (Backup in "Files and folders" mode, or Profiles). Refused elsewhere (`DragDropEffects.None`) | Today they go to the one editor. The page that shows a list is the one a drop means | Always the Backup page |
| Settings | A `SettingsPage` like Launchpad's: Appearance (theme), Storage (folder + Open folder), About (name, version, "About Enigma Backup" button). The Settings dialog and `IDialogService.ShowSettingsAsync` are removed | As asked. About opens from this page only | Keeping the dialog |
| Window title | The constant "Enigma Backup", with no dirty marker. The Profiles page's status line still says "Unsaved changes." | As asked | — |
| Shared run state | A singleton `RunCoordinator` (ViewModels): `IsBusy`, the cancellation source, the run dialog and run logging. Used by the Backup and Verify & Restore pages | One run at a time across pages, and one place that guarantees a run ends closable | Duplicating `RunAsync` per page |
| Shared profile state | A singleton `ProfileCatalog` (ViewModels): the stored profiles and their names, plus `ReloadAsync` with today's error handling. Used by the Backup and Profiles pages | A save on Profiles must show up on Backup's picker | Each page reading the store |
| "Add pattern…" | Moves onto `ProfileEditorViewModel`, which takes `IDialogService` | The shared input editor view must be self-contained, and there is no window-level owner any more | Keeping it on a page view model |
| Window size | Pages scroll vertically, so the window's minimum height drops (to about 600). The width stays | The 780-px minimum existed only because nothing scrolled | Unchanged |
| Phasing | Five phases, each leaving a working app: run dialog → rail and Verify & Restore → Settings → Profiles and one-off Backup → profile Backup | Each is one reviewable commit | One big dev |

---

## PHASE01 — Runs in a content dialog

**Status:** TODO

### Steps

1. **`ViewModels/RunViewModel.cs`** (`DialogViewModel`): the run dialog's state.
   - `Title`, `Message`, `CurrentItem`, `Percent`, `IsIndeterminate`.
   - `IsRunning`, `IsCancelling`.
   - `Summary` (`RunSummary?`) and `HasSummary`.
   - `CancelCommand` (enabled while running and not cancelling).
   - `CloseCommand` (Enter, once finished).
   - `Report(...)` and `Finish(RunSummary)`.
2. **`Services/IRunDialog.cs`** and **`Services/RunDialog.cs`**.
   - The interface: `Task ShowAsync(RunViewModel run)` and `Task CloseAsync()`.
   - The implementation is handed the window's `ContentDialog` (`SetHost`) by `App`, and is registered as
     itself plus the interface (one instance, as `ShellLauncher` is).
   - While `run.IsRunning`:
     - the footer has only a **Cancel** button, bound to `run.CancelCommand`;
     - any other close (Escape, scrim) reopens the dialog at once;
     - Cancel's own close also reopens it, because the run is still finishing.
   - When `IsRunning` turns false, the footer becomes **Close** (default button). Any close then ends the
     dialog.
   - The scrim is `EnigmaOverlayBrush`.
3. **`Views/RunView.axaml(.cs)`**: the dialog's content.
   - While running: progress bar, percentage, message, current file.
   - Once finished: headline (coloured by severity), details grid, left-out list and warnings list —
     today's "Last run" markup.
   - Takes the focus on attach.
4. **`MainWindowViewModel.RunAsync`**
   - Builds a `RunViewModel` and shows it.
   - Progress goes to the run view model through the single `ReportProgress`.
   - The `finally` always calls `run.Finish(summary)` (a `Failed` summary for anything unexpected). That is
     the new guarantee that a run ends closable.
   - `Summary` / `HasSummary` and the "Last run" bindings go. `StatusText`, `ProgressPercent`, `CurrentItem`
     and `IsProgressIndeterminate` go if nothing else reads them.
5. **`MainWindow.axaml`**
   - Delete the "Last run" panel; the inputs panel takes the full width.
   - The root `Panel` ends with `HostRunDialog` (ContentDialog) → `HostDialog`.
   - The `Overlay` and `InfoBar` hosts go; `App` stops registering them.
6. **Remove**:
   - `IProgressOverlay`, `ProgressOverlay`, `ProgressOverlayCard(.axaml)` and its `App.axaml` include;
   - `INotificationService`, `NotificationService`, `NotificationSeverity`;
   - their fakes and tests.
   - Update the comment in `EnigmaAvaloniaServiceCollectionExtensions` (overlay and info-bar services
     registered for completeness, unused).
7. **Tests**
   - The fake `IRunDialog` records each `RunViewModel`.
   - Rewrite the overlay/banner tests as run-dialog tests:
     - one dialog per run;
     - it is told every progress report;
     - Cancel stops the run and the dialog ends with the cancelled summary;
     - an unexpected failure still finishes the run (closable, `Failed` summary);
     - a run that never starts shows nothing.
   - `RunViewModel` unit tests: command states and `Finish`.
   - The run tests and the end-to-end test read the summary from the recorded run.
   - DI: `IRunDialog` and `RunDialog` resolve to one instance.

### Acceptance criteria

- A backup, restore or verify shows one content dialog: progress and Cancel while it runs, then the outcome
  and Close. Nothing else reports the outcome. There is no "Last run" panel and no banner.
- A mid-run question (unreadable file, restore conflict, password) appears **above** the run dialog and can
  be answered.
- Escape or a click outside the dialog during a run does not close it. Cancel stops the run and the dialog
  then shows the cancelled outcome.
- Build has zero warnings (Debug and Release). Whole suite green, new tests included.

## PHASE02 — Rail and Verify & Restore page

**Status:** TODO

### Steps

1. **`ViewModels/AppPage.cs`** (`Backup`, `VerifyRestore`, `Profiles`, `Settings`).
2. **`ViewModels/RunCoordinator.cs`** (singleton)
   - Takes `RunAsync`, `ReportProgress`, cancellation, `IsBusy` and run logging out of
     `MainWindowViewModel`, unchanged in behaviour.
   - Returns the summary.
   - The three `Apply*Progress` formatters move with it, or onto the page that owns the run.
3. **`ViewModels/VerifyRestorePageViewModel.cs`**: today's `OnVerifyAsync` / `OnRestoreAsync` (pickers, the
   password retry), run through the coordinator. Commands disabled while busy.
4. **`ViewModels/BackupPageViewModel.cs`**: what is left of `MainWindowViewModel` — profile bar, editor,
   Back up, Settings and About commands. It moves verbatim, so the page looks as it does today minus
   Restore… and Verify….
5. **`MainWindowViewModel`** becomes the shell:
   - `CurrentPage` (default `Backup`) and the `Is*Page` flags;
   - the page view models as properties;
   - `RefreshProfilesCommand` forwarded to the Backup page;
   - `AddDroppedInputs` forwarded;
   - no `Title` property.
6. **Views**
   - `Views/Pages/BackupPage.axaml` (today's window content, minus Restore… / Verify…).
   - `Views/Pages/VerifyRestorePage.axaml`: header, then two `SettingsCard`s ("Verify an archive",
     "Restore an archive"), each with a button.
   - Page header styles (Launchpad's `pageIcon` / `pageTitle` / `pageDescription` / `sectionHeader`) in the
     window's styles.
7. **`MainWindow.axaml(.cs)`**
   - `Title="Enigma Backup"`.
   - `DockPanel`: `NavigationView` (PaneSize 44, TextEditor's icon-only item theme) docked left, then a
     `Panel` of the pages, each `IsVisible` by its flag.
   - The code-behind builds the rail's items and syncs the selection both ways with `CurrentPage`.
8. **Rail items in this phase**: **Backup** and **Verify & Restore** only. The Profiles and Settings items
   are added by the phases that build their pages, so the rail never leads to a placeholder.
9. **DI**
   - Pages' view models are singletons (state survives navigation); views are created by the window.
   - `RunCoordinator` is a singleton.
10. **Tests**
    - Shell: default page, `CurrentPage` flags, the drop forwarded.
    - Coordinator: today's overlay/run behaviour moved, so the existing tests are re-pointed.
    - Verify & Restore page: the existing restore/verify tests are re-pointed.
    - DI: every page view model resolves.
    - End-to-end: it drives the pages.

### Acceptance criteria

- The window title is "Enigma Backup". A rail on the left switches between Backup and Verify & Restore.
  The highlight shows the current page, and the tooltips name the pages.
- Verify & Restore runs its two actions in the run dialog. The Backup page no longer shows Restore… or
  Verify….
- Build has zero warnings. Whole suite green.

## PHASE03 — Settings page

**Status:** TODO

### Steps

1. **`ViewModels/SettingsPageViewModel.cs`**
   - Today's `SettingsDialogViewModel` logic: theme options, the stored theme applied and saved, the
     storage folder and Open folder.
   - Plus `Version` (`AppInfo.Version`) and `OpenAboutCommand` (through `IDialogService.ShowAboutAsync`).
   - Not a `DialogViewModel`.
2. **`Views/Pages/SettingsPage.axaml`**: Launchpad's layout (header; a capped-width column; Appearance →
   Theme; Storage → folder + Open folder; About → "Enigma Backup", "Version x" and an
   "About Enigma Backup" button).
3. **Remove**
   - `SettingsDialog(.axaml)`, `SettingsDialogViewModel`, `IDialogService.ShowSettingsAsync` and the
     `DialogService` arm.
   - The gear and ⓘ buttons and the Backup page's `OpenSettingsCommand` / `OpenAboutCommand`.
4. **Rail**: the Settings item is in the footer.
5. **Tests**
   - Move the settings dialog tests to the page view model: theme applied and saved once, storage
     folder, open folder.
   - About is shown through the dialog service.
   - Shell: `AppPage.Settings`.

### Acceptance criteria

- Settings is a page at the bottom of the rail, laid out like Launchpad's. The theme switch applies at once
  and persists. Open folder works.
- About opens from the Settings page only. There is no gear or ⓘ anywhere else.
- Build has zero warnings. Whole suite green.

## PHASE04 — Profiles page and single backup

**Status:** TODO

### Steps

1. **`ViewModels/ProfileCatalog.cs`** (singleton)
   - `Names` (`ObservableCollection<string>`), `TryGet(name, out profile)`, `ReloadAsync()` (today's
     `ReloadStoreAsync`, error dialogs included) and a `Reloaded` event.
2. **`ProfileEditorViewModel`** takes `IDialogService` and gains `AddPatternCommand`; it moves off the
   window.
3. **Shared editor views** (x:DataType `ProfileEditorViewModel`)
   - `Views/InputsEditor.axaml`: the inputs list, Add files / Add folder / Add pattern / Remove /
     Remove all, the drop hint, and the exclude patterns.
   - `Views/OutputEditor.axaml`: the output folder editor, Compression and Encrypt cards.
4. **`ViewModels/ProfilesPageViewModel.cs`**
   - The profile bar and every profile command (New, Save, Save as…, Rename…, Delete…, Import…,
     Export…), moved from the Backup page.
   - Its own `ProfileEditorViewModel`, `SelectedProfileName` and `ProfileStatus`.
   - Store access goes through the catalog.
5. **`Views/Pages/ProfilesPage.axaml`**: header; a card with the combo, the status and the action buttons
   (wrapping); then an **Input** expander (`InputsEditor`) and an **Output** expander (`OutputEditor`).
6. **`BackupPageViewModel`** loses every profile concern. It holds its own one-off `ProfileEditorViewModel`
   (`Single`) and `BackupCommand` (archives named `Backup_…`).
7. **`Views/Pages/BackupPage.axaml`**: header; an **Input** expander titled "Files, folders and patterns to
   back up" (`InputsEditor`); an **Output** expander (`OutputEditor`); the accent **Back up** button.
8. **Rail**: the Profiles item. The window's first catalog load happens on `Opened`. Drops go to the visible
   page's editor (Backup or Profiles); elsewhere `DragDropEffects.None`.
9. **Tests**
   - Move the profile tests to `ProfilesPageViewModel`.
   - Catalog: reload, an unreadable file, a store that cannot be listed.
   - The editor's Add pattern.
   - Backup page: one-off backup, `CanExecute`.
   - Drop routing.
   - DI.
   - End-to-end: save a profile on the Profiles page.

### Acceptance criteria

- The Profiles page manages profiles with all seven buttons and edits their inputs, excludes, output and
  options.
- The Backup page has no profile buttons. It runs a one-off backup from its Input and Output sections.
- Dropped files land in the visible page's list.
- Build has zero warnings. Whole suite green.

## PHASE05 — Backup with a profile

**Status:** TODO

### Steps

1. **`BackupPageViewModel`**
   - `UseProfile` (bool, two radio buttons).
   - `SelectedProfileName` over the catalog's names.
   - A `FromProfile` `ProfileEditorViewModel`, loaded from the stored profile on selection.
   - `ActiveEditor` (the one-off editor or `FromProfile`).
   - Summary: `SummaryLead`, `SummaryItems`, `SummaryExcludes`, `HasProfiles`.
   - `BackupCommand` runs `ActiveEditor.ToProfile(name)`, where `name` is the profile's name, or "Backup"
     for a one-off. It can execute when not busy and either (one-off) inputs and an output folder are
     set, or (profile) a stored profile is selected and has an output folder.
   - Default mode: "A profile" when the first catalog load finds at least one profile and the user has not
     chosen yet.
   - On `Reloaded`: keep the selection if it still exists, otherwise clear it; refresh `FromProfile` and
     the summary.
2. **`Views/Pages/BackupPage.axaml`**
   - Input expander: the mode radio buttons; then either `InputsEditor` (one-off) or the profile
     `ComboBox` plus the summary. With no profiles, a hint to create one on the Profiles page.
   - Output expander: `OutputEditor` bound to `ActiveEditor`, `IsEnabled` = one-off. In profile mode it
     shows the hint "Set by the profile — change it on the Profiles page."
3. **Drops**: accepted on the Backup page only in one-off mode.
4. **Tests**
   - Mode default, with and without profiles.
   - Summary contents: paths, patterns, excludes, none.
   - The profile backup hands the core the stored profile, under its name.
   - `CanExecute` per mode.
   - Reload keeps or clears the selection.
   - Drop refused in profile mode.
   - End-to-end: back up through the profile mode.

### Acceptance criteria

- The Backup page offers a one-off backup (Input = files, folders and patterns) or a profile backup
  (Input = profile picker + summary). Output shows the output folder and archive options. In profile mode
  they are the profile's and are read-only.
- Back up is clickable only when the chosen mode has what it needs. The run shows in the run dialog.
- Build has zero warnings. Whole suite green.

## Out of scope

- Persisting the Backup page's mode or its one-off inputs.
- Per-run overrides of a profile's output.
- Showing the invisible `StatusText` messages (pre-existing: nothing displays them).
- A release (version bump, release notes) — a separate item.
- Changes to the core, the profile format, `settings.json` or the `EBK1` container.
