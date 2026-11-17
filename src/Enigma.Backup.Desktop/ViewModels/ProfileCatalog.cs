using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace Enigma.Backup.Desktop.ViewModels;

/// <summary>
/// The profiles in the store, as every page sees them: their names, in order, and what each one holds as
/// last saved.
/// </summary>
/// <remarks>
/// <para>
/// One instance for the whole app. The Profiles page writes the store and reloads this; any page that
/// lists profiles reads this, so a profile saved, renamed or deleted on one page is what every other page
/// shows next.
/// </para>
/// <para>
/// <see cref="Names"/> is brought up to date in place rather than rebuilt: a drop-down whose selected
/// name is still there keeps it, and only a drop-down whose name has gone loses its selection.
/// </para>
/// </remarks>
public sealed class ProfileCatalog
{
    private readonly Dictionary<string, BackupProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);

    private readonly IProfileStore _store;
    private readonly IDialogService _dialogs;
    private readonly ILogger<ProfileCatalog> _logger;

    /// <summary>Creates an empty catalog; nothing is read until <see cref="ReloadAsync"/>.</summary>
    /// <param name="store">The store the profiles are read from.</param>
    /// <param name="dialogs">Says when the store, or a profile in it, cannot be read.</param>
    /// <param name="logger">Records what could not be read.</param>
    public ProfileCatalog(IProfileStore store, IDialogService dialogs, ILogger<ProfileCatalog> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(logger);

        _store = store;
        _dialogs = dialogs;
        _logger = logger;
    }

    /// <summary>Raised once <see cref="Names"/> and every profile have been read again.</summary>
    public event EventHandler? Reloaded;

    /// <summary>Every readable profile in the store, by name, in name order.</summary>
    public ObservableCollection<string> Names { get; } = [];

    /// <summary>Whether a profile of that name was read.</summary>
    /// <param name="name">The profile's name; compared without regard to case, as the store does.</param>
    public bool Contains(string name) => _profiles.ContainsKey(name);

    /// <summary>Finds a profile as it was last read from the store.</summary>
    /// <param name="name">The profile's name.</param>
    /// <param name="profile">The profile, when there is one.</param>
    /// <returns><see langword="true"/> when the store has a readable profile of that name.</returns>
    public bool TryGet(string name, [NotNullWhen(true)] out BackupProfile? profile) => _profiles.TryGetValue(name, out profile);

    /// <summary>Reads the store again.</summary>
    /// <returns><see langword="false"/> when the store itself could not be read, which leaves everything as it was.</returns>
    /// <remarks>
    /// A profile file that cannot be read is left out and named in one warning; one bad file must not
    /// hide every other profile.
    /// </remarks>
    public async Task<bool> ReloadAsync()
    {
        Dictionary<string, BackupProfile> loaded = new(StringComparer.OrdinalIgnoreCase);
        List<string> unreadable = [];

        try
        {
            foreach (string name in await _store.ListAsync())
            {
                try
                {
                    loaded[name] = await _store.LoadAsync(name);
                }
                catch (Exception exception) when (exception is ProfileFormatException or IOException or UnauthorizedAccessException)
                {
                    unreadable.Add($"'{name}': {exception.Message}");
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "The profile folder could not be listed.");
            await _dialogs.ShowMessageAsync(
                "Profiles",
                $"The profile folder '{_store.DirectoryPath}' could not be read: {exception.Message}",
                MessageSeverity.Error);

            return false;
        }

        _profiles.Clear();
        foreach ((string name, BackupProfile profile) in loaded)
        {
            _profiles[name] = profile;
        }

        Synchronize([.. loaded.Keys.OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)]);

        if (unreadable.Count > 0)
        {
            await _dialogs.ShowMessageAsync(
                "Profiles",
                "These profile files could not be read and are not listed:" + Environment.NewLine + string.Join(Environment.NewLine, unreadable),
                MessageSeverity.Warning);
        }

        Reloaded?.Invoke(this, EventArgs.Empty);

        return true;
    }

    /// <summary>Brings <see cref="Names"/> to exactly <paramref name="ordered"/>, moving as little as it can.</summary>
    private void Synchronize(IReadOnlyList<string> ordered)
    {
        // Exact comparison: a profile renamed only in case is a name that went and a name that came.
        HashSet<string> wanted = new(ordered, StringComparer.Ordinal);

        for (int index = Names.Count - 1; index >= 0; index--)
        {
            if (!wanted.Contains(Names[index]))
            {
                Names.RemoveAt(index);
            }
        }

        for (int index = 0; index < ordered.Count; index++)
        {
            if (index < Names.Count && string.Equals(Names[index], ordered[index], StringComparison.Ordinal))
            {
                continue;
            }

            int existing = Names.IndexOf(ordered[index]);
            if (existing >= 0)
            {
                Names.Move(existing, index);
            }
            else
            {
                Names.Insert(index, ordered[index]);
            }
        }
    }
}
