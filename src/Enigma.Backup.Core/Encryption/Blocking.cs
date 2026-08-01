using System;
using System.IO;
using System.Threading.Tasks;

namespace Enigma.Backup.Core.Encryption;

/// <summary>
/// Runs one of the container's asynchronous operations to completion for a caller that cannot await.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Stream"/> obliges an implementation to offer synchronous <c>Read</c> and <c>Write</c>,
/// and a caller — a compressor, a disposal that is not <c>await using</c> — is free to use them. The
/// cipher underneath has no synchronous overload, so somewhere the two have to meet.
/// </para>
/// <para>
/// They meet here, in one place, rather than in a second implementation of the chunking: the
/// synchronous entry points are wrappers over the asynchronous ones, so both are the same code and
/// every test covers both. Going through <see cref="Task.Run(Func{Task})"/> detaches the work from
/// whatever synchronization context the caller is on, which is what makes blocking on it safe rather
/// than a deadlock waiting for a UI thread. Nothing waits on I/O while the thread is held: the
/// expensive part is computation over buffers already in memory.
/// </para>
/// </remarks>
internal static class Blocking
{
    /// <summary>Runs <paramref name="operation"/> and waits for it.</summary>
    /// <param name="operation">The operation to run.</param>
    public static void Run(Func<Task> operation) => Task.Run(operation).GetAwaiter().GetResult();

    /// <summary>Runs <paramref name="operation"/> and returns what it produced.</summary>
    /// <typeparam name="T">What the operation produces.</typeparam>
    /// <param name="operation">The operation to run.</param>
    public static T Run<T>(Func<Task<T>> operation) => Task.Run(operation).GetAwaiter().GetResult();
}
