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
| 2.2 | `Save` prompts for a name, then the bar reads `Saved.` and the `*` clears | ☐ | ☐ |
| 2.3 | The profile survives a restart — reopen the app and it is in the drop-down, fully populated | ☐ | ☐ |
| 2.4 | The `.json` file is in `%APPDATA%\Enigma.Backup\profiles` / `~/.config/Enigma.Backup/profiles` | ☐ | ☐ |
| 2.5 | `Save as…` refuses a name already taken; `Rename…` accepts the profile's own name | ☐ | ☐ |
| 2.6 | `Delete…` asks first, and does nothing when declined | ☐ | ☐ |
| 2.7 | `Export…` writes a `.json` to an arbitrary folder; `Import…` reads one back in | ☐ | ☐ |
| 2.8 | Switching profiles with unsaved edits discards them (the bar warned: *Unsaved changes.*) | ☐ | ☐ |

## 3. Inputs, excludes and command enablement

| # | Check | L | W |
|---|-------|---|---|
| 3.1 | `Add files…` and `Add folder…` both add to the list | ☐ | ☐ |
| 3.2 | Dropping files/folders anywhere on the window adds them | ☐ | ☐ |
| 3.3 | `Remove` takes out the selected entry; `Remove all` empties the list | ☐ | ☐ |
| 3.4 | Adding the exclude `**/build/**` lists it; `Remove` takes it away | ☐ | ☐ |
| 3.5 | **Back up** is disabled with no inputs, and with no output folder | ☐ | ☐ |
| 3.6 | Every editor control is disabled while a run is in progress | ☐ | ☐ |

## 4. Backup

| # | Check | L | W |
|---|-------|---|---|
| 4.1 | Zstd, unencrypted: produces `<Profile>_yyyy-MM-dd_HHmmss.tar.zst` **and** its `.sha256` | ☐ | ☐ |
| 4.2 | Gzip, unencrypted: same, with `.tar.gz` | ☐ | ☐ |
| 4.3 | With **Encrypt with a password** ticked: prompts twice (entry + confirmation), output ends `.enc` | ☐ | ☐ |
| 4.4 | The password prompt refuses a mismatch, and refuses an empty password | ☐ | ☐ |
| 4.5 | `build/junk.o` is **not** in the archive — the exclude was applied | ☐ | ☐ |
| 4.6 | The empty directory **is** in the archive | ☐ | ☐ |
| 4.7 | Progress shows the current file and advances; the summary gives counts, duration and path | ☐ | ☐ |
| 4.8 | Pointing the output folder *inside* an input folder still works (it excludes itself) | ☐ | ☐ |

## 5. Restore

| # | Check | L | W |
|---|-------|---|---|
| 5.1 | Restoring the unencrypted archive reproduces the tree byte-for-byte | ☐ | ☐ |
| 5.2 | Restoring the encrypted archive prompts for the password, then does the same | ☐ | ☐ |
| 5.3 | A wrong password is reported clearly, and **nothing** is written to the destination | ☐ | ☐ |
| 5.4 | Restoring twice over the same destination raises Skip / Overwrite / Abort | ☐ | ☐ |
| 5.5 | *Apply to all* on that dialog stops it reappearing for the remaining files | ☐ | ☐ |

## 6. Verify

| # | Check | L | W |
|---|-------|---|---|
| 6.1 | Verifying a fresh archive succeeds | ☐ | ☐ |
| 6.2 | Verifying the encrypted archive prompts for the password and authenticates every block | ☐ | ☐ |
| 6.3 | Flipping one byte in the archive makes verify fail | ☐ | ☐ |
| 6.4 | Editing the `.sha256` sidecar makes verify fail | ☐ | ☐ |

## 7. Errors and cancellation

| # | Check | L | W |
|---|-------|---|---|
| 7.1 | Make an input unreadable (`chmod 000` / deny read in the ACL): the run raises **Skip / Retry / Abort** | ☐ | ☐ |
| 7.2 | **Skip** continues, and the skipped file is listed prominently in the summary | ☐ | ☐ |
| 7.3 | *Apply to all further errors* stops the dialog reappearing | ☐ | ☐ |
| 7.4 | **Abort** ends the run and leaves **no** partial archive in the output folder | ☐ | ☐ |
| 7.5 | Deleting an input path before running routes through the same dialog | ☐ | ☐ |
| 7.6 | `Cancel` mid-backup stops it and deletes the partial archive | ☐ | ☐ |
| 7.7 | Dismissing a decision dialog with the title-bar ✕ aborts (it never silently skips) | ☐ | ☐ |

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
