# Golden fixture — an encrypted archive from an earlier release

These bytes are the evidence behind one promise in the README and `RELEASENOTES.md`: **an encrypted
archive written by an earlier release of Enigma.Backup stays readable by a later one.** They are not
test *input* in the usual sense — they are a frozen artifact of a past build, kept so that
`Encryption/GoldenArchiveCompatibilityTests.cs` can open them with today's code.

The archive is what makes the promise checkable: it sits directly on top of Argon2id and AES-256-GCM
as `Enigma.Core` — and, under it, BouncyCastle — implemented them at the time. A silent change in
either primitive would not fail a build and would not fail any round-trip test, because a round trip
encrypts and decrypts with the *same* build. It would fail here.

## What is in this folder

| File | What it is |
|------|------------|
| `Golden_2026-08-01_120000.tar.zst.enc` | The archive: tar → Zstandard → `EBK1` (AES-256-GCM). |
| `Golden_2026-08-01_120000.tar.zst.enc.sha256` | Its sidecar, in `sha256sum` format, as the run wrote it. |
| `source-tree.txt` | The tree the archive was made from — one `<path>\t<entry>` line per entry, sorted, where `<entry>` is `dir` or `file <length> <sha256>`. This is what a restore has to reproduce. |

The password is **`golden-fixture-password`** — a throwaway constant, printed here on purpose. There
is nothing secret inside; the point of the fixture is that anybody can open it.

## How it was produced

- **Written by:** `Enigma.Core 1.0.0` (BouncyCastle.Cryptography 2.6.2), Enigma.Backup 1.0.0,
  .NET 10, Linux x64.
- **Date:** 2026-08-01.
- **By:** the real `BackupService`, over the real streaming pipeline — not a hand-built container.
  It therefore carries a genuine `EBK1` header, a real Argon2id salt, a GCM tag per chunk, a zstd
  frame, real tar entries and a sidecar computed on the output bytes.
- **Argon2id parameters:** the shipped defaults — 64 MiB memory, 3 iterations, parallelism 4, 16-byte
  salt, 1 MiB chunks (`new Ebk1Options()`), *not* the cheap parameters the rest of the suite uses.
  Real archives have no other parameter set.
- **Clock:** frozen at `2026-08-01T12:00:00Z`, which is where the file name's timestamp comes from.
- **Source tree** (`source/`, so tar entries are `source/…`):
  - `notes.txt` — the UTF-8 text `Enigma.Backup golden fixture.\nWritten by Enigma.Core 1.0.0.\n`.
  - `data.bin` — 4096 bytes, byte *i* = `(i * 37 + 11) % 256`.
  - `empty/` — an empty directory.

An equivalent fixture can be rebuilt from that recipe, but it will not be byte-identical: every run
draws a fresh salt and nonce prefix, so two archives of the same tree share no bytes by design.

## If the test that reads this fails

Take it seriously before touching anything here. It means today's code can no longer open an archive
an earlier release wrote — the failure this fixture exists to catch. Look first at what changed
underneath the cipher (an `Enigma.Core` or BouncyCastle bump), not at the fixture.

**Do not regenerate these files to make the test pass.** A fixture regenerated with the current build
is a same-version round trip and proves nothing; it would delete the evidence and leave the promise
untested. Replace them only when the file *format* is deliberately versioned forward — and then keep
this one and add the new one beside it.
