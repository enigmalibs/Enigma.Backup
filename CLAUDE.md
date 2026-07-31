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
```

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
  Avalonia.

Invariants that are easy to violate and must be preserved:

- **The core has zero UI dependencies and stays CLI-ready.** Every interactive decision enters
  through an interface — `IBackupErrorHandler` (Skip/Retry/Abort + apply-to-all), the restore
  conflict handler, progress via `IProgress<T>`. Never reference Avalonia, show a dialog, or read the
  console from Core; the Desktop implements those interfaces with dialogs, tests implement them
  programmatically.
- **Passwords are parameters, never state.** They cross the API as a parameter, are prompted per run,
  and are never persisted, logged, or cached.
- **The pipeline is single-pass and streaming.** No temp files, constant memory regardless of archive
  size; the SHA-256 sidecar is computed on the output bytes while writing.
- **Composition roots are the two `AddEnigmaBackup*` extensions** (`Core/Hosting/`,
  `Desktop/Hosting/ServiceCollectionExtensions.cs`). Register services there; nothing news up a
  service.
- **The `EBK1` container format is specified in the plan.** Field details may be refined *within*
  version 1 before the first release; any post-release change bumps the version byte.

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
  reference packages without a `Version`. The Avalonia set is version-coupled — bump it as a unit.
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
