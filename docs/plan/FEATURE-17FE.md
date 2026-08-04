# FEATURE-17FE — Enigma.Avalonia.Desktop adoption & 1.1.0 release

**Status:** TODO
**Type:** multi-phase feature (5 phases)
**Branches (created by `/build`, each from `HEAD`):** one per phase, see *Phases*

## Objective

Rebuild the desktop app's UI on the house control library **`Enigma.Avalonia.Desktop` 1.0.0** — its
Fluent theme, its `ContentDialog` / `Overlay` / `InfoBar` host services, its file and folder picker
services, its `SettingsCard` family and its typed editors — and release the result as **1.1.0**.

The app today is "pure" Avalonia: a `FluentTheme`, six hand-rolled `App*` brushes defined in
`App.axaml`, five modal `Window` dialogs, and a 255-line `MainWindow.axaml` of `Border.panel`
sections. Nothing about that is broken; the point of this item is that the house library now exists
and this app should be built on it, so the two stay in step and the app inherits the library's look
and its services instead of maintaining private equivalents.

**The layout stays a single window.** This is a restyle *in place*, not a redesign: no
`NavigationView`, no pages, no `DockingHost`, no `Ribbon`. Enigma.Backup's whole workflow — choose
inputs, choose a destination, press *Back up* — belongs on one screen, and splitting the 809-line
`MainWindowViewModel` across page ViewModels would buy navigation nobody asked for at the cost of
every Desktop test. What changes is which controls draw that screen, and which services stand behind
its dialogs, pickers, progress and notifications.

`Enigma.Backup.Core` is not touched by any phase.

## Validated decisions (interview 2026-08-04)

- **Adoption depth: restyle in place, but use the library's controls and services wherever they
  fit.** The single-window layout is kept; `ContentDialog`, the picker services, `SettingsCard` /
  `SettingsCardExpander`, the typed `Editors`, `InfoBar` and `Overlay` are all adopted.
- **All five dialogs become `ContentDialog`s**, behind the app's existing `IDialogService`. The
  dialog ViewModels are unchanged; only their Views (now `UserControl`s) and the service
  implementation change.
- **The Phosphor icon pack is adopted** — `Enigma.Icons`, `Enigma.Icons.Phosphor`,
  `Enigma.Icons.Avalonia` — and icons appear across the restyled UI. Every `IconData` slot in the
  library is a `Geometry?` that renders as nothing when left null, so a restyle without icons looks
  half-finished.
- **Version 1.1.0 — a minor.** User-visible functionality is added (a theme preference, in-window
  dialogs, an overlay, notifications) and nothing breaks: profile JSON, the `EBK1` container version
  byte, archive and sidecar naming and the restore path are all untouched, so every 1.0.x archive
  still opens. A patch would misdescribe a redrawn app; a major would overstate a release that breaks
  no compatibility.
- **Run progress moves into the `Overlay`**, as a `ProgressOverlayCard` carrying the phase, the
  current item, the percentage and **Cancel**, for backup, restore and verify alike. The window's
  bottom panel keeps only the action buttons; its `ProgressBar` and status lines are removed. Two
  live progress readouts for one run would be duplicate state to keep in sync, and Cancel has to be
  *on* the card — behind the scrim it cannot be clicked.
- **The three hosts are ordered `Overlay` → `ContentDialog` → `InfoBar`**, deliberately *not* the
  order in the library's own `setup.md`. A `Panel` z-stacks its children, so the documented order
  (dialog, overlay, infobar) would draw the overlay's scrim on top of a `ContentDialog` — and a mid-run
  decision dialog (unreadable file, restore conflict) is raised *while* the overlay is open. This
  reordering is the whole reason the overlay option is viable, and it must not be "corrected" later.
- **`InfoBar` carries the run outcome as a banner; the "Last run" details panel stays.** The panel
  holds the archive path, sizes, durations and the per-file skipped and warning lists — a one-line bar
  cannot replace it, and after a run that half-worked those lists are exactly what the user needs.
- **The typed `Editors` and the `SettingsCard` family restyle the form rows**; `Ribbon` and the
  `CollectionView` subsystem were both offered and declined — the first is a layout change rather than
  a restyle, the second is machinery for a sorting/filtering need this app does not have.
- **Theme preference: `System` / `Light` / `Dark`, persisted, defaulting to `System`.** Two values
  were rejected because "follow the OS" would exist only until the first click and could never be
  returned to. `System` assigns `ThemeVariant.Default`; the other two pin the variant.
- **The preference is Desktop-owned**, in `%APPDATA%\Enigma.Backup\settings.json` /
  `~/.config/Enigma.Backup/settings.json` — the folder that already holds `profiles/`. Core stays
  UI-free and CLI-ready (CLAUDE.md's first invariant): a theme variant means nothing to a CLI, so a
  settings concept in Core would be UI leaking inward. A missing or corrupt file falls back to
  `System`, logged, never fatal.
- **The theme control lives in a Settings `ContentDialog`**, opened by a gear button in the profile
  bar. A preference is not part of the backup workflow, the main screen is already dense, and a dialog
  of `SettingsCard` rows gives later preferences somewhere to go without touching the layout again.
- **Release scope: code, version and release docs; the artifacts stay with the user.** In scope:
  `<Version>1.1.0</Version>`, the `RELEASENOTES.md` 1.1.0 section, the README callout and Installing
  table, `SECURITY.md`'s supported-versions row, and `docs/QA-CHECKLIST.md` rewritten for the new UI.
  Out of scope: `./publish.sh`, the zips, the git tag — `/build` prints the runbook instead.
- **Verification includes running the app.** Per phase: clean Release build, zero `AVLN*` warnings,
  full suite green, then launch on `DISPLAY=:1` and screenshot **both** theme variants, recorded in
  the completion doc. A headless ViewModel suite cannot see a missing `ResourceInclude`, an
  unreadable brush pair or a collapsed card row — every one of those passes every test.
- **Five phases**, each leaving the app fully working and independently reviewable.

### Findings established before planning

- **The package resolves.** `Enigma.Avalonia.Desktop` 1.0.0 is published on nuget.org (`dotnet
  package search --exact-match` confirms it) and targets `net8.0` + `net10.0`. The three icon
  packages are at 1.0.0 and already in the local NuGet cache.
- **Its `Enigma.Core` dependency is 1.0.0; this solution pins 1.1.0 in CPM, and the higher version
  wins.** No downgrade, no conflict — and `GoldenArchiveCompatibilityTests` already guards exactly
  that resolution, so a behavioural change under the cipher would fail the build rather than ship.
- **Its Avalonia pins are 12.1.1** — identical to this solution's coupled set, so nothing in the
  version-coupled group has to move.
- **`ContentDialog` handles `Escape` only.** `OnKeyDown` closes with `DialogResult.None` on Escape;
  `DefaultButton` "declares intent only" and there is no Enter handling. Every dialog that today
  submits on Enter (`IsDefault="True"` on OK/Skip/Keep) needs an explicit `KeyBinding` in its content.
- **Dismissal already maps correctly.** `ErrorDecisionDialogViewModel.Decision` defaults to
  `BackupErrorDecision.Abort()` and `RestoreConflictDialogViewModel.Decision` to
  `RestoreConflictDecision.Abort()`, with `PasswordDialogViewModel.Result` / `ProfileNameDialogViewModel.Result`
  defaulting to `null` — so `DialogResult.None` from Escape or a scrim click needs no new logic: read
  the ViewModel, as `DialogService` already does for a window closed by its title bar.
- **`Enigma.Icons.Phosphor.dll` is 3.9 MB**; the library itself is ~170 KB packed and
  `Enigma.Icons` + `Enigma.Icons.Avalonia` are ~58 KB of assemblies. Each release zip therefore grows
  by roughly 4 MB. `Enigma.Core` and BouncyCastle are already shipped, so the library's transitive
  set adds nothing new.
- **`AVLN*` XAML warnings are not promoted by `TreatWarningsAsErrors`** (they come from an MSBuild
  task), so `Build succeeded` can hide them. Every phase that touches `.axaml` reads the warning
  count explicitly.
- **The library ships no `AddEnigmaServices()`** — the registration extension for its six singletons
  is ours to write.

## Architecture

### What the seams are, and why they do not move

CLAUDE.md's Desktop invariant is that **view models never name Avalonia**: file pickers, dialogs and
UI-thread marshaling enter through `IFilePicker` / `IDialogService` / `IUiDispatcher`, and only the
Views, `App` and those implementations touch Avalonia types. The library's services do not satisfy
that on their own — `IContentDialogService.ShowAsync(Action<ContentDialog>)` hands the caller an
Avalonia control, and the picker services deal in `IStorageFile` / `FilePickerOpenOptions`. So:

> **The library's six services are consumed only inside `src/Enigma.Backup.Desktop/Services/`, behind
> the app's own interfaces. No ViewModel ever injects one.**

That is also what keeps the whole Desktop test suite alive: `Harness` news up the real
`MainWindowViewModel` and `ProfileEditorViewModel` against `FakeDialogService` / `FakeFilePicker` /
`ImmediateUiDispatcher`, and none of those change.

| ViewModel-facing seam | Before | After |
|---|---|---|
| `IDialogService` (unchanged interface) | five modal `Window`s via `ShowDialog(owner)` | `IContentDialogService` + one `UserControl` per dialog |
| `IFilePicker` (unchanged interface) | `Window.StorageProvider` directly | `IFileDialogService` / `IFolderDialogService` |
| `IProgressOverlay` (**new**) | inline `ProgressBar` bound in `MainWindow.axaml` | `IOverlayService` + `ProgressOverlayCard` |
| `INotificationService` (**new**) | — | `IInfoBarService` |
| `IAppSettingsStore` (**new**) | — | `settings.json` beside `profiles/` |
| `IUiDispatcher` | `Avalonia.Threading.Dispatcher` | unchanged |
| `IMainWindowProvider` | supplied the owner window | **removed** — nothing needs the window once dialogs and pickers go through the library |

`MainWindowProvider` exists solely because dialogs and pickers needed an owner `Window` that could
not be injected without closing a cycle. After PHASE02 nothing asks for it: the hosts and the storage
provider are handed to the library's services in `App.OnFrameworkInitializationCompleted`, which
already holds the resolved window. Drag-and-drop stays where it is, in `MainWindow.axaml.cs`.

### Window composition

```
Window  Background="{DynamicResource EnigmaBackgroundBrush}"
└─ Panel                          ← z-stacks; hosts are the LAST children
   ├─ Grid                        ← the app: profile bar, inputs, excludes, destination, actions, last run
   ├─ controls:Overlay        x:Name="HostOverlay"     ← first host: the run scrim
   ├─ contentDialog:ContentDialog x:Name="HostDialog"  ← above the scrim, so mid-run dialogs are usable
   └─ infoBar:InfoBar         x:Name="HostInfoBar"     ← always on top
```

Four `xmlns` prefixes are needed: `…Controls` (`Overlay`, `SettingsCard`, `SettingsCardExpander`),
`…Controls.ContentDialog`, `…Controls.InfoBar`, `…Controls.Editors`, plus
`xmlns:ei="https://github.com/josueclement/Enigma.Icons"` for the icon markup extensions.

### Startup wiring (`App.axaml.cs`)

Unchanged in shape — build the `IHost`, `Start()` it (never `Run()`), resolve `MainWindow`, stop the
host on `Exit`. Added, all before the window is shown, because every one of these services throws
`InvalidOperationException` from its first call if it is skipped:

```
IContentDialogService.RegisterHost(window.HostDialog)
IOverlayService     .RegisterHost(window.HostOverlay)
IInfoBarService     .RegisterHost(window.HostInfoBar)
IFileDialogService  .SetStorageProvider(window.StorageProvider)
IFolderDialogService.SetStorageProvider(window.StorageProvider)
```

then the persisted theme variant is applied to `Application.Current.RequestedThemeVariant`.

### Theme

`App.axaml` keeps `<FluentTheme />` in `Application.Styles` (the library themes only its own
controls; dropping it leaves every framework control — including those nested inside the library's
templates — without one) and merges
`avares://Enigma.Avalonia.Desktop/Themes/Fluent.axaml` as a **`ResourceInclude`** in
`Application.Resources`. A `StyleInclude` fails the build with `AVLN2000`.

The six `App*` brushes are retired in favour of the library's keys, always through
`DynamicResource` — `StaticResource` freezes the colour and the control stops following a variant
switch:

| Retired | Replacement |
|---|---|
| `AppDangerBrush` | `EnigmaErrorBrush` |
| `AppWarningBrush` | `EnigmaWarningBrush` |
| `AppSuccessBrush` | `EnigmaSuccessBrush` |
| `AppMutedBrush` | `EnigmaForegroundSecondaryBrush` |
| `AppPanelBrush` | `EnigmaSurfaceBrush` |
| `AppPanelBorderBrush` | `EnigmaBorderSubtleBrush` |

`TextBox` and `ComboBox` restyle themselves — the dictionary overrides FluentTheme's own
`TextControl*` / `ComboBox*` keys — so no hand-styling of either. Any app-level override of an
`Enigma*` key must be merged *after* `Fluent.axaml`, or it is silently ignored.

### Passwords

`ContentDialog.ShowAsync` resets the host's content only on the **next** call, so a password box left
in `Content` keeps the plaintext alive in the visual tree for as long as the app runs. Every password
dialog therefore clears `PasswordDialogViewModel.Password` and `.Confirmation` once the result has
been read (the box's text is bound, so it clears with them). Passwords remain parameters, never
state: prompted per run, never persisted, never logged.

## Package versions (CPM — `Directory.Packages.props`)

Added to the Desktop group, which stays version-coupled with Avalonia 12.1.1:

| Package | Version | Why |
|---|---|---|
| `Enigma.Avalonia.Desktop` | 1.0.0 | the control library |
| `Enigma.Icons` | 1.0.0 | icon core (`Geometry` conversion) |
| `Enigma.Icons.Phosphor` | 1.0.0 | the `PhosphorIcon` glyph set |
| `Enigma.Icons.Avalonia` | 1.0.0 | the `Icon` control + `{ei:IconGeometry}` markup extensions |

`Avalonia.Themes.Fluent` and `CommunityToolkit.Mvvm` stay explicit references — they now also arrive
transitively, and keeping them pinned here documents the versions rather than inheriting them.
Nothing else moves; the Avalonia set is *not* bumped as part of this item.

## Phases

### PHASE01 — Package, theme & icon foundation (TODO)

Branch: `feature/feature-17fe-phase01-theme-foundation`

1. Add the four packages to `Directory.Packages.props` (Desktop group, with the comment explaining
   the coupling) and `PackageReference`s without versions to `Enigma.Backup.Desktop.csproj`.
2. `App.axaml`: keep `<FluentTheme />`; merge the library dictionary as a `ResourceInclude`; delete
   the `ThemeDictionaries` block holding the six `App*` brushes.
3. Replace every `App*` brush reference across `MainWindow.axaml` and the five dialog Views with its
   `Enigma*` equivalent (table above), all `DynamicResource`.
4. Set `Background="{DynamicResource EnigmaBackgroundBrush}"` on `MainWindow` and on each dialog
   window (the theme paints controls, not windows).
5. Confirm the icon markup extension resolves: one icon in the UI (e.g. the *Back up* button) via
   `{ei:IconGeometry …}`, proving the `xmlns` and the pack wiring before later phases depend on them.

**Acceptance:** Debug **and** Release build clean, zero warnings **and zero `AVLN*` warnings**; full
suite green with no test changes; the app launches and every panel, dialog and status colour still
reads correctly in both variants (screenshots in the completion doc); no `App*` brush key remains in
the solution.

### PHASE02 — Dialogs & pickers on the library services (TODO)

Branch: `feature/feature-17fe-phase02-dialogs-pickers`

1. Write the registration extension for the library's six singletons (there is none in the package)
   and call it from `AddEnigmaBackupDesktop`.
2. Place `HostOverlay` / `HostDialog` / `HostInfoBar` in `MainWindow.axaml`'s root `Panel` **in that
   order** (the overlay is registered in PHASE03; the host may sit unused until then), set the
   `ContentDialog` size properties on the host in XAML — they are *not* reset between dialogs — and
   wire the five `RegisterHost` / `SetStorageProvider` calls in `App.axaml.cs`.
3. Convert the five dialog Views from `Window` to `UserControl` (each with `x:DataType`), keeping
   their ViewModels and their wording unchanged. Buttons map onto the dialog's own
   Primary/Secondary/Close; each content root gets a `KeyBinding` for Enter where the old window used
   `IsDefault`.
4. Rewrite `DialogService` over `IContentDialogService`: one `ShowAsync(configure)` per question,
   result read back off the ViewModel exactly as today, `DialogResult.None` treated as dismissal,
   cancellation mid-question resolved through `HideAsync()`, password fields cleared after reading.
5. Rewrite `FilePicker` over `IFileDialogService` / `IFolderDialogService` using the string-path
   extension overloads, preserving the archive and profile `FilePickerFileType` filters, the
   `SuggestedStartLocation` hint and the "drop anything without a local path" rule.
6. Delete `IMainWindowProvider`, `MainWindowProvider` and their registrations; update the Desktop
   DI-registration test accordingly.

**Acceptance:** build clean, zero `AVLN*` warnings; the full suite passes **with no changes to any
existing ViewModel test**; the DI test asserts the six library services and the retired provider;
manually, in the running app — every one of the five dialogs opens in-window, OK/Cancel, Enter and
Escape behave as before, a scrim click cancels, cancelling a run while a decision dialog is open
closes it and aborts, and all five pickers (files, folder, archive, import, export) still return
usable paths.

### PHASE03 — Main-window restyle (TODO)

Branch: `feature/feature-17fe-phase03-window-restyle`

1. Destination and options: "Save backups in" becomes a `TextEditor` with `Title` and the *Browse…*
   button in `ActionContent`; compression and *Encrypt with a password* become `SettingsCard` rows
   grouped in a `SettingsCardExpander`.
2. Excludes: the glob field becomes a `TextEditor` with *Add* in `ActionContent`, keeping the Enter
   `KeyBinding`; the two `ListBox`es keep their bindings and gain `EnigmaSurface`/`EnigmaBorderSubtle`
   framing.
3. Icons on the profile, input and action commands, and on the dialog headers (warning/error), from
   the Phosphor set.
4. `ProgressOverlayCard` (a `ContentControl` subclass in `Views/`, ours — the library ships none)
   exposing title, message/current item, `IsIndeterminate`, `Progress` and a Cancel command; plus
   `IProgressOverlay` + its `IOverlayService`-backed implementation, `show`/`update`/`hide` wrapped in
   `try`/`finally` so an exception can never leave the scrim up.
5. Remove the bottom panel's `ProgressBar` and the two status `TextBlock`s; the action row keeps
   *Back up* / *Restore…* / *Verify…*. Cancel now lives on the card.
6. `INotificationService` over `IInfoBarService`: on completion, map `RunSummary` to
   Title/Message/`InfoBarSeverity` (success → `Success`, warnings → `Warning`, failure → `Error`);
   hide it when the next run starts. The "Last run" panel keeps every detail it shows today.
7. `MainWindowViewModel` / `ProfileEditorViewModel` change only where they must — the progress and
   notification calls now go through the two new seams. `Harness` gains fakes for both.

**Acceptance:** build clean, zero `AVLN*` warnings; suite green including new assertions that a run
opens and closes the overlay exactly once and publishes one notification of the right severity;
manually — a backup, a restore and a verify each show the card with live progress and a working
Cancel, a mid-run unreadable-file dialog appears **above** the scrim and is clickable, the InfoBar
reports each outcome, and the whole window still reads correctly in Light and Dark (screenshots).

### PHASE04 — Theme preference & Settings dialog (TODO)

Branch: `feature/feature-17fe-phase04-settings`

1. `AppSettings` (theme variant, `formatVersion` guard in the shape of `ProfileSerializer`'s) and
   `IAppSettingsStore` / `AppSettingsStore` in `src/Enigma.Backup.Desktop/Settings/`, writing
   `<config>/Enigma.Backup/settings.json`. Load returns defaults for a missing, unreadable or
   malformed file, logging at warning level; save is atomic-enough (write, then replace) and never
   throws into the UI.
2. `SettingsDialogViewModel` + `SettingsDialogView` (`SettingsCard` row, `System` / `Light` / `Dark`),
   raised by a gear button in the profile bar through `IDialogService` — one new method there, one new
   fake response in `FakeDialogService`.
3. Applying a choice assigns `Application.Current.RequestedThemeVariant` (`Default` for `System`)
   through an interface so no ViewModel names Avalonia, and persists it.
4. Startup applies the stored variant before the window is shown.
5. Tests: store round-trip; missing file → `System`; corrupt JSON → `System` and no throw; unknown
   value → `System`; the ViewModel persists exactly once per change and applies through the seam.

**Acceptance:** build clean, zero `AVLN*` warnings; the new tests plus the whole suite green;
manually — the gear opens the dialog, each of the three choices repaints the app immediately, the
choice survives a restart, deleting `settings.json` returns the app to following the OS, and a
hand-corrupted `settings.json` still starts the app.

### PHASE05 — Release readiness: 1.1.0 (TODO)

Branch: `feature/feature-17fe-phase05-release`

1. `<Version>1.1.0</Version>` in `Directory.Build.props`.
2. `RELEASENOTES.md`: a top `1.1.0` section — what the UI now is, the four new packages with the
   ~4 MB size note, the new `settings.json` (additive; absent is normal), and an explicit
   compatibility statement that the `EBK1` version byte, profile JSON, archive naming and the sidecar
   format are unchanged and 1.0.x archives still open.
3. `README.md`: the what's-new callout and the Installing table's zip names at 1.1.0; the dependency
   list extended with the library and the icon packs (with Phosphor's upstream MIT attribution).
4. `SECURITY.md`: supported-versions row covering `1.1.x`.
5. `docs/QA-CHECKLIST.md`: rewritten where the UI moved — in-window dialogs (§§2, 4–6), the overlay
   card and its Cancel (§5), the InfoBar outcome, and a new section for the theme preference
   including the deleted/corrupt-file cases.
6. `docs/RELEASE.md`: only if a step no longer matches reality after the four phases.
7. `dotnet list package --outdated` / `--vulnerable --include-transitive`, findings recorded in the
   notes; the Avalonia set moves as a unit or not at all.
8. Print the merge/tag/publish runbook. **No zips, no tag, no push** — `./publish.sh`, the win-x64
   build on Windows, and the GitHub release stay with the user.

**Acceptance:** Release build clean and suite green at 1.1.0; every box in `docs/RELEASE.md` §1
either ticked or explicitly deferred to the user with a reason; no unreleased-version string left in
the docs; the runbook printed.

## Files expected to change

**Created** — `src/Enigma.Backup.Desktop/Hosting/` registration for the library's services (or an
extension of the existing one); `Views/ProgressOverlayCard.*`; `Views/SettingsDialogView.axaml(.cs)`;
`ViewModels/SettingsDialogViewModel.cs`; `Services/IProgressOverlay.cs` + implementation;
`Services/INotificationService.cs` + implementation; `Settings/AppSettings.cs`,
`Settings/IAppSettingsStore.cs`, `Settings/AppSettingsStore.cs`; new test files for the settings
store and the two new seams; `docs/done/FEATURE-17FE-PHASE01..05.md`.

**Modified** — `Directory.Packages.props`, `Directory.Build.props` (version),
`Enigma.Backup.Desktop.csproj`, `App.axaml`, `App.axaml.cs`,
`Hosting/ServiceCollectionExtensions.cs`, `Services/DialogService.cs`, `Services/IDialogService.cs`
(one settings method), `Services/FilePicker.cs`, `Views/MainWindow.axaml`, the five dialog Views and
their code-behind, `ViewModels/MainWindowViewModel.cs`, `ViewModels/ProfileEditorViewModel.cs`,
`tests/Enigma.Backup.Desktop.Tests/TestSupport/Harness.cs` + `FakeDialogService.cs`,
`tests/Enigma.Backup.Desktop.Tests/Hosting/ServiceCollectionExtensionsTests.cs`, `README.md`,
`RELEASENOTES.md`, `SECURITY.md`, `docs/QA-CHECKLIST.md`, `docs/roadmap.md`, this file, and
`CLAUDE.md` (the Desktop seam list and the `MainWindowProvider` rationale) via the per-phase
documentation freshness sweep.

**Deleted** — `Services/IMainWindowProvider.cs`, `Services/MainWindowProvider.cs`.

**Expected untouched** — all of `src/Enigma.Backup.Core/`, all of
`tests/Enigma.Backup.Core.Tests/` (including `Fixtures/`, which is never regenerated), `publish.sh`,
`publish.ps1`, `global.json`, `.editorconfig`.

## Risks

- **The host z-order.** If the three hosts are ever reordered to match the library's `setup.md`, a
  mid-run decision dialog disappears behind the overlay scrim and a backup that hits an unreadable
  file becomes unanswerable — the app looks hung. The order is asserted by a comment in
  `MainWindow.axaml` and exercised by the PHASE03 manual check.
- **A stuck overlay.** An exception between show and hide leaves the scrim up and the app unusable.
  Mitigated by `try`/`finally` in the implementation, not at the call sites, plus a test that a
  failing run still hides it.
- **Plaintext lingering in a closed dialog.** `ContentDialog` keeps its `Content` until the next
  `ShowAsync`; the mitigation is clearing the ViewModel's password fields after reading the result,
  and it is an acceptance criterion, not a convention.
- **Existing ViewModel tests quietly rewritten.** If PHASE02 or PHASE03 needs to change an existing
  test's expectations, that is a signal the seams moved further than planned — it is a finding to
  raise before changing the test.
- **`AVLN` warnings hiding behind a green build.** `TreatWarningsAsErrors` does not promote them.
  Every phase reads the count.
- **Publish-size growth (~4 MB/zip) from the Phosphor pack.** Accepted at planning; recorded in the
  release notes so it is not a surprise at release time.

## Overall acceptance criteria

1. The Desktop app references `Enigma.Avalonia.Desktop` and uses its theme, `ContentDialog`,
   `Overlay`, `InfoBar`, picker services, `SettingsCard` family and typed editors; no `App*` brush and
   no dialog `Window` remains.
2. `Enigma.Backup.Core` is byte-for-byte unchanged; no ViewModel references an Avalonia or library
   type; every library service is consumed only from `Desktop/Services/`.
3. Debug and Release builds are clean — zero warnings and zero `AVLN*` warnings — and the full suite
   passes, with the pre-existing ViewModel tests unmodified.
4. Backup, restore and verify still work end to end, including encryption, cancellation, the
   Skip/Retry/Abort loop with apply-to-all, and restore conflicts.
5. The theme preference persists, defaults to following the OS, and survives a missing or corrupt
   settings file.
6. The app is verified running on Linux with screenshots in both variants, per phase.
7. The repository is release-ready at 1.1.0 per `docs/RELEASE.md` §1, with the artifact steps left to
   the user.
