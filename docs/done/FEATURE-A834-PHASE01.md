# FEATURE-A834-PHASE01 — Vendor the Enigma.Msi worker CLI

**Item:** FEATURE-A834 — Windows MSI build script
**Branch:** `feature/feature-a834-phase01-vendor-msi-worker`
**Run:** feature/2026-11-03-icons-msi-release

## Summary

The repository now carries Enigma.Msi's headless worker CLI in `packaging/windows/Enigma.Msi.Worker/`.
PHASE02's `buildmsi.ps1` runs it as `Enigma.Msi.Worker.exe build <profile>`. It is Enigma.Msi `1.7.0`
(`a7a1bfb`) built against WixSharp_wix4 2.14.2: the 20 files of its `bin/Release/net472/` output,
without the PDBs, the API XML and `.wixsharp/` (3.4 MB). These are the same 20 files Enigma.GitClient
vendors.

- A `README.md` in the folder gives:
  - its source;
  - the CLI's exit codes;
  - the requirements: .NET Framework 4.7.2 and the WiX CLI, with WiX 7's EULA;
  - `.wixsharp/`;
  - the components' licences: Enigma.Msi, WixSharp and .NET are MIT; the WiX 4.0.1 assemblies are MS-RL;
  - how to refresh it.
- `.gitignore` ignores `packaging/windows/Enigma.Msi.Worker/.wixsharp/`. WixSharp writes it beside the
  worker on its first build. `git clean -dxf` removes it, and the next build writes it again.

## Files/modules touched

- Created:
  - `packaging/windows/Enigma.Msi.Worker/` (20 binaries: `Enigma.Msi.Worker.exe`,
    `Enigma.Msi.Worker.exe.config`, `Enigma.Msi.dll`, the `WixSharp*`, `WixToolset.*`, `System.*`,
    `Microsoft.Bcl.AsyncInterfaces.dll` and `mbanative.dll`)
  - `packaging/windows/Enigma.Msi.Worker/README.md`
  - `docs/done/FEATURE-A834-PHASE01.md`
- Modified:
  - `.gitignore`
  - `docs/roadmap.md`, `docs/plan/FEATURE-A834.md`: statuses.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Which build to copy | Enigma.Msi's existing `bin/Release/net472/` output | Its tree is clean at the `1.7.0` tag, and GitClient's copy matches it. Rebuilding would have written into another repository |
| README | GitClient's, with this repository's MSI name, and the line-ending note replaced | The source `.exe.config` is already LF here, so every file is byte for byte |
| Link to Enigma.Msi or GitClient | Plain text, no relative link | A link out of the repository works only with the sibling checkout |

## Deviations & follow-ups

- GitClient's README says its `.exe.config` differs from the build in line endings. Here it does not:
  Enigma.Msi's build output now has LF, so all 20 files are identical, the config included.
- No test reads `packaging/` as text in this repository, so the binaries affect no suite.

## Build/test evidence

- Baseline: see `docs/done/FEATURE-F207.md` (0 warnings; 549 tests, 0 failed).
- `dotnet build Enigma.Backup.slnx -c Release`: **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Backup.slnx -c Release`: **549 total, 537 passed, 12 skipped, 0 failed**.
- `cmp` of each copied file against its source: all 20 identical.
- `git status --porcelain --ignored -uall packaging/windows/`: the 20 binaries are untracked, none
  ignored. `git check-ignore -v` matches `.wixsharp/…` to the new rule (`.gitignore:374`).
- `git check-attr`: the `.dll`s are `binary`. The `.exe.config` is `text eol=lf` and has no CR.
- `Enigma.Msi.Worker.exe` with no arguments printed "No arguments were given." and the two usage forms,
  exit code **2**.
- Fix budget: 0 cycles used.

## Documentation sweep

- Nothing stale yet. The README, `docs/RELEASE.md` and `CLAUDE.md` change with the scripts themselves
  (PHASE02).
