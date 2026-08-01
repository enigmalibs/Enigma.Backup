using System;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Errors;

namespace Enigma.Backup.Desktop.Services;

/// <summary>
/// The desktop's <see cref="IBackupErrorHandler"/>: every failure the core cannot decide about
/// becomes a Skip / Retry / Abort dialog.
/// </summary>
/// <remarks>
/// A per-run adapter over the injected dialog service rather than a registered service — it carries
/// no state of its own, and the "apply to all" memory it appears to need belongs to the core's
/// <see cref="ErrorDecisionPolicy"/>, which wraps this for the duration of a run.
/// </remarks>
internal sealed class DialogBackupErrorHandler : IBackupErrorHandler
{
    private readonly IDialogService _dialogs;

    /// <summary>Creates a handler that asks through <paramref name="dialogs"/>.</summary>
    /// <param name="dialogs">The dialogs to ask with.</param>
    public DialogBackupErrorHandler(IDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(dialogs);

        _dialogs = dialogs;
    }

    /// <inheritdoc />
    public Task<BackupErrorDecision> HandleAsync(BackupError error, CancellationToken cancellationToken = default) =>
        _dialogs.RequestErrorDecisionAsync(error, cancellationToken);
}
