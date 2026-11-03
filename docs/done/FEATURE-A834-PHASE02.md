# FEATURE-A834-PHASE02 — The buildmsi scripts

**Item:** FEATURE-A834 — Windows MSI build script
**Branch:** `feature/feature-a834-phase02-buildmsi-scripts`
**Run:** feature/2026-11-03-icons-msi-release

## Summary

`packaging/windows/buildmsi.ps1` builds the Windows installer in one command, from any directory. It is
Enigma.GitClient's script with this product's identity:

1. It checks that `git`, `dotnet`, `wix` and the vendored worker are there, **before** anything is
   cleaned.
2. It reads the version from `Directory.Build.props` and checks it is `X.Y.Z`.
3. `git -C <root> clean -dxf`.
4. `packaging/windows/publish.ps1`, the plain `-c Release -r win-x64 -p:PublishReadyToRun=true` publish.
   Then it checks that the exe and `app.ico` are in the publish folder.
5. It writes `artifacts/Enigma.Backup.X.Y.Z.msipkg.json`, an Enigma.Msi profile:
   - absolute paths, a new `productId` each build;
   - upgrade code `d60f30d6-32d0-4629-ba7d-f98eb9731a91`, new for this product line;
   - "Enigma Backup", "Josué Clément", `PerMachine`, `%ProgramFiles%\Enigma Backup`;
   - `compression` `None`;
   - `app.ico` as the Control Panel icon and both shortcuts' icon;
   - shortcuts in `%Desktop%` and `%ProgramMenu%`.
6. It runs `Enigma.Msi.Worker.exe build` on that profile into `artifacts/Enigma.Backup.X.Y.Z.msi`.

Every failure prints `buildmsi: <reason>` and exits non-zero, with the failing tool's exit code when
there is one. `buildmsi.bat` (CRLF) runs it from `cmd` with `-ExecutionPolicy Bypass` and passes the
exit code on. `WindowsInstallerTests` pins the upgrade code, the product name and the assembly.

## Files/modules touched

- Created:
  - `packaging/windows/buildmsi.ps1`
  - `packaging/windows/buildmsi.bat`
  - `tests/Enigma.Backup.Desktop.Tests/WindowsInstallerTests.cs`
  - `docs/done/FEATURE-A834-PHASE02.md`
- Modified:
  - `README.md`, `docs/RELEASE.md`, `CLAUDE.md`
  - `packaging/windows/Enigma.Msi.Worker/README.md`, `docs/done/FEATURE-A834-PHASE01.md`: the
    line-ending correction (below)
  - `docs/roadmap.md`, `docs/plan/FEATURE-A834.md`: statuses.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Script body | GitClient's, line for line, with the product's constants, names and help text | "exactly like done by Enigma.GitClient" |
| Where the tests live | One Desktop class, `WindowsInstallerTests`, three facts | The assembly is `typeof(App)`'s; Backup has no packaging tests in Core |
| `.bat` line endings | Written CRLF directly | `.gitattributes` asks for CRLF in `*.bat`; it then needs no renormalization |

## Deviations & follow-ups

- **PHASE01's record corrected.** It said the worker's `.exe.config` was LF and so identical in git. The
  source is CRLF and git stores LF, as in GitClient. The worker README and PHASE01's done doc now say
  so.
- **A 32-bit package.** Enigma.Msi has no platform setting, so the MSI's template is `Intel;1033`, and
  `%ProgramFiles%` is `Program Files (x86)` on 64-bit Windows. `docs/RELEASE.md` says so.
- **The MSI is the framework-dependent publish**, so the installed app needs the .NET 10 runtime. That
  differs from the self-contained release zips. It is written in the README and in RELEASE.md.
- **WixSharp's "source base directory"** printed the caller's current directory (this repository). It
  only resolves relative paths, and the profile's are absolute. `git status` here showed nothing new
  afterwards.
- **Not run, by design:** installing, launching or uninstalling the MSI.
- Line endings: `buildmsi.bat` is CRLF; the `.ps1` is LF, ASCII, no BOM.

## Build/test evidence

- Baseline: see `docs/done/FEATURE-F207.md` (0 warnings; 549 tests, 0 failed).
- `dotnet build Enigma.Backup.slnx -c Release`: **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Backup.slnx -c Release`: **552 total** (the baseline's 549 plus the 3 new
  tests), 540 passed, 12 skipped, 0 failed.
- The scripts' bytes: `buildmsi.ps1` 0 CR, 0 non-ASCII byte, no BOM; `buildmsi.bat` 14 CRLF lines.
- **End to end, in a scratch clone** of this branch with the dev's tree committed there (the run may not
  `git clean` the user's repository):
  - **No `wix` on the `PATH`:** `buildmsi: wix is not on the PATH: dotnet tool install --global wix.`,
    exit code 1, and an untracked canary file was still there, so nothing was cleaned.
  - **`buildmsi.bat`:**
    - git removed the canary; the publish ran; the worker built the MSI;
    - exit code **0**, in **19 s** wall time;
    - `artifacts/` holds `Enigma.Backup.1.3.0.msi` (70,524,747 bytes) and its `.msipkg.json`
      (`"compression": "None"`);
    - the published `app.ico` is byte for byte the new icon (FEATURE-F207);
    - `git status --porcelain` was empty afterwards. `packaging/windows/Enigma.Msi.Worker/.wixsharp/`
      appears only as ignored.
  - **The MSI's tables**, read through the Windows Installer API:
    - `ProductName` Enigma Backup, `Manufacturer` Josué Clément, `ProductVersion` 1.3.0 (the branch's
      version; FEATURE-9663 moves it to 1.4.0);
    - `UpgradeCode` `{D60F30D6-32D0-4629-BA7D-F98EB9731A91}`, `ALLUSERS` 1, `ARPPRODUCTICON`
      `app_icon.ico`;
    - `INSTALLDIR` "Enigma Backup" under `ProgramFilesFolder`;
    - two "Enigma Backup.lnk" shortcuts (`DesktopFolder`, `ProgramMenuFolder`) on
      `[INSTALLDIR]\Enigma.Backup.Desktop.exe`, icon `IconFile1_app.ico`;
    - `Upgrade`: `VersionMax 1.3.0` (attributes 1, `WIX_UPGRADE_DETECTED`) and `VersionMin 1.3.0`
      (attributes 2, `WIX_DOWNGRADE_DETECTED`);
    - 75 files, no native PDB (only the app's own `Enigma.Backup.Core.pdb` and
      `Enigma.Backup.Desktop.pdb`); template `Intel;1033`.
- Fix budget: 0 cycles used.

## Documentation sweep

- `README.md`:
  - a new *Windows installer* section after *A plain publish*: what `buildmsi` does (with the
    `git clean -dxf` warning), what the MSI installs, and the prerequisites;
  - the *Layout* line for `packaging/windows`;
  - *Installing* said "There is no installer". It now says the zip needs none, and points at the MSI.
- `docs/RELEASE.md`:
  - §2: the `packaging/` publish scripts produce no zip, and the installer is built from them;
  - §6: an optional step to attach the MSI, built on the tagged commit;
  - a new *The Windows installer* section: requirements, no committed profile, the fixed upgrade code,
    the new product code per build, the 32-bit package.
- `CLAUDE.md` *Commands*: the `buildmsi` line, and a paragraph on `git clean -dxf`, the vendored worker,
  the upgrade code that never changes, and why the script is ASCII.
