using System;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Restoring;

namespace Enigma.Backup.Desktop.Services;

/// <summary>
/// The desktop's <see cref="IRestoreConflictHandler"/>: every file a restore would write over
/// becomes a Skip / Overwrite / Abort dialog.
/// </summary>
/// <remarks>
/// The mirror of <see cref="DialogBackupErrorHandler"/>, and per-run for the same reason: the
/// "apply to all" memory belongs to the core's <see cref="RestoreConflictPolicy"/>.
/// </remarks>
internal sealed class DialogRestoreConflictHandler : IRestoreConflictHandler
{
    private readonly IDialogService _dialogs;

    /// <summary>Creates a handler that asks through <paramref name="dialogs"/>.</summary>
    /// <param name="dialogs">The dialogs to ask with.</param>
    public DialogRestoreConflictHandler(IDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(dialogs);

        _dialogs = dialogs;
    }

    /// <inheritdoc />
    public Task<RestoreConflictDecision> HandleAsync(RestoreConflict conflict, CancellationToken cancellationToken = default) =>
        _dialogs.RequestConflictDecisionAsync(conflict, cancellationToken);
}
