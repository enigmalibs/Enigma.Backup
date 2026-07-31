using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Core.KeyDerivation;
using Enigma.Core.Padding;
using Enigma.Core.Symmetric.BlockCiphers;

namespace Enigma.Backup.Core.Encryption;

/// <summary>
/// One container's key and cipher: derives the Argon2id key from the password and the header, then
/// seals and opens individual chunks with AES-256-GCM.
/// </summary>
/// <remarks>
/// <para>
/// Both primitives come from the <c>Enigma.Core</c> package. Its cipher API is stream-to-stream, so a
/// chunk is sealed into a reusable in-memory buffer rather than straight to the file: the framing
/// puts <c>plainLen</c> in front of the ciphertext, and that length is only known once the chunk is
/// closed.
/// </para>
/// <para>
/// The derived key lives for the duration of one backup, restore or verification and is wiped on
/// disposal; the password itself is converted, used, and its bytes cleared immediately.
/// </para>
/// </remarks>
internal sealed class Ebk1Cipher : IDisposable
{
    /// <summary>AES-256.</summary>
    private const int KeyLength = 32;

    /// <summary>
    /// The cipher's internal loop buffer. Larger than the package's 4 KiB default because a chunk is
    /// megabytes, and every iteration of that loop is pure overhead here.
    /// </summary>
    private const int CipherBufferSize = 64 * 1024;

    private readonly IBlockCipherService _aes;
    private readonly byte[] _key;
    private readonly byte[] _nonce;
    private readonly byte[] _associatedData;
    private readonly MemoryStream _sealed;
    private bool _disposed;

    private Ebk1Cipher(Ebk1Header header, byte[] key)
    {
        _aes = new BlockCipherServiceFactory().CreateAesService(CipherBufferSize);
        _key = key;
        _nonce = header.CreateNonce();
        _associatedData = header.CreateAssociatedData();
        _sealed = new MemoryStream(header.ChunkSize + Ebk1Header.TagLength);
    }

    /// <summary>Derives the container's key from a password.</summary>
    /// <param name="header">The header holding the salt and the Argon2id cost parameters.</param>
    /// <param name="password">The password, used for this call only.</param>
    /// <param name="cancellationToken">Cancels before the derivation starts.</param>
    /// <remarks>
    /// Argon2id is deliberately expensive — hundreds of milliseconds at the default cost — so it runs
    /// off the calling thread: a caller on a UI thread must not freeze while a key is stretched.
    /// </remarks>
    public static async Task<Ebk1Cipher> CreateAsync(Ebk1Header header, string password, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(password);

        cancellationToken.ThrowIfCancellationRequested();

        byte[] key = await Task.Run(
            () =>
            {
                byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

                try
                {
                    return new Argon2ServiceFactory().CreateArgon2Service().DeriveKey(
                        passwordBytes,
                        header.Salt,
                        header.Iterations,
                        header.MemoryKiB,
                        header.Parallelism,
                        KeyLength,
                        Argon2Variant.Argon2id,
                        Argon2Version.Version13);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(passwordBytes);
                }
            },
            cancellationToken).ConfigureAwait(false);

        return new Ebk1Cipher(header, key);
    }

    /// <summary>Seals one chunk.</summary>
    /// <param name="plaintext">The buffer holding the chunk.</param>
    /// <param name="count">How much of <paramref name="plaintext"/> the chunk occupies.</param>
    /// <param name="counter">The chunk's zero-based position in the container.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The ciphertext followed by its tag, valid until the next call on this instance.</returns>
    public async ValueTask<ReadOnlyMemory<byte>> SealAsync(byte[] plaintext, int count, uint counter, CancellationToken cancellationToken = default)
    {
        Ebk1Header.StampChunk(_nonce, _associatedData, counter);

        _sealed.Position = 0;
        _sealed.SetLength(0);

        using MemoryStream source = new(plaintext, 0, count, writable: false);
        await _aes.EncryptAsync(
            source,
            _sealed,
            _key,
            _nonce,
            BlockCipherMode.Gcm,
            PaddingScheme.None,
            Ebk1Header.TagBits,
            _associatedData,
            progress: null,
            cancellationToken).ConfigureAwait(false);

        return _sealed.GetBuffer().AsMemory(0, (int)_sealed.Length);
    }

    /// <summary>Opens one chunk, verifying its tag.</summary>
    /// <param name="sealedChunk">The buffer holding the ciphertext and its tag.</param>
    /// <param name="count">How much of <paramref name="sealedChunk"/> the chunk occupies.</param>
    /// <param name="counter">The chunk's zero-based position in the container.</param>
    /// <param name="plaintext">The buffer the plaintext is written into; must be large enough.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How many bytes of <paramref name="plaintext"/> were filled.</returns>
    /// <exception cref="EncryptedArchiveException">The tag did not verify.</exception>
    public async ValueTask<int> OpenAsync(byte[] sealedChunk, int count, uint counter, byte[] plaintext, CancellationToken cancellationToken = default)
    {
        Ebk1Header.StampChunk(_nonce, _associatedData, counter);

        using MemoryStream source = new(sealedChunk, 0, count, writable: false);
        using MemoryStream destination = new(plaintext, 0, plaintext.Length, writable: true, publiclyVisible: false);

        try
        {
            await _aes.DecryptAsync(
                source,
                destination,
                _key,
                _nonce,
                BlockCipherMode.Gcm,
                PaddingScheme.None,
                Ebk1Header.TagBits,
                _associatedData,
                progress: null,
                cancellationToken).ConfigureAwait(false);
        }
        catch (CryptographicException exception)
        {
            throw WrongPasswordOrCorrupted(exception);
        }

        return (int)destination.Position;
    }

    /// <summary>The exception raised when a chunk's tag does not verify.</summary>
    /// <param name="innerException">The cipher's own report; kept for diagnostics, not for display.</param>
    /// <remarks>
    /// GCM cannot distinguish the wrong key from altered bytes, so neither can this message: both
    /// possibilities are stated rather than one being guessed at.
    /// </remarks>
    public static EncryptedArchiveException WrongPasswordOrCorrupted(Exception? innerException = null)
    {
        const string Message =
            "This encrypted archive could not be opened: either the password is wrong, or the file has been damaged or altered since it was written.";

        return innerException is null
            ? new EncryptedArchiveException(EncryptedArchiveProblem.WrongPasswordOrCorrupted, Message)
            : new EncryptedArchiveException(EncryptedArchiveProblem.WrongPasswordOrCorrupted, Message, innerException);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CryptographicOperations.ZeroMemory(_key);
        CryptographicOperations.ZeroMemory(_sealed.GetBuffer());
        _sealed.Dispose();
    }
}
