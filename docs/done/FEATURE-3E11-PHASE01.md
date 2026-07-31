# FEATURE-3E11-PHASE01 — Repo & solution scaffolding

**Completed:** 2026-07-31
**Branch:** `feature/feature-3e11-phase01-scaffolding`

## Summary

Bootstrapped the Enigma.Backup repository and solution from nothing: the git repository itself, the
house root configuration (hygiene, code style, build defaults, Central Package Management, SDK pin),
and the four projects the plan's architecture calls for — a UI-free `net10.0` core library, an
Avalonia 12 desktop app on an `IHost`, and one test project per production project.

The whole v1 dependency set is pinned centrally now (Avalonia 12.1.1, Enigma.Core 1.0.0,
ZstdSharp.Port 0.8.8, CommunityToolkit.Mvvm 8.4.2, FileSystemGlobbing, xunit.v3 3.2.2), so the later
phases add `<PackageReference>`s without touching versions.

Two seams exist from the scaffolding on, so no later phase has to retrofit them:
`IServiceCollection.AddEnigmaBackupCore()` (the core's composition root, empty until PHASE02) and
`AddEnigmaBackupDesktop()` (views + view models). `App` builds the host, restricts logging to the
console and debug sinks per the plan, and resolves `MainWindow` from the container.

## Files/modules touched

**Created — repository root**

- `.gitignore`, `.gitattributes` — verbatim from the `git-repo-hygiene` templates (LF normalization,
  binary rules incl. `*.ico`).
- `.editorconfig` — verbatim from the `dotnet-solution-config` template (full C# style, naming,
  analyzer severities). Enforced at build time via `EnforceCodeStyleInBuild`.
- `Directory.Build.props` — house defaults with `Authors`/`Copyright` filled (Josué Clément, 2026).
- `Directory.Packages.props` — CPM, the plan's full version set, grouped Core / Desktop / Tests.
- `global.json` — SDK 10.0.103 + `rollForward: latestFeature`, and the MTP `test.runner` entry.
- `Enigma.Backup.slnx` — `/src/` and `/tests/` solution folders, four projects.
- `LICENSE.md` (MIT), `README.md` (name, purpose, planned capabilities, layout, build instructions),
  `RELEASENOTES.md` (empty placeholder, filled at release time).
- `CLAUDE.md` — added from the documentation freshness sweep (accepted by the user): commands,
  architecture invariants (UI-free core, password-as-parameter, single-pass pipeline), the enforced
  conventions, the branch/status workflow, and the GUI-verification notes for this machine.

**Created — `src/Enigma.Backup.Core`**

- `Enigma.Backup.Core.csproj` — `net10.0` with a csproj comment justifying the deviation from the
  `netstandard2.0` library default (`System.Formats.Tar`, modern Span/Stream APIs); references
  `Microsoft.Extensions.DependencyInjection.Abstractions` only.
- `Hosting/ServiceCollectionExtensions.cs` — `AddEnigmaBackupCore()`.

**Created — `src/Enigma.Backup.Desktop`**

- `Enigma.Backup.Desktop.csproj` — `WinExe`/`net10.0`, Avalonia 12.1.1 set, `AvaloniaUI.DiagnosticsSupport`
  Debug-only, compiled bindings on, `ApplicationIcon` + `AvaloniaResource Assets/**`.
- `Program.cs` — classic desktop lifetime, `WithDeveloperTools()` under `#if DEBUG`, Inter font;
  keeps the parameterless `BuildAvaloniaApp()` the XAML previewer needs.
- `App.axaml` / `App.axaml.cs` — Fluent theme; host built, started and stopped with the app lifetime;
  logging reduced to console + debug providers; `MainWindow` resolved from DI.
- `Hosting/ServiceCollectionExtensions.cs` — `AddEnigmaBackupDesktop()`.
- `ViewModels/MainWindowViewModel.cs` — explicit `ObservableObject` property (no source generators).
- `Views/MainWindow.axaml` / `.axaml.cs` — `x:DataType`-typed empty shell, `Title` bound, window icon
  wired; previewer ctor + DI ctor.
- `Assets/enigma-backup.ico` — placeholder art, one file with 16/32/48/256 px frames.
- `app.manifest` — per-monitor DPI awareness (Windows).

**Created — tests**

- `tests/Enigma.Backup.Core.Tests` — csproj (`Exe`, xunit.v3, `xunit.runner.json`) +
  `Hosting/ServiceCollectionExtensionsTests.cs` (2 tests).
- `tests/Enigma.Backup.Desktop.Tests` — csproj + `ViewModels/MainWindowViewModelTests.cs` (3 tests).

**Modified**

- `docs/roadmap.md`, `docs/plan/FEATURE-3E11.md` — PHASE01 `TODO` → `IN PROGRESS` → `DONE`, item
  `TODO` → `IN PROGRESS`; correction note added to the plan's PHASE01 section (see below).

## Deviations & follow-ups

- **The plan's repo precondition did not hold.** *Validated decisions* stated that `git init` plus the
  house `.gitignore` / `.gitattributes` / `.editorconfig` had been done at planning time; the working
  directory contained only `docs/`. Agreed with the user before building: the repository was
  initialized here and PHASE01 absorbed all three hygiene files. `feature/feature-3e11-phase01-scaffolding`
  is therefore the repository's initial branch — its first commit is the root commit, and there is no
  base branch yet (creating `main` from that commit afterwards is the user's call).
- **Test project names** follow the plan (`*.Tests`) and the sibling repos (Draw, HardCopy), not
  `xunit-v3` v2's preferred `<Project>.UnitTests`. In-repo consistency and the validated plan win;
  a sibling `*.IntegrationTests` project would still be unambiguous.
- **`LICENSE.md`**, not the plan's `LICENSE` — the `dotnet-solution-setup` checklist owns that file
  and names it `LICENSE.md`; `README.md` links it.
- **`README.md` was written as a skeleton** (plan, PHASE01 step 4) rather than left as the empty
  placeholder the bootstrap checklist describes; PHASE06 completes it.
- **`GenerateDocumentationFile` is not set** on the core library, matching the closest sibling
  precedent (`Draw.Model`, an app-internal library) rather than the packaged-library template.
  Enigma.Backup.Core ships inside the app and is never packed. Public API still carries XML docs by
  hand; turning the property on later would make missing docs build errors under
  `TreatWarningsAsErrors` for phases 02–04.
- **DI extensions use C# 14 `extension(...)` blocks** per the `csharp-extension-methods` skill; the
  sibling repos still use the legacy `this`-parameter form. New repo, so the current house rule
  applies — worth knowing when copying code across from Draw/HardCopy.
- **`xunit.v3` (plain) works** with the .NET 10 SDK in MTP mode — `dotnet test --solution` ran both
  projects. Draw's `xunit.v3.mtp-v2` variant was not needed; noted in case the runner changes later.
- **Namespace note for PHASE04:** the `Enigma.Core` NuGet package and this solution's
  `Enigma.Backup.Core` namespace only stay unambiguous because the house convention puts `using`
  directives *outside* the namespace (compilation-unit scope). Keep them there in the crypto code.
- **Line endings:** the tree is 100% LF today and `.gitattributes` now normalizes to LF, so there is
  nothing to recommend. (No CRLF action taken — line endings remain the user's call.)
- **Follow-up:** placeholder app icon — final `.ico` art is supplied by the user later (plan
  assumption); confirmed again at PHASE06.

## Build/test evidence

- `dotnet restore Enigma.Backup.slnx` — all four projects restored.
- `dotnet build Enigma.Backup.slnx` — **Build succeeded, 0 Warning(s), 0 Error(s)**.
- `dotnet build Enigma.Backup.slnx -c Release` — **0 Warning(s), 0 Error(s)** (proves the
  Debug-only `DiagnosticsSupport` asset condition and the `#if DEBUG` developer-tools call both
  compile out cleanly).
- `dotnet test --solution Enigma.Backup.slnx` — **total: 5, failed: 0, succeeded: 5, skipped: 0**
  (Core.Tests 2, Desktop.Tests 3), MTP runner.
- `dotnet sln Enigma.Backup.slnx list` — lists exactly the four expected projects (the "solution
  opens cleanly" criterion, checked without an IDE).
- `dotnet run --project src/Enigma.Backup.Desktop` on Linux (Wayland/XWayland) — the app starts,
  logs `Application started`, and shows the centred, correctly titled main window; screenshot taken
  and the placeholder icon appears in the taskbar. Process stopped afterwards.
- `git status` — only intended files untracked; `bin/`/`obj/` correctly ignored by `.gitignore`.
