# FEATURE-5D75 — App icon in the output directory

## Summary

The app icon (`Assets/enigma-backup.ico`) is now also written as `app.ico` beside the executable: in the
build output, in every publish folder, and in the release zip. Shortcuts, launchers and installers can
point at a file rather than extracting the icon from the exe. Every Enigma app uses that name.

The embedded `AvaloniaResource` is unchanged, so the window icon is unaffected. The copy flows into the
Desktop test project's output, where `AppIconTests` pins it byte for byte against the asset.

## Files/modules touched

- Created:
  - `tests/Enigma.Backup.Desktop.Tests/AppIconTests.cs`
  - `docs/done/FEATURE-5D75.md`
- Modified:
  - `src/Enigma.Backup.Desktop/Enigma.Backup.Desktop.csproj`
  - `docs/RELEASE.md`
  - `README.md`
  - `docs/QA-CHECKLIST.md`
  - `docs/roadmap.md`
  - `docs/plan/FEATURE-5D75.md`

## Deviations & follow-ups

- **`ExcludeFromSingleFile="true"` was needed.** The plan's step 1 gave only `CopyToOutputDirectory` +
  `TargetPath`. That was enough for `bin/`, but the release zips are single-file publishes, and the
  first `publish.ps1 -Rid win-x64` bundled `app.ico` into the exe, leaving only the exe in the staging
  folder. The attribute keeps it loose, which is the point of the item.
- The linux-x64 zip was not built here (RELEASE.md: build it from Linux). The same item metadata applies
  to it.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Single-file bundle | Excluded (`ExcludeFromSingleFile`) | A bundled icon is back inside the exe, which defeats the item |
| Test lookup of the source asset | Walk up from `AppContext.BaseDirectory` to `src/Enigma.Backup.Desktop/Assets/enigma-backup.ico` | No new test-project item or MSBuild property just to locate one file |

## Documentation sweep

- `docs/RELEASE.md` §2: the staging directory now holds **two** files, the exe and `app.ico`.
- `docs/QA-CHECKLIST.md` 1.1: the zip extracts to two files. The historical 1.1.0/1.2.0 records were left
  as written.
- README *Installing*: says what `app.ico` is for, and that it goes too when uninstalling by deletion.

## Build/test evidence

- `dotnet build Enigma.Backup.slnx -c Release`: **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Backup.slnx -c Release`: **505 total, 493 passed, 12 skipped, 0 failed**
  (new: `AppIconTests.TheIconSitsBesideTheExecutableAsAppIco`).
- `cmp` against `Assets/enigma-backup.ico`: identical in all three places:
  - `src/Enigma.Backup.Desktop/bin/Release/net10.0/app.ico`;
  - `tests/Enigma.Backup.Desktop.Tests/bin/Release/net10.0/app.ico`;
  - `artifacts/publish/win-x64/app.ico`.
- `./publish.ps1 -Rid win-x64` with the attribute in place: the staging folder holds
  `Enigma.Backup.Desktop.exe` and `app.ico`, and the zip's entries are those two. Without it, the folder
  held the exe alone. `artifacts/` is gitignored.
