# FEATURE-3A2D — Enigma.Core 1.1.0 upgrade & 1.0.1 release

**Status:** DONE
**Type:** single-phase feature (routine release, per the `dotnet-release` skill)
**Branch (created by `/build`, from `HEAD`):** `feature/feature-3a2d-enigma-core-1-1-release`

## Objective

Move the solution from **Enigma.Core 1.0.0 to 1.1.0**, prove that nothing breaks — above all that
encrypted archives written by the previous build stay readable — and cut the product release
**1.0.1**.

The upgrade itself is one line. The work is the proof: Enigma.Core 1.1.0 changes the BouncyCastle
version sitting directly underneath Argon2id and AES-256-GCM, which are the two primitives every
`.enc` archive depends on. A silent change in either would not fail a build; it would make people's
existing backups unopenable. This item ships the bump *and* a permanent regression guard against
that class of failure, then releases it.

## Validated decisions (interview 2026-08-01)

- **Version: `1.0.1`, a patch** — not the `1.1.0` of the original request. There is no API change,
  no behaviour change and no format change; strict SemVer makes it a patch. (The upstream library
  numbered the same change `1.1.0` for itself; that precedent was considered and set aside.)
- **Compatibility proof: a golden fixture archive**, produced by 1.0.0 code and checked in, over a
  known-answer vector or a one-off manual check. The contract in the README is *"archives written by
  1.0.0 stay readable"*; the fixture tests that contract end to end, and it guards every future
  Enigma.Core / BouncyCastle bump rather than only this one.
- **The fixture is a real backup output** — the actual `BackupService` pipeline over a throwaway
  tree, encryption on — not a bare `EBK1` container over a synthetic payload. It therefore exercises
  the header, the Argon2id key, every GCM tag, the zstd frame, the tar entries and the sidecar in
  one artifact, and can be driven through both `VerificationService` and `RestoreService`.
- **The fixture uses production Argon2id parameters** (64 MiB / 3 iterations / parallelism 4), not
  the cheap parameters `TestSupport/Ebk1Writer` uses for synthetic containers. It costs the suite a
  fraction of a second and it is the only parameter set real archives actually have.
- **Release scope:** `/build` goes as far as building the zips and re-running the automatable Linux
  QA subset, then **prints** the merge/tag/GitHub-release runbook. Nothing is committed, tagged or
  pushed by the agent.
- **Throughput is re-measured** against the `~71 MiB/s` GCM / `~34 MiB/s` end-to-end baseline
  recorded in `docs/plan/FEATURE-3E11.md`, and the number recorded in the completion doc. A
  BouncyCastle rewrite could regress it by a factor the test suite would never notice.
- **Tag `1.0.1` only.** The repository has no tags, locally or on `origin`: 1.0.0 was built but never
  published, so a retroactive `1.0.0` tag would advertise a release nobody could download. Bare
  `X.Y.Z`, per `docs/RELEASE.md`.
- **No MSI profile.** The `dotnet-release` skill's app-only offer was declined again, as in PHASE06 —
  "no installer" is a documented product decision and reversing it inside a patch release would make
  the installer the largest thing in the release.
- **`docs/RELEASE.md` step 4 is corrected** to the repository's real **feature → develop → main**
  flow (it currently says the release branch merges straight into `main`).
- **`SECURITY.md` needs no change** — its supported-versions table already reads `1.0.x`, which
  covers 1.0.1. Verified, not assumed.
- **Single phase.** `dotnet-release` defines a routine release as one phase covering version, notes,
  callout, dependency refresh and runbook; the dependency refresh here is that one line.

### Findings established before planning (evidence for "nothing breaks")

- **The public API is identical.** A metadata-level diff of the two `net10.0` assemblies (all
  exported types plus public/protected members — 112 types) differs only in the `1.0.0.0 → 1.1.0.0`
  assembly-version stamp embedded in type names. No member added, removed or re-signatured.
- **1.1.0 is a dependency-only release.** Its nuspec bumps `BouncyCastle.Cryptography` **2.6.2 →
  2.7.0** and changes nothing else; the TFM set stays `netstandard2.0;net8.0;net10.0`. Upstream
  states the public API and its behaviour are unchanged.
- **Two call sites only** — `src/Enigma.Backup.Core/Encryption/Ebk1Cipher.cs` and
  `tests/Enigma.Backup.Core.Tests/TestSupport/Ebk1Writer.cs`, using
  `Argon2ServiceFactory().CreateArgon2Service().DeriveKey(…)` and
  `BlockCipherServiceFactory().CreateAesService(…)` with `EncryptAsync` / `DecryptAsync`. Both
  signatures are unchanged in 1.1.0.
- **Nothing else in the solution is outdated.** `dotnet list package --outdated` names Enigma.Core
  alone; the Avalonia 12.1.1 set, ZstdSharp.Port 0.8.8, the Microsoft.Extensions 10.0.10 packages
  and xunit.v3 3.2.2 are all current. The coupled Avalonia set therefore needs no decision.
- **No known vulnerabilities** — `dotnet list package --vulnerable --include-transitive` is clean
  across all four projects.
- **BouncyCastle is purely transitive.** It has no `PackageVersion` entry and transitive pinning is
  off, so the bump reaches it through Enigma.Core with nothing to edit.

So the compile risk is effectively nil, and the whole residual risk is behavioural — which is what
step 2 below exists to close.

### Assumptions accepted at validation

- The 1.0.0 section of `RELEASENOTES.md` is history and is **not** rewritten; it keeps naming
  Enigma.Core 1.0.0.
- `docs/plan/FEATURE-3E11.md`'s version references stay as written — a completed item's plan file is
  a record of what was planned, and the `dev-workflow` freshness sweep excludes `docs/plan/`.
- **One fixture**, zstd + encrypted. Gzip is unaffected: compression sits *under* the cipher, and no
  unencrypted path touches Enigma.Core at all.
- The fixture password is a throwaway constant in test code, commented as such.
- `docs/QA-CHECKLIST.md` keeps its 1.0.0 record and gains a 1.0.1 one above it.
- **No explicit `BouncyCastle.Cryptography` entry** is added to `Directory.Packages.props`: no
  project references it, and a `PackageVersion` with no matching `PackageReference` is inert.
- No lock file, no new NuGet source.
- The fixture is copied to the test output through a `Content` item mirroring the existing
  `xunit.runner.json` entry in `Enigma.Backup.Core.Tests.csproj`.

## Scope

**In scope:** the package bump, the compatibility regression test and its fixture, the throughput
measurement, the version bump, the release documentation, the release zips, the automatable Linux QA
pass, and the printed runbook.

**Out of scope:** any production code change (none is expected — if the build demands one, that is a
finding to report, not a silent edit); the manual Windows and Linux click-through QA; the MSI
profile; retroactively releasing 1.0.0; refreshing any other dependency (none is outdated).

## Steps

**Ordering matters at step 2:** the fixture must be generated by 1.0.0 code, so it is produced
*before* the package bump and is a genuine artifact of the previous version rather than a
same-version round-trip that proves nothing.

1. **Baseline.** On the new branch, with `Enigma.Core 1.0.0` still restored, confirm the starting
   point is green: `dotnet build Enigma.Backup.slnx` (Debug and `-c Release`) and
   `dotnet test --solution Enigma.Backup.slnx`.
2. **Generate the golden fixture, still on 1.0.0.** Drive the real `BackupService` over a small
   throwaway tree (a text file, a binary blob, an empty directory), Zstandard, **encryption on**, at
   the production Argon2id parameters. Keep the produced `*.tar.zst.enc` and its `.sha256`, and the
   source tree's expected content, under `tests/Enigma.Backup.Core.Tests/Fixtures/`. Record beside
   them — a short `README.md` in that folder — the exact recipe, the throwaway password, the
   Enigma.Core version that wrote it and the date, so a future reader can tell what the bytes are and
   rebuild an equivalent.
3. **Write the compatibility tests** (still on 1.0.0, so they must pass *before* the bump):
   `VerificationService` succeeds against the fixture and its sidecar, authenticating every GCM tag;
   `RestoreService` reproduces the recorded tree byte-for-byte. Add the `Content` item that copies
   `Fixtures/**` to the test output.
4. **Bump the package** — `Directory.Packages.props`, `Enigma.Core` `1.0.0` → `1.1.0`. Restore and
   confirm `BouncyCastle.Cryptography 2.7.0` resolves.
5. **Re-verify.** Debug and Release builds clean with zero warnings; the full suite green, the two
   new tests included; `dotnet list package --vulnerable --include-transitive` clean.
6. **Measure throughput.** One timed AES-GCM encrypt over a fixed buffer, compared against the
   recorded `~71 MiB/s` baseline; note the result for the completion doc. A large regression is a
   finding to raise, not something to absorb silently.
7. **Bump the version** — `Directory.Build.props`, `<Version>` `1.0.0` → `1.0.1`.
8. **Release documentation:**
   - `RELEASENOTES.md` — prepend an `Enigma.Backup v1.0.1 Release Notes` section with
     *Dependencies* (`Enigma.Core 1.0.0 → 1.1.0`, and transitively `BouncyCastle.Cryptography
     2.6.2 → 2.7.0`; every other package current at release time), *Compatibility* (the `EBK1`
     version byte is unchanged and 1.0.0 archives stay readable, with the new fixture test as the
     evidence) and *Version*. Leave the 1.0.0 section untouched.
   - `README.md` — replace the what's-new callout with a 1.0.1 one, and update the Installing
     table's zip filenames to `Enigma.Backup-1.0.1-win-x64.zip` / `-linux-x64.zip`.
   - `docs/RELEASE.md` — correct step 4 to feature → develop → main, tagging on `main`.
   - `SECURITY.md` — confirm `1.0.x` still covers the release; no edit expected.
9. **Build the artifacts** — `./publish.sh` for both runtime identifiers. Confirm each staging
   directory holds exactly one file, both zip names carry `1.0.1`, and the linux-x64 zip was built
   from Linux.
10. **Run the automatable Linux QA subset** against the *extracted* linux-x64 zip: §1.1 (one file),
    §1.2 (executable bit), §1.4 (window opens with icon and title — launch, screenshot, then kill by
    PID, never a `pkill -f` pattern that would match the launching shell), §1.6 (`kill -TERM`
    terminates it), plus the profile-folder checks that were automatable for 1.0.0 with
    `XDG_CONFIG_HOME` pointed at a scratch directory. Add a *What was verified for 1.0.1* section
    above the 1.0.0 one in `docs/QA-CHECKLIST.md`, stating plainly what was **not** exercised.
11. **Close out** — roadmap and this file to `DONE`, write `docs/done/FEATURE-3A2D.md` (including the
    measured throughput and the QA record), run the documentation freshness sweep, then **print** the
    runbook: Release-config pre-flight, merge feature → develop → main, `git tag 1.0.1` and push,
    GitHub release against the tag with both zips attached and the 1.0.1 notes as the body.

## Acceptance criteria

1. `Directory.Packages.props` pins `Enigma.Core 1.1.0`, and restore resolves
   `BouncyCastle.Cryptography 2.7.0`.
2. `dotnet build Enigma.Backup.slnx` **and** `dotnet build Enigma.Backup.slnx -c Release` both
   succeed with zero warnings.
3. `dotnet test --solution Enigma.Backup.slnx` is fully green, including the new tests.
4. **The golden fixture — written by 1.0.0 code — verifies and restores byte-for-byte under 1.1.0**,
   and the same tests were green before the bump.
5. `dotnet list package --vulnerable --include-transitive` reports nothing.
6. Measured GCM throughput is in the same band as the recorded baseline, and the figure is in
   `docs/done/FEATURE-3A2D.md`.
7. `<Version>` is `1.0.1`; both zips are named `Enigma.Backup-1.0.1-<rid>.zip`; each staging
   directory holds exactly one file; the extracted Linux binary carries its executable bit.
8. The automatable Linux QA subset passes and is recorded in `docs/QA-CHECKLIST.md`, with its gaps
   stated.
9. `RELEASENOTES.md`, `README.md` and `docs/RELEASE.md` are updated as specified, and `SECURITY.md`
   is verified as needing no change.
10. Roadmap and plan statuses updated, `docs/done/FEATURE-3A2D.md` written, and the runbook printed —
    with nothing committed, tagged or pushed by the agent.

## Impacted files

**Modified**

- `Directory.Packages.props` — the `Enigma.Core` version.
- `Directory.Build.props` — `<Version>`.
- `tests/Enigma.Backup.Core.Tests/Enigma.Backup.Core.Tests.csproj` — a `Content` item copying
  `Fixtures/**` to the output.
- `README.md`, `RELEASENOTES.md`, `docs/RELEASE.md`, `docs/QA-CHECKLIST.md`.
- `docs/roadmap.md`, this file.

**Created**

- `tests/Enigma.Backup.Core.Tests/Fixtures/` — the golden archive, its `.sha256`, and the `README.md`
  recording how they were produced.
- One test file under `tests/Enigma.Backup.Core.Tests/Encryption/` plus, if it earns its place, a
  small `TestSupport` loader for the fixture.
- `docs/done/FEATURE-3A2D.md`.

**Expected untouched:** all of `src/`. No production code change is planned; if one turns out to be
required, it is a finding to raise before making it.

## Risks

- **A behavioural change in BouncyCastle 2.7.0's Argon2id or AES-GCM.** Low — upstream states
  encodings and behaviour are unchanged — but it is the failure that matters, and step 2 is the
  entire mitigation. If the fixture test fails after the bump, the release does not ship: the finding
  is reported and the bump is reverted pending an upstream answer.
- **A throughput regression** invisible to a pass/fail suite. Mitigated by step 6.
- **The fixture becoming a mystery blob.** Mitigated by the recipe file beside it; without that, a
  future maintainer facing a red test has no way to tell a real regression from a stale artifact.
