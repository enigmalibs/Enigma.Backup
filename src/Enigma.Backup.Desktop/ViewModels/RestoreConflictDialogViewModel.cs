using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Backup.Core.Restoring;

namespace Enigma.Backup.Desktop.ViewModels;

/// <summary>
/// The Skip / Overwrite / Abort prompt a restore raises for every file that is already there.
/// </summary>
/// <remarks>
/// Both sides are described — size and last write time — because that is what tells the user which
/// copy they want. Closing the window without choosing counts as Abort: overwriting somebody's file
/// is not an answer to assume.
/// </remarks>
public sealed class RestoreConflictDialogViewModel : DialogViewModel
{
    /// <summary>Creates a prompt for one conflict.</summary>
    /// <param name="conflict">The entry and the file it would replace.</param>
    public RestoreConflictDialogViewModel(RestoreConflict conflict)
    {
        ArgumentNullException.ThrowIfNull(conflict);

        Conflict = conflict;
        ArchiveDescription = Describe(conflict.ArchiveLength, conflict.ArchiveLastWriteTime);
        ExistingDescription = Describe(conflict.ExistingLength, conflict.ExistingLastWriteTime);

        SkipCommand = new RelayCommand(() => Decide(RestoreConflictAction.Skip));
        OverwriteCommand = new RelayCommand(() => Decide(RestoreConflictAction.Overwrite));
        AbortCommand = new RelayCommand(() => Decide(RestoreConflictAction.Abort));
    }

    /// <summary>The conflict being asked about.</summary>
    public RestoreConflict Conflict { get; }

    /// <summary>The entry's path inside the archive.</summary>
    public string ArchivePath => Conflict.ArchivePath;

    /// <summary>The file on disk that would be replaced.</summary>
    public string DestinationPath => Conflict.DestinationPath;

    /// <summary>The archived copy's size and date.</summary>
    public string ArchiveDescription { get; }

    /// <summary>The existing file's size and date.</summary>
    public string ExistingDescription { get; }

    /// <summary>Whether the same answer should be used for every further conflict of this restore.</summary>
    public bool ApplyToAll
    {
        get;
        set => SetProperty(ref field, value);
    }

    /// <summary>
    /// What was decided. Abort until a button is pressed, so a dismissed window neither overwrites
    /// nor silently passes over the file.
    /// </summary>
    public RestoreConflictDecision Decision { get; private set; } = RestoreConflictDecision.Abort();

    /// <summary>Keeps the file that is on disk.</summary>
    public RelayCommand SkipCommand { get; }

    /// <summary>Replaces the file on disk with the archived copy.</summary>
    public RelayCommand OverwriteCommand { get; }

    /// <summary>Stops the restore, keeping everything already written.</summary>
    public RelayCommand AbortCommand { get; }

    private static string Describe(long length, DateTimeOffset lastWriteTime) =>
        $"{DisplayFormat.Bytes(length)} · {DisplayFormat.Timestamp(lastWriteTime)}";

    private void Decide(RestoreConflictAction action)
    {
        Decision = new RestoreConflictDecision(action, ApplyToAll);

        RequestClose();
    }
}
