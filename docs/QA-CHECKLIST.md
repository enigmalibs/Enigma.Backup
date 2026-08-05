# Manual QA checklist

Run this against the **extracted release zips**, not a `dotnet run` build — the point is to test what
ships. Both platforms, every release. Enigma.Backup has no command line, so the button presses below
cannot be automated; this file is the record that a human made them.

Set up a throwaway tree first, so nothing here touches real data:

```
qa/
  source/
    notes/readme.txt          a small text file
    notes/blob.bin            ~200 KB of random bytes
    build/junk.o              something for the exclude pattern to catch
    empty/                    an empty directory
  out/                        the profile's output folder
  restored/                   the restore destination
```

Legend: **L** = Linux x64 · **W** = Windows x64. Tick both.

**Since 1.1.0 the app asks its questions inside the window.** Every dialog below — the password, a
profile name, a confirmation, a decision — is raised over a dimmed main window rather than as a
separate window of its own, so there is no title-bar ✕ and **Escape or a click on the dimmed area**
is what cancels one. Run progress is likewise on a card over the window, with `Cancel` on the card
itself, and the outcome of a run arrives as a banner across the top of the window.

## 1. The artifact itself

| # | Check | L | W |
|---|-------|---|---|
| 1.1 | The zip extracts to **exactly one file**, the executable | ☐ | ☐ |
| 1.2 | Linux only: the extracted binary has its executable bit (`-rwxr-xr-x`) | ☐ | — |
| 1.3 | It launches on a machine with **no .NET runtime installed** | ☐ | ☐ |
| 1.4 | The window opens with the app icon and the title `Enigma.Backup` | ☐ | ☐ |
| 1.5 | Closing the window exits the process — nothing is left running | ☐ | ☐ |
| 1.6 | Linux only: `kill -TERM <pid>` terminates it (a session logout must not hang) | ☐ | — |

## 2. Profiles

| # | Check | L | W |
|---|-------|---|---|
| 2.1 | `New` clears the editor; the title shows `*` once anything is edited | ☐ | ☐ |
| 2.2 | `Save` prompts for a name **in the window**, over a dimmed background; the bar then reads `Saved.` and the `*` clears | ☐ | ☐ |
| 2.3 | The profile survives a restart — reopen the app and it is in the drop-down, fully populated | ☐ | ☐ |
| 2.4 | The `.json` file is in `%APPDATA%\Enigma.Backup\profiles` / `~/.config/Enigma.Backup/profiles` | ☐ | ☐ |
| 2.5 | `Save as…` refuses a name already taken (its OK button greys out); `Rename…` accepts the profile's own name | ☐ | ☐ |
| 2.6 | `Delete…` asks first, and does nothing when declined | ☐ | ☐ |
| 2.7 | `Export…` writes a `.json` to an arbitrary folder; `Import…` reads one back in | ☐ | ☐ |
| 2.8 | Switching profiles with unsaved edits discards them (the bar warned: *Unsaved changes.*) | ☐ | ☐ |
| 2.9 | On a name prompt, **Enter** confirms and **Escape** cancels; a click on the dimmed area also cancels | ☐ | ☐ |
| 2.10 | Each dialog carries a header icon matching what it is asking (a pencil to name, a warning to confirm a delete) | ☐ | ☐ |

## 3. Inputs, excludes and command enablement

| # | Check | L | W |
|---|-------|---|---|
| 3.1 | `Add files…` and `Add folder…` both add to the list | ☐ | ☐ |
| 3.2 | Dropping files/folders anywhere on the window adds them | ☐ | ☐ |
| 3.3 | `Remove` takes out the selected entry; `Remove all` empties the list | ☐ | ☐ |
| 3.4 | Typing `**/build/**` in the exclude field and pressing **Enter** (or `Add`) lists it; `Remove` takes it away | ☐ | ☐ |
| 3.5 | **Back up** is disabled with no inputs, and with no output folder | ☐ | ☐ |
| 3.6 | Every editor control is disabled while a run is in progress (and covered by the run card's dim) | ☐ | ☐ |
| 3.7 | *Archive options* shows **Compression** and **Encrypt with a password** as two cards; the compression text under the card changes with the choice | ☐ | ☐ |
| 3.8 | At the window's **minimum size** nothing overlaps — the headings stay above their lists and the action row stays visible | ☐ | ☐ |

## 4. Backup

| # | Check | L | W |
|---|-------|---|---|
| 4.1 | Zstd, unencrypted: produces `<Profile>_yyyy-MM-dd_HHmmss.tar.zst` **and** its `.sha256` | ☐ | ☐ |
| 4.2 | Gzip, unencrypted: same, with `.tar.gz` | ☐ | ☐ |
| 4.3 | With **Encrypt with a password** switched on: prompts in-window for entry + confirmation, output ends `.enc` | ☐ | ☐ |
| 4.4 | The password prompt refuses a mismatch, and refuses an empty password | ☐ | ☐ |
| 4.5 | `build/junk.o` is **not** in the archive — the exclude was applied | ☐ | ☐ |
| 4.6 | The empty directory **is** in the archive | ☐ | ☐ |
| 4.7 | The **run card** covers the window and shows the phase, the current file and a percentage that advances | ☐ | ☐ |
| 4.8 | The card is gone the moment the run ends — no dim is left over an app that cannot be clicked | ☐ | ☐ |
| 4.9 | A **banner** reports the outcome across the top of the window: green for a clean run | ☐ | ☐ |
| 4.10 | The **Last run** panel fills in with the counts, the duration and the output path | ☐ | ☐ |
| 4.11 | Pointing the output folder *inside* an input folder still works (it excludes itself) | ☐ | ☐ |

## 5. Restore

| # | Check | L | W |
|---|-------|---|---|
| 5.1 | Restoring the unencrypted archive reproduces the tree byte-for-byte | ☐ | ☐ |
| 5.2 | Restoring the encrypted archive prompts for the password, then does the same | ☐ | ☐ |
| 5.3 | A wrong password is reported clearly — a red banner — and **nothing** is written to the destination | ☐ | ☐ |
| 5.4 | Restoring twice over the same destination raises Skip / Overwrite / Abort **above the run card**, and it is clickable | ☐ | ☐ |
| 5.5 | *Apply to all* on that dialog stops it reappearing for the remaining files | ☐ | ☐ |
| 5.6 | The run card says `Restoring…` (not `Backing up…`) and its `Cancel` stops the restore | ☐ | ☐ |

## 6. Verify

| # | Check | L | W |
|---|-------|---|---|
| 6.1 | Verifying a fresh archive succeeds — green banner | ☐ | ☐ |
| 6.2 | Verifying the encrypted archive prompts in-window for the password and authenticates every block | ☐ | ☐ |
| 6.3 | Flipping one byte in the archive makes verify fail — red banner, and the reason in **Last run** | ☐ | ☐ |
| 6.4 | Editing the `.sha256` sidecar makes verify fail | ☐ | ☐ |
| 6.5 | The run card says `Verifying…` and the banner from the previous run is taken away when this one starts | ☐ | ☐ |

## 7. Errors and cancellation

| # | Check | L | W |
|---|-------|---|---|
| 7.1 | Make an input unreadable (`chmod 000` / deny read in the ACL): the run raises **Skip / Retry / Abort** — **above the run card**, and its buttons respond | ☐ | ☐ |
| 7.2 | **Skip** continues, and the skipped file is listed prominently in the summary; the banner is amber, not green | ☐ | ☐ |
| 7.3 | *Apply to all further errors* stops the dialog reappearing | ☐ | ☐ |
| 7.4 | **Abort** ends the run and leaves **no** partial archive in the output folder | ☐ | ☐ |
| 7.5 | Deleting an input path before running routes through the same dialog | ☐ | ☐ |
| 7.6 | `Cancel` **on the run card** stops the backup and deletes the partial archive | ☐ | ☐ |
| 7.7 | Dismissing a decision dialog with **Escape**, or by clicking the dimmed area, aborts (it never silently skips) | ☐ | ☐ |
| 7.8 | After an abort, a cancel and a failure alike, the card is gone and the window is clickable again | ☐ | ☐ |

## 8. Theme preference

New in 1.1.0. The preference lives in `settings.json` **beside** the `profiles/` folder —
`%APPDATA%\Enigma.Backup\settings.json` / `~/.config/Enigma.Backup/settings.json`.

| # | Check | L | W |
|---|-------|---|---|
| 8.1 | The gear at the right-hand end of the profile bar opens **Settings** in the window | ☐ | ☐ |
| 8.2 | Choosing `Light` and then `Dark` repaints the whole app at once — panels, editors, cards, buttons, **and the open dialog** | ☐ | ☐ |
| 8.3 | `System` returns the app to following the desktop's own light/dark setting | ☐ | ☐ |
| 8.4 | There is no OK button: the choice is already applied, and `Close`/Escape just closes the dialog | ☐ | ☐ |
| 8.5 | `settings.json` appears beside `profiles/` after the first change and holds the chosen theme | ☐ | ☐ |
| 8.6 | The choice survives a restart — the app opens in it, with **no flash** of the other variant first | ☐ | ☐ |
| 8.7 | Deleting `settings.json` returns the app to following the system, and merely starting the app does not recreate it | ☐ | ☐ |
| 8.8 | A hand-corrupted `settings.json` (truncate it mid-string) still starts the app, following the system, and the file is left exactly as it was | ☐ | ☐ |
| 8.9 | An unknown theme name in the file (`"theme": "Aubergine"`) is ignored the same way | ☐ | ☐ |

---

## What was verified for 1.1.0

**The artifacts do not exist yet.** 1.1.0's release preparation deliberately stopped short of
`./publish.sh`: the zips, the tag and the GitHub release are the maintainer's steps, so **nothing in
§1 has been ticked** and everything below was exercised against the Release *build*
(`bin/Release/net10.0/`), not an extracted zip. Run this whole checklist against the zips once they
are built.

**Linux x64 — against the 1.1.0 Release build** (Wayland + XWayland, `DISPLAY=:1`, with
`XDG_CONFIG_HOME` redirected to a scratch folder so no real profile was touched):

| # | Check | Result |
|---|-------|--------|
| 1.4 | The window opens with the app icon and the title | Pass — titled `Enigma.Backup — Documents`, icon in the title bar; screenshotted in both variants |
| 2.3 / 2.4 | A profile in the per-user config folder loads at startup | Pass — a seeded `.json` appeared in the drop-down with its two inputs, both exclude globs, its output folder, Zstandard and encryption **On** |
| 3.7 | *Archive options* shows both cards, with the compression sentence under the header | Pass |
| 8.6 | The stored theme is applied before the window is shown | Pass — `Light` and then `Dark` in `settings.json`, each opening in that variant with no flash of the other |
| — | The whole window reads correctly in **Light** and **Dark** | Pass — profile bar and gear, both panels, the framed lists, the `Save backups in` editor, the exclude editor, the expander and its two cards, the toggle switch and the action row |

**What was *not* exercised.** All of §1 (no zips), and everything requiring a button press on both
platforms — §§2–8 beyond the at-rest checks above. The cause is unchanged from 1.0.0 and 1.0.1: no
input-automation tool (`xdotool`, `ydotool`, `wmctrl`, `kdotool`) is installed on the build machine
and the session is Wayland, so release QA cannot click anything. What sits behind those buttons is
covered by the 392-test suite — in particular
`tests/Enigma.Backup.Desktop.Tests/Integration/WindowEndToEndTests.cs`, which drives the real
container, profile store and streaming pipeline through the same view models the window binds to.

**Windows x64 — not exercised.** The build machine is Linux only.

**Specific to this release.** 1.1.0 is almost entirely a UI change, so the risk sits precisely in the
sections a click is needed for: §§2 and 4–8. Each was, however, driven directly during the phases
that built it — PHASE02 drove all five dialogs and both pickers through the real services, PHASE03
photographed the run card, a mid-run question above the scrim and all three banner severities across
29 scripted checks, and PHASE04 drove the theme through the real store including the deleted and
corrupted files. Those are recorded in `docs/done/FEATURE-17FE-PHASE02..04.md`. What none of them
covers is the click itself, on the shipped artifact.

---

## What was verified for 1.0.1

**Linux x64 — automated, against the extracted release zip** (`Enigma.Backup-1.0.1-linux-x64.zip`):

| # | Check | Result |
|---|-------|--------|
| 1.1 | Extracts to exactly one file | Pass — one entry, `Enigma.Backup.Desktop` |
| 1.2 | Executable bit survives extraction | Pass — `-rwxr-xr-x` |
| 1.4 | Window opens with the app icon and title | Pass — titled `Enigma.Backup — QA Release`, icon in the title bar; screenshotted |
| 1.6 | `kill -TERM <pid>` terminates it | Pass — exited within a second |
| 2.3 / 2.4 | A profile in the per-user config folder loads at startup | Pass — a `.json` placed under `XDG_CONFIG_HOME` appeared in the drop-down with its inputs, exclude glob, output folder and compression all populated |
| 3.5 | **Back up** disabled with no inputs and no output folder | Pass, at rest — a second launch against an input-less profile shows it greyed out while Restore… and Verify… stay enabled |

**What was *not* exercised.** Everything requiring a button press, on both platforms — all of §4
(backup), §5 (restore), §6 (verify) and §7 (errors and cancellation), the interactive half of §2 and
§3, plus §1.3 (no .NET runtime installed) and §1.5 (closing the window exits the process). The gap is
unchanged from 1.0.0 and has the same cause: no input-automation tool (`xdotool`, `ydotool`,
`wmctrl`, `kdotool`) is installed on the build machine and the session is Wayland, so the release QA
cannot click anything. What sits behind those buttons is covered by the 354-test suite — in
particular `tests/Enigma.Backup.Desktop.Tests/Integration/WindowEndToEndTests.cs`, which drives the
real container, profile store and streaming pipeline through the same view models the window binds
to.

**Windows x64 — not exercised.** The build machine is Linux only. The `win-x64` zip was
cross-published and its staging directory inspected (§1.1: one file, `Enigma.Backup.Desktop.exe`),
but nothing in it has been run.

**Specific to this release.** 1.0.1 changes no application code — only the cryptography dependency
underneath it. The risk it carries is not in the buttons but in whether existing encrypted archives
still open, and that is covered by an automated test rather than by this checklist: a real 1.0.0
archive is checked into `tests/Enigma.Backup.Core.Tests/Fixtures/` and is verified and restored
byte-for-byte on every test run.

---

## What was verified for 1.0.0

**Linux x64 — automated, against the extracted release zip** (`Enigma.Backup-1.0.0-linux-x64.zip`):
1.1, 1.2, 1.4, 1.6 passed, plus the profile from §2.3–2.4 loading from the per-user config folder,
and the at-rest half of 3.5. The app was launched from the zip with `XDG_CONFIG_HOME` pointed at a
scratch folder, screenshotted, and signalled.

**Everything requiring a button press is unticked on both platforms.** No input-automation tool
(`xdotool`, `ydotool`, `wmctrl`, `kdotool`) is installed on the build machine and the session is
Wayland, so the release QA could not click anything — the same gap PHASE05 recorded. What sits
*behind* those buttons is covered by the 352-test suite, in particular
`tests/Enigma.Backup.Desktop.Tests/Integration/WindowEndToEndTests.cs`, which drives the real
container, the real profile store and the real streaming pipeline over real files through the same
view models the window binds to — Zstd and gzip, encrypted and not. The untested gap is the click
itself, and the Windows platform entirely.

**Windows x64 — not exercised.** The build machine is Linux only. The `win-x64` zip was
cross-published and its staging directory inspected (§1.1), but nothing in it has been run.
