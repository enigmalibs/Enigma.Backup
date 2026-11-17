# FEATURE-464A — Centre the page content

**Status:** DONE
**Type:** FEATURE
**Branch:** `feature/feature-464a-centred-pages`
**Run:** feature/2026-11-16-centered-pages-release

## Objective

On a wide window, every page (Backup, Verify & Restore, Profiles, Settings) shows its content in a
column centred in the space right of the rail. Today that column sticks to the left edge.

## Context & constraints

- Each page is a `DockPanel` with three parts:
  - a header `Grid.pageHeader` (icon tile, title, one sentence) docked at the top;
  - on the Backup page only, an action bar `Border.actionBar` docked at the bottom;
  - a `ScrollViewer` holding `<Grid Margin="24,0,24,24">`, whose single column is
    `<ColumnDefinition Width="*" MaxWidth="900" />`.

  The column caps the cards at 900 px but leaves them at the left. The header spans the whole width and
  is left-aligned. The action bar's contents sit in the same capped, left-aligned column.
- The styles live in `MainWindow.axaml`'s `Window.Styles` (`pageHeader`, `actionBar`, …). The pages are
  `UserControl`s under `Views/Pages/`.
- Avalonia arranges a `Stretch` element that is constrained by `MaxWidth` **centred** in its slot.
  Enigma.GitClient's settings page relies on this already (`MaxWidth="900" HorizontalAlignment="Stretch"`,
  described as "a centred column with a ceiling on it").
- House rule: Desktop tests are headless ViewModel tests only, with no UI automation. Layout is
  verified by running the app and taking screenshots.

## Design

- Add one style in `MainWindow.axaml`: `:is(Control).pageColumn` sets `MaxWidth` to `900`. It is the one
  place that says how wide a page's column is.
- Give that class to the three parts of every page, so they line up on one column:
  - **Header**: the `Grid.pageHeader` gets `Classes="pageHeader pageColumn"`. Its margin stays.
  - **Content**: the single-column `Grid` wrapper in each `ScrollViewer` is removed. The `StackPanel`
    it held takes the `Margin="24,0,24,24"` and `Classes="pageColumn"`.
  - **Action bar** (Backup page): the strip `Border.actionBar` stays full width (it is a surface, not
    content). Its single-column wrapper `Grid` is removed, and the inner `Grid ColumnDefinitions="*,Auto"`
    gets `Classes="pageColumn"`, so the **Back up** button's right edge lines up with the cards'.
- Widths for the three parts are the same: margins/padding of 24 px each side, then at most 900 px,
  centred. Below 948 px of page width (the window's 820 px minimum gives about 728 px), the column fills
  the width as it does today.
- Comments that described the old capped column are updated, not duplicated.

## Steps

1. Add the `pageColumn` style to `MainWindow.axaml`'s `Window.Styles`, beside the page-header styles,
   with a one-line "why" comment.
2. `BackupPage.axaml`: the header class, the content `StackPanel`, and the action bar's inner grid.
3. `ProfilesPage.axaml`, `SettingsPage.axaml`, `VerifyRestorePage.axaml`: the header class and the
   content `StackPanel`.
4. Build in Debug and Release, and run the whole suite.
5. Run the app and take screenshots of each page at the default size (1120 px) and maximised. At a
   narrow width (820 px), check that nothing moved.
6. `docs/QA-CHECKLIST.md`: one row under §3 or the page sections saying the page column is centred
   on a wide window.

## Acceptance criteria

- At 1120 px and maximised, the header, the cards and (on Backup) the action bar's contents of all four
  pages sit in one column of at most 900 px, centred right of the rail. The left edges of the header
  and the cards line up, and so do the right edges of the cards and the **Back up** button.
- At the 820 px minimum width, the layout is the same as before: the column fills the space, and nothing
  is clipped or scrolls sideways.
- The action bar's background strip still spans the full page width.
- Debug and Release builds have zero warnings. The whole suite passes against the run's baseline.
- Screenshots back the layout claims in the completion doc.

## Out of scope

- Changing the 900 px ceiling, the margins, or the header's look.
- Dialogs (they are centred by their host already) and the rail.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| What does "content" cover? | Header, cards and the action bar's contents, as one centred column. The action-bar strip stays full width | Centring only the cards would leave the header at the far left of a wide window and look broken. The strip is a surface that frames the page, like the rail | Cards only; centring the strip itself |
| How to centre | `MaxWidth` on the column element with the default `Stretch` alignment, through one `pageColumn` style class | Avalonia centres a `Stretch` element constrained by `MaxWidth`, and Enigma.GitClient already uses it. One class keeps the three parts aligned and the width in one place | A `*,900,*` grid (a fixed 900 column can't shrink below 900); `HorizontalAlignment="Center"` (the column would size to its content and change width with a path, as `SettingsPage`'s own comment warns) |
| Column width | Keep 900 px | Unchanged house ceiling (Launchpad, GitClient, TextEditor) | 1000 px |
| Style selector | `:is(Control).pageColumn` | It applies to `Grid` and `StackPanel` alike. The `:is()` form is already used in this app (`AboutDialog`) | One typed selector per element type |
| Verification | Screenshots at the default size, maximised and at the minimum width | Headless tests can't see layout, and the house rule forbids UI automation | A layout test in the Desktop test project |
