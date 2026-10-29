# FEATURE-0725 — Update NuGet packages to latest

**Status:** DONE
**Type:** FEATURE
**Branch:** `feature/feature-0725-update-nuget-packages`
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

Every package pinned in `Directory.Packages.props` is on its latest stable version. The solution still
builds with zero warnings and its whole suite is still green. This is the first item of the run, so every
later dev is built and tested against the libraries that ship.

## Context & constraints

`dotnet list Enigma.Backup.slnx package --outdated` on 2026-10-29 (nuget.org):

| Package | Pinned | Latest |
|---|---|---|
| Avalonia, .Desktop, .Fonts.Inter, .Themes.Fluent | 12.1.1 | 12.1.4 |
| Enigma.Avalonia.Desktop | 1.0.0 | 1.3.0 (nuspec: Avalonia 12.1.4, Enigma.Core ≥ 1.0.0) |
| Enigma.Core | 1.1.0 | 2.0.0 (major) |
| Microsoft.Extensions.Hosting, .DependencyInjection(.Abstractions), .FileSystemGlobbing | 10.0.10 | 10.0.14 |
| xunit.v3 | 3.2.2 | 4.0.2 (major) |

These are already at their latest: CommunityToolkit.Mvvm 8.4.2, AvaloniaUI.DiagnosticsSupport 2.2.3,
ZstdSharp.Port 0.8.8, and Enigma.Icons / .Phosphor / .Avalonia 1.0.0. Re-check them at build time.
`Microsoft.Extensions.Logging.Abstractions` 10.0.10 is pinned but referenced by no project, so `--outdated`
cannot see it.

- **The Avalonia set is coupled** (CLAUDE.md). Avalonia 12.1.x and Enigma.Avalonia.Desktop move together.
  Enigma.Avalonia.Desktop 1.3.0 is backward compatible with 1.0.0 (its release notes list no API, key or
  value change, only new keys, a new `DisplayDuration` and `Classes="secondary"`), and it is built
  against 12.1.4.
- **Enigma.Core 2.0.0 is a major release.** Its breaking changes are confined to the RSA and X.509 API
  (`RsaKey` handles). Backup uses only AES-256-GCM and Argon2id. Enigma.Avalonia.Desktop uses only
  `Enigma.Core.Encoding`. **`GoldenArchiveCompatibilityTests` is the gate**: it restores a real 1.0.0
  archive, so a behavioural change in the primitives shows up there. Never regenerate the fixture.
- xunit.v3 4.x is a major. Both suites are plain xunit (no Avalonia headless adapter), and Enigma.GitClient
  already runs 4.0.2.
- `docs/RELEASE.md` asks for every `old → new` to be logged in `RELEASENOTES.md` under *Dependencies*. That
  happens in the release item (FEATURE-93D8). This dev records the table in its completion doc.

## Steps

1. Re-run `dotnet list package --outdated` to confirm the table.
2. `Directory.Packages.props`: bump every row. Raise the unreferenced Logging.Abstractions pin to 10.0.14
   too, so the Microsoft.Extensions family stays on one version. Refresh the comments that name versions:
   the coupled set at 12.1.4, Enigma.Avalonia.Desktop at 1.3.0, and the Enigma.Core transitive note,
   which now reads "1.0.0 floor, resolved to 2.0.0".
3. Restore, then build Release with zero warnings. Fix any API break or new analyzer warning at its cause.
4. Run the whole suite, including `GoldenArchiveCompatibilityTests`.
5. If a package cannot move without a change outside this item's scope, hold it at its newest compatible
   version and record why in the props comment and the completion doc. For Enigma.Core specifically: a
   red golden test means holding it at 1.1.0, never touching the fixture.
6. `dotnet list package --vulnerable --include-transitive`: the result goes in the completion doc.

## Acceptance criteria

- `dotnet list package --outdated` reports nothing, apart from rows recorded as held back with a reason.
- Release build has zero warnings. Whole suite green, with `GoldenArchiveCompatibilityTests` passing and the
  fixture untouched.
- No vulnerable package (direct or transitive), or each one is recorded.

## Out of scope

- Adopting Enigma.Avalonia.Desktop 1.1–1.3 features the app does not need (timed info bars, secondary
  dialog background).
- The SDK pin in `global.json`.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Majors (Enigma.Core 2.0.0, xunit.v3 4.0.2) | Included | The spec asks for "latest". Enigma.Core's break is in an API Backup does not call. The golden test guards the primitives | Patch/minor only |
| Coupled Avalonia set | Moved as a whole to 12.1.4 with Enigma.Avalonia.Desktop 1.3.0 | House coupling rule. 1.3.0 is built against 12.1.4 | Avalonia alone; holding the set back |
| A package that cannot move | Held back and recorded | A dependency bump should not turn into an unrelated rewrite | Quarantining the whole item |
| Unreferenced Logging.Abstractions pin | Bumped with its family | One version for the Microsoft.Extensions set. Removing it is out of scope | Removing the pin; leaving it at 10.0.10 |
| Order in the run | First | Later devs are written and tested against the libraries that ship | Last, before the release |
