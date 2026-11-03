# FEATURE-F207 — New icon set without the tile

**Branch:** `feature/feature-f207-new-icon-set`
**Run:** feature/2026-11-03-icons-msi-release

## Summary

The app's icon is now the `Branding/Apps/final2/backup` set: the same "Zipped page" drawing in the
*Soft* palette, without the dark tile behind it (the background is transparent). It was copied byte for
byte into the three places this repository carries the icon:

- `Assets/enigma-backup.ico`: the exe's icon, the windows' icon, and the `app.ico` copied beside the
  executable (and so the icon the MSI of FEATURE-A834 will use);
- `Assets/enigma-backup.png`: the About dialog's image, the `.ico`'s 256 frame;
- `packaging/linux/icons/enigma-backup-{16…256}.png`: the eight sizes `install.sh` installs.

The file names are unchanged, so no reference moved. The `.ico` has the same ten frames as before
(8,058 bytes instead of 15,514).

## Files/modules touched

- Modified:
  - `src/Enigma.Backup.Desktop/Assets/enigma-backup.ico`
  - `src/Enigma.Backup.Desktop/Assets/enigma-backup.png`
  - `packaging/linux/icons/enigma-backup-{16,24,32,48,64,96,128,256}.png`
  - `CLAUDE.md`
  - `docs/roadmap.md`, `docs/plan/FEATURE-F207.md`: statuses.
- Created: `docs/done/FEATURE-F207.md`

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| How to check the app for real on Windows | Launch the Release build, screenshot the window, open About through UI Automation | `CLAUDE.md` describes a Linux screenshot tool; on this Windows host, `CopyFromScreen` worked |

## Deviations & follow-ups

- None from the plan. The SVG masters and the 20/40-px PNGs stay in `Branding/`, as the plan decided.
- The Linux launcher icon is checked only by its bytes here. On a Linux desktop, `install.sh` installs the
  same eight PNGs as before.

## Build/test evidence

- **Run baseline** (on the run branch after the planning commit `9199900`, before any dev branch):
  `dotnet build Enigma.Backup.slnx -c Release` 0 warnings, 0 errors;
  `dotnet test --solution Enigma.Backup.slnx -c Release` 549 total, 537 passed, 12 skipped, 0 failed. A
  clean baseline: the strict rule applies to every dev of the run.
- This dev: Release build **0 warnings, 0 errors**; suite **549 total, 537 passed, 12 skipped, 0 failed**.
  `AppIconTests` passes against the new asset; the test output's `app.ico` is byte for byte the new `.ico`.
- `cmp` of the ten copied files against `Branding/Apps/final2/backup`: identical.
- Script check of the asset `.ico`: ten frames (16, 20, 24, 32, 40, 48, 64, 96, 128, 256), each a 32-bit
  RGBA PNG (bit depth 8, colour type 6). `enigma-backup.png` equals the 256 frame; each Linux PNG equals
  the frame of its size.
- Launched `bin/Release/net10.0/Enigma.Backup.Desktop.exe`:
  - the title bar shows the new blue page without a tile;
  - **About** shows it at 96 px on the dialog's own background, with no tile.
  The process was stopped by its PID afterwards.
- Fix budget: 0 cycles used.

## Documentation sweep

- `CLAUDE.md` *Environment notes*: the icon now comes from `Branding/Apps/final2/backup` and has no tile.
  The note now names the Linux PNGs as copies of the `.ico`'s frames too.
- No prose in the README, the QA checklist or the code describes a tile; nothing else to change.
