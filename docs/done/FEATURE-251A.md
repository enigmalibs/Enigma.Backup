# FEATURE-251A — Publish scripts for Windows and Linux

## Summary

Each platform now has the one-command publish every Enigma app has. Each script runs from any directory
and exits with `dotnet`'s exit code.

- Windows: `packaging/windows/publish.ps1` and `publish.bat`, both running
  `dotnet publish <Desktop project> -c Release -r win-x64 -p:PublishReadyToRun=true`.
- Linux: `packaging/linux/publish.sh`, next to `install.sh` and `uninstall.sh`, running the same command
  with `-r linux-x64`.

`publish.bat` calls `dotnet` itself rather than wrapping the `.ps1`, so no PowerShell execution policy
applies. The root `publish.ps1` / `publish.sh` are untouched: they build the self-contained single-file
release zips that `docs/RELEASE.md` ships, which is a different job. The docs now tell the two sets apart.

## Files/modules touched

- Created:
  - `packaging/windows/publish.ps1` (LF)
  - `packaging/windows/publish.bat` (CRLF)
  - `packaging/linux/publish.sh` (LF, mode `100755`)
  - `docs/done/FEATURE-251A.md`
- Modified:
  - `README.md`
  - `docs/RELEASE.md`
  - `CLAUDE.md`
  - `docs/roadmap.md`
  - `docs/plan/FEATURE-251A.md`

## Deviations & follow-ups

- None from the plan.
- `publish.sh` was run through Git Bash on Windows, as a cross-publish. A native Linux run is owed with the
  rest of the Linux checks (FEATURE-61C4).

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| `publish.sh` ending | `exec dotnet publish …` | The script's exit code is `dotnet`'s with no extra line |
| `publish.bat` ending | `exit /b %ERRORLEVEL%` | Passes the code on without closing a parent `cmd` |
| A `pause` in the `.bat` | None | It would block any automated caller |
| Pointer to the release zips in each header | Yes, one line | Two scripts named `publish.*` exist, and the header is where someone looks |
| Writing the `.bat` | Generated with explicit `\r\n` | The file is CRLF in the working tree from the start, matching `.gitattributes` (`*.bat eol=crlf`) |

## Documentation sweep

- README: a *A plain publish* section under *Building*, and the *Layout* lines for `packaging/windows` and
  `packaging/linux`.
- `docs/RELEASE.md` §2: the release zips are the **root** scripts; the `packaging/` ones ship nothing.
- CLAUDE.md *Commands*: the packaging publish and installer, with the `100755` / CRLF rules for those
  files.

## Build/test evidence

- Each script was run from the session scratchpad, not from the repository:
  - `cmd /c packaging\windows\publish.bat`: exit 0;
  - `powershell -ExecutionPolicy Bypass -File packaging\windows\publish.ps1`: exit 0;
  - both leave `src/Enigma.Backup.Desktop/bin/Release/net10.0/win-x64/publish/` with
    `Enigma.Backup.Desktop.exe` and `app.ico`;
  - `bash packaging/linux/publish.sh` (Git Bash, a cross-publish): exit 0. It leaves
    `…/linux-x64/publish/` with the `Enigma.Backup.Desktop` launcher and `app.ico`.
- ReadyToRun applied: `Enigma.Backup.Core.dll` is 169,984 bytes as IL, 425,984 bytes in the win-x64
  publish and 429,568 bytes in the linux-x64 one.
- Failures propagate: copies of all three scripts placed where the project path does not resolve each
  exit **1**.
- `bash -n packaging/linux/publish.sh`: OK. The `.sh` and `.ps1` have no CR byte, and all 25 lines of the
  `.bat` end in CRLF.
- Root `publish.ps1` / `publish.sh`: no diff.
- `dotnet build Enigma.Backup.slnx -c Release`: **0 warnings**.
- `dotnet test --solution Enigma.Backup.slnx -c Release`: **549 total, 537 passed, 12 skipped, 0 failed**.
