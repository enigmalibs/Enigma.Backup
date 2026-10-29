# FEATURE-61C4 — Linux install and uninstall scripts

## Summary

`packaging/linux/` now holds Enigma.GitClient's per-user Linux installer, adapted to this app.

`install.sh`:

- builds from the repository (self-contained by default, `--framework-dependent` on request) or takes a
  published folder with `--from`;
- swaps the result into `~/.local/share/enigma-backup` by rename, so a failed build leaves the old
  install untouched;
- links `~/.local/bin/enigma-backup`, installs the icon at 8 hicolor sizes, and writes the launcher
  entry from `enigma-backup.desktop.in`;
- pins `DOTNET_ROOT` in the entry when a framework-dependent runtime is not where a launcher looks;
- refuses to run as root and validates the XDG paths.

`uninstall.sh` removes exactly that:

- the symlink only if it points into the install;
- nothing at all if the install directory does not end in `enigma-backup`.

Neither script touches `~/.config/Enigma.Backup` (profiles and `settings.json`) or any backup, and both
say so.

The icons are the Branding set's PNG frames, byte-identical to the frames of the shipped
`Assets/enigma-backup.ico`.

## Files/modules touched

- Created:
  - `packaging/linux/install.sh` (mode `100755`, LF)
  - `packaging/linux/uninstall.sh` (mode `100755`, LF)
  - `packaging/linux/enigma-backup.desktop.in`
  - `packaging/linux/icons/enigma-backup-{16,24,32,48,64,96,128,256}.png`
  - `docs/done/FEATURE-61C4.md`
- Modified:
  - `README.md`
  - `docs/roadmap.md`
  - `docs/plan/FEATURE-61C4.md`

## Deviations & follow-ups

- **Owed on Linux**, because this host is Windows:
  - a real build-and-install (`install.sh` with no `--from`) and the launcher entry appearing in a desktop;
  - the native symlink. This Windows account may not create native symlinks, so the round trip ran with
    MSYS's `.lnk` emulation, which `[[ -L ]]` and `readlink` treat as symlinks;
  - the root refusal (`EUID` is read-only in Git Bash).
- The first attempt, with `MSYS=winsymlinks:nativestrict`, failed at `ln` ("Operation not permitted")
  after the install directory was already in place. The second run upgraded over it ("Replacing …"),
  which also exercised the swap path. On a failed `ln` the installed application stays without a
  symlink, exactly as in the reference script.
- Release zips: the README *Installing* table still names 1.2.0. The release item updates it.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| `GenericName` / `Comment` | "Backup Tool" / the README's one-line description | What a launcher shows under and beside the name |
| Help text sizes | "much smaller" instead of the reference's measured MB figures | This app's sizes were not measured on Linux, and a wrong number is worse than none |
| Closing note | Where profiles and preferences live; no prerequisite check | The reference checks for git, but this app needs nothing on PATH |
| `config_home` in `install.sh` | Read for the closing note only | The user should know where their data is, and that the script did not touch it |

## Documentation sweep

- README:
  - *Installing* gains *Linux: into your application launcher* (the four invocations, and what is never
    touched);
  - *Layout* lists `packaging/linux`.

## Build/test evidence

- `bash -n` on both scripts: OK. `--help` on both prints the usage. No CR byte in either script or in the
  template.
- Icons: all 8 byte-identical to the `.ico` frames of the same size (scratchpad check).
- Round trip in Git Bash, against a scratchpad `XDG_DATA_HOME` / `XDG_BIN_HOME` / `XDG_CONFIG_HOME`, using
  a `dotnet publish -r linux-x64 --self-contained` (260 files):
  - `uninstall.sh` on an empty tree: "Nothing to remove", exit 0;
  - `install.sh --from <publish>`: install dir, symlink to `…/Enigma.Backup.Desktop`, 8 icons under
    `hicolor/<N>x<N>/apps/`, and a desktop entry with a quoted `Exec`. Exit 0;
  - `uninstall.sh`: removes all four. The only file left in the tree is a `profiles/keep.json` placed
    under the config folder beforehand;
  - a foreign symlink of the same name (`→ /usr/bin/true`) is left alone, and the script says so;
  - `XDG_DATA_HOME=relative` is refused, exit 1.
- `dotnet build Enigma.Backup.slnx -c Release`: **0 warnings**.
- `dotnet test --solution Enigma.Backup.slnx -c Release`: **549 total, 537 passed, 12 skipped, 0 failed**
  (nothing in the suite changes).
