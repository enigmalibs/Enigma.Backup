# FEATURE-0725 — Update NuGet packages to latest

## Summary

Every package pinned in `Directory.Packages.props` is now on its latest stable version on nuget.org
(2026-10-29). A re-run of `dotnet list package --outdated` reports no updates for any of the four projects.

| Package | From | To |
|---|---|---|
| Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent, Avalonia.Fonts.Inter | 12.1.1 | 12.1.4 |
| Enigma.Avalonia.Desktop | 1.0.0 | 1.3.0 |
| Enigma.Core | 1.1.0 | 2.0.0 (brings BouncyCastle.Cryptography 2.7.0) |
| Microsoft.Extensions.Hosting, .DependencyInjection, .DependencyInjection.Abstractions, .FileSystemGlobbing, .Logging.Abstractions | 10.0.10 | 10.0.14 |
| xunit.v3 | 3.2.2 | 4.0.2 |

These were already at their latest and did not move: CommunityToolkit.Mvvm 8.4.2,
AvaloniaUI.DiagnosticsSupport 2.2.3, ZstdSharp.Port 0.8.8, Enigma.Icons / .Phosphor / .Avalonia 1.0.0.

- The coupled Avalonia set moved as a whole. Enigma.Avalonia.Desktop 1.3.0's nuspec pins Avalonia 12.1.4.
- Enigma.Core 2.0.0 is a major release, but its breaks are in the RSA/X.509 API, which this app never
  calls. Nothing needed changing.
- `GoldenArchiveCompatibilityTests` (the real 1.0.0 archive) verifies and restores under 2.0.0, and the
  fixture was not touched.

## Files/modules touched

- Modified:
  - `Directory.Packages.props`
  - `CLAUDE.md`
  - `docs/roadmap.md`
  - `docs/plan/FEATURE-0725.md`
- Created:
  - `docs/done/FEATURE-0725.md`

## Deviations & follow-ups

- None from the plan. No package had to be held back.
- The `Microsoft.Extensions.Logging.Abstractions` pin is still referenced by no project, as before. It was
  bumped with its family. Whether to drop it is a separate call.
- `RELEASENOTES.md` *Dependencies* is written by the release item (FEATURE-93D8) from the table above.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Comment on the transitive Enigma.Core | "1.0.0 floor, resolves to 2.0.0; only `Enigma.Core.Encoding` is used" | The source of Enigma.Avalonia.Desktop imports only that namespace, which 2.0.0 left unchanged. A runtime break there was the one risk the build could not see |
| Extra UI check | A smoke launch of the Release build, with a screenshot | Enigma.Avalonia.Desktop moved three minors. The main window renders exactly as before |

## Documentation sweep

- `CLAUDE.md` *Conventions*: the coupled-set version (12.1.4) and the Enigma.Core resolution (2.0.0 pin
  against a 1.0.0 floor).

## Build/test evidence

- `dotnet build Enigma.Backup.slnx -c Release`: **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Backup.slnx -c Release`: **489 total, 477 passed, 12 skipped, 0 failed**.
  The baseline before the bump was identical. The 12 skips are the existing platform skips (Unix file modes
  and names a Windows host cannot produce).
- `--filter-class "*GoldenArchiveCompatibilityTests"`: 2/2 passed. The test binaries carry
  `Enigma.Core.dll` 2.0.0.0.
- `dotnet list package --outdated`: "no updates given the current sources" for all four projects.
- `dotnet list package --vulnerable --include-transitive`: no vulnerable packages in any project.
- Smoke run: the Release `Enigma.Backup.Desktop.exe` started and showed the main window. It was
  screenshotted and closed.
