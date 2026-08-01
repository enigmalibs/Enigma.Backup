# FEATURE-3E11-PHASE06 — Release readiness

**Completed:** 2026-08-01
**Branch:** `feature/feature-3e11-phase06-release`

## Summary

The last phase turns a working solution into something that can be handed to someone else: a version,
two self-contained zips built by a script rather than by memory, the documentation a first release
needs, and a QA pass over what actually ships.

- **Version 1.0.0**, set once in `Directory.Build.props`. One product, one version — the Core and the
  Desktop app ship together and neither is packable, so versioning them apart would be a fiction.
  The publish scripts read it back through `dotnet msbuild -getProperty:Version`, so a zip's name
  cannot disagree with the binary inside it.
- **`publish.sh` / `publish.ps1`** — self-contained, single-file, compressed `win-x64` and
  `linux-x64` builds, zipped to `artifacts/Enigma.Backup-1.0.0-<rid>.zip`. Each zip holds exactly one
  file: the executable. No .NET runtime is needed on the target machine.
- **Documentation** — the README completed with an install/usage walkthrough and a
  *restore without Enigma.Backup* section, first-release `RELEASENOTES.md`, `SECURITY.md`,
  `docs/RELEASE.md` (the runbook), and `docs/QA-CHECKLIST.md` (the manual pass, since the app has no
  CLI and the button presses cannot be automated).
- **A bug the release QA found and fixed**: the app ignored `SIGTERM`. See below — it is the only
  code change in the phase.

## Files/modules touched

**Created — repository root**

- `publish.sh`, `publish.ps1` — the release builds. Both take optional runtime identifiers, validate
  them, and fail loudly rather than producing a half-named zip.
- `SECURITY.md` — vulnerability reporting through GitHub's private advisories, with the scope split
  between this repo and the upstream crypto (Enigma.Core / BouncyCastle), and an explicit
  "don't attach a real archive or password to a report".

**Created — `src/Enigma.Backup.Desktop/Hosting/`**

- `AvaloniaHostLifetime.cs` — a no-op `IHostLifetime` replacing the host builder's `ConsoleLifetime`.

**Created — `docs/`**

- `RELEASE.md` — the release runbook, adapted from the `dotnet-release` template for an app: no
  `dotnet pack`, no `nuget push`, no API key. Pre-flight → zips → QA → merge → tag → GitHub release.
- `QA-CHECKLIST.md` — 40 manual checks across artifact, profiles, inputs, backup, restore, verify and
  the error/cancel paths, with a Linux and a Windows column, plus a record of what 1.0.0 actually
  verified and what it could not.

**Modified**

- `Directory.Build.props` — `<Version>1.0.0</Version>`.
- `README.md` — rewritten past the "in development" note: licence badge, what's-new callout,
  *Installing* (zip per platform, what to run), *Using it* (a walkthrough of the window, top to
  bottom), *What a backup run writes*, *Restoring without Enigma.Backup*, publish instructions,
  security pointer.
- `RELEASENOTES.md` — was an empty file; now the 1.0.0 first-release section (feature overview,
  compatibility, version).
- `src/Enigma.Backup.Desktop/Enigma.Backup.Desktop.csproj` — a publish-time target that drops native
  `.pdb`s from the publish set (see below).
- `src/Enigma.Backup.Desktop/Hosting/ServiceCollectionExtensions.cs` — registers the host lifetime.
- `tests/Enigma.Backup.Desktop.Tests/Hosting/ServiceCollectionExtensionsTests.cs` — one new test.
- `docs/roadmap.md`, `docs/plan/FEATURE-3E11.md` — PHASE06 `TODO` → `IN PROGRESS` → `DONE`, the item
  itself to `DONE`, plus a note on the phase.

## Two defects the release pass found

Both were found by packaging and running the artifact rather than by reading code, which is the
argument for doing this phase at all.

**1. 105 MB of someone else's debug symbols in the Windows zip.** The first `win-x64` publish came out
at 71 MB against Linux's 44 MB. The difference was `libSkiaSharp.pdb` (84 MB) and
`libHarfBuzzSharp.pdb` (21 MB) — native symbol files that Avalonia's rendering stack ships as package
*content*, so `DebugType=none` has no effect on them. A target in the Desktop csproj now removes
every native `.pdb` from `ResolvedFileToPublish`; the zip is 46 MB and the staging directory holds
one file. `docs/RELEASE.md` §2 checks for the regression by asserting that staging holds exactly one
file, because the failure mode is silent — a working app, just 60% bigger.

**2. The app ignored `SIGTERM`.** Confirmed by measurement, not inspection: the pre-fix binary
survived a `SIGTERM` for 12 seconds and had to be `SIGKILL`ed.
`Host.CreateApplicationBuilder()` installs a `ConsoleLifetime`, whose POSIX handler sets
`Cancel = true` for SIGINT/SIGTERM — suppressing the runtime's own termination — and then merely
signals `StopApplication`, expecting `RunAsync` to notice and call `StopAsync`. This app calls
`Start`, not `Run`, because the foreground loop belongs to Avalonia, so nothing ever noticed: the
signal was swallowed and the window stayed open. The user-visible symptom is an app that cannot be
shut down by a session manager — a logout hangs until something forces the kill.

`AvaloniaHostLifetime` replaces it with a no-op, since Avalonia owns the process lifetime. The
registration uses `Replace`, not `TryAdd`, because the host builder gets there first. Closing the
window still stops the host through the desktop lifetime's `Exit` event, unchanged. As a side
benefit the app no longer prints `Application started. Press Ctrl+C to shut down.` at launch, which
was never true of a GUI app.

Raised with the user before building, since it is outside PHASE06's step list; the decision was to
fix it in this phase.

## Decisions taken inside the plan's latitude

- **The version lives in `Directory.Build.props`, not in a csproj.** Neither project is packable and
  they are never released separately, so a single solution-wide `<Version>` is the honest model. The
  `dotnet-release` skill's "a CLI/app keeps its own independent `<Version>`" applies to a solution
  that also publishes a library; this one doesn't.
- **Compression on, trimming off.** `EnableCompressionInSingleFile` roughly halves the download for a
  small startup cost. `PublishTrimmed` is deliberately absent: trimming Avalonia needs a reflection
  audit that no test here would catch the failure of, and the plan never asked for it.
- **The zips are named for the product, the executable is not renamed.** `Enigma.Backup-1.0.0-<rid>.zip`
  contains `Enigma.Backup.Desktop(.exe)`. Renaming the assembly at this point would churn the icon
  wiring and every doc reference for cosmetics.
- **`docs/RELEASE.md` is the app variant of the skill's runbook.** Pack/push/API-key steps are absent
  rather than commented out, and the tag convention is recorded as bare `X.Y.Z` (the repo had no tags
  before this release).

## Deviations & follow-ups

- **Windows was never run.** The build machine is Linux only. The `win-x64` zip was cross-published
  and its contents inspected, but nothing in it has been executed — no window has been opened on
  Windows, at any point in this project. The whole Windows column of `docs/QA-CHECKLIST.md` is
  outstanding, and the plan's acceptance ("both zips … smoke-tested", "manual QA executed on both
  platforms") is met for Linux only. Agreed with the user before building: produce both zips, verify
  Linux here, hand over the checklist.
- **No button was pressed, on either platform.** No input-automation tool (`xdotool`, `ydotool`,
  `wmctrl`, `kdotool`) is installed and the session is Wayland, so the Linux QA covers launching the
  shipped binary and observing it, not driving it — the same gap PHASE05 recorded. What is behind the
  buttons is covered by `WindowEndToEndTests`, which drives the real container, store and pipeline
  through the same view models the window binds to. The gap is the click.
- **The MSI profile was declined.** The `dotnet-release` skill offers a WixSharp MSI profile when
  releasing an app; the plan's validated decisions say "no installer, no CI". The plan wins — no
  `msiProfiles/` directory exists. Recorded here so the omission is a choice, not an oversight.
- **The app icon is still placeholder art** (`src/Enigma.Backup.Desktop/Assets/enigma-backup.ico`,
  16/32/48/256 px), confirmed with the user for 1.0.0. It is wired both ways — `<ApplicationIcon>`
  for the Windows exe, `Icon="/Assets/enigma-backup.ico"` on the window for Linux. Final art is a
  follow-up.
- **Dependency refresh was a no-op.** `dotnet list package --outdated` reported nothing outdated
  across all four projects, so no `old → new` transitions are recorded in `RELEASENOTES.md` and the
  version-coupled Avalonia set did not need holding back.
- **A stale `~/.config/Enigma.Backup/logs/app-2026-07-04.log` exists on this machine**, dated a month
  before this repository's first commit. Nothing in `src/` writes file logs (logging is console/debug
  only, per the plan), so it is unrelated pre-existing data. Left alone, noted in case it confuses a
  later reader.
- **Line endings:** the tree remains 100% LF and `.gitattributes` normalizes to LF — nothing to
  recommend, no action taken.

## Build/test evidence

- `dotnet build Enigma.Backup.slnx` — **Build succeeded, 0 Warning(s), 0 Error(s)**.
- `dotnet build Enigma.Backup.slnx -c Release` — **0 Warning(s), 0 Error(s)**.
- `dotnet test --solution Enigma.Backup.slnx` — **total: 352, failed: 0, succeeded: 352, skipped: 0**
  (351 at PHASE05; one new test,
  `TheHostLifetimeReplacesTheConsoleOne_SoSigtermIsNotSwallowed`, which registers a `ConsoleLifetime`
  first and asserts that `AddEnigmaBackupDesktop` replaces it rather than deferring to it — and that
  exactly one `IHostLifetime` descriptor survives).
- **Version stamping:** `<Version>1.0.0</Version>` reaches both assemblies — `1.0.0.0` is present in
  the built `Enigma.Backup.Core.dll` and `Enigma.Backup.Desktop.dll`, and
  `dotnet msbuild -getProperty:Version` returns `1.0.0`, which is what the publish scripts name the
  zips from.
- **Both zips produced by the scripts**, from a deleted `artifacts/`:
  `Enigma.Backup-1.0.0-linux-x64.zip` (44 MB) and `Enigma.Backup-1.0.0-win-x64.zip` (46 MB). Each
  contains **exactly one entry** — `Enigma.Backup.Desktop` / `Enigma.Backup.Desktop.exe` — confirmed
  with `unzip -l`.
- **Linux smoke test, against the extracted release zip** (not a `dotnet run` build):
  - the executable bit survives the round trip (`-rwxr-xr-x`), and the binary is a self-contained,
    stripped ELF;
  - it launches and renders the whole window — profile drop-down showing a profile read from the
    per-user config folder, the inputs list, the exclude editor showing `**/build/**`, the output
    path, `Zstandard (.tar.zst)` with its description, the encryption checkbox, the action row with
    `Cancel` correctly disabled, and the `Last run` placeholder. Screenshotted and inspected. This is
    the part single-file publishing can break: the managed assemblies decompress and the Skia and
    HarfBuzz natives extract and load;
  - `XDG_CONFIG_HOME` was pointed at a scratch folder throughout, so the user's real
    `~/.config/Enigma.Backup` was never written to;
  - **`SIGTERM` terminates it in ~1 s** (twice, on two separate builds of the fixed binary), against
    12 s-and-still-alive before the fix;
  - **stability:** watched for 90 s under observation — alive throughout, resident set flat at
    ~317 MB from t+10s onward, no drift, no self-exit, and no output on stdout or stderr.
- **Everything requiring a click is untested**, on both platforms — see *Deviations* above and the
  bottom section of `docs/QA-CHECKLIST.md`, which records precisely which checklist items 1.0.0
  verified and which are outstanding.
