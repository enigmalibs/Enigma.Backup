# FEATURE-5D75 — App icon in the output directory

**Status:** DONE
**Type:** FEATURE
**Branch:** `feature/feature-5d75-icon-in-output-dir`
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

The application icon is written as `app.ico` to the build output directory, and therefore to the publish
directory, next to `Enigma.Backup.Desktop(.exe)`. Shortcuts, launchers and installers can then point at a
file instead of extracting the icon from the executable. Every Enigma app does this.

## Context & constraints

- `src/Enigma.Backup.Desktop/Assets/enigma-backup.ico` (the new art since FEATURE-9755) is the
  `ApplicationIcon` and the window's `Icon`. `Assets/**` is an `AvaloniaResource` (embedded), so nothing
  lands in `bin/` today.
- An item with `CopyToOutputDirectory` is also published, and it flows to the output of any referencing
  project (`Enigma.Backup.Desktop.Tests`).
- The root release-zip scripts publish with `PublishSingleFile=true`. A `None` item with
  `CopyToOutputDirectory` is a loose file beside the single-file exe: it is not bundled unless
  `ExcludeFromSingleFile` is false and `IncludeAllContentForSelfExtract` is set. `docs/RELEASE.md` §2
  checks that each staging directory holds **exactly one file**, so that claim changes to "the executable
  and `app.ico`".
- Siblings: GitClient, MarkdownEditor, TextEditor and Msi all copy their icon to the output root as
  `app.ico`.

## Steps

1. `Enigma.Backup.Desktop.csproj`: `<None Include="Assets/enigma-backup.ico" CopyToOutputDirectory="PreserveNewest" TargetPath="app.ico" />`,
   with a one-line comment giving the reason. The file stays an `AvaloniaResource` too.
2. Test `AppIconTests` (Desktop tests): `app.ico` is in `AppContext.BaseDirectory` and is byte-identical to
   `src/Enigma.Backup.Desktop/Assets/enigma-backup.ico`, found by walking up to the repository root.
3. Verify:
   - a Release build leaves `app.ico` in `src/Enigma.Backup.Desktop/bin/Release/net10.0/`;
   - `./publish.ps1 -Rid win-x64` leaves the exe and `app.ico` in `artifacts/publish/win-x64/`;
   - both copies are byte-identical to the asset.
4. `docs/RELEASE.md` §2: the staging directory holds the executable and `app.ico`.

## Acceptance criteria

- `app.ico` sits beside the executable in the build output and in the publish folder, byte-identical to the asset.
- The embedded resource still resolves (the window icon is unchanged).
- Release build has zero warnings. Whole suite green, new test included.

## Out of scope

- The PNG copy, and the Linux hicolor icons (FEATURE-61C4 installs those).

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Output name | `app.ico` at the output root | The name every other Enigma app ships | `enigma-backup.ico`; an `Assets/` sub-folder |
| Source asset name | Unchanged (`enigma-backup.ico`) | FEATURE-9755 kept it on purpose | Renaming the asset |
| Mechanism | `None` + `CopyToOutputDirectory` + `TargetPath` | Standard MSBuild, and it flows to publish without a custom target | A custom `Copy` target |
| Single-file zips | `app.ico` stays a loose file beside the exe, and RELEASE.md says so | The point is a file on disk that a shortcut can name | Bundling it inside the single-file exe |
| Test | One Desktop-suite test plus a manual build/publish check | The test pins the copy. The publish check is the real proof | No test |
