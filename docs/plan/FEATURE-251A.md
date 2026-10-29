# FEATURE-251A — Publish scripts for Windows and Linux

**Status:** DONE
**Type:** FEATURE
**Branch:** `feature/feature-251a-publish-scripts`
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

One-command publish scripts on both platforms, part of the uniform baseline for every Enigma app:

- **Windows:** `packaging/windows/publish.ps1` and `publish.bat`, both running
  `dotnet publish src/Enigma.Backup.Desktop -c Release -r win-x64 -p:PublishReadyToRun=true`.
- **Linux:** `packaging/linux/publish.sh`, next to `install.sh` and `uninstall.sh`, running
  `dotnet publish src/Enigma.Backup.Desktop -c Release -r linux-x64 -p:PublishReadyToRun=true`.

## Context & constraints

- The root `publish.ps1` and `publish.sh` already exist, but they do something else. They build the
  **release zips**: self-contained, single-file, into `artifacts/`. `docs/RELEASE.md` §2 runs them, so
  they stay as they are.
- The reference is Enigma.GitClient's `packaging/windows/publish.{ps1,bat}` and
  `packaging/linux/publish.sh` (@ `2c781d3`):
  - each resolves the project from its own directory, so it runs from anywhere;
  - each passes `dotnet`'s exit code on (`exit $LASTEXITCODE`, `exit /b %ERRORLEVEL%`, `exec`);
  - none takes arguments.
- `.gitattributes`: `*.bat` is CRLF and `*.sh` is LF. `packaging/linux/` exists after FEATURE-61C4.
- The output lands in `src/Enigma.Backup.Desktop/bin/Release/net10.0/<rid>/publish/`, which is gitignored.
- A RID-specific publish in .NET 10 is framework-dependent unless told otherwise. The spec's command is
  used verbatim.

## Steps

1. `packaging/windows/publish.ps1`: the reference adapted (header help, `Set-StrictMode`,
   `$ErrorActionPreference = 'Stop'`, project resolved from `$PSScriptRoot`, `exit $LASTEXITCODE`).
2. `packaging/windows/publish.bat`: `@echo off`, `setlocal`, the project from `%~dp0`, the command, then
   `exit /b %ERRORLEVEL%`. CRLF.
3. `packaging/linux/publish.sh`: `#!/usr/bin/env bash`, `set -euo pipefail`, the project resolved from the
   script's directory, `exec dotnet publish …`. Mode `100755`, LF.
4. Docs:
   - `docs/RELEASE.md` gains a short note telling the release zips (root scripts) apart from the plain
     ReadyToRun publish (`packaging/` scripts);
   - the README names the three scripts and where their output lands.
5. Verify, running each script from the scratchpad directory rather than the repository:
   - `cmd /c packaging\windows\publish.bat` and `powershell -ExecutionPolicy Bypass -File packaging\windows\publish.ps1`
     exit 0 and leave `…\win-x64\publish\` with the exe and `app.ico`;
   - `bash packaging/linux/publish.sh` (a cross-publish from Git Bash) exits 0;
   - ReadyToRun applied: `Enigma.Backup.Core.dll` is larger in `publish/` than as plain IL;
   - a failure propagates: a copy of each script placed where the project path does not resolve exits non-zero.

## Acceptance criteria

- The three scripts run from any directory and exit with `dotnet`'s exit code.
- `publish.sh` is `100755` in the index. `publish.bat` is CRLF in the working tree.
- The root release-zip scripts are unchanged.
- Release build has zero warnings. Whole suite green.

## Out of scope

- Self-contained or single-file publishing (the root scripts and `install.sh` cover those). Other RIDs.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Where | `packaging/windows/` and `packaging/linux/` | The siblings' layout, beside `install.sh` | Replacing the root scripts |
| The root release-zip scripts | Kept unchanged | The release runbook depends on them, and they serve a different purpose | Moving or renaming them (a drive-by) |
| `publish.bat` | Calls `dotnet` itself | No PowerShell execution policy applies | Wrapping `publish.ps1` |
| Arguments | None | The spec fixes the command | Passing `%*` / `"$@"` through |
| Self-contained flag | Not set | The spec's command, verbatim | Adding `--self-contained` |
