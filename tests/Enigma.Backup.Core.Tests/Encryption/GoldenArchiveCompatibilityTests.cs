using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Restoring;
using Enigma.Backup.Core.Tests.TestSupport;
using Enigma.Backup.Core.Verification;
using Xunit;

namespace Enigma.Backup.Core.Tests.Encryption;

/// <summary>
/// The forward-compatibility guarantee: an encrypted archive written by an earlier release opens
/// under this one.
/// </summary>
/// <remarks>
/// <para>
/// Every other encryption test round-trips within a single build, which cannot detect a change in the
/// primitives underneath: the same code seals and opens the bytes, so it agrees with itself whatever
/// it does. These two read bytes this build never wrote — see <c>Fixtures/README.md</c> — and are
/// therefore the only place a behavioural change in Argon2id or AES-256-GCM, arriving through an
/// <c>Enigma.Core</c> or BouncyCastle bump, would show up as a failure rather than as unreadable
/// backups in the field.
/// </para>
/// <para>
/// A failure here is not a stale fixture. It means archives people already hold can no longer be
/// opened.
/// </para>
/// </remarks>
public sealed class GoldenArchiveCompatibilityTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Verify_AuthenticatesAnArchiveWrittenByAnEarlierRelease()
    {
        VerificationResult verification = await Pipeline.Verification()
            .VerifyAsync(GoldenFixture.ArchivePath, GoldenFixture.Password, progress: null, Token);

        Assert.Equal(VerificationOutcome.Succeeded, verification.Outcome);
        Assert.True(verification.IsEncrypted);

        // Not just the digest: every chunk's GCM tag was checked against a key Argon2id had to
        // re-derive, from the header the earlier release wrote, to exactly the same 32 bytes.
        Assert.True(verification.PayloadAuthenticated);

        string sidecar = await File.ReadAllTextAsync(GoldenFixture.SidecarPath, Token);
        Assert.StartsWith(verification.ActualSha256!, sidecar, System.StringComparison.Ordinal);
        Assert.Equal(verification.ExpectedSha256, verification.ActualSha256);
    }

    [Fact]
    public async Task Restore_ReproducesTheTreeAnEarlierReleaseArchived()
    {
        using TempDirectory temp = new();

        RestoreResult restore = await Pipeline.Restore().RestoreAsync(
            GoldenFixture.ArchivePath,
            temp.Resolve("restored"),
            GoldenFixture.Password,
            new ScriptedConflictHandler(RestoreConflictDecision.Abort()),
            progress: null,
            Token);

        Assert.Equal(RestoreOutcome.Completed, restore.Outcome);
        Assert.Empty(restore.Skipped);

        // Byte-for-byte: the snapshot carries a SHA-256 per file, so this compares contents, not names.
        Assert.Equal(GoldenFixture.ExpectedTree(), FileTree.Snapshot(temp.Resolve("restored/source")));
    }
}
