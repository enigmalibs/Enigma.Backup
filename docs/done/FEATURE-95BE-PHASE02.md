# FEATURE-95BE-PHASE02 — Rail and Verify & Restore page

## Summary

The window is now a narrow navigation rail with pages, as in Enigma.TextEditor.

**The rail**
- Icon-only, 44 px wide, using TextEditor's `NavigationItem` theme. The tooltip and the screen-reader name
  are the page's name, and the library's highlight marks the current page.
- It lists **Backup** and **Verify & Restore**. Profiles and Settings join in the phases that build their
  pages.

**The pages**
- Each opens with Enigma.Launchpad's header: an icon tile, the page's name and one sentence.
- Pages are shown and hidden, never rebuilt, so each keeps its state.
- The window title is the constant "Enigma Backup".

**The split**
- `MainWindowViewModel` became `BackupPageViewModel` (git rename). It is the old content minus
  Restore…, Verify… and the dynamic title.
- **Verify & Restore** has a page of its own (`VerifyRestorePageViewModel`), with one card per action.
- The run plumbing moved into a shared singleton, `RunCoordinator`. It holds the busy state,
  cancellation, the run dialog and run logging, so one run happens at a time across every page.
- A new, small `MainWindowViewModel` is the shell. It holds `CurrentPage`, the page view models, and drop
  routing.

## Files/modules touched

- Created:
  - `src/Enigma.Backup.Desktop/ViewModels/AppPage.cs`
  - `src/Enigma.Backup.Desktop/ViewModels/RunCoordinator.cs`
  - `src/Enigma.Backup.Desktop/ViewModels/VerifyRestorePageViewModel.cs`
  - `src/Enigma.Backup.Desktop/ViewModels/MainWindowViewModel.cs` (the new shell)
  - `src/Enigma.Backup.Desktop/Views/Pages/BackupPage.axaml(.cs)`
  - `src/Enigma.Backup.Desktop/Views/Pages/VerifyRestorePage.axaml(.cs)`
  - `tests/Enigma.Backup.Desktop.Tests/ViewModels/MainWindowViewModelTests.cs`
  - `tests/Enigma.Backup.Desktop.Tests/ViewModels/VerifyRestorePageViewModelTests.cs`
- Renamed:
  - `ViewModels/MainWindowViewModel.cs` → `BackupPageViewModel.cs`
  - tests `MainWindowViewModelProfileTests` → `BackupPageViewModelProfileTests`
  - tests `MainWindowViewModelRunTests` → `BackupPageViewModelRunTests`
  - tests `MainWindowViewModelRunDialogTests` → `RunCoordinatorTests`
- Modified:
  - `src/Enigma.Backup.Desktop/Hosting/ServiceCollectionExtensions.cs`
  - `src/Enigma.Backup.Desktop/Views/MainWindow.axaml(.cs)`
  - tests: `Harness`, `ServiceCollectionExtensionsTests`, `WindowEndToEndTests`,
    `AboutDialogViewModelTests`, `SettingsDialogViewModelTests`
  - `README.md`, `docs/QA-CHECKLIST.md`, `CLAUDE.md`, `docs/roadmap.md`, `docs/plan/FEATURE-95BE.md`

## Deviations & follow-ups

- **The Backup page's `Title` was removed**, along with its two tests. Those tests were replaced by two
  that check `ProfileStatus`, which still says "Unsaved changes.". Plan step 5 only said the shell has no
  title, but nothing reads the page's title any more.
- **Dropped files are only accepted on the Backup page, and only while no run is going.** The window
  shows `DragDropEffects.None` elsewhere. Full per-page drop routing is PHASE04, as planned.
- **The window's minimum size is unchanged** (960×780) until PHASE04 reworks the Backup page into
  scrolling sections. The header costs about 80 px, which is taken from the inputs list (its row is the
  one that gives).
- **Rail items are built in `MainWindow`'s code-behind** (plan step 7), not in a view model.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Where the progress formatters went | Onto the page that owns the run: backup progress in `BackupPageViewModel`, restore and verify progress in `VerifyRestorePageViewModel` | The coordinator stays generic and knows no core progress type |
| `RunCoordinator.RunAsync` return value | The summary, or `null` when a run is already going | Lets a caller tell "did not start" from an outcome |
| How a page binds its own `DataContext` without breaking the shell's `IsVisible` binding | Each page sits in a `Panel` whose `IsVisible` binds to the shell; the page inside it gets the page view model as its `DataContext` | A binding on the same element would resolve against the page's context |
| Header styles | In `MainWindow`'s `Window.Styles`, beside the existing ones | That is the convention here, and the pages are the window's children |
| Rail ↔ view model sync | Code-behind handles `NavigationView.SelectedItemProperty` and the view model's `CurrentPage`. A null selection or a foreign item is ignored | The rail clears its selection while it rebuilds its lists |
| Icons | `Archive` (Backup) and `ClockCounterClockwise` (Verify & Restore); `SealCheck` and `ArrowCounterClockwise` on the cards | Matches the buttons' existing glyphs |

## Build/test evidence

- Run baseline: see `FEATURE-95BE-PHASE01.md` (552 total, 540 passed, 12 skipped, 0 failed; 0 warnings).
- Debug and Release builds: **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Backup.slnx`: **567 total, 555 passed, 12 skipped, 0 failed**.
- Tests added:
  - `MainWindowViewModelTests` (7): default page, switching, state kept, drop accepted, drop refused on
    another page, drop refused while running, null guards.
  - DI test `ThePagesAndTheRunCoordinatorAreOneInstanceEach`.
  - The restore and verify tests moved verbatim to `VerifyRestorePageViewModelTests`.
- In the running app (Debug build on Windows, via `PrintWindow` and UI Automation):
  - The title reads "Enigma Backup", and the rail shows two icons with Backup highlighted.
  - The Backup page has its header and no Restore… or Verify….
  - Selecting **Verify & Restore** moves the highlight and shows the two cards.
  - **Verify…** on a copy of the golden encrypted archive runs in the run dialog. Cancelling its password
    prompt gives the partial outcome, and **Close** dismisses it.

## Documentation sweep

- `README.md` "Using it":
  - the rail of pages;
  - "Unsaved changes." instead of the `*` in the title bar;
  - item 8 now says Verify & Restore is a page.
- `docs/QA-CHECKLIST.md`: the 1.2.0 preamble paragraph now describes the rail and the title. Rows 3.10 and
  3.11 now check the rail and the page header, instead of the old top-bar layout.
- `CLAUDE.md`:
  - the run invariant now names `RunCoordinator.RunAsync`;
  - the "view models never name Avalonia" invariant notes that the rail's items are built by the window's
    code-behind, and that pages are shown and hidden.
