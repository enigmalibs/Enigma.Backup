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
```

`publish.sh` / `publish.ps1` build self-contained single-file apps into `artifacts/publish/<rid>/`
and zip them; `<Version>` in `Directory.Build.props` names the zips. Build the **linux-x64** zip from
Linux — `Compress-Archive` records no Unix file mode. Full procedure: `docs/RELEASE.md`.

**Warnings are build errors solution-wide** (`TreatWarningsAsErrors`), and the root `.editorconfig`
is enforced at build time (`EnforceCodeStyleInBuild`) — a style rule at `warning` severity fails the
build. Both live in `Directory.Build.props` / `.editorconfig` at the root; don't repeat those
properties in a `.csproj`.

## Architecture

Two production projects (`src/`), both `net10.0` on purpose:

- **`Enigma.Backup.Core`** — everything: profiles + JSON persistence, input scanning with glob
  excludes, the streaming `scan → tar → compress → [encrypt] → file` pipeline, restore, and
  verification. Targets `net10.0` rather than `netstandard2.0` because it is built on
  `System.Formats.Tar` (see the csproj comment).
- **`Enigma.Backup.Desktop`** — Avalonia 12 MVVM app, bootstrapped through
  `Microsoft.Extensions.Hosting` (`App.OnFrameworkInitializationCompleted` builds and starts the
  `IHost`, resolves `MainWindow` from DI, and stops the host on exit). The only project that touches
  Avalonia. Its look comes from the house control library **`Enigma.Avalonia.Desktop`**: `App.axaml`
  keeps `<FluentTheme />` in `Application.Styles` (the library themes only its own controls, plus
  `TextBox`/`ComboBox` via FluentTheme's key overrides — drop it and everything else goes
  untemplated) and merges `avares://Enigma.Avalonia.Desktop/Themes/Fluent.axaml` into
  `Application.Resources` as a **`ResourceInclude`**; a `StyleInclude` fails the build with
  `AVLN2000`. Colours are the library's `Enigma*` brush keys, **always `DynamicResource`** —
  `StaticResource` freezes the colour and the control stops following a theme switch. The window's own
  `Background` is ours to set (`EnigmaBackgroundBrush`): the theme paints controls, not windows.
  Icons come from `Enigma.Icons.Phosphor` through `xmlns:ei="https://github.com/josueclement/Enigma.Icons"`.

Invariants that are easy to violate and must be preserved:

- **The core has zero UI dependencies and stays CLI-ready.** Every interactive decision enters
  through an interface — `IBackupErrorHandler` (Skip/Retry/Abort + apply-to-all), the restore
  conflict handler, progress via `IProgress<T>`. Never reference Avalonia, show a dialog, or read the
  console from Core; the Desktop implements those interfaces with dialogs, tests implement them
  programmatically.
- **The Desktop's view models never name Avalonia either.** File pickers, dialogs and UI-thread
  marshaling enter through `IFilePicker` / `IDialogService` / `IUiDispatcher` (`Desktop/Services/`,
  with the Avalonia implementations beside them); only the Views, `App` and those implementations
  touch Avalonia types. This is what keeps `Enigma.Backup.Desktop.Tests` headless — including the
  end-to-end test that drives the real pipeline through the real container.
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
- **An in-window dialog has to be given the keyboard focus.** `ContentDialog` handles Escape itself and
  each dialog's content binds Enter, but both need the focus to be inside the dialog; a window used to
  grant that for free. Every dialog content takes it on attach (posted at `DispatcherPriority.Loaded`,
  because the content is attached while the host is still collapsed). Without it the keys reach the
  window *behind* the scrim. Note also that the dialog's own buttons ignore a command's `CanExecute`,
  so a question that validates input drives `IsPrimaryButtonEnabled` from its view model.
- **Passwords are parameters, never state.** They cross the API as a parameter, are prompted per run,
  and are never persisted, logged, or cached.
- **The pipeline is single-pass and streaming.** No temp files, constant memory regardless of archive
  size; the SHA-256 sidecar is computed on the output bytes while writing.
- **Composition roots are the two `AddEnigmaBackup*` extensions** (`Core/Hosting/`,
  `Desktop/Hosting/ServiceCollectionExtensions.cs`). Register services there; nothing news up a
  service.
- **`AvaloniaHostLifetime` is load-bearing, not dead code.** It is a no-op `IHostLifetime` that
  `AddEnigmaBackupDesktop` *replaces* the host builder's `ConsoleLifetime` with. Delete it as
  "an empty class" and the app silently goes back to **ignoring `SIGTERM`** — `ConsoleLifetime`
  cancels the signal and then waits for a `StopAsync` that never comes, because Avalonia owns the
  foreground loop and `App` calls `_host.Start()`, not `Run()`. A session logout hangs. Keep the
  `Replace` too: the host builder registers its own lifetime first, so `TryAdd` would do nothing.
- **The `EBK1` container format is specified in the plan.** Field details may be refined *within*
  version 1 before the first release; any post-release change bumps the version byte.
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
  `Enigma.Avalonia.Desktop` belongs to it (it pins Avalonia 12.1.1 itself, so it moves with the set or
  not at all). `Enigma.Core` arrives both from Core's own 1.1.0 pin and transitively from
  `Enigma.Avalonia.Desktop` at 1.0.0; the higher version wins, and
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
- The app icon `src/Enigma.Backup.Desktop/Assets/enigma-backup.ico` is placeholder art
  (16/32/48/256 px); final art comes from the user.
