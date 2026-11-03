# FEATURE-A834 — Windows MSI build script

**Status:** IN PROGRESS
**Type:** FEATURE
**Branch:** one per phase, see below
**Run:** feature/2026-11-03-icons-msi-release

## Objective

One command builds the Windows installer: `packaging/windows/buildmsi.ps1` (and a `buildmsi.bat` twin)
cleans the repository, publishes the app, and builds `artifacts/Enigma.Backup.X.Y.Z.msi` with Enigma.Msi.
The repository carries a copy of Enigma.Msi's worker CLI. The scripts work exactly like Enigma.GitClient's
(its FEATURE-EAB7), with "Enigma Backup" as the product name, its own upgrade code, and the version from
`Directory.Build.props`.

## Context & constraints

- **The template** is Enigma.GitClient (`C:\Dev\EnigmaLibs\Enigma.GitClient`, `develop` @ `a58d8fe`):
  - `packaging/windows/Enigma.Msi.Worker/`: 20 binaries and a `README.md`;
  - `packaging/windows/buildmsi.ps1`, `buildmsi.bat` (CRLF);
  - `.gitignore`: `packaging/windows/Enigma.Msi.Worker/.wixsharp/`;
  - `WindowsInstallerIdentityTests` (pins the upgrade code) and an `AssemblyNameConsistencyTests` case
    (pins `$Assembly`);
  - README *Windows installer* and `docs/RELEASE.md` *The Windows installer's identity*.
- **Enigma.Msi** (`C:\Dev\EnigmaLibs\Enigma.Msi`, tag `1.7.0` = `a7a1bfb`, clean tree):
  - Its worker, `Enigma.Msi.Worker.exe build <file.msipkg.json>`, is a net472 CLI. Exit codes: `0` built,
    `1` invalid package or failed build, `2` bad invocation.
  - Its built output `src/Enigma.Msi.Worker/bin/Release/net472/` is the 1.7.0 tag, and GitClient's
    vendored copy matches it byte for byte (all 19 binaries; the `.exe.config` differs only in line
    endings). Checked while planning.
  - It needs the WiX CLI (`dotnet tool install --global wix`); 7.0.0 is installed here.
- **No Enigma Backup is installed** on this machine (the Uninstall registry keys list Git Client, Text
  Editor, Msi, Markdown Editor and Launchpad only). No MSI of Enigma Backup was ever built, so there is
  no upgrade code to keep: this item creates the product line's one.
- **Publish** (FEATURE-251A): `packaging/windows/publish.ps1` runs `dotnet publish
  src\Enigma.Backup.Desktop -c Release -r win-x64 -p:PublishReadyToRun=true` and exits with dotnet's
  code. Result: `src\Enigma.Backup.Desktop\bin\Release\net10.0\win-x64\publish\`, with `app.ico` beside
  `Enigma.Backup.Desktop.exe` (FEATURE-5D75). The csproj's `RemoveNativeSymbolsFromPublish` keeps the
  Skia/HarfBuzz native PDBs out of it.
- **The version** lives in `Directory.Build.props` (`<Version>`), which is UTF-8 (`Josué` in
  `<Authors>`).
- `.gitignore` ignores `artifacts/`, `[Bb]in/`, `x64/` and more. The vendored folder must not match any
  of them, because `git clean -dxf` deletes ignored files.
- `.gitattributes` marks `*.dll` / `*.exe` binary; `*.bat` is CRLF.
- Windows PowerShell 5.1 reads a BOM-less script as ANSI, and `.editorconfig` says UTF-8 without BOM. So
  the `.ps1` stays ASCII.
- The app's own display name is `Enigma.Backup` (`AppInfo.Name`, the Linux launcher). The MSI's product
  name is "Enigma Backup", as the spec asks.
- **The run may not run `git clean`** in this repository. The script is verified end to end in a scratch
  clone instead.

## PHASE01 — Vendor the Enigma.Msi worker CLI

**Branch:** `feature/feature-a834-phase01-vendor-msi-worker`
**Status:** DONE

### Steps

1. Copy `C:\Dev\EnigmaLibs\Enigma.Msi\src\Enigma.Msi.Worker\bin\Release\net472\` into
   `packaging/windows/Enigma.Msi.Worker/`, without `*.pdb`, `Enigma.Msi.xml` and `.wixsharp/`.
2. `packaging/windows/Enigma.Msi.Worker/README.md`, GitClient's adapted to this repository:
   - what it is and what runs it;
   - its source (Enigma.Msi 1.7.0, `a7a1bfb`, WixSharp_wix4 2.14.2);
   - its requirements (.NET Framework 4.7.2, the WiX CLI, WiX 7's EULA);
   - `.wixsharp/`;
   - the components' licences;
   - how to refresh it.
3. `.gitignore`: `packaging/windows/Enigma.Msi.Worker/.wixsharp/`.
4. Check that `git status --porcelain --ignored` lists every vendored file as untracked (none ignored),
   and that the worker runs: no arguments → usage, exit code 2.

### Acceptance criteria

- Every vendored file is identical to its source (`cmp`; the `.exe.config` may differ only in line
  endings).
- No vendored file is git-ignored; `.wixsharp/` is.
- `Enigma.Msi.Worker.exe` with no arguments prints its usage and exits 2.
- The Release build has zero warnings; the whole suite shows no new failure against the run's baseline.

## PHASE02 — The buildmsi scripts

**Branch:** `feature/feature-a834-phase02-buildmsi-scripts`
**Status:** TODO

### Steps

1. `packaging/windows/buildmsi.ps1`, GitClient's with this product's identity (ASCII, `Set-StrictMode`,
   `$ErrorActionPreference = 'Stop'`, comment-based help), resolved from its own location:
   1. constants: `$UpgradeCode = 'd60f30d6-32d0-4629-ba7d-f98eb9731a91'`, `$ProductName = 'Enigma
      Backup'`, the manufacturer "Josué Clément" (built with `[char]0x00E9`),
      `$Assembly = 'Enigma.Backup.Desktop'`;
   2. check `git`, `dotnet` and `wix` are on the `PATH`, and the vendored worker exists;
   3. read and check (`^\d+\.\d+\.\d+$`) the version in `Directory.Build.props`;
   4. `git -C <root> clean -dxf`; stop on a non-zero exit code;
   5. run `publish.ps1`; stop on a non-zero exit code; check the exe and `app.ico` are in the publish
      folder;
   6. write `artifacts/Enigma.Backup.X.Y.Z.msipkg.json` (UTF-8 without BOM, absolute paths, a new
      `productId`): `PerMachine`, `%ProgramFiles%\Enigma Backup`, `compression` `None`,
      `controlPanel.productIcon` and both shortcuts' icon = the publish folder's `app.ico`, shortcuts
      `%Desktop%` and `%ProgramMenu%` named "Enigma Backup" targeting
      `[INSTALLDIR]\Enigma.Backup.Desktop.exe`;
   7. run `Enigma.Msi.Worker.exe build <profile>`; exit with its code; on success print the MSI's path
      and the elapsed time.
2. `packaging/windows/buildmsi.bat` (CRLF): runs `buildmsi.ps1` through `powershell -NoProfile
   -ExecutionPolicy Bypass -File`, and passes its exit code on.
3. Tests, `tests/Enigma.Backup.Desktop.Tests/WindowsInstallerTests.cs`, reading `buildmsi.ps1` from the
   repository root:
   - the upgrade code is `d60f30d6-32d0-4629-ba7d-f98eb9731a91`;
   - the product name is "Enigma Backup";
   - `$Assembly` is the Desktop assembly's name (`typeof(App).Assembly`).
4. Docs:
   - `README.md`: a *Windows installer* section after *A plain publish*, and the *Layout* line for
     `packaging/windows`;
   - `docs/RELEASE.md`: §2 says which scripts ship nothing, §6/§7 the MSI as an optional release
     asset, and a new *The Windows installer's identity* section (the upgrade code is fixed in the
     script, the product code is new per build);
   - `CLAUDE.md` *Commands*: `buildmsi.ps1`/`.bat`, and that it runs `git clean -dxf`.
5. Verify in a scratch clone: commit the dev's tree there, run `buildmsi.bat`, then read the MSI's
   `Property`, `Directory`, `Shortcut`, `Upgrade` and `Icon` tables through the Windows Installer API.

### Acceptance criteria

- In the scratch clone, `buildmsi.bat` exits 0 and leaves `artifacts/Enigma.Backup.<version>.msi` and
  its `.msipkg.json`. After it, `git status --porcelain` is empty.
- The MSI's tables show:
  - `ProductName` "Enigma Backup", `Manufacturer` "Josué Clément", `ProductVersion` = the
    `Directory.Build.props` version;
  - `UpgradeCode` `{D60F30D6-32D0-4629-BA7D-F98EB9731A91}`, `ALLUSERS` 1, `ARPPRODUCTICON` set;
  - `INSTALLDIR` "Enigma Backup" under `ProgramFilesFolder`;
  - two "Enigma Backup" shortcuts (`DesktopFolder`, `ProgramMenuFolder`) on
    `[INSTALLDIR]\Enigma.Backup.Desktop.exe`, each with an icon.
- The profile says `"compression": "None"`.
- A missing `wix` stops the script before `git clean`, with the install command in the message.
- The Release build has zero warnings; the whole suite (with the new tests) shows no new failure.

## Out of scope

- Installing, launching or uninstalling the MSI (outward-facing; the user's).
- Signing the MSI; a 64-bit package (Enigma.Msi has no platform setting).
- Changing Enigma.Msi itself, or the app's display name `Enigma.Backup`.
- A `-NoClean` switch or other options the spec does not ask for.
- A self-contained MSI (the MSI packages the framework-dependent publish, as GitClient's does).

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Upgrade code | A new GUID, `d60f30d6-32d0-4629-ba7d-f98eb9731a91`, drawn at random while planning and fixed forever | "its own UpgradeCode"; no MSI of Enigma Backup exists to inherit one from | GitClient's code (Enigma Backup would then replace Enigma Git Client) |
| How to use Enigma.Msi | A copy of the 1.7.0 worker CLI in `packaging/windows/Enigma.Msi.Worker/` | "exactly like done by Enigma.GitClient". Keeps a net472/WixSharp project out of a solution that is also built on Linux | A builder project of our own |
| Where the copy comes from | Enigma.Msi's own `bin/Release/net472/` output | It is the source of truth. GitClient's copy matches it, so the result is the same | Copying GitClient's folder |
| Which files are vendored | The net472 output without `*.pdb`, `Enigma.Msi.xml` and `.wixsharp/` | As GitClient | Everything |
| Profile | Generated by the script into `artifacts/Enigma.Backup.X.Y.Z.msipkg.json`, absolute paths | Version from `Directory.Build.props`, upgrade code a constant, so a release edits nothing | A committed profile per version under `msiProfiles/` |
| `productId` | A new GUID per build | Enigma.Msi: "can be generated freely" | Deriving it from the version |
| Names | Product, install folder and shortcuts "Enigma Backup"; MSI file `Enigma.Backup.X.Y.Z.msi`; manufacturer "Josué Clément" | The spec's name; GitClient's file pattern (`Enigma.GitClient.X.Y.Z.msi`); `<Authors>` | `Enigma.Backup` as the product name (the spec says "Enigma Backup") |
| Install path, scope | `%ProgramFiles%\Enigma Backup`, `PerMachine` | As GitClient and the sibling Enigma apps | `%ProgramFiles64%` (a 64-bit folder in a package Enigma.Msi builds as 32-bit) |
| Compression, UI | `None`; Enigma.Msi's default UI | As GitClient | — |
| The publish step | `buildmsi.ps1` calls `packaging/windows/publish.ps1` | One definition of the publish | The root `publish.ps1` (self-contained single file: a zip format, not an install folder) |
| `.bat` twin | `buildmsi.bat` runs `buildmsi.ps1` with `-ExecutionPolicy Bypass` | As GitClient and `publish.bat` | Duplicating the logic in batch |
| Prerequisites | `git`, `dotnet`, `wix` and the worker checked before anything is cleaned | Fails fast with the install command | Letting the worker fail after a full publish |
| Tests | One Desktop test class pinning the upgrade code, the product name and the assembly | Backup has no Core-side packaging tests, and the assembly is the Desktop's (`typeof(App)`); `AppIconTests` already walks to the repository root there | Splitting across Core and Desktop like GitClient |
| Verification | The full script in a scratch clone of the dev branch, then the MSI's tables read. Nothing installed | The run may not `git clean` the user's repository; installing is outward-facing | Running it in the repository |
| Phases | Two: the vendored worker, then the scripts | Where a binary comes from and whether a script is right are separate reviews | One phase |
