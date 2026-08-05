# FEATURE-17FE-PHASE03 — Main-window restyle

**Status:** DONE · phase 3 of 5 · branch `feature/feature-17fe-phase03-window-restyle`

## Summary

The window is now drawn with the control library's controls, and a run reports itself through two new
seams instead of through a bar at the bottom of the window.

- **The run moved onto the overlay.** `IProgressOverlay` + `ProgressOverlay` raise a
  `ProgressOverlayCard` (ours — the library ships none) over the whole window, carrying the heading,
  the percentage, the core's own status line, the file being read and **Cancel**. The window's
  `ProgressBar` and its two status `TextBlock`s are gone, and so is the action row's Cancel button:
  behind the scrim it could not be clicked.
- **The outcome is a banner.** `INotificationService` + `NotificationService` map the `RunSummary` onto
  the library's `InfoBar` — success → `Success`, anything left out or cancelled → `Warning`, a failure
  → `Error` — with the summary's own headline as the message. The next run takes it away. The "Last run"
  panel is untouched and still holds the archive path, the sizes, the durations and the per-file lists.
- **The form rows are the library's.** "Save backups in" is a `TextEditor` with a `HardDrives` glyph in
  its leading slot and *Browse…* in its action slot; compression and encryption are two `SettingsCard`
  rows inside a `SettingsCardExpander`; the exclude-glob box is a `TextEditor` with *Add* in its action
  slot, Enter still bound. Both `ListBox`es are framed in `EnigmaSurfaceLow` / `EnigmaBorderSubtle`.
- **Icons across the window and the dialog headers.** Every profile, input and action command carries a
  Phosphor glyph, as do the four section headings, and `DialogService` now sets each dialog's
  `IconData` / `IconBrush` — `LockKey` for a password, `PencilSimple` for a name, `Warning` in
  `EnigmaWarningBrush` for both decisions and for a confirmation, `WarningCircle` in `EnigmaErrorBrush`
  for a failure, `Info` for a plain message. That restores the severity signal PHASE02 dropped when the
  dialogs lost their in-content headings.

`MainWindowViewModel` gained the two seams and one `ReportProgress` method that sets its own progress
state and the card together. No other view model changed, and **no existing ViewModel test's
expectations changed**.

## Files/modules touched

**Created**

- `src/Enigma.Backup.Desktop/Services/IProgressOverlay.cs`, `Services/ProgressOverlay.cs` — the run
  card seam and its `IOverlayService`-backed implementation. `HideAsync` clears the card in a `finally`.
- `src/Enigma.Backup.Desktop/Services/INotificationService.cs`, `Services/NotificationService.cs`,
  `Services/NotificationSeverity.cs` — the outcome banner seam, its `IInfoBarService`-backed
  implementation, and the three-value severity that keeps `InfoBarSeverity` out of the view models.
- `src/Enigma.Backup.Desktop/Views/ProgressOverlayCard.cs` + `Views/ProgressOverlayCard.axaml` — the
  card (`ContentControl` + styled properties) and its `ControlTheme`, merged from `App.axaml` after the
  library's dictionary so it reads the same `Enigma*` brushes.
- `tests/Enigma.Backup.Desktop.Tests/TestSupport/FakeProgressOverlay.cs`,
  `TestSupport/FakeNotificationService.cs` — recording fakes: shows, hides, every report, every banner.
- `tests/Enigma.Backup.Desktop.Tests/ViewModels/MainWindowViewModelOverlayTests.cs` — 10 tests over the
  two new seams.
- `docs/done/FEATURE-17FE-PHASE03.md` — this file.

**Modified**

- `src/Enigma.Backup.Desktop/Views/MainWindow.axaml` — the restyle: `TextEditor`s, the
  `SettingsCardExpander` and its two `SettingsCard`s, icons on every command and heading, framed lists,
  the bottom panel reduced to the three action buttons, `EnigmaOverlayBrush` on the overlay host, and a
  larger default/minimum window. The three hosts and their z-order comment are unchanged.
- `src/Enigma.Backup.Desktop/App.axaml` — one more `ResourceInclude`, for the card's control theme.
- `src/Enigma.Backup.Desktop/ViewModels/MainWindowViewModel.cs` — two injected seams; `RunAsync` takes
  the card's heading, hides the previous banner, raises the card, and in its `finally` takes the card
  down and publishes the outcome; `ReportProgress` writes the four progress properties and the card in
  one place; the three `Apply*Progress` methods and `OnCancel` go through it.
- `src/Enigma.Backup.Desktop/Hosting/ServiceCollectionExtensions.cs` — the two new singletons.
- `src/Enigma.Backup.Desktop/Services/DialogService.cs` — the header icon and brush per dialog, plus two
  private helpers (`Glyph`, `Brush`).
- `tests/Enigma.Backup.Desktop.Tests/TestSupport/Harness.cs`,
  `tests/Enigma.Backup.Desktop.Tests/Integration/WindowEndToEndTests.cs` — the two fakes registered.
- `tests/Enigma.Backup.Desktop.Tests/Hosting/ServiceCollectionExtensionsTests.cs` — the two seams
  resolve to their implementations, and to one instance each.
- `docs/roadmap.md`, `docs/plan/FEATURE-17FE.md` — statuses.
- `CLAUDE.md`, `README.md` — from the documentation freshness sweep. `CLAUDE.md` records the second
  `ResourceInclude` and why it is merged last, the two new seams in the view-model invariant, that the
  scrim must always come down from `RunAsync`'s `finally`, and that `IInfoBarService.ShowAsync` must
  never be awaited. `README.md`'s *Using it* steps 5–7 now describe the *Archive options* group, the
  switch, the run card and the outcome banner. `docs/QA-CHECKLIST.md` was offered and deliberately left
  to PHASE05, which rewrites it.

**Untouched, as the plan expected** — all of `src/Enigma.Backup.Core/`, all of
`tests/Enigma.Backup.Core.Tests/` (including `Fixtures/`), the five dialog Views and their view models
(only the headers the *service* sets changed), `ProfileEditorViewModel`, `FakeDialogService.cs`,
`FakeFilePicker.cs`, every existing ViewModel test's expectations, `publish.sh`, `publish.ps1`,
`global.json`, `.editorconfig`.

## Deviations & follow-ups

- **The four progress properties stayed on the view model.** The plan's decision that "two live
  progress readouts would be duplicate state" is about the *window*, which does lose its bar and its
  two status lines. `StatusText`, `CurrentItem`, `ProgressPercent` and `IsProgressIndeterminate` are
  still the run's state — they are what the card is filled from, and `StatusText` is still what the
  window falls back to once a run ends. Deleting them would have rewritten five existing ViewModel
  tests, which the plan names as its own tripwire. The single `ReportProgress` method is what stops the
  card and the properties from drifting: nothing sets one without the other.
- **`Harness` and the end-to-end `Fixture` both gained the two fakes.** The plan says `Harness` gains
  them; `WindowEndToEndTests.Fixture` needs them for the same reason (it resolves the *real*
  `MainWindowViewModel` from the shipping container, and the real overlay reaches for a host control
  that only exists once a window has been shown). That is wiring, not expectations — no assertion in
  either file changed.
- **The show/hide pairing is in the view model's one `RunAsync`, not only in the implementation.** The
  plan's risk note asks for `try`/`finally` "in the implementation, not at the call sites". The seam it
  also specifies is a plain show/update/hide, so the pairing cannot live entirely behind it; what it
  does instead is the next best thing — all three operations funnel through a single `RunAsync` whose
  pre-existing `finally` hides the card, and `ProgressOverlay.HideAsync` is itself idempotent and
  clears its card in a `finally`. `ARun_ThatFailsUnexpectedlyStillTakesTheCardDown` is the test, and the
  verification run photographs a thrown run leaving no scrim behind.
- **`IInfoBarService.ShowAsync` completes when the banner is *dismissed*, not when it is shown.**
  `InfoBar.ShowAsync` returns a `TaskCompletionSource` resolved by the `Closed` event. Awaiting it in
  `NotificationService` would have left every run's `finally` hanging until the user clicked the banner
  away. It is deliberately not awaited — but it *is* handed back when it has already completed, which
  is how a host that was never registered still surfaces as an exception rather than as silence. The
  comment in the file says so, because the obvious "fix" is to await it.
- **The window grew, and the layout had to be compacted to fit the expander.** A
  `SettingsCardExpander` with two cards is much taller than the two-row grid it replaced, and the first
  run of the app showed the left panel's `ListBox` drawn *over* its own heading — a `Grid` given less
  room than its children need overlaps rather than clips. Four changes together fixed it, all verified
  by screenshot at both the default and the minimum size: the default window is `1120×860` and the
  minimum `960×780` (was `1080×780` / `900×660`); the two `SettingsCard`s sit side by side rather than
  stacked; the inputs `ListBox` lost its `MinHeight`, so the star row is what gives when the window
  shrinks; and the drag-and-drop hint and the encryption description were shortened to one line each.
  Worth knowing: the *previous* layout had the same failure mode at its own minimum height — this is
  now checked rather than assumed.
- **The overlay's scrim is the theme's, not the control's.** `Overlay.OverlayBrush` defaults to a fixed
  `#4D000000`; the host now binds `EnigmaOverlayBrush`, which is the theme's modal-scrim key and differs
  per variant (`#80000000` Dark, `#40000000` Light). In Dark the card reads markedly better for it.
  The `ContentDialog` host keeps its own default, so a dialog raised over an open overlay stacks two
  scrims — that is what the screenshot shows and it looks intentional, but it is worth remembering.
- **`IsExpanded="True"` on the expander.** Compression and encryption both decide what the next run
  produces — and whether an archive is encrypted is not something to hide behind a chevron in a backup
  app. The expander is there for the grouping and the frame, which the reference documents as a
  supported use.
- **Encryption is a `ToggleSwitch` now, not a `CheckBox`.** Its wording moved to the card's `Header` and
  its tooltip to the card's `Description`; the text is otherwise unchanged.
- **`MessageDialogViewModel.IsError` / `.IsWarning` are in use again**, which is what PHASE02 kept them
  for: they choose the message dialog's header icon and brush.
- **The `InfoBar` draws at the top of the window and covers the profile bar while it is open.** That is
  the library's own placement (`VerticalAlignment="Top"`, 24px margin, stretched). It closes with its
  own ✕ or when the next run starts. If it grates in use, the options are bottom alignment (which would
  cover the action row instead) or an auto-dismiss timer in `NotificationService`; neither is in this
  phase's plan, so neither was done.
- **Icon names were verified against the pack, not guessed.** `Enigma.Icons.Phosphor` carries its own
  name table; every glyph used here was checked against it before being written into XAML. `PathIcon`
  (not a bare `Path`) is used throughout, per the comment PHASE01 left: the markup extension supplies
  the geometry and the control inherits the brush, so a glyph follows the accent button's white and
  every hover state for free.
- **`ProgressOverlayCard` is a `ContentControl`, as the plan says, but its `Content` slot is not
  presented.** Everything the card shows is one of its styled properties; there is nothing arbitrary to
  place. The showcase's card in the library repo is built the same way.
- **No line-ending problems.** Every file touched is LF throughout; the diff carries no CRLF↔LF churn.

## Build/test evidence

- **Debug build:** `dotnet build Enigma.Backup.slnx` → `Build succeeded`, **0 warnings, 0 errors**.
- **Release build:** `dotnet build Enigma.Backup.slnx -c Release -t:Rebuild` → `Build succeeded`,
  **0 warnings, 0 errors**.
- **`AVLN*` warnings: 0** in both configurations, counted by grepping the captured logs — a green
  summary line does not rule them out.
- **Tests:** `dotnet test --solution Enigma.Backup.slnx` → **366 passed, 0 failed, 0 skipped** (355
  before; +10 for the new overlay/banner tests, +1 for the DI singleton test). The new tests cover: a
  run raises the card exactly once and takes it down exactly once; a run that throws still takes it
  down; a run that never starts (a cancelled password prompt) shows nothing at all; every report the
  core makes reaches the card, including the indeterminate scan; the card's Cancel button *is* the
  window's command and stops the run; a clean run publishes one `Success` banner, a run that leaves
  something out one `Warning`, a failed verification one `Error`; the previous banner is taken away
  before the next run starts; and restore and verify get their own card headings.
- **The manual criteria were driven, not eyeballed.** A throwaway harness in the session scratchpad
  (nothing added to the repository) references the built `Enigma.Backup.Desktop` assembly and runs the
  real `MainWindow` with the real `ProgressOverlay`, `NotificationService`, `DialogService` and card
  theme, wired with the same five calls `App.axaml.cs` makes, on X11 (`DISPLAY=:1`). Only the core's
  backup service is scripted, so a run can be held open long enough to photograph. **29/29 checks
  passed**, including: the hosts are the last children in overlay → dialog → infoBar order; all three
  are closed while nothing runs; the card carries the heading, 45 %, a determinate bar, the core's line
  and the file; its Cancel button is the window's command and can be pressed; **a mid-run question is
  open with the card still behind it** (the plan's headline z-order risk); the card is gone after a
  clean run, an aborted run, a thrown run and a cancelled one; the banner's severity and title are
  right in all three cases; pressing Cancel *on the card* says "Cancelling…" there and leaves the bar
  where the run had got to; and both a confirmation and an error message carry a header icon.
- **Screenshots, both variants** (`shots/` in the scratchpad, 20 PNGs rendered from the live window):
  the empty window, the filled window, **the filled window at its minimum size** (the layout check),
  the run card, the mid-run question over the scrim, and the Warning, Success and Error banners, plus a
  confirmation and an error message. Every one reads correctly in Dark and in Light; the warning icon
  and the error icon resolve their `Enigma*` brushes in both.
- **The shipping app itself was launched** (`bin/Debug/net10.0/Enigma.Backup.Desktop` on `DISPLAY=:1`,
  photographed with `spectacle`, then stopped by PID) with a seeded profile: the real `App.axaml` — both
  `ResourceInclude`s, the card theme among them — the real host, the real DI graph including the two new
  seams, and the restyled window with the profile loaded and its encryption toggle **On**. Clean exit,
  nothing in the log, no stray process.
- **The user's real configuration was not touched.** Everything ran under a redirected
  `XDG_CONFIG_HOME` in the scratchpad; `~/.config/Enigma.Backup` still holds only its July `logs/`
  folder and has no `profiles/` directory.

Acceptance criteria for PHASE03 — build clean with zero `AVLN*` warnings, the suite green including new
assertions that a run opens and closes the overlay exactly once and publishes one notification of the
right severity, and manually a backup/restore/verify showing the card with live progress and a working
Cancel, a mid-run unreadable-file dialog **above** the scrim and clickable, the InfoBar reporting each
outcome, and the whole window reading correctly in Light and Dark — are all met.
