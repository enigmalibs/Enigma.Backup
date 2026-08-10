# Release runbook

Reusable checklist for releasing a new **Enigma.Backup** version. Nothing here is published to
NuGet: neither project is packable, and the release artifacts are the two self-contained zips built
by `publish.sh` / `publish.ps1`. There are consequently no `dotnet pack` or `dotnet nuget push`
steps, and no API key to handle.

Replace `X.Y.Z` with the version being released (e.g. `1.0.1`) throughout. The version lives in
`Directory.Build.props` (`<Version>`) and covers the whole solution — the Core and the Desktop app
ship together and are never versioned apart.

## 1. Pre-release checks

Run from the repository root, on the branch that will be merged:

- [ ] `<Version>X.Y.Z</Version>` set in `Directory.Build.props`.
- [ ] `RELEASENOTES.md` has a top `X.Y.Z` section describing the release (newest-first; any
      `(unreleased)` heading renamed to `X.Y.Z`).
- [ ] The README's "what's new" callout reflects `X.Y.Z`, and its **Installing** table names the
      right zip filenames.
- [ ] `SECURITY.md`'s supported-versions table covers `X.Y.x`.
- [ ] Clean, warning-free build:
      ```bash
      dotnet build Enigma.Backup.slnx -c Release
      ```
- [ ] Full test suite green:
      ```bash
      dotnet test --solution Enigma.Backup.slnx
      ```
- [ ] Dependency check — apply non-coupled bumps, and bump the **Avalonia set as a unit** or hold
      the whole set back (`Enigma.Avalonia.Desktop` belongs to that set). Log every `old → new` in
      `RELEASENOTES.md` under *Dependencies*, and record a clean vulnerability scan there too:
      ```bash
      dotnet list package --outdated
      dotnet list package --vulnerable --include-transitive
      ```
- [ ] `docs/QA-CHECKLIST.md` still describes the UI as it now is — a release that moved a control,
      a dialog or a button has to move the check that presses it.
- [ ] The `EBK1` container version byte is unchanged, or the change is called out as breaking in
      `RELEASENOTES.md` — archives written by earlier releases must stay readable.
- [ ] The profile write rule still holds: a profile using no feature newer than `formatVersion` 1 is
      still saved as 1 (a serializer test pins it), and any *new* feature that forces a higher version
      is named in `RELEASENOTES.md` as refused by older builds — that refusal is by design, but it is
      only honest if it is written down.

## 2. Build the release zips

```bash
./publish.sh                    # Linux and macOS hosts
```
```powershell
./publish.ps1                   # Windows hosts
```

Each runtime is published self-contained and single-file into `artifacts/publish/<rid>/` and zipped
to `artifacts/Enigma.Backup-X.Y.Z-<rid>.zip`. Confirm:

- [ ] Both zips exist and their filenames carry `X.Y.Z`.
- [ ] Each staging directory holds **exactly one file** — the executable. Anything else (notably the
      Skia/HarfBuzz native `.pdb`s) means the publish-time filter in
      `src/Enigma.Backup.Desktop/Enigma.Backup.Desktop.csproj` has stopped working.
- [ ] The **linux-x64 zip was built from Linux**. PowerShell's `Compress-Archive` records no Unix
      file mode, so a Linux zip built on Windows extracts without its executable bit.

## 3. Smoke-test the artifacts

Run [`docs/QA-CHECKLIST.md`](QA-CHECKLIST.md) against the extracted zips — both platforms. The
checklist is the one thing here that cannot be automated: the app has no CLI, so the button presses
are manual.

- [ ] Windows checklist complete.
- [ ] Linux checklist complete.

## 4. Merge to the default branch

The repository branches **feature → develop → main**: work lands on a `feature/…` (or `bugfix/…`,
`review/…`) branch, merges into `develop`, and only `develop` merges into `main`. A release therefore
takes two merges, not one — the release branch never goes straight to `main`.

```bash
git switch develop
git merge --no-ff feature/<the-release-branch>

git switch main
git merge --no-ff develop
```

Then make sure `main` is what gets tagged:

```bash
git switch main
git pull
```

## 5. Tag the release

The convention is a **bare** `X.Y.Z` — `1.0.0`, `1.0.1` and `1.1.0` are the tags that exist. Run
`git tag` to confirm before adding a new one. Tag the merge commit on `main` and push the tag:

```bash
git tag X.Y.Z
git push origin X.Y.Z
```

## 6. Publish the release

- [ ] Create the GitHub release against the `X.Y.Z` tag.
- [ ] Attach both zips from `artifacts/`.
- [ ] Paste the `RELEASENOTES.md` section for `X.Y.Z` as the release body.

## 7. Post-release verification

- [ ] Both zips download from the release page and their SHA-256 matches the local artifact.
- [ ] A fresh extraction on a machine with **no .NET runtime installed** launches and shows the main
      window — this is what "self-contained" is claiming, and it is the one thing a developer
      machine cannot check.
- [ ] The tag exists on `main` and its release notes match `RELEASENOTES.md`.
