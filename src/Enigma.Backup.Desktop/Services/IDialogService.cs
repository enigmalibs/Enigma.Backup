using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Errors;
using Enigma.Backup.Core.Restoring;

namespace Enigma.Backup.Desktop.Services;

/// <summary>
/// Every question the app puts to the user in a modal window.
/// </summary>
/// <remarks>
/// <para>
/// This is the desktop end of the core's CLI-readiness seam: the core asks
/// <see cref="IBackupErrorHandler"/> and <see cref="IRestoreConflictHandler"/> what to do, and those
/// are implemented here in terms of dialogs. It is also the reason the view models are testable —
/// a test implements this interface with canned answers and never opens a window.
/// </para>
/// <para>
/// Implementations are called from whatever thread the core happens to be on and marshal to the UI
/// thread themselves, so callers never have to.
/// </para>
/// </remarks>
public interface IDialogService
{
    /// <summary>Asks for a password.</summary>
    /// <param name="purpose">Whether the password protects a new archive or opens an existing one.</param>
    /// <param name="subject">What the password is for — a profile name, or an archive's file name.</param>
    /// <param name="cancellationToken">Cancels waiting for the answer.</param>
    /// <returns>The password, or <see langword="null"/> when the prompt was cancelled.</returns>
    Task<string?> RequestPasswordAsync(PasswordPurpose purpose, string subject, CancellationToken cancellationToken = default);

    /// <summary>Asks for a profile name.</summary>
    /// <param name="request">What to ask, and which names are already taken.</param>
    /// <param name="cancellationToken">Cancels waiting for the answer.</param>
    /// <returns>The name, or <see langword="null"/> when the prompt was cancelled.</returns>
    Task<string?> RequestProfileNameAsync(ProfileNameRequest request, CancellationToken cancellationToken = default);

    /// <summary>Asks what to do about a file a backup could not read.</summary>
    /// <param name="error">The failure to show.</param>
    /// <param name="cancellationToken">Cancels waiting for the answer.</param>
    /// <returns>Skip, Retry or Abort, and whether it applies to every further error.</returns>
    Task<BackupErrorDecision> RequestErrorDecisionAsync(BackupError error, CancellationToken cancellationToken = default);

    /// <summary>Asks what to do about a file a restore would write over.</summary>
    /// <param name="conflict">The conflict to show, with both sides described.</param>
    /// <param name="cancellationToken">Cancels waiting for the answer.</param>
    /// <returns>Skip, Overwrite or Abort, and whether it applies to every further conflict.</returns>
    Task<RestoreConflictDecision> RequestConflictDecisionAsync(RestoreConflict conflict, CancellationToken cancellationToken = default);

    /// <summary>Tells the user something, with a single button to dismiss it.</summary>
    /// <param name="title">The dialog's title.</param>
    /// <param name="message">What to say.</param>
    /// <param name="severity">How much weight to give it.</param>
    /// <param name="cancellationToken">Cancels waiting for the acknowledgement.</param>
    Task ShowMessageAsync(string title, string message, MessageSeverity severity, CancellationToken cancellationToken = default);

    /// <summary>Asks the user to confirm something before it happens.</summary>
    /// <param name="title">The dialog's title.</param>
    /// <param name="message">What is about to happen.</param>
    /// <param name="confirmLabel">The wording of the confirming button, e.g. "Delete".</param>
    /// <param name="cancellationToken">Cancels waiting for the answer.</param>
    /// <returns><see langword="true"/> when the user confirmed.</returns>
    Task<bool> ConfirmAsync(string title, string message, string confirmLabel, CancellationToken cancellationToken = default);
}
