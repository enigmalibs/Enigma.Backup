# FEATURE-3E11-PHASE04 — Core: encryption container (Enigma.Core)

**Completed:** 2026-08-01
**Branch:** `feature/feature-3e11-phase04-encryption`

## Summary

The pipeline can now encrypt. PHASE03 built `scan → tar → compress → SHA-256 → file`; this phase
slots the `EBK1` container between the compressor and the digest, and teaches restore and verify to
read it back:

```
backup   scan → tar → compress → EBK1 (AES-256-GCM) → SHA-256 → file  +  <archive>.sha256
restore  file → EBK1 → detect zstd|gzip → decompress → untar → destination
verify   file → SHA-256, and (with a password) every GCM tag — in one read of the disk
```

- **The container.** A plaintext header records the Argon2id cost parameters, the salt, the chunk
  size and a random nonce prefix; the payload follows as independently sealed chunks of
  `plainLen ‖ ciphertext ‖ tag`, closed by a `plainLen = 0` end marker. Each chunk's nonce and
  associated data embed its counter, and the associated data begins with the whole header — so a
  chunk cannot be altered, reordered, dropped, or lifted into another container, and the header
  cannot be edited, without the very first affected tag failing.
- **Reading never consults local settings.** Everything a reader needs is in the file, which is why
  an archive written with any parameters, by any build, opens: `Ebk1Options` only describes what a
  *new* container is written with. Header fields are bounds-checked on the way in, so a hostile file
  cannot ask for a 4 TiB allocation before a byte is decrypted.
- **Nothing unauthenticated reaches the user's disk.** A chunk is handed to the decompressor only
  after its tag verifies, so a tampered archive fails before its contents are written anywhere.
- **Verification gained its second half.** For an encrypted archive it now also opens every chunk,
  and because the container is decrypted *through* the hashing stream, the file is still read once.
  The two halves answer different questions and the messages say which: an archive whose digest
  matches but whose chunks will not open has not been damaged — the password is wrong, and that is
  what it says.

Unencrypted archives are untouched: still plain `.tar.zst` / `.tar.gz` that `tar` and `sha256sum`
handle without this application.

## Files/modules touched

**Created — `src/Enigma.Backup.Core/Encryption/`**

- `Ebk1Options` (public) — the parameters new containers are written with, with the shipped defaults
  and the bounds a header is validated against. Registered in the composition root.
- `Ebk1Header` — the on-disk layout: create, serialise, read, validate; builds each chunk's nonce and
  associated data.
- `Ebk1Cipher` — Argon2id key derivation and per-chunk AES-256-GCM, both from `Enigma.Core`. Zeroes
  the key and its buffers on disposal.
- `Ebk1EncryptStream` / `Ebk1DecryptStream` — the chunking, as `Stream`s that slot into the pipeline.
- `EncryptedArchiveException` + `EncryptedArchiveProblem` (both public) — the container's failures.
- `Blocking` — the one place where `Stream`'s synchronous contract meets an asynchronous-only cipher.

**Created — `src/Enigma.Backup.Core/Archiving/`**

- `HashingReadStream` — the mirror of `HashingWriteStream`; what makes a verification single-pass.
- `PrefixedReadStream` — replays bytes already read from a stream that cannot be rewound.

**Modified**

- `Archiving/ArchiveFormatDetector` — gained `DetectForwardOnlyAsync`, which identifies a decrypted
  payload's compression from its own leading bytes and hands back a stream still at its start.
- `Backups/BackupService` — takes `Ebk1Options`; the encrypting layer is built between the digest and
  the compressor; an encrypting profile with no password is refused before the scan.
- `Restoring/RestoreService` — an `EBK1` file is decrypted, then its inner compression detected.
- `Verification/VerificationService` — rewritten around one read of the file; `VerificationOutcome`
  gained `PasswordRequired` and `AuthenticationFailed`, `VerificationResult` gained `IsEncrypted` and
  `PayloadAuthenticated`.
- `Hosting/ServiceCollectionExtensions` — registers `Ebk1Options`.
- `Enigma.Backup.Core.csproj` — added the `Enigma.Core` reference (version already pinned centrally).
- `docs/roadmap.md`, `docs/plan/FEATURE-3E11.md` — PHASE04 `TODO` → `IN PROGRESS` → `DONE`, plus the
  two container-format refinements folded into the `EBK1` spec and a note on the phase.

**Created — tests (`tests/Enigma.Backup.Core.Tests`)**

- `Encryption/EncryptedRoundTripTests.cs` — the acceptance criterion, for both algorithms.
- `Encryption/Ebk1ContainerTests.cs` — every way an encrypted archive can go wrong.
- `TestSupport/Ebk1File.cs` — walks and edits a container from the outside, following the spec rather
  than the code that writes it, so header claims and chunk offsets are worth asserting and one
  precise byte can be damaged.
- `TestSupport/Ebk1Writer.cs` — *writes* a container from the spec, with its own Argon2id and GCM
  calls. A container it produces and the application opens is the check that `EBK1` is a format and
  not just a shape one class agrees with itself about. Needed the `Enigma.Core` package reference in
  the test project.

**Modified — tests**

- `TestSupport/Pipeline.cs` — `Backup` takes `Ebk1Options`; `FastEncryption` supplies cheap ones.
- `Backups/BackupServiceTests.cs`, `Restoring/RestoreServiceTests.cs` — the two
  `NotSupportedException` placeholders PHASE03 left became tests of the real behaviour.
- `Hosting/ServiceCollectionExtensionsTests.cs` — `Ebk1Options` registration and its override.

## Decisions taken inside the plan's latitude

Two container-format refinements were **agreed with the user before building** (the plan permits
refining field details within version 1 before release) and are recorded in the plan's spec:

- **`chunkSize` defaults to 1 MiB, not 64 MiB.** A chunk is buffered to be sealed, so the chunk size
  is the pipeline's memory floor; 64 MiB meant ~128 MiB of buffers per run to save 20 bytes of
  framing per chunk — 0.002% of the archive at 1 MiB. It is a header field, so either value reads.
- **A `plainLen = 0` end marker closes the container.** Without it, a file cut exactly on a chunk
  boundary decrypts perfectly and stops early, and the failure only surfaces one layer up as a
  compressor complaining about damage. With it, running out of file before the marker is the
  definition of truncation, which is what makes the plan's specific "truncated" error reliable.

The rest, each enforced by a test:

- **One exception type, not a family.** The plan asks for "specific error types"; these are one
  public `EncryptedArchiveException` with an `EncryptedArchiveProblem` discriminator
  (`PasswordRequired`, `WrongPasswordOrCorrupted`, `Truncated`, `UnsupportedVersion`,
  `UnsupportedKeyDerivation`, `MalformedHeader`). They all mean "this encrypted file cannot be
  opened" to a caller and differ only in what to tell the user, so the Desktop gets one `catch` and
  a message already written for it. It matches the `VerificationOutcome` / `BackupErrorKind` shape
  the rest of the core already uses.
- **Wrong password and corruption are one outcome, because GCM cannot tell them apart.** The message
  says both — except in verification, where the digest has already proved the file is unchanged, and
  the message is allowed to say the password is simply wrong.
- **The digest still covers the encrypted bytes.** The container sits *inside* the hashing layer, so
  `sha256sum -c` still validates an encrypted archive with nobody knowing the password, and a
  verification without one is a real (partial) answer rather than a refusal.
- **An encrypted container does not record which compression it holds** — the decrypted payload's own
  first four bytes say so, the same way an unencrypted archive's do. One rule, not two.
- **A password of spaces is a password.** The write side refuses only a null or empty one.
- **The synchronous `Stream` entry points are wrappers, not a second implementation.** Measured over
  the whole suite (1 767 chunk operations, both algorithms, both directions), the pipeline only ever
  takes the asynchronous path — the compressors write and read asynchronously. Rather than ship a
  parallel synchronous implementation that nothing exercises, `Write`/`Read`/`Dispose` block on the
  asynchronous one through `Blocking`, which detaches the work from any synchronization context so
  blocking on it cannot deadlock a UI thread.
- **A flush does not cut a chunk.** A compressor that flushed often would otherwise pepper the file
  with tiny chunks, each paying its own framing and cipher setup; the container is only meaningful
  once complete.
- **The chunk counter refuses to wrap.** A repeated (key, nonce) pair is the one way to break GCM
  outright. It would take 4 PiB to get there, which is not a reason to carry on if it happens.

## Deviations & follow-ups

- **The cipher stays on Enigma.Core, with the cost measured and accepted.** Probing the package
  before building showed its BouncyCastle AES-GCM runs at **71 MiB/s** against **3 105 MiB/s** for
  the BCL's hardware-accelerated `System.Security.Cryptography.AesGcm` on this machine — a 44×
  gap that makes encryption by far the slowest stage. This was put to the user with the measurement;
  they chose the plan as written (Enigma.Core for both Argon2id and the chunk cipher). End to end, an
  encrypted backup runs at ~34 MiB/s against ~250 MiB/s unencrypted. The container bytes would be
  identical either way, so this is reversible later by changing `Ebk1Cipher` alone, with no format
  change and no effect on existing archives.
- **A restore does not prove the container is whole; a verification does.** A restore stops when the
  tar inside says it is done, so an archive missing nothing but its end marker restores — correctly,
  since every byte it read was authenticated. Reading the container to the end is verification's job,
  and both behaviours are pinned by tests. Making restore drain the remainder would close the gap for
  a few kilobytes of extra reading, but it would also change PHASE03's unencrypted restore semantics,
  so it is left as a deliberate follow-up rather than a silent change here.
- **The synchronous stream wrappers have no direct test.** They are three lines each over the
  asynchronous implementation every test covers, and nothing in the pipeline reaches them; testing
  them directly would mean opening the container's internals to the test assembly, which this
  solution deliberately does not do.
- **Restore still takes no `IBackupErrorHandler`** — unchanged from PHASE03's follow-up note.
- **No `ILogger` in the core yet**, consistent with PHASE02 and PHASE03.
- **Line endings:** the tree remains 100% LF and `.gitattributes` normalizes to LF — nothing to
  recommend, no action taken.

## Build/test evidence

- `dotnet build Enigma.Backup.slnx` — **Build succeeded, 0 Warning(s), 0 Error(s)**.
- `dotnet build Enigma.Backup.slnx -c Release` — **0 Warning(s), 0 Error(s)**.
- `dotnet test --solution Enigma.Backup.slnx` — **total: 210, failed: 0, succeeded: 210, skipped: 0**
  (up from 173 at PHASE03; 37 new tests).
- **The acceptance criterion** — encrypted backup → verify → restore, for **both Zstd and Gzip**, over
  the same awkward tree PHASE03 uses (empty files, empty directories, dotfiles, spaces and quotes,
  Unicode names, binary content, a 150-character name): output named `.tar.zst.enc` / `.tar.gz.enc`,
  verification `Succeeded` with `PayloadAuthenticated`, restored tree identical to the source.
- **Tampering and truncation** — a flipped byte in chunk 0, 1 or 2; a flipped tag; a flipped byte of
  the header's salt; two chunks swapped with every byte otherwise intact; cut mid-header, cut
  mid-chunk, cut exactly on a chunk boundary; data appended past the end marker. Every one is caught,
  with the right `EncryptedArchiveProblem`.
- **Header validation** — a version byte and a KDF id from the future each say to upgrade; an
  impossible chunk size, memory cost or salt length is refused as malformed before anything is
  allocated.
- **Key derivation** — the parameters written are read back out of the file by an independent parser
  and are what a restore actually uses; the shipped defaults (64 MiB / 3 / 4, 16-byte salt, 1 MiB
  chunks) get their own end-to-end test; two backups of the same input share no salt, no nonce prefix
  and no digest.
- **Multi-chunk** — 512 KiB of incompressible data at a 1 KiB chunk size produces 514 chunks: 512 full
  ones, a short one, and the marker, whose end is exactly the end of the file.
- **Secrecy** — a distinctive string repeated 200 times in the source appears nowhere in the archive,
  nor does the file name.
- **End to end by hand**, with the shipped defaults, resolved through `AddEnigmaBackupCore()`
  (28.5 MiB in, 16.1 MiB out, 0.8 s):
  - `sha256sum -c` on the sidecar of the encrypted archive reports **OK**;
  - `diff -r` between the source tree and the restored tree reports **no differences**;
  - `file` reports `data` and `tar -tf` refuses it, as an encrypted archive should;
  - the first 48 bytes decode by hand exactly as the spec says — `EBK1`, version 1, kdfId 1,
    `0x00010000` = 65536 KiB, 3, 4, salt length 16, 16 salt bytes, `0x00100000` = 1 MiB, 8 nonce
    bytes: a 47-byte header, with the first chunk immediately after it;
  - verify with the right password `Succeeded`; with the wrong one `AuthenticationFailed` while the
    digest still matched, reported as "the file has not changed since it was written, which means the
    password is wrong"; with no password `PasswordRequired`;
  - restore with the wrong password threw `WrongPasswordOrCorrupted` and wrote nothing.
