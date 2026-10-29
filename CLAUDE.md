# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Enigma.Backup is a cross-platform Avalonia 12 / .NET 10 desktop app that packs selected files and
folders into a single compressed — optionally AES-256-GCM encrypted — archive, and restores and
verifies it again. The work is planned phase by phase: read `docs/plan/FEATURE-3E11.md` first (the
canonical plan: validated decisions, architecture, the `EBK1` container spec, per-phase steps and
acceptance criteria) and `docs/roadmap.md` (status of every item/phase). `README.md` covers what the
app is and how to build it. This file captures only what those don't, or what is easy to get wrong.

## Commands

```bash
dotnet build Enigma.Backup.slnx                 # build all (solution is .slnx, not .sln)
dotnet build Enigma.Backup.slnx -c Release      # also check Release: DiagnosticsSupport/#if DEBUG paths
dotnet run --project src/Enigma.Backup.Desktop  # run the desktop app
dotnet test --solution Enigma.Backup.slnx       # xUnit v3 on MTP — note: --solution, not a positional arg
./publish.sh                                    # release zips for win-x64 + linux-x64 (publish.ps1 on Windows)
./packaging/linux/publish.sh                    # plain ReadyToRun publish into bin/ (windows/publish.ps1|.bat)
./packaging/linux/install.sh                    # per-user Linux install; uninstall.sh removes it
```

`publish.sh` / `publish.ps1` build self-contained single-file apps into `artifacts/publish/<rid>/`
and zip them; `<Version>` in `Directory.Build.props` names the zips. Build the **linux-x64** zip from
Linux — `Compress-Archive` records no Unix file mode. Full procedure: `docs/RELEASE.md`. The
`packaging/` scripts are the plain publish and the Linux installer every Enigma app has, not the release:
keep `packaging/linux/*.sh` at mode `100755` (`git update-index --chmod=+x` when adding one from Windows)
and `publish.bat` CRLF (`.gitattributes` does the latter).

**Warnings are build errors solution-wide** (`TreatWarningsAsErrors`), and the root `.editorconfig`
is enforced at build time (`EnforceCodeStyleInBuild`) — a style rule at `warning` severity fails the
build. Both live in `Directory.Build.props` / `.editorconfig` at the root; don't repeat those
properties in a `.csproj`.

## Architecture

Two production projects (`src/`), both `net10.0` on purpose:

- **`Enigma.Backup.Core`** — everything: profiles + JSON persistence, input scanning with glob
  excludes and pattern inputs, the streaming `scan → tar → compress → [encrypt] → file` pipeline (or
  `scan → zip → file`), restore, and verification. Targets `net10.0` rather than `netstandard2.0`
  because it is built on `System.Formats.Tar` (see the csproj comment).
- **`Enigma.Backup.Desktop`** — Avalonia 12 MVVM app, bootstrapped through
  `Microsoft.Extensions.Hosting` (`App.OnFrameworkInitializationCompleted` builds and starts the
  `IHost`, resolves `MainWindow` from DI, and stops the host on exit). The only project that touches
  Avalonia. Its look comes from the house control library **`Enigma.Avalonia.Desktop`**: `App.axaml`
  keeps `<FluentTheme />` in `Application.Styles` (the library themes only its own controls, plus
  `TextBox`/`ComboBox` via FluentTheme's key overrides — drop it and everything else goes
  untemplated) and merges `avares://Enigma.Avalonia.Desktop/Themes/Fluent.axaml` into
  `Application.Resources` as a **`ResourceInclude`**; a `StyleInclude` fails the build with
  `AVLN2000`. Our own `Views/ProgressOverlayCard.axaml` is merged **after** it — a control theme of ours
  reads the library's brushes, and on a duplicate key the last dictionary wins, so anything of ours
  merged *before* it is silently ignored. Colours are the library's `Enigma*` brush keys, **always `DynamicResource`** —
  `StaticResource` freezes the colour and the control stops following a theme switch. The window's own
  `Background` is ours to set (`EnigmaBackgroundBrush`): the theme paints controls, not windows.
  Icons come from `Enigma.Icons.Phosphor` through `xmlns:ei="https://github.com/josueclement/Enigma.Icons"`.

Invariants that are easy to violate and must be preserved:

- **The core has zero UI dependencies and stays CLI-ready.** Every interactive decision enters
  through an interface — `IBackupErrorHandler` (Skip/Retry/Abort + apply-to-all), the restore
  conflict handler, progress via `IProgress<T>`. Never reference Avalonia, show a dialog, or read the
  console from Core; the Desktop implements those interfaces with dialogs, tests implement them
  programmatically.
- **The Desktop's view models never name Avalonia either.** File pickers, dialogs, the run card, the
  outcome banner, the theme variant, opening a folder on the desktop, and UI-thread
  marshaling enter through `IFilePicker` / `IDialogService` / `IProgressOverlay` /
  `INotificationService` / `IThemeSwitcher` / `IShellLauncher` / `IUiDispatcher`
  (`Desktop/Services/`, with the Avalonia
  implementations beside them); only the Views, `App` and those implementations touch Avalonia types.
  `IShellLauncher` is the one whose implementation is *also* registered under its concrete type, because
  `App` has to hand `ShellLauncher` the window's `TopLevel` and the interface deliberately names no
  Avalonia type — both registrations resolve to one instance, and a test pins that they do.
  This is what keeps `Enigma.Backup.Desktop.Tests` headless — including the end-to-end test that drives
  the real pipeline through the real container, which therefore has to register fakes for the two seams
  whose real implementations reach for a host control.
- **The control library's own services are consumed only inside `Desktop/Services/`, behind those
  interfaces — no view model ever injects one.** `IContentDialogService` hands the caller an Avalonia
  control and the picker services deal in `IStorageFile`, so injecting one anywhere else breaks the
  invariant above. They are registered by `AddEnigmaAvaloniaDesktop()` (the package ships no such
  extension) and each is handed its host control — or the window's storage provider — once in
  `App.OnFrameworkInitializationCompleted`, before the window is shown; skip that and the first call
  throws. Since dialogs and pickers no longer need an owner `Window`, nothing supplies one: don't
  reintroduce a service that holds the main window.
- **`MainWindow.axaml`'s root `Panel` ends with `Overlay` → `ContentDialog` → `InfoBar`, in that
  order.** It is deliberately *not* the order in the library's `setup.md`: a `Panel` z-stacks its
  children, and a mid-run decision (unreadable file, restore conflict) is raised *while* the run
  overlay is open — behind the scrim it cannot be clicked and the app looks hung.
- **A run's progress lives on the overlay, and the scrim must always come down.** There is one card
  (`Views/ProgressOverlayCard`, ours — the library ships none), raised and taken away by
  `MainWindowViewModel.RunAsync`, whose `finally` is what guarantees the second half: an exception
  between show and hide leaves a scrim over an app nobody can click. Progress reaches both the view
  model's own properties and the card through the single `ReportProgress` method, so the two cannot
  drift; Cancel is *on* the card, because behind the scrim a button in the window cannot be pressed.
- **Never `await IInfoBarService.ShowAsync`.** Its task completes when the banner is **dismissed**, not
  when it appears (`InfoBar.ShowAsync` resolves a `TaskCompletionSource` from its `Closed` event), so
  awaiting it holds the finishing run open until the user clicks the ✕. `NotificationService` starts it
  and hands the task back only if it has already completed — which is how a host that was never
  registered still surfaces as an exception.
- **An in-window dialog has to be given the keyboard focus.** `ContentDialog` handles Escape itself and
  each dialog's content binds Enter, but both need the focus to be inside the dialog; a window used to
  grant that for free. Every dialog content takes it on attach (posted at `DispatcherPriority.Loaded`,
  because the content is attached while the host is still collapsed). Without it the keys reach the
  window *behind* the scrim. Note also that the dialog's own buttons ignore a command's `CanExecute`,
  so a question that validates input drives `IsPrimaryButtonEnabled` from its view model.
- **The dialogs take turns.** `DialogService` holds a one-slot gate from the moment a dialog is raised
  until it has closed, because the one host replaces whatever it holds: a dialog asked for while another
  is open waits. So never show a dialog from inside another dialog's handler — it would wait on itself
  forever — and never put content on `IContentDialogService` around `DialogService`, which would bypass
  the gate.
- **Unhandled exceptions end in the safety net, which waits its turn.** `UnhandledExceptionHooks`
  (static: `Install` is `Main`'s first line, `InstallDispatcher` the first line of `App`'s composition)
  hands every exception nothing else caught to `UnhandledErrorReporter`. The reporter logs it at once,
  then shows it through `IDialogService.ShowUnhandledErrorAsync`, behind any open question. It is
  attached on the window's `Opened` — reports before that are buffered — and detached on `Exit` before
  the host stops. `UnhandledErrorPolicy` decides what is shown: one dialog at a time, later errors counted
  into it, the same error again within a minute only logged, requested cancellations never. It is a
  safety net, not error handling: a failure the app expects is still said where it happens. A failure
  inside `App.Compose` — before there is a container or a main window — is caught in
  `OnFrameworkInitializationCompleted`, which disposes the half-built host and makes a
  `StartupErrorWindow` the main window instead: the same dialog on a host of its own, configured by hand
  because `DialogService` may be what failed. Closing it ends the app.
- **The app's own preferences are Desktop-owned, and losing them is never fatal.** The theme variant
  (`System` / `Light` / `Dark`, default `System`) lives in `Desktop/Settings/` and is written to
  `<config>/Enigma.Backup/settings.json`, beside the `profiles/` folder — deliberately *not* in Core: a
  theme variant means nothing to a CLI, so a settings concept there would be UI leaking inward past the
  first invariant above. `IAppSettingsStore` is synchronous because the stored variant has to be applied
  before the window is shown, and neither of its calls throws — a missing, unreadable or malformed file
  reads as the defaults with a log line, so a hand-edited preference can never stop the app from
  opening. `System` is a value of its own (`ThemeVariant.Default`), not the absence of one: collapse it
  into "whatever we last saw" and "follow the OS" becomes unreachable after the first click.
- **Passwords are parameters, never state.** They cross the API as a parameter, are prompted per run,
  and are never persisted, logged, or cached.
- **The pipeline is single-pass and streaming.** No temp files, constant memory regardless of archive
  size; the SHA-256 sidecar is computed on the output bytes while writing. The one documented
  exception is zip's central directory, which is held until the archive closes — ~100 bytes per
  *entry*, linear in entry count and still independent of archive size.
- **`Zip` is a sibling of the container, not a change to it, and it is never encrypted.** It sits in
  `CompressionAlgorithm` because the user's choice is "what kind of file do I get" — one control — but
  a zip is an archive, not a stream codec, so it does **not** go through `CompressionStreams`:
  `BackupService` writes through the internal `IArchiveEntryWriter` seam (`TarEntryWriter` /
  `ZipEntryWriter`), and `RestoreService` branches to a zip reader that feeds the *same* traversal
  check, conflict handler and progress. `VerificationService` needs no zip arm at all — it hashes
  bytes against the sidecar. Two things are load-bearing: `HashingWriteStream.CanSeek` is `false`, which
  is what makes `ZipArchive` write trailing data descriptors instead of seeking back to patch a header
  the digest already covered; and `ZipEntryWriter` writes **local** wall-clock timestamps, because a DOS
  date carries no zone and every tool — including `ZipArchiveEntry.LastWriteTime` on the way back in —
  reads one as local. `Encrypt` + `Zip` is refused by `BackupService` before the output is named, and
  the Desktop clears and disables the toggle (`ProfileEditorViewModel.CanEncrypt`), so `.zip.enc` is
  unreachable rather than merely unwanted. `ArchiveFormatDetector` knows **two** zip signatures:
  `PK\x03\x04`, and `PK\x05\x06` for an entry-less archive, which a run whose every input was skipped
  really does produce.
- **Composition roots are the two `AddEnigmaBackup*` extensions** (`Core/Hosting/`,
  `Desktop/Hosting/ServiceCollectionExtensions.cs`). Register services there; nothing news up a
  service.
- **`AvaloniaHostLifetime` is load-bearing, not dead code.** It is a no-op `IHostLifetime` that
  `AddEnigmaBackupDesktop` *replaces* the host builder's `ConsoleLifetime` with. Delete it as
  "an empty class" and the app silently goes back to **ignoring `SIGTERM`** — `ConsoleLifetime`
  cancels the signal and then waits for a `StopAsync` that never comes, because Avalonia owns the
  foreground loop and `App` calls `_host.Start()`, not `Run()`. A session logout hangs. Keep the
  `Replace` too: the host builder registers its own lifetime first, so `TryAdd` would do nothing.
- **The `EBK1` container format is specified in the plan, and version 1 is now frozen.** The window
  for refining field details *within* version 1 closed at 1.0.0: archives written by every released
  build are in people's hands, so **any** change to the layout bumps the version byte and is called
  out as breaking in `RELEASENOTES.md`. Every release since has carried the opposite claim — that the
  byte is unchanged and old archives still open — and `GoldenArchiveCompatibilityTests` is what backs
  it.
- **A profile is written as the lowest format version that can express it.**
  `BackupProfile.CurrentFormatVersion` is 2 and `MinimumFormatVersion` is 1; `ProfileSerializer` reads
  both and writes **2 only when the profile carries something version 1 has no field for, or names a
  value version 1 never knew** — today a `PatternInputs` entry, or `Compression: Zip` — and 1
  otherwise, stamping the profile with what it wrote. That rule *is* the
  compatibility claim in the release notes: a profile using no newer feature is still readable by
  1.0.x/1.1.0, while one that does use a newer feature is refused loudly by those builds instead of
  being read as less than it says. So a new feature extends `RequiredFormatVersion`, never the
  unconditional write; a serializer test pins that a feature-free profile still writes 1. Reading is
  the lenient half — a hand-edited version-1 file carrying a version-2 field is read rather than
  refused, and the next save states 2, which is what the profile actually is.
- **A pattern input is a rule, not a place, and it never merges.** `PatternInput` is a
  *(base folder, glob)* pair re-resolved on every run, matched by `PatternMatcher` — the exclude
  field's `Matcher` and `PathUtilities.Comparison`, in the include direction, plus one documented
  extension: a pattern matching a *directory's* relative path takes that directory's whole subtree
  (what `DirPrefix*` means). The scan resolves each pattern independently of the path-input
  merge/overlap logic, so a base folder that is also a path input becomes a second top-level name via
  `AllocateRootName`; excludes still win over a match; the base folder is emitted even when nothing
  matched, and a match-free pattern is a `PatternMatchedNothing` **warning**, never an error — there
  may be no logs today, and a pattern that quietly backs up nothing is the failure a backup tool must
  not have.
- **`tests/Enigma.Backup.Core.Tests/Fixtures/` is evidence, not test input — never regenerate it.**
  It holds a real encrypted archive written by the 1.0.0 build, and `GoldenArchiveCompatibilityTests`
  verifies and restores it on every run. That is the only check that can catch a behavioural change
  in Argon2id or AES-256-GCM arriving through an `Enigma.Core` / BouncyCastle bump: every other
  encryption test seals and opens with the *same* build, so it agrees with itself whatever the
  primitives do. Regenerating the fixture to make a red test pass turns it into a same-version round
  trip, silently deletes the evidence, and leaves "your old backups still open" untested — which is
  why `Fixtures/README.md` says so too. A failure there means archives people already hold can no
  longer be read; investigate what changed under the cipher.

## Conventions (deliberate, enforced)

- C# 14, `Nullable=enable`, **no implicit usings** — every file declares its own `using` directives,
  **outside** the namespace. This is load-bearing here: it is what keeps the `Enigma.Core` NuGet
  package's namespace unambiguous against this solution's `Enigma.Backup.Core`.
- File-scoped namespaces, Allman braces, `I`-prefixed interfaces, `_camelCase` private fields.
- **No MVVM source generators.** Explicit `ObservableObject` properties with the `field` keyword +
  `SetProperty`, and get-only `RelayCommand`/`AsyncRelayCommand` properties initialized in the
  constructor. Never `[ObservableProperty]` / `[RelayCommand]`.
- **Compiled bindings**: every view root and `DataTemplate` needs `x:DataType`.
- **UI strings are hardcoded English** — an explicit decision for this app; no `.resx`.
- **Central Package Management**: versions live only in `Directory.Packages.props`; `.csproj` files
  reference packages without a `Version`. The Avalonia set is version-coupled — bump it as a unit, and
  `Enigma.Avalonia.Desktop` belongs to it (it pins Avalonia 12.1.4 itself, so it moves with the set or
  not at all). `Enigma.Core` arrives both from Core's own 2.0.0 pin and transitively from
  `Enigma.Avalonia.Desktop` as a 1.0.0 floor; the higher version wins, and
  `GoldenArchiveCompatibilityTests` is what guards that resolution.
- **New extension methods use C# 14 `extension(...)` blocks**, not the legacy `this`-parameter form
  (the sibling repos Draw/HardCopy still use the old form — don't copy it across).
- Tests are `tests/<Project>.Tests` (xUnit v3, `xunit.v3` package, MTP via `global.json`). Desktop
  tests are headless ViewModel tests only — no UI automation.

## Workflow

Tracked work follows the house `dev-workflow` skill: one branch per phase
(`feature/feature-3e11-phaseNN-<slug>`), status flipped in `docs/roadmap.md` + the plan file, a
completion record in `docs/done/<ID>.md`, and **commits are left to the user** — never commit.

Note that `feature/feature-3e11-phase01-scaffolding` is the repository's initial branch (the root
commit), so there is no base branch to fork later phases from other than the current `HEAD`.

## Environment notes

- The GUI runs on this Linux machine (Wayland + XWayland, `DISPLAY=:1`): launching the app and
  screenshotting it (e.g. `spectacle -b -n -f -o out.png`) works, so verify UI changes for real.
  Kill the process by PID afterwards — a `pkill -f` pattern that matches the launching shell will
  kill the shell too.
- The app icon `src/Enigma.Backup.Desktop/Assets/enigma-backup.ico` (ten PNG frames, 16–256 px) and
  `enigma-backup.png` (its 256-px frame) are the final art, copied byte for byte from the user's
  `Branding/Apps/final/backup` set, whose SVG masters live outside this repository. Replace them from
  there, never by re-rendering here.
