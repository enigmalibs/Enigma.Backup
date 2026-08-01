using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.Backup.Core.Profiles;

/// <summary>
/// The managed store of backup profiles in the per-user configuration folder. A profile's name is
/// its identity here: it is the file name, so it must satisfy <see cref="ProfileName.IsValid(string?)"/>.
/// Import and export move profiles between the store and arbitrary locations.
/// </summary>
public interface IProfileStore
{
    /// <summary>The directory the store keeps its profile files in.</summary>
    string DirectoryPath { get; }

    /// <summary>Lists the names of every profile in the store, ordered case-insensitively.</summary>
    /// <param name="cancellationToken">Cancels the listing.</param>
    /// <returns>The profile names; empty when the store has never been written to.</returns>
    Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Loads one profile by name.</summary>
    /// <param name="name">The profile name.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The stored profile.</returns>
    /// <exception cref="ProfileNotFoundException">No profile of that name is stored.</exception>
    /// <exception cref="ProfileFormatException">The stored file cannot be read.</exception>
    Task<BackupProfile> LoadAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a profile, creating it or replacing the one of the same name — which is what both
    /// "Save" and "Save As" do.
    /// </summary>
    /// <param name="profile">The profile to write; its <see cref="BackupProfile.Name"/> chooses the file.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task SaveAsync(BackupProfile profile, CancellationToken cancellationToken = default);

    /// <summary>Renames a stored profile, keeping its contents.</summary>
    /// <param name="oldName">The current name.</param>
    /// <param name="newName">The new name.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <exception cref="ProfileNotFoundException">There is nothing stored under <paramref name="oldName"/>.</exception>
    /// <exception cref="ProfileAlreadyExistsException">A different profile already uses <paramref name="newName"/>.</exception>
    Task RenameAsync(string oldName, string newName, CancellationToken cancellationToken = default);

    /// <summary>Deletes a stored profile.</summary>
    /// <param name="name">The profile to delete.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <exception cref="ProfileNotFoundException">No profile of that name is stored.</exception>
    Task DeleteAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies a profile file from anywhere on disk into the store. The name is taken from the file's
    /// own <c>name</c> field, falling back to its file stem, and is given a numeric suffix rather
    /// than overwriting a profile that already uses it.
    /// </summary>
    /// <param name="path">The profile file to import.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The imported profile, carrying the name it was stored under.</returns>
    /// <exception cref="ProfileFormatException">The file cannot be read as a profile.</exception>
    Task<BackupProfile> ImportAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Writes a stored profile to an arbitrary path, creating the parent directory if needed.</summary>
    /// <param name="name">The profile to export.</param>
    /// <param name="path">The destination file path.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <exception cref="ProfileNotFoundException">No profile of that name is stored.</exception>
    Task ExportAsync(string name, string path, CancellationToken cancellationToken = default);
}
