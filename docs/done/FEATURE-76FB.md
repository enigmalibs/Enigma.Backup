# FEATURE-76FB — Icon set final3

**Branch:** `feature/feature-76fb-icon-set-final3`
**Run:** feature/2026-11-04-icons-release

## Summary

The app's icon is now the `Branding/Apps/final3/backup` set. It is the same "Zipped page" drawing in the
*Soft* palette, still without a tile, but scaled ×1.33 so that it fills its frame. The small frames
(16–32 px) come from the simplified small master. It was copied byte for byte into the three places
this repository carries the icon:

- `Assets/enigma-backup.ico` is the exe's icon, the windows' icon and taskbar entry, and the `app.ico`
  copied beside the executable. That copy is also what the MSI's shortcuts and *Apps & features* entry
  use;
- `Assets/enigma-backup.png` is the About dialog's image, the `.ico`'s 256 frame;
- `packaging/linux/icons/enigma-backup-{16…256}.png` are the eight sizes `install.sh` installs.

The file names are unchanged, so no reference moved and no installer script needed an edit. The `.ico`
has the same ten frames as before (11,382 bytes instead of 8,058).

## Files/modules touched

- Modified:
  - `src/Enigma.Backup.Desktop/Assets/enigma-backup.ico`
  - `src/Enigma.Backup.Desktop/Assets/enigma-backup.png`
  - `packaging/linux/icons/enigma-backup-{16,24,32,48,64,96,128,256}.png`
  - `CLAUDE.md`
  - `docs/roadmap.md`, `docs/plan/FEATURE-76FB.md`: statuses.
- Created: `docs/done/FEATURE-76FB.md`

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| How to check the app for real on Windows | Launch the Release build, screenshot the window, open About through UI Automation, extract the exe's embedded icon | As FEATURE-F207; `CLAUDE.md` describes a Linux screenshot tool, and this host is Windows |

## Deviations & follow-ups

- None from the plan. The SVG masters and the 20/40 px PNGs stay in `Branding/`, as the plan decided.
- The Linux launcher icon is checked only by its bytes here. On a Linux desktop, `install.sh` installs the
  same eight file names as before.
- The app was launched against the user's real profiles folder (it opened the last profile). Nothing
  that writes was clicked.

## Build/test evidence

- **Run baseline** (on the run branch after the planning commit `5276ced`, before any dev branch):
  `dotnet build Enigma.Backup.slnx -c Release` 0 warnings, 0 errors;
  `dotnet test --solution Enigma.Backup.slnx -c Release` 552 total, 540 passed, 12 skipped, 0 failed. A
  clean baseline: the strict rule applies to every dev of the run.
- This dev: Release build **0 warnings, 0 errors**; suite **552 total, 540 passed, 12 skipped, 0 failed**.
  `AppIconTests` passes against the new asset; the `app.ico` in the app's and the tests' build output is
  byte for byte the new `.ico`.
- `cmp` of the ten copied files against `Branding/Apps/final3/backup`: identical.
- Script check of the asset `.ico`: ten frames (16, 20, 24, 32, 40, 48, 64, 96, 128, 256), each a 32-bit
  RGBA PNG (bit depth 8, colour type 6). Each Linux PNG equals the frame of its size, and
  `enigma-backup.png` equals the 256 frame.
- Launched `bin/Release/net10.0/Enigma.Backup.Desktop.exe`:
  - the title bar and the exe's embedded icon show the new blue page, which fills its frame;
  - **About** shows it on the dialog's own background, with no tile.
  The process was stopped by its PID afterwards.
- Fix budget: 0 cycles used.

## Documentation sweep

- `CLAUDE.md` *Environment notes*: the icon now comes from `Branding/Apps/final3/backup`, and the drawing
  fills a transparent frame.
- No other prose names `final2` or describes the drawing's size. The QA checklist's "no tile" note is
  still true. The release notes and QA records for 1.5.0 belong to FEATURE-63DB.
