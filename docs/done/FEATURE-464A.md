# FEATURE-464A — Centre the page content

**Branch:** `feature/feature-464a-centred-pages`
**Run:** feature/2026-11-16-centered-pages-release

## Summary

Every page now shows its content in a column centred in the space right of the rail, instead of a
column that sticks to the left edge. The column is the same as before: at most 900 px, with 24 px
margins. The page header, the cards and, on Backup, the action bar's contents all keep to that one
column, so their edges line up. The action bar's background strip still spans the whole page.

- `MainWindow.axaml`: one new style, `:is(Control).pageColumn`, which sets `MaxWidth` to 900. Avalonia
  centres a `Stretch` element that is held below the width it is given, so the column is centred on a
  wide window and fills a narrow one.
- The four pages each change in the same way:
  - the header `Grid` gets the class;
  - the one-column wrapper `Grid` around the content is gone, and its `StackPanel` carries the margin
    and the class.

  On Backup, the action bar's wrapper grid is gone too, and its inner grid carries the class.
- `docs/QA-CHECKLIST.md`: new row 3.14.

## Files/modules touched

- Modified:
  - `src/Enigma.Backup.Desktop/Views/MainWindow.axaml`
  - `src/Enigma.Backup.Desktop/Views/Pages/BackupPage.axaml`, `ProfilesPage.axaml`,
    `SettingsPage.axaml`, `VerifyRestorePage.axaml`
  - `docs/QA-CHECKLIST.md`, `docs/roadmap.md`, `docs/plan/FEATURE-464A.md`
- Created: `docs/done/FEATURE-464A.md`

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Rewriting four pages' wrappers | A one-off script in the session's scratchpad (not committed) removed the wrapper and de-indented its body by two spaces | A mechanical edit over ~300 lines. Done by hand, it would have risked a stray indent; the diff is whitespace apart from the wrapper lines |
| Comments | Merged the action bar's two comments into one, and folded Verify & Restore's two comments into one that says the column is centred | No comment restates another, and none still describes the left-aligned column |
| Verification | Screenshots driven by UIA: each page at 1120 px, maximised (1936 px) and at the 820 px minimum | House rule: no UI automation in the test suite. Screenshots are what the plan asked for |

## Deviations & follow-ups

- None against the plan.
- The diff of the four pages is mostly re-indentation. `git diff -w` shows only the wrapper lines and
  the class and margin changes.

## Build/test evidence

- **Run baseline** (run branch `feature/2026-11-16-centered-pages-release` @ `f65068c`, before this dev):
  - `dotnet build Enigma.Backup.slnx` and `-c Release`: 0 warnings, 0 errors;
  - `dotnet test --solution Enigma.Backup.slnx`: 590 total, 578 passed, 12 skipped (Unix-only file
    modes and the like), 0 failed;
  - no pre-existing warnings or failures.
- After the change:
  - `dotnet build Enigma.Backup.slnx` and `-c Release`: **0 warnings, 0 errors**;
  - `dotnet test --solution Enigma.Backup.slnx`: **590 total, 578 passed, 12 skipped, 0 failed**.
- Screenshots of the Debug build, dark variant (session scratchpad, not committed):
  - **Maximised:** on every page, the column runs from x 540 to 1440 in a page area from 52 to 1928,
    centred on 990. The header's icon tile lines up with the cards' left edge, and **Back up**'s right
    edge with the cards' right edge. The action-bar strip spans the full width.
  - **1120 px:** the column runs from 132 to 1032 in a page area from 52 to 1112, centred on 582.
  - **820 px:** the column fills the page as before. Nothing is clipped, nothing scrolls sideways, and
    **Back up** lines up with the cards.
- No tests added: the change is layout only, and the Desktop suite is headless view-model tests only.
- Fix budget: 0 cycles used.

## Documentation sweep

- `README.md` and `CLAUDE.md` say nothing about the page column's alignment, so neither changes.
  `docs/QA-CHECKLIST.md` gains row 3.14.
