# Enigma.Backup

[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE.md)

A cross-platform desktop backup tool for Windows and Linux. It packs any number of selected files
and folders into a single compressed — optionally password-encrypted — archive, restores them again,
and verifies what it wrote.

> **What's new in 1.2.0** — an input can be a **pattern** now (a folder plus a glob, worked out again
> on every run), a backup can be written as a plain **`.zip`** that any file manager opens, `Restore…`
> and `Verify…` moved up beside the settings gear, and `Settings` opens the folder your profiles live in
> and says which version you are running. The archive container, the file names and every existing
> profile are untouched, so all your archives still open. See [RELEASENOTES.md](RELEASENOTES.md).

## Capabilities

- **Reusable profiles** — the input paths, output folder, compression and encryption choices of a
  backup, stored as JSON in the per-user config folder, with import/export.
- **Full backups** — one timestamped archive per run: `<Profile>_yyyy-MM-dd_HHmmss.tar.zst`
  (or `.tar.gz`, or `.zip`), plus `.enc` when encrypted, and a `.sha256` sidecar.
- **Streaming pipeline** — `scan → tar → compress → [encrypt] → file` in a single pass, so archive
  size is bounded by disk, not memory.
- **Optional encryption** — AES-256-GCM in chunked segments, key derived with Argon2id; the
  password is prompted per run and never stored.
- **Restore and verify** — restore from the app with conflict handling, and re-check an archive
  against its sidecar (and every GCM tag, when encrypted).
- **Pattern inputs** — a base folder plus a glob (`/var/log` + `*.log`, or `DirPrefix*`), worked out
  again on every run rather than fixed when you add it. `*` matches inside one folder level and `**`
  at any depth; a pattern that matches a folder's name takes that folder with everything in it.
- **A plain `.zip`, when you want one** — a third choice under *Compression* that writes an ordinary
  zip any file manager opens without this app. It can never be encrypted, and the app says so instead
  of offering something that only looks like protection.
- **Glob excludes**, live progress with cancel, and a per-error Skip / Retry / Abort policy with an
  end-of-run summary of everything skipped.
- **Light, dark, or whatever the system asks for** — chosen under `Settings` (the gear in the profile
  bar), applied the moment you pick it and remembered afterwards. The same dialog opens the folder
  your profiles and preferences live in, and says which version you are running.

## Installing

Download the zip for your platform from the release, unpack it anywhere, and run the single
executable inside — `app.ico` beside it is the app's icon, for a shortcut or a launcher to point at.
It is self-contained — **no .NET runtime needs to be installed.**

| Platform | Zip | Run |
|----------|-----|-----|
| Windows x64 | `Enigma.Backup-1.2.0-win-x64.zip` | `Enigma.Backup.Desktop.exe` |
| Linux x64 | `Enigma.Backup-1.2.0-linux-x64.zip` | `./Enigma.Backup.Desktop` |

There is no installer and nothing is written outside your home directory. To uninstall, delete the
executable and its `app.ico` and — if you want your profiles gone too — the config folder listed below.

If the Linux binary is not executable after unpacking (some archivers drop the permission bit),
`chmod +x Enigma.Backup.Desktop`.

## Using it

The app is a single window, top to bottom:

1. **Profile** — pick a saved profile from the drop-down, or start from `New`. A profile is just the
   settings below, under a name; `Save`, `Save as…`, `Rename…`, `Delete…`, `Import…` and `Export…`
   are beside it. Unsaved edits are marked with a `*` in the title bar, and switching profiles
   discards them. The gear at the right-hand end of the bar opens **Settings**: the theme, and an
   `Open folder` button on the folder your profiles and preferences are kept in. The ⓘ beside it opens
   **About**: the version, the build it was cut from, and the copyright.
2. **Files, folders and patterns to back up** — `Add files…`, `Add folder…`, or drop them anywhere on
   the window. Overlapping entries are merged automatically when the backup runs. `Add pattern…` adds
   a rule instead of a path: pick the folder it applies to, type the glob, and the row reads
   `folder ▸ pattern`. It is re-resolved on every run, so a pattern that matches nothing today simply
   says so in the run summary.
3. **Exclude patterns** — optional globs applied during the scan, one per line: `**/bin/**`,
   `*.log`, `**/node_modules/**`.
4. **Save backups in** — the output folder. It is always excluded from the backup itself, so
   pointing it inside an input folder is safe.
5. **Archive options → Compression** — Zstandard (`.tar.zst`, the default: faster and smaller), gzip
   (`.tar.gz`, readable by anything), or Zip (`.zip`, which any file manager opens without this app —
   at the cost of two-second timestamps, no Unix permission bits, and no encryption).
6. **Archive options → Encrypt with a password** — switch it on to get AES-256-GCM encryption. You are
   prompted for the password each run, with a confirmation field; it is never saved anywhere. Choosing
   Zip switches it off and greys it out, with the reason on the card.
7. **Back up** — the accent button at the bottom. It runs the backup behind a card that shows the
   phase, the current file, the percentage and `Cancel`. When a run ends, the outcome appears as a
   banner at the top of the window, and the **Last run** panel fills with the counts, the duration, the
   output path, and a prominent list of anything skipped — including any pattern that matched nothing.
8. **Restore… and Verify…** are in the **top bar**, beside the gear, because neither one reads anything
   from the form: **Restore…** asks for an archive and a destination, **Verify…** re-reads an archive
   and checks it against its sidecar. Both report on the same card and the same banner as a backup.

If a file cannot be read, the run stops and asks: **Skip**, **Retry** or **Abort**, with an
*apply to all further errors* option. Aborting — like cancelling — deletes the partial archive, so a
half-written backup is never left behind.

Profiles live in `%APPDATA%\Enigma.Backup\profiles` on Windows and
`~/.config/Enigma.Backup/profiles` on Linux, one `.json` file per profile. The app's own preferences
sit in `settings.json` beside that folder. It appears the first time you change one, and deleting it
simply returns the app to following the system. `Settings` names that folder and has an `Open folder`
button that shows it in your file manager — making it first if you have not saved anything yet.

## What a backup run writes

A run produces exactly two files in the output folder:

```
Documents_2026-08-01_143012.tar.zst          the archive
Documents_2026-08-01_143012.tar.zst.sha256   its checksum
```

With Zip selected it is the same pair under a different name — `Documents_2026-08-01_143012.zip` and
`Documents_2026-08-01_143012.zip.sha256`.

Encrypted runs add `.enc` to the archive name (`…tar.zst.enc`, with the sidecar alongside it as
`…tar.zst.enc.sha256`). Nothing is ever overwritten or pruned — the timestamp makes each run a new
pair of files, and deleting old ones is up to you.

## Restoring without Enigma.Backup

**Unencrypted archives are ordinary files** — a tar, or a zip. The app is a convenience, not a
dependency: if you ever lose it, any standard tool will unpack your data.

```bash
# gzip
tar -xzf Documents_2026-08-01_143012.tar.gz -C ./restored

# Zstandard, with a tar that speaks zstd
tar --zstd -xf Documents_2026-08-01_143012.tar.zst -C ./restored

# Zstandard, with any tar
zstd -dc Documents_2026-08-01_143012.tar.zst | tar -xf - -C ./restored

# Zip — Deflate, no Zip64, no encryption, so anything opens it
unzip Documents_2026-08-01_143012.zip -d ./restored
```

The sidecar is in `sha256sum` format, so it verifies with the standard tool — run it from the folder
holding the archive:

```bash
sha256sum -c Documents_2026-08-01_143012.tar.zst.sha256
```

On Windows, a `.zip` needs nothing at all — Explorer opens it by double-click; 7-Zip opens `.tar.gz`
directly and `.tar.zst` with the Zstandard-enabled build. The checksum can be compared against
`certutil -hashfile <archive> SHA256`.

**Encrypted archives (`.enc`) need Enigma.Backup** — or an implementation of its container format.
They are not a standard format: the `EBK1` container is specified in full in
[docs/plan/FEATURE-3E11.md](docs/plan/FEATURE-3E11.md), down to the header layout and per-chunk
framing, so the data is recoverable from the specification alone. **If you lose the password, the
data is gone** — there is no recovery path, by design.

## Layout

```
src/Enigma.Backup.Core       net10.0 library — profiles, scanning, archive pipeline, crypto (no UI)
src/Enigma.Backup.Desktop    net10.0 Avalonia 12 desktop app — MVVM layer over the core
tests/Enigma.Backup.Core.Tests
tests/Enigma.Backup.Desktop.Tests
docs/                        roadmap, per-item plans, completion records, release runbook
```

## Built on

| Dependency | What it provides |
|---|---|
| [.NET 10](https://dotnet.microsoft.com/) | the runtime; the core targets `net10.0` because the archive pipeline is built on `System.Formats.Tar` |
| [Avalonia 12](https://avaloniaui.net/) | the cross-platform UI framework (with its Fluent theme and the Inter font) |
| [Enigma.Avalonia.Desktop](https://www.nuget.org/packages/Enigma.Avalonia.Desktop) | the house control library — the theme, the in-window dialog / overlay / notification hosts, the file and folder pickers, the setting cards and typed editors |
| [Enigma.Icons](https://www.nuget.org/packages/Enigma.Icons) · [.Phosphor](https://www.nuget.org/packages/Enigma.Icons.Phosphor) · [.Avalonia](https://www.nuget.org/packages/Enigma.Icons.Avalonia) | the icons across the window and the dialog headers. The glyphs are [Phosphor Icons](https://phosphoricons.com/), MIT-licensed upstream |
| [Enigma.Core](https://www.nuget.org/packages/Enigma.Core) | AES-256-GCM and Argon2id, over BouncyCastle |
| [ZstdSharp.Port](https://www.nuget.org/packages/ZstdSharp.Port) | Zstandard compression (gzip comes from the BCL) |
| [CommunityToolkit.Mvvm](https://www.nuget.org/packages/CommunityToolkit.Mvvm) · [Microsoft.Extensions](https://www.nuget.org/packages/Microsoft.Extensions.Hosting) | the MVVM layer, and hosting, DI, logging and glob matching |

Versions are pinned centrally in `Directory.Packages.props`; the Avalonia set — the control library
included — is version-coupled and moves as a unit.

## Building

Requires the .NET 10 SDK (pinned in `global.json`).

```bash
dotnet restore Enigma.Backup.slnx
dotnet build Enigma.Backup.slnx
dotnet test --solution Enigma.Backup.slnx      # xUnit v3 on the Microsoft Testing Platform
dotnet run --project src/Enigma.Backup.Desktop
```

The build treats warnings as errors and enforces the repository `.editorconfig` at build time.

### Publishing the release zips

```bash
./publish.sh                 # both runtimes
./publish.sh linux-x64       # just one
```

```powershell
./publish.ps1                # both runtimes
./publish.ps1 -Rid win-x64   # just one
```

Each produces a self-contained, single-file build in `artifacts/publish/<rid>/` and zips it to
`artifacts/Enigma.Backup-<version>-<rid>.zip`. The version comes from `<Version>` in
`Directory.Build.props`, so the zip name cannot disagree with the binary inside it. Cross-publishing
works in both directions, with one caveat: build the **linux-x64** zip from Linux, because
PowerShell's `Compress-Archive` stores no Unix file mode and the extracted binary would lose its
executable bit.

The full release procedure is in [docs/RELEASE.md](docs/RELEASE.md).

## Security

For how to report a vulnerability, see [SECURITY.md](SECURITY.md).

## License

MIT — see [LICENSE.md](LICENSE.md).
