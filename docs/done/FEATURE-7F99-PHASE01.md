# FEATURE-7F99-PHASE01 — Pattern inputs

**Branch:** `feature/feature-7f99-phase01-pattern-inputs`
**Status:** DONE

## Summary

An input can now be a *rule* rather than a place: a base folder plus a glob — `C:\Logs` + `*.log`,
`/var/log` + `DirPrefix*` — worked out again on every run, alongside the file and folder inputs that
were already there.

The shape of the code follows from four decisions:

- **A pattern is a pair, and it is its own thing.** `PatternInput(BaseDirectory, Pattern)` lives beside
  `Inputs` in the profile rather than inside it. An input is a bare `string` end to end, and the
  scanner canonicalizes every one of them: a pattern smuggled into that list would be reported as a
  missing path. So patterns get their own list, their own resolve path, and their own dialog.
- **One rulebook per window.** The glob language and the case sensitivity are the exclude field's —
  `Microsoft.Extensions.FileSystemGlobbing.Matcher` with `PathUtilities.Comparison` — so `*` matches
  inside one folder level and `**` at any depth. The one documented departure is the include
  direction's own: **a pattern that matches a folder's name takes that folder with everything in it**,
  which is what `DirPrefix*` is asking for and what strict file-glob semantics would silently answer
  with nothing.
- **A pattern that matches nothing is a warning, not an error.** There may be no logs today. The base
  folder is still recorded in the archive — so it says where it looked — and the run reports
  `PatternMatchedNothing` in the Last-run panel. Silence would leave "my backup is smaller than I
  thought" as the only symptom.
- **Version 2 is written only when it is earned.** `BackupProfile.CurrentFormatVersion` is 2, but
  `ProfileSerializer` writes the *lowest* version that can express the profile: version 2 if and only
  if it carries a pattern (PHASE02 adds `Compression == Zip` to the same rule), else version 1. That
  is what keeps 1.1.0's compatibility claim true for every profile that uses nothing new, while a
  profile that does use something new is refused loudly by 1.0.x/1.1.0 rather than read as less than
  it says.

`EBK1` is untouched: the container version byte, archive naming, the sidecar and the golden fixture
are all exactly as they were.

## Files/modules touched

### Created — production

- `src/Enigma.Backup.Core/Profiles/PatternInput.cs` — the `(BaseDirectory, Pattern)` record, whose
  `<remarks>` is where the glob rules and the folder-name extension are written down.
- `src/Enigma.Backup.Core/Scanning/PatternMatcher.cs` — `Matcher` in the include direction, built with
  `PathUtilities.Comparison`. `MatchesAnything` is false for a blank pattern, which the scan reports as
  a pattern that matched nothing rather than refusing the profile.
- `src/Enigma.Backup.Desktop/ViewModels/InputItem.cs` — the one item type the inputs list needs, since
  the profile keeps paths and patterns apart but the window shows one list. Immutable, compares by
  value, renders as `folder ▸ pattern`.
- `src/Enigma.Backup.Desktop/ViewModels/PatternInputDialogViewModel.cs` — the prompt: a picked folder,
  a typed glob, validation as you type driving `CanAccept` (which is what greys the dialog's Add
  button out, since its buttons ignore `CanExecute`).
- `src/Enigma.Backup.Desktop/Views/PatternInputDialog.axaml(.cs)` — read-only folder row with a
  Choose… button, the pattern `TextEditor`, two lines of inline help, the error line. Enter submits,
  and the pattern box takes the focus on attach at `DispatcherPriority.Loaded` — both dialog
  invariants from `CLAUDE.md`.

### Created — tests

- `tests/Enigma.Backup.Core.Tests/Scanning/PatternInputScanTests.cs` — 26 cases against real trees:
  one-level `*`, `**` at any depth, a folder-name match taking its whole subtree, structure-only
  directories (and the match-free ones never appearing), a matched *empty* folder still archived,
  excludes winning over a match, the empty-match warning (and the base folder still recorded), a blank
  pattern, per-platform case sensitivity, `\` accepted for `/`, the missing / blank / is-a-file base
  folder through Skip / Retry / Abort, root-name collision with a path input, two patterns over one
  folder, output-directory and symlink rules on both sides, mixed path+pattern totals, cancellation,
  and a null pattern list or entry.
- `tests/Enigma.Backup.Desktop.Tests/ViewModels/PatternInputDialogViewModelTests.cs` — 11 cases: the
  empty prompt is not an error, folder+pattern is acceptable, each half alone is not, a rooted pattern
  in either platform's shape is refused, spaces are trimmed, a dismissed picker leaves the folder
  alone, and accept/cancel each close exactly once with the right answer.

### Modified

- `src/Enigma.Backup.Core/Profiles/BackupProfile.cs` — `PatternInputs`, `CurrentFormatVersion = 2` and
  a new `MinimumFormatVersion = 1`; `FormatVersion`'s doc now says what is read and what is written.
- `src/Enigma.Backup.Core/Profiles/ProfileSerializer.cs` — reads 1 **and** 2, refuses anything else on
  both sides, writes `RequiredFormatVersion(profile)` and stamps the profile with it, and normalizes
  the pattern list (a null entry is dropped; a null field becomes blank, which the scan then reports —
  dropping it would back up less than the profile says).
- `src/Enigma.Backup.Core/Scanning/InputScanner.cs` — pattern inputs resolved after the path inputs,
  each independently: base folder through the same Skip/Retry/Abort flow (with a message that names
  the pattern), the same symlink and output-directory rules, `AllocateRootName` for the top-level name,
  then a walk that records candidates and works the selection back up the parent links — so a
  directory is emitted only when it or something below it matched, and still before its own contents.
  The child-directory link and output-directory checks the two walks share moved into
  `CanDescendInto`.
- `src/Enigma.Backup.Core/Scanning/ScanWarningKind.cs` — `PatternMatchedNothing`.
- `src/Enigma.Backup.Desktop/ViewModels/ProfileEditorViewModel.cs` — `Inputs` is now
  `ObservableCollection<InputItem>`; `AddInputs` dedupes against the *path* rows only (a folder listed
  outright and the same folder as a pattern's base are different requests), `AddPatternInput` dedupes
  the pair and stores the glob with `/` separators, and `ToProfile`/`LoadFrom` split and rejoin the two
  lists.
- `src/Enigma.Backup.Desktop/ViewModels/MainWindowViewModel.cs` — `AddPatternCommand`: the window asks
  (`IDialogService`), the editor stores. It reports what was added, or that the pattern was already
  there, in the same voice as a drop.
- `src/Enigma.Backup.Desktop/Services/IDialogService.cs` + `DialogService.cs` — a seventh question,
  `RequestPatternInputAsync`, through the same `ShowAsync` helper (Asterisk glyph, "Add"/"Cancel",
  `isPrimaryEnabled` from the view model). `DialogService` takes `IFilePicker` for the same reason it
  takes the settings store: this is where each dialog's view model is built.
- `src/Enigma.Backup.Desktop/Views/MainWindow.axaml` — the inputs template binds `InputItem.Display`
  with the pair in the tooltip, the section heading names patterns, and an **Add pattern…** button
  joins the row — which becomes a `WrapPanel`, because five buttons do not fit the panel at the
  window's minimum width and a button pushed past the edge cannot be pressed.
- Tests: `FakeDialogService` (a `Patterns` queue and a request count), `ProfileEditorViewModelTests`
  (+8 pattern cases; the existing assertions now read `InputItem.Path`),
  `MainWindowViewModelProfileTests` (+3 for the command), `ProfileSerializationTests` (+6: version 1
  stays version 1, patterns force version 2, a version-2 file reads back, missing/null pattern data,
  and a hand-edited version-1 file carrying a pattern), `ProfileStoreTests` (the round trip now asserts
  version 1 for a feature-free profile, plus a new version-2 round trip),
  `WindowEndToEndTests` (a new end-to-end: two patterns over one folder, saved as version 2, backed up
  and restored for real).
- `docs/roadmap.md`, `docs/plan/FEATURE-7F99.md` — PHASE01 status.

**Untouched:** `src/Enigma.Backup.Core/Encryption/` in its entirety,
`tests/Enigma.Backup.Core.Tests/Fixtures/` (verified by `git status`), `BackupService`,
`RestoreService`, `VerificationService`, `ArchiveNaming`, `ArchiveFormatDetector`,
`Directory.Packages.props`, `Directory.Build.props`, `global.json`, `.editorconfig`, `publish.sh`,
`publish.ps1`.

## Deviations & follow-ups

- **`PatternMatcher`'s semantics are covered through the scanner, not directly.** It is `internal` and
  this solution has no `InternalsVisibleTo`; `GlobExcluder` is tested the same way, and what matters is
  which entries come out of a real walk. Adding an assembly-level attribute for one test would have
  been the larger change.
- **`InputItem.ForPath` / `ForPattern`, not `InputItem.Path(...)` / `InputItem.Pattern(...)`** as the
  plan wrote the factories: `Path` and `Pattern` are the record's own properties, so the plan's names
  could not both be members of the same type.
- **The pattern dialog is one prompt, and the "Add pattern…" command sits on `MainWindowViewModel`**
  rather than on the editor. The editor asks the file system (`IFilePicker`); the window asks the user
  (`IDialogService`). Handing the editor a dialog service would have given the profile's working copy
  the power to raise any modal question in the app. The plan's own file list anticipated this ("
  `MainWindowViewModel.cs` (dialog call)").
- **The action row wraps.** Five buttons need ~600 px and the panel has ~530 px at the default window
  size, so `Add files… / Add folder… / Add pattern…` and `Remove / Remove all` fall onto two lines —
  the same grouping at 960 px as at 1120 px, which reads deliberately. It costs the inputs list ~38 px
  of height at the minimum window size (two visible rows become one; the list scrolls and the window
  grows). If that trade is unwanted, the alternative is icon-only Remove buttons — a wider change than
  this phase's plan asked for, so it is left as the user's call.
- **A blank base folder is reported, not ignored.** A blank *path* input is skipped silently (it can
  only be an empty row); a blank pattern base is a rule that would quietly back up nothing, so it goes
  through the same MissingInput flow with a message that names the pattern. Only a hand-edited profile
  can produce one.
- **A version-1 file that carries `patternInputs` is read rather than refused**, and the next save
  stamps it version 2. Refusing it would throw away data the user asked for; the write rule is what
  makes the file honest again. Pinned by a test.
- **Verification opened the dialog and started the run from a temporary startup hook**, because there
  is no `xdotool`/`wmctrl` on this machine to synthesise a click. The hook was two lines in
  `MainWindow.axaml.cs` (plus a one-line window size change in `MainWindow.axaml` for the minimum-width
  shot); both were reverted with `git checkout`, and `grep -rn "SCAFFOLD\|DispatcherTimer.RunOnce" src/`
  finds nothing. The click itself is the one thing not exercised; the command's wiring and
  `CanExecute` are asserted by test.
- **The machine was left as it was found:** the app ran against a throwaway `XDG_CONFIG_HOME` in the
  session scratchpad, so `~/.config/Enigma.Backup/` still holds only the user's own
  `settings.json` (`"theme": "System"`) and no demo profile.
- **Line endings:** nothing in this phase's diff is CRLF↔LF churn. No action taken — per the workflow
  this is recommendation-only.
- **For PHASE05:** the release notes need the version rule stated plainly (feature-free profiles stay
  version 1 both ways; a pattern makes a profile version 2 and older builds refuse it by design), and
  `docs/QA-CHECKLIST.md` needs the pattern section (match, empty match, missing base). Both are already
  in the plan's PHASE05 steps.

## Build/test evidence

- **Build (Debug):** `dotnet build Enigma.Backup.slnx` → **0 warnings, 0 errors**.
- **Build (Release):** `dotnet build Enigma.Backup.slnx -c Release` → **0 warnings, 0 errors**, and
  `grep -c AVLN` over the full verbose log → **0**. (`TreatWarningsAsErrors` does not promote `AVLN*`,
  so every `.axaml`-touching phase reads the count explicitly.)
- **Tests:** `dotnet test --solution Enigma.Backup.slnx` → **450 passed, 0 failed, 0 skipped** (392
  before; +58). `GoldenArchiveCompatibilityTests` green with the fixture untouched.
- **A feature-free profile is still written as `formatVersion` 1** — asserted by
  `SavingAProfileThatUsesNoNewerFeature_StillWritesFormatVersion1` and by the `ProfileStore` round
  trip, which is what the 1.1.0 compatibility claim rests on.
- **Verified running** on Linux (Wayland + XWayland, `DISPLAY=:1`), Debug build, against a throwaway
  config folder; screenshots taken with `spectacle`:
  1. **The list renders both kinds of row**, in **Dark** and in **Light**: `/etc/hostname`,
     `/etc ▸ *.conf`, `/usr/share/icons ▸ breeze*`, with the pair spelled out in the tooltip — loaded
     from a `formatVersion` 2 profile written by hand.
  2. **The Add pattern… dialog**, in both variants: the Asterisk header, the prompt, the read-only
     Folder row with its Choose… button, the focused Pattern box, the two-line help, and **Add greyed
     out** while the pair is incomplete — which is `IsPrimaryButtonEnabled` following `CanAccept`.
  3. **At the 960 px minimum width** nothing overflows or is cut off: the action buttons wrap to
     `Add files… / Add folder… / Add pattern…` + `Remove / Remove all`, and the inputs list is what
     gives.
  4. **A real run**, started against a prepared tree with three patterns
     (`*.log`, `DirPrefix*`, and one that matches nothing): the banner says
     *"Backed up 2 files to 'Run_2026-08-10_224157.tar.zst'"*, the Last-run panel shows
     `2 files · 4 folders` and **two warning notes** — the numbered top-level name for the second
     pattern over the same folder, and *"Nothing under '…/source/docs' matched the pattern
     '*.nothing-matches-this'"*.
  5. **The archive holds exactly what the patterns asked for** (`zstd -dc … | tar -tvf -`):
     `logs/`, `logs/app.log`, `logs_2/`, `logs_2/DirPrefixOne/`, `logs_2/DirPrefixOne/inner.txt`,
     `docs/` — and neither `keep.txt`, nor `other/x.txt`, nor `other/` itself.

## Acceptance criteria

| Criterion | Met |
|---|---|
| Core model: `PatternInput`, `BackupProfile.PatternInputs`, serializer round trip; `CurrentFormatVersion = 2` with the minimum-necessary write rule and the 1-or-2 read rule | ✅ |
| Core scanning: `PatternMatcher` (include direction, folder-subtree rule), pattern resolution in `InputScanner` (missing base, excludes, root naming, structure-only directories, `PatternMatchedNothing`) | ✅ |
| Desktop: input descriptor collection (+ `ToProfile`/`LoadFrom`, dedup, `CanRun`), `base ▸ pattern` rendering, **Add pattern…** command | ✅ |
| Desktop: `PatternInputDialogViewModel` + view + `IDialogService` method + fake response; validation drives `IsPrimaryButtonEnabled`; focus-on-attach and Enter binding | ✅ |
| Tests — Core: matcher semantics, scanner integration, serializer (v1 stays v1, patterns force v2, v2 reads back, v3 refused, old-shape v1 loads). Desktop: mixed round trip, dialog validation, `CanRun` with patterns only | ✅ (matcher semantics through the scanner — see *Deviations*) |
| Debug and Release build clean, zero warnings and zero `AVLN*`; full suite green | ✅ — 450/450 |
| A feature-free profile saved by this build is byte-identical in `formatVersion` to 1.1.0's output | ✅ |
| Manually: `*.log` and `DirPrefix*` against a prepared folder archive exactly the expected entries; the empty-match warning appears in the Last-run panel; both variants render the new list items and dialog | ✅ (dialog opened from a temporary hook — see *Deviations*) |
