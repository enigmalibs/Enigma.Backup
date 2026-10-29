# FEATURE-9755 — New app icons

## Summary

The placeholder icon is replaced with the final artwork from `C:\Dev\EnigmaLibs\Branding\Apps\final\backup`.

- `Assets/enigma-backup.ico` is now the new set's `.ico`: ten PNG frames, 16/20/24/32/40/48/64/96/128/256.
  It replaces 4 placeholder frames.
- `Assets/enigma-backup.png` is now the set's 256-px frame.

Both names are unchanged, so the csproj's `ApplicationIcon`, `MainWindow.axaml`'s `Icon` and the Settings
image still point at the right files. Every PNG in the set's `png/` folder is byte-identical to the `.ico`
frame of the same size (checked with a scratchpad script that was not committed).

## Files/modules touched

- Modified:
  - `src/Enigma.Backup.Desktop/Assets/enigma-backup.ico`
  - `src/Enigma.Backup.Desktop/Assets/enigma-backup.png`
  - `CLAUDE.md`
  - `docs/roadmap.md`
  - `docs/plan/FEATURE-9755.md`
- Created:
  - `docs/done/FEATURE-9755.md`

## Deviations & follow-ups

- None from the plan.
- The Linux hicolor PNGs (FEATURE-61C4) and the `app.ico` beside the exe (FEATURE-5D75) build on these
  files.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Source of the PNG | `png/enigma-backup-256.png` | Byte-identical to the `.ico`'s 256 frame, so the logo and the icon cannot drift |
| Visual check | Title-bar crop of a Release launch | The ApplicationIcon/window-icon path is what changed; the About surface is reworked by FEATURE-4573 next |

## Documentation sweep

- `CLAUDE.md` *Environment notes*: the icon was described as "placeholder art; final art comes from the
  user". It now says the files are the final art and where they come from.

## Build/test evidence

- `cmp` of both assets against their sources: identical.
- `dotnet build Enigma.Backup.slnx -c Release`: **0 warnings, 0 errors**. Roslyn embedded the new `.ico`
  as the Win32 icon.
- `dotnet test --solution Enigma.Backup.slnx -c Release`: **489 total, 477 passed, 12 skipped, 0 failed**.
- Release launch: the title bar shows the new icon (blue page, green zip). Screenshot in the session
  scratchpad.
