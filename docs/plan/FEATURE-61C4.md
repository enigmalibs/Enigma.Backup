# FEATURE-61C4 — Linux install and uninstall scripts

**Status:** TODO
**Type:** FEATURE
**Branch:** `feature/feature-61c4-linux-install-scripts`
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

Add `packaging/linux/install.sh` and `uninstall.sh` in the form Enigma.GitClient ships them. `install.sh`
installs the desktop app for the current user, without root, into the XDG directories, with a launcher
entry and icons. `uninstall.sh` removes exactly what it installed. Part of the uniform baseline.

## Context & constraints

**The reference** is Enigma.GitClient's `packaging/linux/` (@ `2c781d3`).

`install.sh`:

- Options: `--from DIR`, `--framework-dependent`, `--rid RID`, `-h`.
- Refuses to run as root, and validates the XDG paths.
- Publishes into a sibling staging directory, then swaps it in by rename.
- Puts a symlink in `$XDG_BIN_HOME` and installs hicolor icons at 16/24/32/48/64/96/128/256.
- Writes the `.desktop` file from `<app-id>.desktop.in`, substituting `@EXEC@` in bash.
- Pins `DOTNET_ROOT` when a framework-dependent build's runtime is not where a launcher looks.
- Refreshes the desktop caches as a convenience.

`uninstall.sh`:

- Removes exactly that, and the symlink only if it points into the install directory.
- Refuses an install directory whose path does not end in the app id.

Both scripts are mode `100755`. `.gitattributes` keeps `*.sh` LF.

This repository:

- No `packaging/` folder yet. `.gitattributes` already has `*.sh text eol=lf`.
- App id `enigma-backup`, launcher `Enigma.Backup.Desktop`, display name `Enigma.Backup` (`AppInfo.Name`).
- The README tagline gives the entry's `Comment`.
- The app runs on Linux. Its profiles and `settings.json` live in `~/.config/Enigma.Backup`, which
  uninstall must never touch.
- The new icon set has PNG frames at every size the reference installs:
  `Branding/Apps/final/backup/png/enigma-backup-<N>.png`.
- This machine is Windows. The scripts can be checked through Git Bash, but a real desktop-launcher check
  needs Linux.

## Steps

1. `packaging/linux/icons/enigma-backup-<N>.png` for the eight sizes: the new set's frames, after checking
   they are byte-identical to the shipped `.ico`'s PNG frames (scratchpad check, not committed).
2. `packaging/linux/enigma-backup.desktop.in`:
   - the reference's keys;
   - `Categories=Utility;Archiving;`;
   - `Keywords=backup;archive;restore;verify;compression;encryption;zip;tar;`;
   - `StartupWMClass=Enigma.Backup.Desktop`.
3. `packaging/linux/install.sh`, the reference adapted:
   - names and help text;
   - the closing notes say where profiles live, with no git check.
4. `packaging/linux/uninstall.sh`, the reference adapted:
   - no former-launcher name;
   - the closing line names `~/.config/Enigma.Backup` as left untouched.
5. Stage both scripts with mode `100755` (`git update-index --chmod=+x` after `git add`).
6. Verify through Git Bash:
   - `bash -n` and `--help` on both scripts;
   - `uninstall.sh` on an empty scratchpad `XDG_DATA_HOME` reports "nothing to remove";
   - `install.sh --from <linux-x64 publish>` into scratchpad `XDG_DATA_HOME` / `XDG_BIN_HOME`, then
     `uninstall.sh` removes everything. Symlinks may need `MSYS=winsymlinks:nativestrict`; whatever cannot
     run here is recorded as owed on Linux.
7. README: a *Linux* subsection under the build or install section.

## Acceptance criteria

- Both scripts parse (`bash -n`), print their help, are `100755` in the index, and are LF in the tree.
- Icons are byte-identical to the shipped `.ico`'s frames of the same size, or to the Branding PNGs if the
  `.ico` frames differ (recorded).
- The install → uninstall round trip leaves the scratchpad XDG tree free of `enigma-backup` files, or the
  part that cannot run on Windows is recorded as owed.
- Release build has zero warnings. Whole suite green (nothing in it changes).

## Out of scope

- `publish.sh` (FEATURE-251A). Distro packages (deb, rpm, AppImage, Flatpak).

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Default build | Self-contained, as the reference | Works on a machine with no .NET runtime | Framework-dependent by default |
| Icons | The Branding set's PNG frames (checked against the `.ico`) | Same artwork as the shipped icon, with no tool dependency | Re-rendering from the SVG |
| Desktop entry Name | `Enigma.Backup` | `AppInfo.Name`, the name the window already shows | "Enigma Backup" |
| User data | Never touched by uninstall, and named in its closing line | Profiles are the user's work | Offering `--purge` |
| Tests | Script checks and a scratchpad round trip, no unit test | They are shell scripts, and the round trip is the real proof | A test project for shell |
