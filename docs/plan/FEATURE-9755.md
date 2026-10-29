# FEATURE-9755 — New app icons

**Status:** TODO
**Type:** FEATURE
**Branch:** `feature/feature-9755-new-app-icons`
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

The app's icon and logo are replaced with the final artwork in
`C:\Dev\EnigmaLibs\Branding\Apps\final\backup` (the CLAUDE.md note "placeholder art; final art comes from
the user" is now resolved).

## Context & constraints

- The two image assets live under `src/Enigma.Backup.Desktop/Assets/`, and `<AvaloniaResource Include="Assets/**" />`
  embeds both:
  - `enigma-backup.ico` (4,860 bytes, placeholder art at 16/32/48/256 px) is used in two places: as the
    csproj's `<ApplicationIcon>` (the Win32 icon) and as `MainWindow.axaml`'s `Icon`.
  - `enigma-backup.png` is 256×256 and is the Settings About section's image. FEATURE-4573 moves it into
    the About dialog.
- The new set:
  - `enigma-backup.ico` (15,514 bytes), with PNG frames expected at 16/20/24/32/40/48/64/96/128/256;
  - `png/enigma-backup-<N>.png` at the same ten sizes;
  - `enigma-backup.svg` and `enigma-backup-small.svg`. Their header names their master: `Branding/Apps/export.py`,
    outside this repository.
- `.gitattributes` marks `*.ico` and `*.png` binary.

## Steps

1. Check the new `.ico` (frame count and sizes) and confirm `png/enigma-backup-256.png` is byte-identical to
   its 256 frame.
2. Overwrite `Assets/enigma-backup.ico` with the new `.ico`, and `Assets/enigma-backup.png` with
   `png/enigma-backup-256.png`. The names stay the same, so no reference changes.
3. Build Release (Roslyn parses the `.ico` to embed it, so a malformed file fails the build). Run the suite.
4. Launch the app (Debug) and check the window and taskbar icon. Record what was seen in the completion doc.
5. `CLAUDE.md` *Environment notes*: the icon is no longer placeholder art.

## Acceptance criteria

- Both assets are byte-identical to the new set's `.ico` and 256-px PNG.
- Release build has zero warnings and the whole suite is green.
- The running app shows the new icon.

## Out of scope

- The Linux hicolor PNGs (FEATURE-61C4) and the `app.ico` copy beside the executable (FEATURE-5D75).
- Committing the SVG masters or the other PNG frames into `Assets/`.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| File names | Overwrite in place (`enigma-backup.ico`, `enigma-backup.png`) | The new set uses the same names, and every reference stays unchanged | Renaming to the siblings' `app.ico` (touches the csproj, XAML and docs for nothing) |
| Logo PNG | The 256-px frame | The About dialog draws it at 96 DIP. At 200 % scaling that is 192 px, which only the 256 frame covers. The current PNG is 256 too | A smaller frame |
| Other frames and the SVGs | Not committed under `Assets/` | `Assets/**` is embedded wholesale, so they would ride in the assembly for nothing. The SVG master lives in `Branding/` | Committing the full set |
| New tests | None here | The build parses the `.ico`. FEATURE-5D75 pins the copied icon | A frame-count test (it would pin artwork, not behaviour) |
