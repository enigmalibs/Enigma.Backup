using System;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Backups;
using Enigma.Backup.Core.Errors;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Core.Restoring;
using Enigma.Backup.Core.Verification;

namespace Enigma.Backup.Desktop.Tests.TestSupport;

/// <summary>What a scripted backup does when the view model runs one.</summary>
/// <param name="profile">The profile the view model built.</param>
/// <param name="password">The password it prompted for, if any.</param>
/// <param name="errorHandler">The handler it wired the error dialog to.</param>
/// <param name="progress">Where progress reports go.</param>
/// <param name="cancellationToken">The token the Cancel button holds.</param>
internal delegate Task<BackupResult> BackupBehaviour(
    BackupProfile profile,
    string? password,
    IBackupErrorHandler errorHandler,
    IProgress<BackupProgress>? progress,
    CancellationToken cancellationToken);

/// <summary>What a scripted restore does.</summary>
/// <param name="archivePath">The archive that was chosen.</param>
/// <param name="destinationDirectory">The folder that was chosen.</param>
/// <param name="password">The password the view model supplied, if any.</param>
/// <param name="conflictHandler">The handler it wired the conflict dialog to.</param>
/// <param name="progress">Where progress reports go.</param>
/// <param name="cancellationToken">The token the Cancel button holds.</param>
internal delegate Task<RestoreResult> RestoreBehaviour(
    string archivePath,
    string destinationDirectory,
    string? password,
    IRestoreConflictHandler conflictHandler,
    IProgress<RestoreProgress>? progress,
    CancellationToken cancellationToken);

/// <summary>What a scripted verification does.</summary>
/// <param name="archivePath">The archive that was chosen.</param>
/// <param name="password">The password the view model supplied, if any.</param>
/// <param name="progress">Where progress reports go.</param>
/// <param name="cancellationToken">The token the Cancel button holds.</param>
internal delegate Task<VerificationResult> VerificationBehaviour(
    string archivePath,
    string? password,
    IProgress<VerificationProgress>? progress,
    CancellationToken cancellationToken);

/// <summary>An <see cref="IBackupService"/> a test drives directly.</summary>
internal sealed class FakeBackupService : IBackupService
{
    /// <summary>What a run does; a plain successful result when nothing is scripted.</summary>
    public BackupBehaviour? Behaviour { get; set; }

    /// <summary>How many runs were started.</summary>
    public int Runs { get; private set; }

    /// <summary>The profile the last run was given.</summary>
    public BackupProfile? LastProfile { get; private set; }

    /// <summary>The password the last run was given — the check that the prompt reaches the core.</summary>
    public string? LastPassword { get; private set; }

    /// <inheritdoc />
    public Task<BackupResult> RunAsync(
        BackupProfile profile,
        string? password,
        IBackupErrorHandler errorHandler,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Runs++;
        LastProfile = profile;
        LastPassword = password;

        return Behaviour is not null
            ? Behaviour(profile, password, errorHandler, progress, cancellationToken)
            : Task.FromResult(Results.Backup());
    }
}

/// <summary>An <see cref="IRestoreService"/> a test drives directly.</summary>
internal sealed class FakeRestoreService : IRestoreService
{
    /// <summary>What a restore does; a plain successful result when nothing is scripted.</summary>
    public RestoreBehaviour? Behaviour { get; set; }

    /// <summary>How many restores were attempted, retries after a password prompt included.</summary>
    public int Attempts { get; private set; }

    /// <summary>The password the last attempt was given.</summary>
    public string? LastPassword { get; private set; }

    /// <inheritdoc />
    public Task<RestoreResult> RestoreAsync(
        string archivePath,
        string destinationDirectory,
        string? password,
        IRestoreConflictHandler conflictHandler,
        IProgress<RestoreProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Attempts++;
        LastPassword = password;

        return Behaviour is not null
            ? Behaviour(archivePath, destinationDirectory, password, conflictHandler, progress, cancellationToken)
            : Task.FromResult(Results.Restore(archivePath, destinationDirectory));
    }
}

/// <summary>An <see cref="IVerificationService"/> a test drives directly.</summary>
internal sealed class FakeVerificationService : IVerificationService
{
    /// <summary>What a check does; a plain success when nothing is scripted.</summary>
    public VerificationBehaviour? Behaviour { get; set; }

    /// <summary>How many checks were attempted, retries after a password prompt included.</summary>
    public int Attempts { get; private set; }

    /// <summary>The password the last attempt was given.</summary>
    public string? LastPassword { get; private set; }

    /// <inheritdoc />
    public Task<VerificationResult> VerifyAsync(
        string archivePath,
        string? password = null,
        IProgress<VerificationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Attempts++;
        LastPassword = password;

        return Behaviour is not null
            ? Behaviour(archivePath, password, progress, cancellationToken)
            : Task.FromResult(Results.Verification(archivePath));
    }
}
