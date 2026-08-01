using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.Backup.Desktop.Services;

/// <summary>
/// The system file and folder pickers, as plain paths.
/// </summary>
/// <remarks>
/// Everything a view model deals with is a <see cref="string"/> path, because that is what a
/// <see cref="Core.Profiles.BackupProfile"/> stores and what the core takes. Avalonia's storage
/// items stop at this interface.
/// </remarks>
public interface IFilePicker
{
    /// <summary>Asks for any number of files to add to a profile's inputs.</summary>
    /// <param name="cancellationToken">Cancels waiting for the picker.</param>
    /// <returns>The chosen paths; empty when the picker was dismissed.</returns>
    Task<IReadOnlyList<string>> PickInputFilesAsync(CancellationToken cancellationToken = default);

    /// <summary>Asks for a folder.</summary>
    /// <param name="title">The picker's title, which says what the folder is for.</param>
    /// <param name="startAt">A path to open the picker at, when there is a sensible one.</param>
    /// <param name="cancellationToken">Cancels waiting for the picker.</param>
    /// <returns>The chosen path, or <see langword="null"/> when the picker was dismissed.</returns>
    Task<string?> PickFolderAsync(string title, string? startAt = null, CancellationToken cancellationToken = default);

    /// <summary>Asks for an archive to restore or verify.</summary>
    /// <param name="title">The picker's title, which says what the archive is for.</param>
    /// <param name="cancellationToken">Cancels waiting for the picker.</param>
    /// <returns>The chosen path, or <see langword="null"/> when the picker was dismissed.</returns>
    Task<string?> PickArchiveAsync(string title, CancellationToken cancellationToken = default);

    /// <summary>Asks for a profile file to import.</summary>
    /// <param name="cancellationToken">Cancels waiting for the picker.</param>
    /// <returns>The chosen path, or <see langword="null"/> when the picker was dismissed.</returns>
    Task<string?> PickProfileToImportAsync(CancellationToken cancellationToken = default);

    /// <summary>Asks where to export a profile to.</summary>
    /// <param name="suggestedFileName">The file name to offer, including its extension.</param>
    /// <param name="cancellationToken">Cancels waiting for the picker.</param>
    /// <returns>The chosen path, or <see langword="null"/> when the picker was dismissed.</returns>
    Task<string?> PickProfileExportPathAsync(string suggestedFileName, CancellationToken cancellationToken = default);
}
