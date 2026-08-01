# FEATURE-3A2D — Enigma.Core 1.1.0 upgrade & 1.0.1 release

**Status:** DONE · single-phase · branch `feature/feature-3a2d-enigma-core-1-1-release`

## Summary

Moved the solution from **Enigma.Core 1.0.0 to 1.1.0** — and, transitively,
**BouncyCastle.Cryptography 2.6.2 → 2.7.0**, the code that actually performs Argon2id and
AES-256-GCM — then cut the product release **1.0.1**.

The bump is one line. The work was proving it safe. Every existing encryption test round-trips inside
a single build, which cannot detect a change in the primitives: the same code seals and opens the
bytes, so it agrees with itself whatever it does. A silent change in either primitive would pass the
whole suite and make people's existing backups unopenable. So the release ships a permanent guard
against that class of failure:

**A golden fixture.** A real encrypted archive, produced by the 1.0.0 build through the actual
`BackupService` pipeline at the shipped Argon2id parameters, is checked in and read by two tests that
verify it against its sidecar and restore it byte-for-byte. It was generated *before* the package
bump, so it is a genuine artifact of the previous version rather than a same-version round trip that
proves nothing — and it guards every future Enigma.Core / BouncyCastle bump, not only this one.

Ordering, and what it established:

1. Baseline on 1.0.0: Debug and Release clean, 352 tests green.
2. Fixture generated on 1.0.0 (BouncyCastle 2.6.2), by a throwaway generator that was deleted once it
   had run — the recipe lives in `Fixtures/README.md` instead.
3. Compatibility tests written and green **on 1.0.0** (354 tests).
4. Package bumped; restore resolved BouncyCastle 2.7.0.
5. Re-verified: **the 1.0.0 archive verifies and restores byte-for-byte under 1.1.0.**

The guard was also checked for non-vacuity: flipping one byte of the fixture's *output* copy fails
both tests (GCM tag rejection out of `BlockCipherService.DecryptAsync`), so a green result means the
bytes were genuinely opened, not skipped.

No production code changed — `src/` is untouched, as the plan expected.

## Files/modified

**Created**

- `tests/Enigma.Backup.Core.Tests/Fixtures/Golden_2026-08-01_120000.tar.zst.enc` — the golden archive
  (727 bytes), written by Enigma.Core 1.0.0.
- `tests/Enigma.Backup.Core.Tests/Fixtures/Golden_2026-08-01_120000.tar.zst.enc.sha256` — its sidecar,
  as the run wrote it.
- `tests/Enigma.Backup.Core.Tests/Fixtures/source-tree.txt` — the archived tree as
  `<path>\t(dir | file <length> <sha256>)` lines; what a restore must reproduce.
- `tests/Enigma.Backup.Core.Tests/Fixtures/README.md` — the recipe: writer version, date, Argon2id
  parameters, frozen clock, tree contents, the throwaway password, and an explicit instruction **not**
  to regenerate the files to make a failing test pass.
- `tests/Enigma.Backup.Core.Tests/TestSupport/GoldenFixture.cs` — resolves the fixture beside the test
  assembly and parses the tree manifest into the shape `FileTree.Snapshot` produces.
- `tests/Enigma.Backup.Core.Tests/Encryption/GoldenArchiveCompatibilityTests.cs` — the two tests.
- `docs/done/FEATURE-3A2D.md` — this file.

**Modified**

- `Directory.Packages.props` — `Enigma.Core` 1.0.0 → 1.1.0.
- `Directory.Build.props` — `<Version>` 1.0.0 → 1.0.1.
- `tests/Enigma.Backup.Core.Tests/Enigma.Backup.Core.Tests.csproj` — `Content` item copying
  `Fixtures/**` to the output.
- `RELEASENOTES.md` — a 1.0.1 section (Dependencies / Compatibility / Version) above the untouched
  1.0.0 one.
- `README.md` — 1.0.1 what's-new callout; Installing table zip names.
- `docs/RELEASE.md` — step 4 corrected to the real **feature → develop → main** flow (it said the
  release branch merges straight into `main`); step 5 reworded now that the no-tags fact is about
  1.0.1; the `X.Y.Z` example changed from `1.1.0` to `1.0.1` so it cannot be read as the dependency's
  version.
- `docs/QA-CHECKLIST.md` — a *What was verified for 1.0.1* section above the 1.0.0 one.
- `docs/roadmap.md`, `docs/plan/FEATURE-3A2D.md` — status.

## Throughput

Measured directly, as the plan asked, against the `~71 MiB/s` GCM figure recorded in
`docs/plan/FEATURE-3E11.md` / `docs/done/FEATURE-3E11-PHASE04.md`. A throwaway probe encrypted a fixed
32 MiB buffer through `BlockCipherService` with GCM and a 128-bit tag, three timed runs per
configuration, on this machine.

| Configuration | Single 32 MiB stream | 1 MiB chunks with AAD (what `Ebk1Cipher` does) |
|---|---|---|
| **Enigma.Core 1.1.0 / BouncyCastle 2.7.0** | 248.6 · 251.7 MiB/s | 255.8 · 259.2 · 254.3 MiB/s |
| **Enigma.Core 1.0.0 / BouncyCastle 2.6.2** | 249.8 · 248.3 MiB/s | 255.8 · 256.7 · 248.0 MiB/s |

**No regression — the two versions are indistinguishable**, within run-to-run noise on both access
patterns. That controlled A/B is the answer to the question the plan posed (could a BouncyCastle
rewrite regress throughput invisibly?): the answer is no, because the same probe on the same machine
gives the same numbers before and after. The probe was deleted after measuring.

Today's absolute number is ~3.5× the recorded `~71 MiB/s` baseline, and that gap is **not** caused by
this change — 1.0.0 measures just as fast. Three hypotheses were tested and eliminated: build
configuration (Debug matches Release), the cipher's internal buffer (4 KiB, the package default,
matches the 64 KiB `Ebk1Cipher` uses), and the access pattern (streamed matches chunked). The
remaining likely explanation is that PHASE04's pre-build probe measured something differently shaped,
or measured under different machine conditions; it was not pursued further, since every version of
the measurement agrees that the bump costs nothing. Anyone re-deriving the end-to-end figure should
re-measure rather than trust the `~34 MiB/s` in the FEATURE-3E11 plan.

## Build/test evidence

- `dotnet build Enigma.Backup.slnx` — **succeeded, 0 warnings, 0 errors.**
- `dotnet build Enigma.Backup.slnx -c Release` — **succeeded, 0 warnings, 0 errors.**
- `dotnet test --solution Enigma.Backup.slnx` — **354 passed, 0 failed, 0 skipped** (352 before this
  item; +2 compatibility tests). Green both before the bump (on 1.0.0) and after it.
- `dotnet list package --vulnerable --include-transitive` — clean across all four projects.
- Resolved versions confirmed in `project.assets.json`: `Enigma.Core/1.1.0`,
  `BouncyCastle.Cryptography/2.7.0`.
- `./publish.sh` — both zips built from Linux:

  | Artifact | Size | SHA-256 |
  |---|---|---|
  | `Enigma.Backup-1.0.1-linux-x64.zip` | 45.4 MB | `db4495ea39709d27a8b52fd2cd52e8118337ce394df35b5407113037332635a3` |
  | `Enigma.Backup-1.0.1-win-x64.zip` | 47.6 MB | `90f4b63b44367f66f054149518e46d410ef0a3a04bbfb9c456262d660b120488` |

  Each staging directory holds exactly one file, so the publish-time native-`.pdb` filter still works.
- **Linux QA subset**, against the extracted `linux-x64` zip: §1.1 (one file), §1.2 (`-rwxr-xr-x`),
  §1.4 (window opens with icon and title; screenshotted), §1.6 (`kill -TERM` exits within a second),
  §2.3–2.4 (a profile under a scratch `XDG_CONFIG_HOME` loads fully populated at startup), and the
  at-rest half of §3.5 (**Back up** greyed out with no inputs and no output folder). Recorded in
  `docs/QA-CHECKLIST.md` with the gaps stated: everything requiring a click, on both platforms, plus
  §1.3 and §1.5, and Windows entirely.
- `SECURITY.md` — **verified, not assumed**: its supported-versions table reads `1.0.x`, which covers
  1.0.1. No edit.

## Acceptance criteria

All ten met. Criterion 4 — the 1.0.0-written fixture verifying and restoring under 1.1.0, green
before the bump as well — is the one this item existed for.

## Deviations & follow-ups

- **The version is `1.0.1`, not the `1.1.0` of the original request.** Decided at interview and
  implemented as planned: no API, behaviour or format change makes it a patch under SemVer, whatever
  the upstream package numbered the same change for itself.
- **The fixture generator was not kept.** It was a temporary xUnit fact, run once against the 1.0.0
  package and deleted; keeping it would invite someone to re-run it and overwrite the evidence with a
  same-version round trip. `Fixtures/README.md` carries the recipe instead, along with an explicit
  warning against regenerating.
- **The archived tree is stored as a manifest, not as checked-in source files.** `source-tree.txt`
  records a SHA-256 per file, so the comparison is still byte-for-byte, while the repository gains
  one 177-byte text file rather than a directory of blobs.
- **One fixture, zstd + encrypted**, as planned. Gzip needs none: compression sits under the cipher,
  and no unencrypted path touches Enigma.Core at all.
- **`docs/RELEASE.md` grew a second correction beyond the planned step 4.** Step 5's tag rationale
  said "the repository had no tags before 1.0.0", which reads as though 1.0.0 was released; it is now
  stated as what it is — 1.0.0 was built but never published, and 1.0.1 is the first tag.
- **`artifacts/` holds the 1.0.0 zips as well as the new ones.** The directory is gitignored and the
  older pair was left alone; deleting build output that predates this item is the user's call.
- **Line endings:** nothing to report — no CRLF churn was observed in the touched files.
- **Follow-up: the throughput figures in `docs/plan/FEATURE-3E11.md` are stale as measurements** (the
  `~71 MiB/s` GCM and `~34 MiB/s` end-to-end numbers). They were left as written, since a completed
  item's plan file is a record of what was planned; the current numbers are in this document.
- **Nothing was committed, tagged or pushed.** The runbook below is printed for the user to run.
