using System.Collections.Generic;
using Enigma.Backup.Desktop.Settings;

namespace Enigma.Backup.Desktop.Tests.TestSupport;

/// <summary>
/// An <see cref="IAppSettingsStore"/> that keeps the preferences in memory and counts every read and
/// write.
/// </summary>
/// <remarks>
/// Both calls copy rather than hand out the stored instance, exactly as the file-backed store does —
/// otherwise a view model mutating what it loaded would silently update the "stored" value without
/// ever saving, and a test asserting that a change was persisted would pass either way.
/// </remarks>
internal sealed class FakeAppSettingsStore : IAppSettingsStore
{
    /// <summary>What is currently stored.</summary>
    public AppSettings Stored { get; set; } = new();

    /// <summary>How many times the preferences were read.</summary>
    public int Loads { get; private set; }

    /// <summary>The theme of every save, in order — so "exactly once per change" is checkable.</summary>
    public List<AppTheme> Saves { get; } = [];

    /// <inheritdoc />
    public string FilePath => "/config/Enigma.Backup/settings.json";

    /// <inheritdoc />
    public AppSettings Load()
    {
        Loads++;

        return Copy(Stored);
    }

    /// <inheritdoc />
    public void Save(AppSettings settings)
    {
        Saves.Add(settings.Theme);
        Stored = Copy(settings);
    }

    private static AppSettings Copy(AppSettings settings) =>
        new() { FormatVersion = settings.FormatVersion, Theme = settings.Theme };
}
