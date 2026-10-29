# FEATURE-4573 — Uniform About dialog

**Status:** TODO
**Type:** FEATURE
**Branch:** `feature/feature-4573-uniform-about-dialog`
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

Enigma.Backup gets the About dialog every Enigma app shares. It has the same layout, sizes, type ramp,
colours and dialog chrome as the reference, Enigma.GitClient's
`src/Enigma.GitClient.Desktop/Views/Dialogs/AboutView.axaml` (itself made from Enigma.MarkdownEditor's),
and is opened from an **About** button in the top bar.

## Context & constraints

**The reference** (Enigma.GitClient @ `2c781d3`):

- **Dialog chrome** (`AboutDialogService`): `Title = null` and no icon, one `Close` button, and
  `DefaultButton = DefaultButton.Close`.
- **Content**: a `UserControl` with `Width="380"`, holding one `StackPanel Spacing="14"`:
  1. the 256-px PNG, shown at 96×96 and centred;
  2. a `StackPanel Spacing="3"` with:
     - the product name: `FontSize 18`, `SemiBold`, centred, `EnigmaForegroundBrush`;
     - a horizontal `Spacing="5"` line: `Version` + the version, in `EnigmaForegroundSecondaryBrush`;
     - a `Build` + short-SHA line: `FontSize 11`, `EnigmaForegroundTertiaryBrush`, the SHA a
       `SelectableTextBlock`. The line is hidden when the build recorded no revision;
  3. the copyright: `FontSize 11`, centred, wrapping, `EnigmaForegroundSecondaryBrush`, hidden when empty.
- **ViewModel**: `DisplayName`, `Version`, `BuildSha` + `HasBuildSha`, `Copyright` + `HasCopyright`, with
  a `(version, buildSha, copyright)` constructor for tests beside the parameterless one. The SHA is the
  first 7 characters after the `+` of the informational version, or `null` when there is none.

**This app today:**

- The Settings dialog ends with an *About* section: a 48-px icon, then name, `Version X`, copyright and a
  project-page link (FEATURE-7F99 PHASE04).
- `AppInfo` holds `Name`, `Version` (via `ShortVersion`), `Copyright` and `ProjectPage`.
- `IShellLauncher.OpenUriAsync` exists for the project-page link alone. `OpenFolderAsync` stays, because
  the Storage card uses it.
- Settings is a ContentDialog itself, and there is one dialog host. About therefore cannot open from
  inside Settings without replacing it.
- House conventions:
  - dialogs go through `IDialogService` / `DialogService`, and the view models never name Avalonia;
  - the pairs are named `XxxDialog` / `XxxDialogViewModel : DialogViewModel`;
  - every dialog's content takes the focus on attach (posted at `Loaded`), so Escape works.

## Steps

1. **`AppInfo`**
   - Add `BuildSha`: the short revision, or `null`.
   - Add a public static `ShortBuildSha(string? informationalVersion)`, testable like `ShortVersion`:
     7 characters, a shorter one kept whole, blank → `null`.
   - Remove `ProjectPage`.
2. **`ViewModels/AboutDialogViewModel.cs`** (`DialogViewModel`)
   - The reference's shape, with `DisplayName => AppInfo.Name`.
   - A parameterless constructor over `AppInfo`, and a `(version, buildSha, copyright)` constructor.
   - A `CloseCommand` for the Enter key binding, as in `SettingsDialogViewModel`.
3. **`Views/AboutDialog.axaml(.cs)`**
   - The reference's markup: its styles, named elements (`BuildLine`, `BuildSha`, `Copyright`) and
     comments, with `/Assets/enigma-backup.png`.
   - `Focusable="True"`, Enter bound to `CloseCommand`, and focus taken on attach.
4. **`IDialogService.ShowAboutAsync`** and its `DialogService` implementation:
   - `Title = null`, no icon;
   - `CloseButtonText = "Close"`, `DefaultButton = DefaultButton.Close`.
5. **`MainWindowViewModel.OpenAboutCommand`**
   - Same `CanExecute` as `OpenSettingsCommand` (`!IsBusy`), re-notified wherever that one is.
   - In `MainWindow.axaml`, an **Info** glyph button beside the gear: tooltip "About", automation name
     "About Enigma.Backup", same size as the gear.
6. **Settings dialog**: remove the About section and the link style, along with `ApplicationName`,
   `VersionLabel`, `Copyright`, `ProjectPageLabel` and `OpenProjectPageCommand`. Remove
   `IShellLauncher.OpenUriAsync` from the interface, `ShellLauncher` and `FakeShellLauncher`, and update
   the remarks on `IShellLauncher` and `SettingsDialogViewModel`.
7. **Tests**
   - `AppInfoTests`: the short-SHA theory; the project-page test removed.
   - New `AboutDialogViewModelTests`: shape, no build line without a SHA, no copyright line when empty,
     and the parameterless constructor reads `AppInfo`.
   - `SettingsDialogViewModelTests`: drop the About and project-page tests.
   - `MainWindowViewModel` tests: the About command opens the dialog and is disabled while busy, as for
     Settings.
   - `FakeDialogService` gains `ShowAboutAsync`.
8. Run the app and screenshot the dialog in both themes. The screenshot paths go in the completion doc.
9. **Docs**:
   - `docs/QA-CHECKLIST.md`: the About checks move from Settings to the About button;
   - CLAUDE.md: the `IShellLauncher` wording ("opening a folder or a link").

## Acceptance criteria

- The dialog matches the reference element for element: order, sizes, font sizes, brushes, spacing,
  no title or icon, one default Close.
- About opens from the top bar. Escape and Enter close it. It is disabled during a run.
- The Settings dialog shows only Theme and Storage. Nothing in `src/` or `tests/` mentions `ProjectPage`
  or `OpenUriAsync` any more.
- Release build has zero warnings. Whole suite green, new tests included.

## Out of scope

- A shared About library across Enigma apps (each app carries its own copy, as the siblings do).
- Any change to the Theme or Storage cards.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Where About opens | An Info button beside the gear in the top bar | Settings is a ContentDialog on the single host, so About cannot open from inside it. GitClient also has a toolbar Info button | An "About…" button inside Settings that replaces it; keeping the section |
| Settings' About section | Removed | One About surface with one look. Keeping both would be two About presentations, which is the opposite of uniform | Restyling the section to match |
| Project-page link | Dropped, with `OpenUriAsync` | The reference shows icon, name, version, build and copyright, and nothing else (Enigma.Msi dropped its GitHub button the same way). The seam would be dead code | Keeping the link inside the dialog; keeping the seam "for later" |
| Type names | `AboutDialog` / `AboutDialogViewModel` | This repo's `*Dialog` convention wins over the reference's `AboutView` | `AboutView` |
| Display name | `AppInfo.Name` ("Enigma.Backup") | What the window title already says. No new product name is invented | "Enigma Backup" |
| Build SHA source | The informational version's `+<sha>`, 7 characters | Same as the reference. The SDK already stamps it | A separate MSBuild property |
| Dialog sizes | Not set | No dialog in this app sets the host's `Dialog*` sizes, so there is nothing stale to override. The 380-px content governs | Stating all six as Enigma.Msi does (its other dialogs set them) |
