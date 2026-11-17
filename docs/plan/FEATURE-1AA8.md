# FEATURE-1AA8 — Enigma Backup in every user-facing text

**Status:** IN PROGRESS
**Type:** FEATURE (multi-phase — 3 phases)
**Branch (per phase, at build time):** `feature/feature-1aa8-phaseNN-<slug>`, cut from the run-branch tip.
**Run:** feature/2026-11-16-display-name-everywhere

## Objective

Every text the product shows its user names it **Enigma Backup**, never `Enigma.Backup`. That covers the
app, the error messages it shows, the Windows exe's metadata, the Linux launcher entry, and the install
scripts a user runs.

## Context & constraints

- FEATURE-3310 (this branch's base) already fixed three things: the About dialog (`AppInfo.Name`), the
  startup-failure window title, and the archive filter in the file picker. Its done doc left the Core
  error messages as a follow-up, and the user has now asked for all of them.
- **What still shows the dotted name:**
  - `ArchiveFormatDetector` throws "The file is not an **Enigma.Backup** archive: …". It reaches the
    run dialog when Verify or Restore is given a file that is not an archive.
  - `Ebk1Header` throws two messages, "… this version of **Enigma.Backup** only reads version …" and
    "… which this version of **Enigma.Backup** does not know …". They are raised for an encrypted
    archive from a newer format.
  - The Windows exe sets neither `AssemblyTitle` nor `Product`, so both default to the assembly name
    `Enigma.Backup.Desktop`. That becomes the file's *FileDescription* and *ProductName*: the name
    Task Manager, the file's Properties and Windows' *Open with* list show.
  - `packaging/linux/enigma-backup.desktop.in` has `Name=Enigma.Backup`: the name in the user's
    application launcher.
  - `packaging/linux/install.sh` / `uninstall.sh` print the dotted name: "Installing …", "Done.
    "Enigma.Backup" is in your application launcher.", "Removing …", "Nothing to remove — …", and their
    `--help` text.
- **The dotted name is also an identifier, and those uses stay:**
  - the config folder `<config>/Enigma.Backup/`, which holds every saved profile;
  - the project, assembly, exe, zip and MSI file names;
  - namespaces, log categories and stack traces;
  - `StartupWMClass=Enigma.Backup.Desktop`, which must match the window's class for the launcher to group
    the running window with its entry;
  - the docs' prose and titles, where it names the repository.
- Two Core tests assert "not an Enigma.Backup archive": `Ebk1ContainerTests` line 390 and
  `RestoreServiceTests` line 201.
- **1.6.0 is prepared but not tagged** (FEATURE-63B5, in this branch's base). This work ships in it: its
  notes' "Enigma Backup everywhere the app names itself" bullet grows with each phase, and the version
  does not move.
- No test covers the Linux packaging files, and this host is Windows. The scripts can be syntax-checked
  and dry-run in Git Bash; a real install needs Linux.

## PHASE01 — Core error messages

**Status:** DONE

Steps:
1. `ArchiveFormatDetector`: "The file is not an Enigma Backup archive: …".
2. `Ebk1Header`: both messages say "this version of Enigma Backup".
3. Tests:
   - `Ebk1ContainerTests` and `RestoreServiceTests` expect "not an Enigma Backup archive";
   - the unsupported-version and unsupported-key-derivation tests assert that the message names
     `Enigma Backup` (add the assertion where those tests already check the message).
4. `RELEASENOTES.md` 1.6.0: the name bullet mentions the error messages.

Acceptance criteria:
- The three messages say `Enigma Backup`, and tests pin each one.
- No string literal in `src/Enigma.Backup.Core` that a user can see names `Enigma.Backup`. The config
  folder's path segment is the one remaining literal.
- Debug and Release builds have zero warnings. The whole suite passes against the run's baseline.

## PHASE02 — The Windows exe's metadata

**Status:** TODO

Steps:
1. `Enigma.Backup.Desktop.csproj`: `<AssemblyTitle>Enigma Backup</AssemblyTitle>` and
   `<Product>Enigma Backup</Product>`, with a one-line "why" comment. They go there, not in
   `Directory.Build.props`, so the Core and test assemblies keep their own titles.
2. A Desktop test reads the app assembly's `AssemblyTitleAttribute` and `AssemblyProductAttribute`, and
   asserts that both equal `AppInfo.Name`.
3. Build a Release `win-x64` publish (or the release zip) and read the exe's `FileVersionInfo`:
   `FileDescription` and `ProductName` are `Enigma Backup`. `AppInfo.Version` / `BuildSha` /
   `Copyright` are unaffected (About still shows them).
4. `RELEASENOTES.md` 1.6.0: the name bullet mentions Task Manager and the file's Properties.
   `docs/QA-CHECKLIST.md`: a §1 row for the Windows column, checking that Task Manager lists
   `Enigma Backup`.

Acceptance criteria:
- The published exe's `FileDescription` and `ProductName` are `Enigma Backup`, and a test pins the
  assembly attributes.
- The exe's file name, the assembly name, the version attributes and the MSI are unchanged.
- Debug and Release builds have zero warnings. The whole suite passes against the run's baseline.

## PHASE03 — Linux launcher and install scripts

**Status:** TODO

Steps:
1. `enigma-backup.desktop.in`: `Name=Enigma Backup`. `StartupWMClass` and the file's name stay as they
   are.
2. `install.sh` / `uninstall.sh`: every message and `--help` line that names the app says
   `Enigma Backup`. Paths (`~/.config/Enigma.Backup`), `APP_EXE` and the comments that name files stay
   as they are.
3. Verification in Git Bash:
   - `bash -n` on both scripts;
   - `install.sh --help` and `uninstall.sh --help`;
   - `uninstall.sh` against an empty temporary `XDG_DATA_HOME`, which prints the "Nothing to remove"
     line.
4. `RELEASENOTES.md` 1.6.0: the name bullet mentions the launcher entry. `docs/QA-CHECKLIST.md`: a row
   for the Linux column, checking that after `install.sh` the launcher lists `Enigma Backup`.

Acceptance criteria:
- The launcher entry's `Name` is `Enigma Backup`, and its `StartupWMClass` is unchanged.
- No message either script prints names the app `Enigma.Backup`. Paths still do, by design.
- Both scripts pass `bash -n`, and the dry runs print the new name.
- The scripts keep mode `100755` (`git ls-files -s`).
- The whole suite passes against the run's baseline. A real install on Linux is listed as owed.

## Out of scope

- Renaming the config folder, the assemblies, the exe, the zips or the MSI file.
- The docs' prose and titles (README, RELEASENOTES history, SECURITY, CLAUDE.md), where `Enigma.Backup`
  names the repository. Only places that quote UI text change.
- The maintainer's build scripts (`publish.*`, `buildmsi.*`, `packaging/*/publish.*`): their output names
  files and is read by whoever cuts a release.
- A version bump: 1.6.0 is not released yet.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| What counts as "displayed to the user" | Text the app shows, the exe's metadata, the launcher entry, and the output of the scripts the README tells users to run | Those are the places a user reads the product's name. Paths, file names and identifiers are contracts, and the docs name the repository | Also renaming docs prose and the maintainer's scripts; only the Core messages |
| Rename the config folder | No | Every saved profile and preference would be left behind. A migration is a risky feature nobody asked for, and the folder is a path, not display text | Renaming it with a one-time move |
| Where to set the exe's metadata | `AssemblyTitle` + `Product` in the Desktop csproj | Only the app's exe is seen by a user. `Directory.Build.props` would also rename the Core and test assemblies' titles for no one's benefit | `Directory.Build.props`; leaving Windows' Task Manager name as is |
| `StartupWMClass` | Unchanged | It must equal the window's class, which comes from the exe, so it is an identifier, not a label. Changing it would break the launcher's window grouping | Renaming it too |
| A product-name constant in Core | No; plain literals | Core has no name constant, and the Desktop's `AppInfo.Name` is out of its reach. Three messages do not justify a new abstraction, and the tests pin each one | A `Product.Name` constant in Core |
| Version | No bump; 1.6.0's notes absorb the change | 1.6.0 is prepared but not tagged, and these changes are part of the same rename | 1.6.1; 1.7.0 |
| Breakdown | One item, three phases: Core → Windows exe → Linux packaging | One request. Each surface is a small commit that can be reviewed alone | One single dev; three separate items |
