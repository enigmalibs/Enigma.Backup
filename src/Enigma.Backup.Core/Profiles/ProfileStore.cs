using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.Backup.Core.Profiles;

/// <summary>
/// The file-backed <see cref="IProfileStore"/>: one <c>&lt;name&gt;.json</c> file per profile in the
/// per-user configuration folder.
/// </summary>
/// <remarks>
/// <para>
/// Every name that reaches a path is run through <see cref="ProfileName.EnsureValid"/> first, so a
/// name can never traverse out of <see cref="DirectoryPath"/>.
/// </para>
/// <para>
/// Writes go to a temporary file that is then moved over the target, so an interrupted save leaves
/// the previous profile intact rather than a half-written one.
/// </para>
/// </remarks>
public sealed class ProfileStore : IProfileStore
{
    private const string FileExtension = ".json";
    private const string TempExtension = ".tmp";

    /// <summary>Creates a store over the per-user configuration folder.</summary>
    public ProfileStore()
        : this(DefaultDirectory)
    {
    }

    /// <summary>Creates a store over an explicit directory — how tests and imports of a portable configuration point it elsewhere.</summary>
    /// <param name="directory">The directory profile files live in; created on first write.</param>
    public ProfileStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        DirectoryPath = Path.GetFullPath(directory);
    }

    /// <summary>
    /// The per-user profile folder: <c>%APPDATA%\Enigma.Backup\profiles</c> on Windows,
    /// <c>~/.config/Enigma.Backup/profiles</c> on Linux.
    /// </summary>
    public static string DefaultDirectory { get; } = ResolveDefaultDirectory();

    /// <inheritdoc />
    public string DirectoryPath { get; }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(DirectoryPath))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        List<string> names = [];
        foreach (string file in Directory.EnumerateFiles(DirectoryPath, "*" + FileExtension))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string name = Path.GetFileNameWithoutExtension(file);

            // A file whose stem is not a legal profile name could never have been written by the
            // store, and could not be loaded by name either — leave it out rather than list a
            // profile that cannot be opened.
            if (ProfileName.IsValid(name))
            {
                names.Add(name);
            }
        }

        names.Sort(StringComparer.OrdinalIgnoreCase);

        return Task.FromResult<IReadOnlyList<string>>(names);
    }

    /// <inheritdoc />
    public async Task<BackupProfile> LoadAsync(string name, CancellationToken cancellationToken = default)
    {
        string path = GetPath(name);
        if (!File.Exists(path))
        {
            throw new ProfileNotFoundException(name);
        }

        string json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        BackupProfile profile = ProfileSerializer.Deserialize(json, path);

        // Inside the store the file name is the identity, so a file that was renamed by hand still
        // loads under the name it is listed as.
        profile.Name = name;

        return profile;
    }

    /// <inheritdoc />
    public async Task SaveAsync(BackupProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        string path = GetPath(profile.Name);
        string json = ProfileSerializer.Serialize(profile);

        Directory.CreateDirectory(DirectoryPath);
        await WriteAtomicallyAsync(path, json, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RenameAsync(string oldName, string newName, CancellationToken cancellationToken = default)
    {
        string source = GetPath(oldName);
        string target = GetPath(newName);

        if (!File.Exists(source))
        {
            throw new ProfileNotFoundException(oldName);
        }

        // On a case-insensitive file system "docs" and "Docs" are the same file: that is a rename of
        // the profile onto itself, not a collision with a different profile.
        bool sameFile = string.Equals(source, target, PathComparison);
        if (!sameFile && File.Exists(target))
        {
            throw new ProfileAlreadyExistsException(newName);
        }

        // Rewriting rather than moving keeps the 'name' inside the file in step with its file name.
        BackupProfile profile = await LoadAsync(oldName, cancellationToken).ConfigureAwait(false);
        profile.Name = ProfileName.EnsureValid(newName);
        await SaveAsync(profile, cancellationToken).ConfigureAwait(false);

        if (!sameFile)
        {
            File.Delete(source);
        }
    }

    /// <inheritdoc />
    public Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string path = GetPath(name);
        if (!File.Exists(path))
        {
            throw new ProfileNotFoundException(name);
        }

        File.Delete(path);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<BackupProfile> ImportAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"There is no profile file at '{fullPath}'.", fullPath);
        }

        string json = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
        BackupProfile profile = ProfileSerializer.Deserialize(json, fullPath);

        string candidate = ProfileName.IsValid(profile.Name)
            ? profile.Name
            : ProfileName.Sanitize(Path.GetFileNameWithoutExtension(fullPath));

        profile.Name = AllocateFreeName(candidate);
        await SaveAsync(profile, cancellationToken).ConfigureAwait(false);

        return profile;
    }

    /// <inheritdoc />
    public async Task ExportAsync(string name, string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        BackupProfile profile = await LoadAsync(name, cancellationToken).ConfigureAwait(false);

        string fullPath = Path.GetFullPath(path);
        string? parent = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        await WriteAtomicallyAsync(fullPath, ProfileSerializer.Serialize(profile), cancellationToken).ConfigureAwait(false);
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static string ResolveDefaultDirectory()
    {
        // ApplicationData is %APPDATA% on Windows and $XDG_CONFIG_HOME (or ~/.config) on Linux.
        string configurationRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (configurationRoot.Length == 0)
        {
            configurationRoot = Path.Combine(AppContext.BaseDirectory, "config");
        }

        return Path.Combine(configurationRoot, "Enigma.Backup", "profiles");
    }

    private static async Task WriteAtomicallyAsync(string path, string contents, CancellationToken cancellationToken)
    {
        string temporaryPath = path + TempExtension;
        try
        {
            await File.WriteAllTextAsync(temporaryPath, contents, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, path, overwrite: true);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // The half-written temporary file is not worth failing the original error over.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private string GetPath(string? name) => Path.Combine(DirectoryPath, ProfileName.EnsureValid(name) + FileExtension);

    private string AllocateFreeName(string candidate)
    {
        string name = candidate;
        for (int suffix = 2; File.Exists(Path.Combine(DirectoryPath, name + FileExtension)); suffix++)
        {
            name = $"{candidate}_{suffix}";
        }

        return name;
    }
}
