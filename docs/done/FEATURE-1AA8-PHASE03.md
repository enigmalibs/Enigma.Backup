# FEATURE-1AA8-PHASE03 — Linux launcher and install scripts

**Branch:** `feature/feature-1aa8-phase03-linux-packaging`
**Run:** feature/2026-11-16-display-name-everywhere

## Summary

On Linux, the application launcher now lists the app as **Enigma Backup**, and the install scripts call
it that.

- `packaging/linux/enigma-backup.desktop.in`: `Name=Enigma Backup`. The entry's file name
  (`enigma-backup.desktop`) and `StartupWMClass=Enigma.Backup.Desktop` are unchanged. The class must
  match the window's for the launcher to group the running app under its entry. Running `install.sh`
  again rewrites an existing entry in place.
- `install.sh`: the header comment, the `--help` text, "Installing Enigma Backup", and "Done. "Enigma
  Backup" is in your application launcher."
- `uninstall.sh`: the `--help` text, "Removing Enigma Backup", and "Nothing to remove — Enigma Backup is
  not installed under …".
- What still says `Enigma.Backup` in the scripts, on purpose:
  - the paths (`~/.config/Enigma.Backup`, `%s/Enigma.Backup`);
  - `APP_EXE`, the `--from` help line naming the `Enigma.Backup.Desktop` launcher file, and the
    `dotnet publish` project path.
- `docs/QA-CHECKLIST.md`: new row 1.8 (Linux). `RELEASENOTES.md` 1.6.0: the name bullet covers the
  launcher entry and the scripts.

## Files/modules touched

- Modified:
  - `packaging/linux/enigma-backup.desktop.in`
  - `packaging/linux/install.sh`, `packaging/linux/uninstall.sh` (mode `100755` kept)
  - `RELEASENOTES.md`, `docs/QA-CHECKLIST.md`, `docs/roadmap.md`, `docs/plan/FEATURE-1AA8.md`
- Created: `docs/done/FEATURE-1AA8-PHASE03.md`

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| `install.sh`'s header comment ("# Installs Enigma.Backup …") | Renamed with the `--help` text below it | It names the app, not a file. A comment that contradicts the usage text two screens down helps nobody |
| How far to verify on a Windows host | Beyond the plan's syntax check and help texts, a full `install.sh --from` round trip in Git Bash. It used a stub launcher, and `HOME` and all three `XDG_*` variables pointed into a temporary folder that was deleted afterwards | It renders the real launcher entry from the template and runs both scripts' message paths. Nothing under the real home directory was touched |

## Deviations & follow-ups

- **Owed on Linux:** a real `install.sh` on a desktop (QA 1.8): the launcher's label, and the window
  grouping under it. This host is Windows.
- Seen in the Windows round trip only: Git Bash's `ln -s` makes a copy, so `uninstall.sh` rightly left
  the non-symlink `enigma-backup` command alone (its guard against deleting a user's own file). On
  Linux it is a real symlink and is removed. Nothing to fix.

## Build/test evidence

- Baseline: see `docs/done/FEATURE-1AA8-PHASE01.md` (0 warnings; 590 tests, 578 passed, 12 skipped,
  0 failed). PHASE02 added one test.
- `dotnet build Enigma.Backup.slnx -c Release`: **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Backup.slnx`: **591 total, 579 passed, 12 skipped, 0 failed**.
- `bash -n` on both scripts: clean.
- `install.sh --help` and `uninstall.sh --help` both say "… Enigma Backup …".
- `install.sh --from <stub>` into the temporary folder:
  - it printed "Installing Enigma Backup" … "Done. "Enigma Backup" is in your application launcher."
    and exited 0;
  - the written `enigma-backup.desktop` reads `Name=Enigma Backup` and
    `StartupWMClass=Enigma.Backup.Desktop`.
- `uninstall.sh`:
  - after the install, "Removing Enigma Backup", which removed the install folder, the entry and the
    icons;
  - on a second run, "Nothing to remove — Enigma Backup is not installed under …".
- `git ls-files -s`: both scripts are still `100755`.
- Fix budget: 0 cycles used.

## Documentation sweep

- `RELEASENOTES.md` 1.6.0 and `docs/QA-CHECKLIST.md` 1.8 updated (above).
- The README's Linux section quotes no script message or launcher label, so it does not change.
  `CLAUDE.md`'s packaging notes (mode `100755`, LF) still hold.
