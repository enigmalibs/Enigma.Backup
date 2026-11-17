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
  logs/
    app.log                   for a *.log pattern to match
    keep.txt                  for it not to match
    DirPrefixOne/inner.txt    for a DirPrefix* pattern to take whole
    other/x.txt               for neither to touch
  out/                        the profile's output folder
  restored/                   the restore destination
```

Legend: **L** = Linux x64 · **W** = Windows x64. Tick both.

**Since 1.1.0 the app asks its questions inside the window.** Every dialog below — the password, a
profile name, a confirmation, a decision — is raised over a dimmed main window rather than as a
separate window of its own, so there is no title-bar ✕ and **Escape or a click on the dimmed area**
is what cancels one. **Since 1.6.0 a run is shown in a dialog of its own**: its progress and
`Cancel` while it runs, then — in the same dialog — its outcome and `Close`. There is no banner and no
**Last run** panel any more; wherever the rows below speak of the outcome, it is that dialog.

**Since 1.6.0 the window has a rail of pages on the left**, and `Restore…` and `Verify…` are
on the **Verify & Restore** page (the rail's second icon) — wherever §§5–6 below say "start a restore"
or "start a verify", that is where the button is now. The window title is just `Enigma Backup`, and
**Settings** is a page at the foot of the rail (§8). On a wide window every page sits in a centred
column (3.14), and About names the app `Enigma Backup` (8.13). The two capabilities 1.2.0 added have
sections of their own at the end: **pattern inputs** (§9) and **zip archives** (§10).

**Since 1.3.0 About is a dialog of its own**, opened from the Settings page's About card since
1.6.0 (§8). An unexpected error opens a **Something went wrong** dialog
instead of ending the app (§7, 7.9–7.12 — these need a throwing build), and the zip carries `app.ico`
beside the executable (§1).

**Since 1.4.0 the icon has no tile**: the same page and zip on a transparent background, wherever
§1.1, §1.4 and 8.13 look at it. A release may also ship a **Windows MSI**, built with
`packaging\windows\buildmsi.ps1`; §11 covers it, on a Windows machine with the .NET 10 runtime.

**Since 1.5.0 the icon fills its frame**: the same tile-less page and zip, a third larger. §1.1, §1.4,
8.13 and 11.2–11.3 should show the larger drawing, and the Linux launcher entry too.

## 1. The artifact itself

| # | Check | L | W |
|---|-------|---|---|
| 1.1 | The zip extracts to **exactly two files**: the executable and `app.ico` (the app's icon, identical to the one the window shows) | ☐ | ☐ |
| 1.2 | Linux only: the extracted binary has its executable bit (`-rwxr-xr-x`) | ☐ | — |
| 1.3 | It launches on a machine with **no .NET runtime installed** | ☐ | ☐ |
| 1.4 | The window opens with the app icon and the title `Enigma Backup` | ☐ | ☐ |
| 1.5 | Closing the window exits the process — nothing is left running | ☐ | ☐ |
| 1.6 | Linux only: `kill -TERM <pid>` terminates it (a session logout must not hang) | ☐ | — |
| 1.7 | Windows only: Task Manager lists the running app as `Enigma Backup`, and the exe's *Properties → Details* show `Enigma Backup` as File description and Product name | — | ☐ |

## 2. Profiles

| # | Check | L | W |
|---|-------|---|---|
| 2.1 | On the **Profiles** page, `New` clears the editor; the Profile card reads `Not saved as a profile yet.` once anything is edited | ☐ | ☐ |
| 2.2 | `Save` prompts for a name **in the window**, over a dimmed background; the Profile card then reads `Saved.` | ☐ | ☐ |
| 2.3 | The profile survives a restart — reopen the app and it is in the drop-down, fully populated | ☐ | ☐ |
| 2.4 | The `.json` file is in `%APPDATA%\Enigma.Backup\profiles` / `~/.config/Enigma.Backup/profiles` | ☐ | ☐ |
| 2.5 | `Save as…` refuses a name already taken (its OK button greys out); `Rename…` accepts the profile's own name | ☐ | ☐ |
| 2.6 | `Delete…` asks first, and does nothing when declined | ☐ | ☐ |
| 2.7 | `Export…` writes a `.json` to an arbitrary folder; `Import…` reads one back in | ☐ | ☐ |
| 2.8 | Switching profiles with unsaved edits discards them (the Profile card warned: *Unsaved changes.*); going to another page and back keeps them | ☐ | ☐ |
| 2.9 | On a name prompt, **Enter** confirms and **Escape** cancels; a click on the dimmed area also cancels | ☐ | ☐ |
| 2.10 | Each dialog carries a header icon matching what it is asking (a pencil to name, a warning to confirm a delete) | ☐ | ☐ |

## 3. Inputs, excludes and command enablement

| # | Check | L | W |
|---|-------|---|---|
| 3.1 | `Add files…` and `Add folder…` both add to the list — on Backup and on Profiles alike | ☐ | ☐ |
| 3.2 | Dropping files/folders on the Backup page (on **Files and folders**) or the Profiles page adds them to that page's list; on the other pages, and on Backup while **A profile** is chosen, a drop is refused | ☐ | ☐ |
| 3.3 | `Remove` takes out the selected entry; `Remove all` empties the list | ☐ | ☐ |
| 3.4 | Typing `**/build/**` in the exclude field and pressing **Enter** (or `Add`) lists it; `Remove` takes it away | ☐ | ☐ |
| 3.5 | **Back up** is disabled with no inputs, and with no output folder — and the line beside it says which is missing | ☐ | ☐ |
| 3.6 | Every editor control is disabled while a run is in progress (and covered by the run dialog's dim) | ☐ | ☐ |
| 3.7 | *Output* shows **Save backups in**, then **Compression** and **Encrypt with a password** as two cards; the compression text under the card changes with the choice | ☐ | ☐ |
| 3.8 | At the window's **minimum size** nothing overlaps — the pages scroll, and **Back up** stays in view at the foot of Backup | ☐ | ☐ |
| 3.9 | `Add pattern…` sits beside `Add files…` and `Add folder…`; at the minimum width the five buttons wrap to two rows and all stay pressable (the rule itself is §9) | ☐ | ☐ |
| 3.10 | The rail shows **Backup**, **Verify & Restore** and **Profiles** as icons, with **Settings** at its foot, each naming its page on hover; the selected one is highlighted, and clicking the other switches the page and keeps what the first one held | ☐ | ☐ |
| 3.11 | Each page opens with its icon, name and one sentence; Backup holds two sections, **Input** and **Output**, and no profile button at all; the profile drop-down and its seven buttons are on **Profiles** | ☐ | ☐ |
| 3.12 | With at least one profile saved, Backup opens on **A profile** with the first one chosen: Input shows its summary (each folder, each pattern as `pattern in folder`, what it leaves out), Output shows its folder and options greyed out under *Set by the profile*; with none, it opens on **Files and folders** | ☐ | ☐ |
| 3.13 | **Back up** on a profile writes `<Profile>_…` archives exactly as last saved — an unsaved edit on the Profiles page is not in it; saving that edit updates the summary at once | ☐ | ☐ |
| 3.14 | Maximised, every page's header, cards and — on Backup — the **Back up** row sit in one column of at most 900 px, **centred** right of the rail, the header's left edge over the cards'; at the minimum width the column fills the page and nothing scrolls sideways | ☐ | ☐ |

## 4. Backup

| # | Check | L | W |
|---|-------|---|---|
| 4.1 | Zstd, unencrypted: produces `<Profile>_yyyy-MM-dd_HHmmss.tar.zst` **and** its `.sha256` | ☐ | ☐ |
| 4.2 | Gzip, unencrypted: same, with `.tar.gz` | ☐ | ☐ |
| 4.3 | With **Encrypt with a password** switched on: prompts in-window for entry + confirmation, output ends `.enc` | ☐ | ☐ |
| 4.4 | The password prompt refuses a mismatch, and refuses an empty password | ☐ | ☐ |
| 4.5 | `build/junk.o` is **not** in the archive — the exclude was applied | ☐ | ☐ |
| 4.6 | The empty directory **is** in the archive | ☐ | ☐ |
| 4.7 | The **run dialog** covers the window and shows the phase, the current file and a percentage that advances; Escape and a click on the dimmed area do **not** close it | ☐ | ☐ |
| 4.8 | When the run ends, the same dialog turns to the outcome with a `Close` button (Enter and Escape close it too) — no dim is ever left over an app that cannot be clicked | ☐ | ☐ |
| 4.9 | The outcome's title and icon say how it went: **Done** with a green check for a clean run | ☐ | ☐ |
| 4.10 | The outcome lists the counts, the duration and the output path, labels aligned in one column | ☐ | ☐ |
| 4.11 | Pointing the output folder *inside* an input folder still works (it excludes itself) | ☐ | ☐ |

## 5. Restore

| # | Check | L | W |
|---|-------|---|---|
| 5.1 | Restoring the unencrypted archive reproduces the tree byte-for-byte | ☐ | ☐ |
| 5.2 | Restoring the encrypted archive prompts for the password, then does the same | ☐ | ☐ |
| 5.3 | A wrong password is reported clearly — **Something went wrong**, in red — and **nothing** is written to the destination | ☐ | ☐ |
| 5.4 | Restoring twice over the same destination raises Skip / Overwrite / Abort **above the run dialog**, and it is clickable | ☐ | ☐ |
| 5.5 | *Apply to all* on that dialog stops it reappearing for the remaining files | ☐ | ☐ |
| 5.6 | The run dialog says `Restoring` (not `Backing up`) and its `Cancel` stops the restore | ☐ | ☐ |

## 6. Verify

| # | Check | L | W |
|---|-------|---|---|
| 6.1 | Verifying a fresh archive succeeds — **Done**, green | ☐ | ☐ |
| 6.2 | Verifying the encrypted archive prompts in-window for the password and authenticates every block | ☐ | ☐ |
| 6.3 | Flipping one byte in the archive makes verify fail — **Something went wrong**, red, with the reason in the outcome | ☐ | ☐ |
| 6.4 | Editing the `.sha256` sidecar makes verify fail | ☐ | ☐ |
| 6.5 | The run dialog says `Checking the archive`, and each run opens a dialog of its own — nothing of the previous run is left on screen | ☐ | ☐ |

## 7. Errors and cancellation

| # | Check | L | W |
|---|-------|---|---|
| 7.1 | Make an input unreadable (`chmod 000` / deny read in the ACL): the run raises **Skip / Retry / Abort** — **above the run dialog**, and its buttons respond | ☐ | ☐ |
| 7.2 | **Skip** continues, and the skipped file is listed prominently in the outcome; its title is **Finished, with something to report**, amber, not green | ☐ | ☐ |
| 7.3 | *Apply to all further errors* stops the dialog reappearing | ☐ | ☐ |
| 7.4 | **Abort** ends the run and leaves **no** partial archive in the output folder | ☐ | ☐ |
| 7.5 | Deleting an input path before running routes through the same dialog | ☐ | ☐ |
| 7.6 | `Cancel` **in the run dialog** stops the backup and deletes the partial archive; the dialog stays up, says `Cancelling…`, then shows the cancelled outcome | ☐ | ☐ |
| 7.7 | Dismissing a decision dialog with **Escape**, or by clicking the dimmed area, aborts (it never silently skips) | ☐ | ☐ |
| 7.8 | After an abort, a cancel and a failure alike, the run dialog shows the outcome, and once closed the window is clickable again | ☐ | ☐ |
| 7.9 | An unexpected error (needs a throwing build — e.g. a temporary `throw` in a command) shows **Something went wrong** with the warning icon, its message and a single `Close`; the app keeps running afterwards | ☐ | ☐ |
| 7.10 | A second error while that dialog is open adds "1 more error occurred." to it rather than opening another; *Details* expands to the type, message and stack, and **Copy details** puts them on the clipboard | ☐ | ☐ |
| 7.11 | An unexpected error while another dialog is open (e.g. About) waits for that dialog to close, then appears — it never replaces it | ☐ | ☐ |
| 7.12 | A failure during start-up (needs a throwing build — a temporary `throw` in `App.Compose`) opens a small `Enigma Backup` window with the same dialog, saying the app could not start; `Close` ends the process | ☐ | ☐ |

## 8. Settings: theme and storage; About

The theme preference (1.1.0) lives in `settings.json` **beside** the `profiles/` folder —
`%APPDATA%\Enigma.Backup\settings.json` / `~/.config/Enigma.Backup/settings.json`. The Storage card
(1.2.0) and the About dialog (1.3.0) store nothing of their own; they read what is already there.

| # | Check | L | W |
|---|-------|---|---|
| 8.1 | The gear at the foot of the rail opens the **Settings** page: an **Appearance**, a **Storage** and an **About** section, laid out like Enigma.Launchpad's | ☐ | ☐ |
| 8.2 | Choosing `Light` and then `Dark` repaints the whole app at once — the rail, the pages, editors, cards and buttons | ☐ | ☐ |
| 8.3 | `System` returns the app to following the desktop's own light/dark setting | ☐ | ☐ |
| 8.4 | There is no OK button: the choice is already applied, and leaving the page keeps it | ☐ | ☐ |
| 8.5 | `settings.json` appears beside `profiles/` after the first change and holds the chosen theme | ☐ | ☐ |
| 8.6 | The choice survives a restart — the app opens in it, with **no flash** of the other variant first | ☐ | ☐ |
| 8.7 | Deleting `settings.json` returns the app to following the system, and merely starting the app does not recreate it | ☐ | ☐ |
| 8.8 | A hand-corrupted `settings.json` (truncate it mid-string) still starts the app, following the system, and the file is left exactly as it was | ☐ | ☐ |
| 8.9 | An unknown theme name in the file (`"theme": "Aubergine"`) is ignored the same way | ☐ | ☐ |
| 8.10 | A **Storage** card names the config folder and carries an **Open folder** button | ☐ | ☐ |
| 8.11 | `Open folder` shows that folder in the desktop's file manager, with `settings.json` and `profiles/` in it | ☐ | ☐ |
| 8.12 | On a fresh config — nothing written yet — `Open folder` **creates** the folder and opens it rather than reporting an error | ☐ | ☐ |
| 8.13 | `About Enigma Backup` on the About card (beside `Version 1.6.0`) opens **About**, with no title bar text and no header icon: the app icon, `Enigma Backup`, `Version 1.6.0`, a quieter `Build <7-char sha>` line and the copyright line, centred — the same dialog as every other Enigma app | ☐ | ☐ |
| 8.14 | About's only button is `Close`; Escape and Enter close it too, and the build sha can be selected and copied | ☐ | ☐ |
| 8.15 | About fits its host — nothing scrolls and nothing is clipped, in both variants; the Settings page holds Theme, Storage and About only, and no gear or ⓘ is left anywhere else | ☐ | ☐ |

## 9. Pattern inputs

New in 1.2.0. A pattern is a *(folder, glob)* pair, re-resolved on every run. Use the `qa/logs` tree
from the top of this file.

| # | Check | L | W |
|---|-------|---|---|
| 9.1 | `Add pattern…` opens a dialog with a read-only **Folder** row and its `Choose…` button, a **Pattern** box, and the inline help | ☐ | ☐ |
| 9.2 | Its **Add** button is greyed out until both halves are given, and a rooted pattern (`/x`, `C:\x`) is refused with a message | ☐ | ☐ |
| 9.3 | **Enter** adds and **Escape** cancels; the pattern box has the keyboard focus the moment the dialog opens | ☐ | ☐ |
| 9.4 | The added row reads `folder ▸ pattern`, with the pair spelled out in its tooltip | ☐ | ☐ |
| 9.5 | `qa/logs` + `*.log` backs up `app.log` and **not** `keep.txt`, `DirPrefixOne/` or `other/` — `*` stays inside one folder level | ☐ | ☐ |
| 9.6 | `qa/logs` + `**/*.txt` reaches `DirPrefixOne/inner.txt` and `other/x.txt` — `**` goes to any depth | ☐ | ☐ |
| 9.7 | `qa/logs` + `DirPrefix*` takes `DirPrefixOne/` **with everything in it** | ☐ | ☐ |
| 9.8 | An exclude glob still wins over a pattern match | ☐ | ☐ |
| 9.9 | A pattern matching nothing does **not** fail the run: the outcome is amber and carries a warning naming the folder and the pattern | ☐ | ☐ |
| 9.10 | A pattern whose base folder has been deleted routes through the same **Skip / Retry / Abort** dialog as a missing path, with a message naming the pattern | ☐ | ☐ |
| 9.11 | Files added to the base folder **after** the profile was saved are in the next backup — the rule is re-resolved, not frozen | ☐ | ☐ |
| 9.12 | A profile carrying a pattern is saved as `"formatVersion": 2`; one with no pattern and no Zip is still `1` (read the `.json`) | ☐ | ☐ |

## 10. Zip archives

New in 1.2.0. Zip is a third **Compression** choice, not a change to the container: it writes a `.zip`
plus the usual `.sha256`, and it can never be encrypted.

| # | Check | L | W |
|---|-------|---|---|
| 10.1 | Selecting `Zip (.zip)` switches **Encrypt with a password** off and greys it out, with the card explaining why | ☐ | ☐ |
| 10.2 | Switching back to Zstandard or Gzip re-enables the toggle, left **off** rather than silently re-armed | ☐ | ☐ |
| 10.3 | A zip backup produces `<Profile>_yyyy-MM-dd_HHmmss.zip` **and** its `.sha256` | ☐ | ☐ |
| 10.4 | The zip opens in the platform's stock tool — double-click in Explorer, `unzip -t` on Linux — with the whole tree inside, folders included | ☐ | ☐ |
| 10.5 | Extracted file timestamps match the source to within two seconds (a DOS date's resolution) and are not shifted by a time zone | ☐ | ☐ |
| 10.6 | `Verify…` on the zip succeeds; editing its `.sha256` makes it fail | ☐ | ☐ |
| 10.7 | `Restore…` reproduces the tree from the zip, and a second restore over the same destination raises Skip / Overwrite / Abort | ☐ | ☐ |
| 10.8 | The empty directory survives the zip round trip | ☐ | ☐ |
| 10.9 | Linux only: a restored executable has **lost** its executable bit — zip carries no Unix modes, and this is the documented cost | ☐ | — |
| 10.10 | A profile hand-edited to `"compression": "Zip"` **and** `"encrypt": true` fails the run with a message naming the problem, and writes no archive | ☐ | ☐ |
| 10.11 | A profile using Zip is saved as `"formatVersion": 2` | ☐ | ☐ |

## 11. Windows installer

New in 1.4.0, Windows only, and only when the release ships `Enigma.Backup.X.Y.Z.msi`. Install it on a
machine that has the **.NET 10 runtime** (the MSI is the framework-dependent publish, unlike the zip).

| # | Check | L | W |
|---|-------|---|---|
| 11.1 | The MSI installs after the UAC prompt, into `Program Files (x86)\Enigma Backup` on 64-bit Windows | — | ☐ |
| 11.2 | An **Enigma Backup** shortcut with the app icon is on the desktop and in the Start menu, and each starts the app | — | ☐ |
| 11.3 | *Apps & features* lists **Enigma Backup**, version `X.Y.Z`, publisher `Josué Clément`, with the app icon | — | ☐ |
| 11.4 | Installing a later MSI over an earlier one replaces it: *Apps & features* lists one **Enigma Backup**, at the new version | — | ☐ |
| 11.5 | Uninstalling removes the folder and both shortcuts, and leaves `%APPDATA%\Enigma.Backup` (profiles and preferences) as it was | — | ☐ |

---

## What was verified for 1.6.0

**Against the Windows x64 zip** (`Enigma.Backup-1.6.0-win-x64.zip`, built here with `./publish.ps1 -Rid
win-x64` and extracted to a scratch folder), and against an MSI built with `buildmsi.bat` in a scratch
clone of the release branch. The app was driven through **UI Automation**, and nothing was clicked that
writes. The workstation was locked during the release check, so the zip's window was read but not
seen, and the MSI was not installed.

| # | Check | Result |
|---|-------|--------|
| 1.1 | The zip extracts to exactly two files, the executable and `app.ico` | Pass — `Enigma.Backup.Desktop.exe` (54,605,373 bytes) and `app.ico` (11,382 bytes, identical to the asset) |
| 1.3 | It launches | Pass, **on a machine that has .NET installed**. A runtime-free machine was not available, so "self-contained" is still the maintainer's check |
| 1.4 | The window's title is `Enigma Backup` | Pass — read through UIA |
| 3.14 | Every page sits in a centred column on a wide window | Pass **by eye on a Debug build of the same markup** (FEATURE-464A): every page at 1120 px, maximised and at the 820 px minimum |
| 8.13 | About shows `Enigma Backup`, `Version 1.6.0`, a `Build` line and the copyright | Pass **by automation**: the Settings page's *About Enigma Backup* invoked, and the dialog's texts read as `Enigma Backup`, `Version`, `1.6.0`, `Build`, `d5e03bd`, `Copyright © 2026 Josué Clément`. Not seen by eye |
| 11.x | The MSI's identity | Pass **by its tables**, not installed — `Enigma.Backup.1.6.0.msi` (70.6 MB): `ProductName` Enigma Backup, `ProductVersion` 1.6.0, `UpgradeCode` `{D60F30D6-…-F98EB9731A91}` unchanged |

**What was *not* exercised.**
- The **Linux x64 zip** (`docs/RELEASE.md` requires it to be built on Linux, and this host is Windows),
  and with it every **L** column.
- §11.1–11.5: installing, upgrading 1.5.0 → 1.6.0 and uninstalling the MSI are the maintainer's.
- 8.13 by eye, in either variant.
- **§§2–10 by hand.** 1.6.0 moves almost every control (the rail, the pages, the run dialog), so this
  release needs the whole checklist run against both zips before it ships. The automated suite (590
  tests, including the end-to-end window test that drives the real pipeline) is green, but it does not
  press buttons.

---

## What was verified for 1.5.0

**Against the Windows x64 zip** (`Enigma.Backup-1.5.0-win-x64.zip`, built here with `./publish.ps1 -Rid
win-x64` and extracted to a scratch folder), and against an MSI built with `buildmsi.bat` in a scratch
clone of the release branch. The app was driven through **UI Automation** and screenshotted; nothing was
clicked that writes, and the MSI was not installed.

| # | Check | Result |
|---|-------|--------|
| 1.1 | The zip extracts to exactly two files, the executable and `app.ico` | Pass — `Enigma.Backup.Desktop.exe` (54,597,178 bytes) and `app.ico` (11,382 bytes, identical to the new asset) |
| 1.3 | It launches | Pass, **on a machine that has .NET installed**. A runtime-free machine was not available, so "self-contained" is still the maintainer's check |
| 1.4 | The window opens with the app icon and the title `Enigma.Backup` | Pass — the title read through UIA; the larger icon seen in the title bar, and as the exe's embedded icon |
| 8.13 | About shows the icon, `Enigma.Backup`, `Version 1.5.0`, a `Build` line and the copyright | Pass **by eye** (dark variant) — screenshot of the extracted exe: the larger icon, `Version 1.5.0`, `Build a017af3` |
| 11.x | The MSI's identity and icon | Pass **by its tables**, not installed — `Enigma.Backup.1.5.0.msi` (70.6 MB): `ProductName` Enigma Backup, `ProductVersion` 1.5.0, `UpgradeCode` `{D60F30D6-…-F98EB9731A91}` unchanged; its two `Icon` rows (*Apps & features* and both shortcuts) are byte for byte the new `.ico` |

**What was *not* exercised.** The **Linux x64 zip** (`docs/RELEASE.md` requires it to be built on Linux,
and this host is Windows), and with it every **L** column and the new icon in a Linux launcher; §11.1–11.5
(installing, upgrading 1.4.0 → 1.5.0 and uninstalling the MSI is the maintainer's); the light variant of
8.13. §§2–10 were not re-run: nothing in this release touches profiles, the pipeline, restore, verify,
patterns, zip, errors or settings, and the full automated suite (552 tests) is green.

---

## What was verified for 1.4.0

**Against the Windows x64 zip** (`Enigma.Backup-1.4.0-win-x64.zip`, built here with `./publish.ps1 -Rid
win-x64` and extracted to a scratch folder), and against an MSI built with `buildmsi.bat` in a scratch
clone of the release branch. The app was driven through **UI Automation** and screenshotted; nothing was
clicked that writes, and the MSI was not installed.

| # | Check | Result |
|---|-------|--------|
| 1.1 | The zip extracts to exactly two files, the executable and `app.ico` | Pass — `Enigma.Backup.Desktop.exe` (54,572,609 bytes) and `app.ico` (8,058 bytes, identical to the new asset) |
| 1.3 | It launches | Pass, **on a machine that has .NET installed**. A runtime-free machine was not available, so "self-contained" is still the maintainer's check |
| 1.4 | The window opens with the app icon and the title `Enigma.Backup` | Pass — the title read through UIA; the tile-less icon seen in the title bar of a Release build (FEATURE-F207) |
| 8.13 | About shows the icon, `Enigma.Backup`, `Version 1.4.0`, a `Build` line and the copyright | Pass **by eye** (dark variant) — screenshot of the extracted exe: the tile-less icon, `Version 1.4.0`, `Build 9f3c163` |
| 11.x | The MSI's identity | Pass **by its tables**, not installed — `Enigma.Backup.1.4.0.msi` (70.5 MB): `ProductName` Enigma Backup, `ProductVersion` 1.4.0, `UpgradeCode` `{D60F30D6-…-F98EB9731A91}`, two shortcuts on `[INSTALLDIR]\Enigma.Backup.Desktop.exe` (see `docs/done/FEATURE-A834-PHASE02.md`) |

**What was *not* exercised.** The **Linux x64 zip** (`docs/RELEASE.md` requires it to be built on Linux,
and this host is Windows), and with it every **L** column and the new icon in a Linux launcher; §11.1–11.5
(installing, upgrading and uninstalling the MSI is the maintainer's); the light variant of 8.13. §§2–10
were not re-run: nothing in this release touches profiles, the pipeline, restore, verify, patterns, zip,
errors or settings, and the full automated suite (552 tests) is green.

---

## What was verified for 1.3.0

**Against the Windows x64 zip** (`Enigma.Backup-1.3.0-win-x64.zip`, built here with `./publish.ps1 -Rid
win-x64` and extracted to a scratch folder). The workstation session was locked for most of the run, so
the checks below were driven and read through **UI Automation** rather than by eye, against the user's
real (unmodified) profiles folder; nothing was clicked that writes.

| # | Check | Result |
|---|-------|--------|
| 1.1 | The zip extracts to exactly two files, the executable and `app.ico` | Pass — `Enigma.Backup.Desktop.exe` (54,609,469 bytes) and `app.ico` (15,514 bytes, identical to the asset) |
| 1.3 | It launches | Pass, **on a machine that has .NET installed** — the extracted exe starts and shows the main window. A runtime-free machine was not available, so "self-contained" is still the maintainer's check |
| 1.4 | The window carries the title `Enigma.Backup` | Pass — read through UIA; the new icon was seen in the title bar of a Release build earlier in the run (FEATURE-9755) |
| 3.10 | ⓘ sits right of the gear | Pass **by automation** — both buttons found by name (`Settings`, `About Enigma.Backup`) |
| 8.13 | About shows `Enigma.Backup`, `Version 1.3.0`, a `Build` line and `Close` | Pass **by automation** — every one found by name in the open dialog; the exe's product version is `1.3.0+<sha>`. Seen by eye on a Release build of the same markup earlier in the run (FEATURE-4573), both variants |
| 8.15 | Settings holds Theme and Storage only | Pass **by automation** — `Open folder` present, no project-page link |
| 7.9–7.12 | The safety net and the startup window | Pass **by automation, on throwing builds that were not shipped** — see `docs/done/FEATURE-AD48-PHASE0{1,2,3}.md` |

**What was *not* exercised.** The **Linux x64 zip** — `docs/RELEASE.md` requires it to be built on Linux,
and this host is Windows — and with it every **L** column, `packaging/linux/install.sh` on a real desktop
(its round trip ran in Git Bash against a scratch XDG tree, see `docs/done/FEATURE-61C4.md`), and the
visual half of the new dialogs (7.9–7.12 and 8.13 by eye, in both variants). §§2–6, 9 and 10 were not
re-run: nothing in this release touches profiles, the pipeline, restore, verify, patterns or zip, and
the full automated suite (549 tests) is green.

---

## What was verified for 1.2.0

**The artifacts do not exist yet.** As in 1.1.0, release preparation stopped short of `./publish.sh`:
the zips, the tag and the GitHub release are the maintainer's steps, so **nothing in §1 has been
ticked** and what follows was exercised against the Release *build* (`bin/Release/net10.0/`), not an
extracted zip. Run this whole checklist against the zips once they are built.

**Linux x64 — against the 1.2.0 Release build** (Wayland + XWayland, `DISPLAY=:1`, launched with
`XDG_CONFIG_HOME` redirected to a scratch folder so no real profile or preference was touched):

| # | Check | Result |
|---|-------|--------|
| 1.4 | The window opens with the app icon and the title `Enigma.Backup` | Pass — screenshotted |
| 3.9 | `Add pattern…` sits beside `Add files…` and `Add folder…` | Pass — the five-button row renders wrapped as designed |
| 3.10 | The top bar reads `Profile │ combo │ status │ Restore… │ Verify… │ ▏ │ ⚙`, bottom bar **Back up** alone | Pass — at rest, at the default width |
| 3.5 | **Back up** disabled with no inputs and no output folder | Pass, at rest — greyed out on an empty editor while `Restore…` and `Verify…` stay enabled |
| 8.13 | About shows `Version 1.2.0` | Pass **by mechanism, not by eye** — the Release assembly's `AssemblyInformationalVersion` is `1.2.0+<git sha>`, and `AppInfo` strips at the `+` (pinned by `AppInfoTests`). The dialog itself was not reopened in this phase; PHASE04 photographed it reading the then-current version |

**What was *not* exercised.** All of §1 (no zips), and everything requiring a button press on either
platform — §§2, 4–7, 9 and 10 in full, and the interactive half of §3 and §8. The cause is unchanged
from every release before it: no input-automation tool (`xdotool`, `ydotool`, `wmctrl`, `kdotool`) is
installed on the build machine and the session is Wayland, so release QA cannot click anything.

**Windows x64 — not exercised.** The build machine is Linux only.

**Specific to this release.** Unlike 1.1.0, 1.2.0 changes the *engine*: pattern resolution and a third
archive format. That risk sits where a test can reach it, and the 489-test suite does — in particular
`ZipFormatTests` (round trip, sidecar, an entry-less zip, restore conflicts, crafted traversal entry
names, the zip+encrypt refusal, both zip signatures), `PatternInputScanTests` (26 cases over real
trees) and `GoldenArchiveCompatibilityTests` (a real 1.0.0 encrypted archive, verified and restored
byte-for-byte). Each feature was also driven for real during the phase that built it, and those runs
are recorded in `docs/done/FEATURE-7F99-PHASE01..04.md`: a live backup with three patterns whose
archive listing was checked entry by entry, a zip proved against Info-ZIP's `unzip -t` / `-l` and
`diff -r`, four screenshots of the relocated buttons at both window widths, and **Open folder** and the
project link driven through the real `TopLevel.Launcher` into Dolphin and Firefox. What none of that
covers is the click itself, on the shipped artifact — which is what §§9 and 10 are for.

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
