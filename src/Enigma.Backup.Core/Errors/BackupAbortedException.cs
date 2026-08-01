using System;

namespace Enigma.Backup.Core.Errors;

/// <summary>
/// Thrown when a run stops because an <see cref="IBackupErrorHandler"/> answered
/// <see cref="BackupErrorAction.Abort"/>. Whoever owns the output file catches this to discard the
/// partial result.
/// </summary>
public sealed class BackupAbortedException : Exception
{
    /// <summary>Creates an aborted-run exception without a triggering error.</summary>
    public BackupAbortedException()
        : base("The run was aborted.")
    {
    }

    /// <summary>Creates an aborted-run exception with an explicit message.</summary>
    /// <param name="message">The message describing the abort.</param>
    public BackupAbortedException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an aborted-run exception with an explicit message and inner exception.</summary>
    /// <param name="message">The message describing the abort.</param>
    /// <param name="innerException">The exception that led to the abort.</param>
    public BackupAbortedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates an aborted-run exception for the error the user answered with Abort.</summary>
    /// <param name="error">The failure that was answered with <see cref="BackupErrorAction.Abort"/>.</param>
    public BackupAbortedException(BackupError error)
        : base(BuildMessage(error), error?.Cause)
    {
        Error = error;
    }

    /// <summary>The failure the user aborted on, when the abort came from an error decision.</summary>
    public BackupError? Error { get; }

    private static string BuildMessage(BackupError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return $"The run was aborted: {error.Message}";
    }
}
