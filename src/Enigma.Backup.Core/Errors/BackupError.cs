using System;

namespace Enigma.Backup.Core.Errors;

/// <summary>
/// A single recoverable failure encountered during a run, handed to an
/// <see cref="IBackupErrorHandler"/> so the caller can decide what to do about it.
/// </summary>
/// <param name="Kind">What sort of failure this is.</param>
/// <param name="Path">The file or directory the failure concerns.</param>
/// <param name="Message">A ready-to-display explanation.</param>
/// <param name="Cause">The underlying exception, when there was one.</param>
public sealed record BackupError(BackupErrorKind Kind, string Path, string Message, Exception? Cause = null);
