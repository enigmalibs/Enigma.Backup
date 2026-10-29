using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Errors;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Core.Restoring;
using Enigma.Backup.Desktop.Services;

namespace Enigma.Backup.Desktop.Tests.TestSupport;

/// <summary>
/// An <see cref="IDialogService"/> that answers from queues and records every question.
/// </summary>
/// <remarks>
/// The default for an empty queue is always the cautious answer — cancel the prompt, abort the run,
/// decline the confirmation — so an unscripted test fails loudly instead of quietly agreeing to
/// something.
/// </remarks>
internal sealed class FakeDialogService : IDialogService
{
    /// <summary>The passwords to hand back, in order; <see langword="null"/> is a cancelled prompt.</summary>
    public Queue<string?> Passwords { get; } = new();

    /// <summary>The profile names to hand back, in order; <see langword="null"/> is a cancelled prompt.</summary>
    public Queue<string?> ProfileNames { get; } = new();

    /// <summary>The patterns to hand back, in order; <see langword="null"/> is a cancelled prompt.</summary>
    public Queue<PatternInput?> Patterns { get; } = new();

    /// <summary>The answers to error prompts, in order.</summary>
    public Queue<BackupErrorDecision> ErrorDecisions { get; } = new();

    /// <summary>The answers to restore-conflict prompts, in order.</summary>
    public Queue<RestoreConflictDecision> ConflictDecisions { get; } = new();

    /// <summary>The answers to confirmations, in order.</summary>
    public Queue<bool> Confirmations { get; } = new();

    /// <summary>Every password that was asked for.</summary>
    public List<(PasswordPurpose Purpose, string Subject)> PasswordRequests { get; } = [];

    /// <summary>Every profile name that was asked for.</summary>
    public List<ProfileNameRequest> ProfileNameRequests { get; } = [];

    /// <summary>Every failure that was raised.</summary>
    public List<BackupError> ErrorRequests { get; } = [];

    /// <summary>Every conflict that was raised.</summary>
    public List<RestoreConflict> ConflictRequests { get; } = [];

    /// <summary>Every message that was shown.</summary>
    public List<(string Title, string Message, MessageSeverity Severity)> Messages { get; } = [];

    /// <summary>Every confirmation that was asked for.</summary>
    public List<(string Title, string Message, string ConfirmLabel)> ConfirmationRequests { get; } = [];

    /// <summary>How many times a pattern was asked for.</summary>
    public int PatternRequests { get; private set; }

    /// <summary>How many times the preferences were opened. There is nothing to answer.</summary>
    public int SettingsRequests { get; private set; }

    /// <summary>How many times the About dialog was opened. There is nothing to answer.</summary>
    public int AboutRequests { get; private set; }

    /// <inheritdoc />
    public Task<string?> RequestPasswordAsync(PasswordPurpose purpose, string subject, CancellationToken cancellationToken = default)
    {
        PasswordRequests.Add((purpose, subject));

        return Task.FromResult(Passwords.Count > 0 ? Passwords.Dequeue() : null);
    }

    /// <inheritdoc />
    public Task<string?> RequestProfileNameAsync(ProfileNameRequest request, CancellationToken cancellationToken = default)
    {
        ProfileNameRequests.Add(request);

        return Task.FromResult(ProfileNames.Count > 0 ? ProfileNames.Dequeue() : null);
    }

    /// <inheritdoc />
    public Task<PatternInput?> RequestPatternInputAsync(CancellationToken cancellationToken = default)
    {
        PatternRequests++;

        return Task.FromResult(Patterns.Count > 0 ? Patterns.Dequeue() : null);
    }

    /// <inheritdoc />
    public Task<BackupErrorDecision> RequestErrorDecisionAsync(BackupError error, CancellationToken cancellationToken = default)
    {
        ErrorRequests.Add(error);

        return Task.FromResult(ErrorDecisions.Count > 0 ? ErrorDecisions.Dequeue() : BackupErrorDecision.Abort());
    }

    /// <inheritdoc />
    public Task<RestoreConflictDecision> RequestConflictDecisionAsync(RestoreConflict conflict, CancellationToken cancellationToken = default)
    {
        ConflictRequests.Add(conflict);

        return Task.FromResult(ConflictDecisions.Count > 0 ? ConflictDecisions.Dequeue() : RestoreConflictDecision.Abort());
    }

    /// <inheritdoc />
    public Task ShowMessageAsync(string title, string message, MessageSeverity severity, CancellationToken cancellationToken = default)
    {
        Messages.Add((title, message, severity));

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ShowSettingsAsync(CancellationToken cancellationToken = default)
    {
        SettingsRequests++;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ShowAboutAsync(CancellationToken cancellationToken = default)
    {
        AboutRequests++;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> ConfirmAsync(string title, string message, string confirmLabel, CancellationToken cancellationToken = default)
    {
        ConfirmationRequests.Add((title, message, confirmLabel));

        return Task.FromResult(Confirmations.Count > 0 && Confirmations.Dequeue());
    }
}
