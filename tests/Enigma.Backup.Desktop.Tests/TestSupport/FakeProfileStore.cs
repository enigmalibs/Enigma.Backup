using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Core.Profiles;

namespace Enigma.Backup.Desktop.Tests.TestSupport;

/// <summary>An in-memory <see cref="IProfileStore"/> with the real one's semantics.</summary>
/// <remarks>
/// Profiles are cloned on the way in and on the way out, the way a store backed by JSON files is,
/// so a test cannot pass by accident because the view model happened to be holding the very object
/// the store has.
/// </remarks>
internal sealed class FakeProfileStore : IProfileStore
{
    private readonly Dictionary<string, BackupProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public string DirectoryPath => Path.Combine("test", "profiles");

    /// <summary>A failure to throw from the next write, import or export — how a test makes the store fail.</summary>
    public Exception? FailNextWrite { get; set; }

    /// <summary>Names whose <see cref="LoadAsync"/> throws, standing in for an unreadable file.</summary>
    public HashSet<string> Unreadable { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>What <see cref="ImportAsync"/> stores; the file at the path is not actually read.</summary>
    public BackupProfile? ImportResult { get; set; }

    /// <summary>Every operation the store was asked to perform, in order.</summary>
    public List<string> Calls { get; } = [];

    /// <summary>How many profiles are stored.</summary>
    public int Count => _profiles.Count;

    /// <summary>Puts a profile in the store without going through <see cref="SaveAsync"/>.</summary>
    /// <param name="profile">The profile to store.</param>
    public void Seed(BackupProfile profile) => _profiles[profile.Name] = Clone(profile);

    /// <summary>Tells whether a profile of that name is stored.</summary>
    /// <param name="name">The name to look for.</param>
    public bool Contains(string name) => _profiles.ContainsKey(name);

    /// <summary>Reads a stored profile directly, for asserting on what was written.</summary>
    /// <param name="name">The name to read.</param>
    public BackupProfile Stored(string name) => _profiles[name];

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        Calls.Add("List");

        List<string> names = [.. _profiles.Keys];
        names.Sort(StringComparer.OrdinalIgnoreCase);

        return Task.FromResult<IReadOnlyList<string>>(names);
    }

    /// <inheritdoc />
    public Task<BackupProfile> LoadAsync(string name, CancellationToken cancellationToken = default)
    {
        Calls.Add($"Load:{name}");

        if (Unreadable.Contains(name))
        {
            throw new ProfileFormatException($"The profile file '{name}.json' could not be read.");
        }

        return _profiles.TryGetValue(name, out BackupProfile? profile)
            ? Task.FromResult(Clone(profile))
            : throw new ProfileNotFoundException(name);
    }

    /// <inheritdoc />
    public Task SaveAsync(BackupProfile profile, CancellationToken cancellationToken = default)
    {
        Calls.Add($"Save:{profile.Name}");
        ThrowIfScripted();

        _profiles[ProfileName.EnsureValid(profile.Name)] = Clone(profile);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RenameAsync(string oldName, string newName, CancellationToken cancellationToken = default)
    {
        Calls.Add($"Rename:{oldName}->{newName}");
        ThrowIfScripted();

        if (!_profiles.TryGetValue(oldName, out BackupProfile? profile))
        {
            throw new ProfileNotFoundException(oldName);
        }

        if (!string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase) && _profiles.ContainsKey(newName))
        {
            throw new ProfileAlreadyExistsException(newName);
        }

        _profiles.Remove(oldName);
        profile.Name = ProfileName.EnsureValid(newName);
        _profiles[newName] = profile;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        Calls.Add($"Delete:{name}");
        ThrowIfScripted();

        if (!_profiles.Remove(name))
        {
            throw new ProfileNotFoundException(name);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<BackupProfile> ImportAsync(string path, CancellationToken cancellationToken = default)
    {
        Calls.Add($"Import:{path}");
        ThrowIfScripted();

        BackupProfile imported = ImportResult ?? throw new ProfileFormatException($"'{path}' is not a profile file.");

        _profiles[imported.Name] = Clone(imported);

        return Task.FromResult(Clone(imported));
    }

    /// <inheritdoc />
    public Task ExportAsync(string name, string path, CancellationToken cancellationToken = default)
    {
        Calls.Add($"Export:{name}->{path}");
        ThrowIfScripted();

        if (!_profiles.ContainsKey(name))
        {
            throw new ProfileNotFoundException(name);
        }

        return Task.CompletedTask;
    }

    private static BackupProfile Clone(BackupProfile profile) =>
        new()
        {
            FormatVersion = profile.FormatVersion,
            Name = profile.Name,
            Inputs = [.. profile.Inputs],
            OutputDirectory = profile.OutputDirectory,
            Compression = profile.Compression,
            Encrypt = profile.Encrypt,
            ExcludeGlobs = [.. profile.ExcludeGlobs],
        };

    private void ThrowIfScripted()
    {
        if (FailNextWrite is Exception failure)
        {
            FailNextWrite = null;

            throw failure;
        }
    }
}
