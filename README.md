# Enigma.Backup

A cross-platform desktop backup tool for Windows and Linux. It packs any number of selected files
and folders into a single compressed — optionally password-encrypted — archive, restores them again,
and verifies what it wrote.

> **Status:** in development. The solution scaffolding, the backup profiles and their store, the input
> scan (glob excludes, overlapping inputs, missing-path handling), the streaming archive pipeline —
> backup, restore and verification, in Zstandard or gzip — and the AES-256-GCM encryption container
> are in place (FEATURE-3E11 PHASE01–PHASE04); the user interface and release packaging land in the
> following phases. See [docs/roadmap.md](docs/roadmap.md).

## Planned capabilities (v1)

- **Reusable profiles** — the input paths, output folder, compression and encryption choices of a
  backup, stored as JSON in the per-user config folder, with import/export.
- **Full backups** — one timestamped archive per run: `<Profile>_yyyy-MM-dd_HHmmss.tar.zst`
  (or `.tar.gz`), plus `.enc` when encrypted, and a `.sha256` sidecar.
- **Streaming pipeline** — `scan → tar → compress → [encrypt] → file` in a single pass, so archive
  size is bounded by disk, not memory.
- **Optional encryption** — AES-256-GCM in chunked segments, key derived with Argon2id; the
  password is prompted per run and never stored.
- **Restore and verify** — restore from the app with conflict handling, and re-check an archive
  against its sidecar (and every GCM tag, when encrypted).
- **Glob excludes**, live progress with cancel, and a per-error Skip / Retry / Abort policy with an
  end-of-run summary of everything skipped.

## Layout

```
src/Enigma.Backup.Core       net10.0 library — profiles, scanning, archive pipeline, crypto (no UI)
src/Enigma.Backup.Desktop    net10.0 Avalonia 12 desktop app — MVVM layer over the core
tests/Enigma.Backup.Core.Tests
tests/Enigma.Backup.Desktop.Tests
docs/                        roadmap, per-item plans, completion records
```

## Building

Requires the .NET 10 SDK (pinned in `global.json`).

```bash
dotnet restore Enigma.Backup.slnx
dotnet build Enigma.Backup.slnx
dotnet test --solution Enigma.Backup.slnx      # xUnit v3 on the Microsoft Testing Platform
dotnet run --project src/Enigma.Backup.Desktop
```

The build treats warnings as errors and enforces the repository `.editorconfig` at build time.

## License

MIT — see [LICENSE.md](LICENSE.md).
