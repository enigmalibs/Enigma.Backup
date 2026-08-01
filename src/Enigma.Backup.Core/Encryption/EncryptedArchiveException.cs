using System;

namespace Enigma.Backup.Core.Encryption;

/// <summary>
/// Thrown when an <c>EBK1</c> container cannot be read: no password, the wrong one, damage, a cut-off
/// file, or a header this build does not understand.
/// </summary>
/// <remarks>
/// One exception type with a <see cref="Problem"/> discriminator rather than a family of types,
/// because these all mean the same thing to a caller — "this encrypted file cannot be opened" — and
/// differ only in what to tell the user. <see cref="Exception.Message"/> is already written for them.
/// </remarks>
public sealed class EncryptedArchiveException : Exception
{
    /// <summary>Creates an exception for a container problem.</summary>
    /// <param name="problem">Which problem was found.</param>
    /// <param name="message">A ready-to-display explanation.</param>
    public EncryptedArchiveException(EncryptedArchiveProblem problem, string message)
        : base(message)
    {
        Problem = problem;
    }

    /// <summary>Creates an exception for a container problem with an underlying cause.</summary>
    /// <param name="problem">Which problem was found.</param>
    /// <param name="message">A ready-to-display explanation.</param>
    /// <param name="innerException">The exception that revealed the problem.</param>
    public EncryptedArchiveException(EncryptedArchiveProblem problem, string message, Exception innerException)
        : base(message, innerException)
    {
        Problem = problem;
    }

    /// <summary>What was wrong with the container.</summary>
    public EncryptedArchiveProblem Problem { get; }
}
