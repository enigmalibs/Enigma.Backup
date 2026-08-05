namespace Enigma.Backup.Desktop.Settings;

/// <summary>Reads and writes the desktop app's preferences.</summary>
/// <remarks>
/// <para>
/// Synchronous on purpose, and it is one small file: the stored variant has to be applied before the
/// window is shown — <see cref="App"/>'s startup is not an <c>async</c> method — and a choice is
/// written from the property setter the drop-down is bound to, where a fire-and-forget task would be
/// a write nobody can see the end of.
/// </para>
/// <para>
/// Neither call throws. A missing, unreadable or malformed file reads as the defaults, and a write
/// that fails is logged and dropped: a preference is worth a line in the log, never an exception in
/// front of the user.
/// </para>
/// </remarks>
public interface IAppSettingsStore
{
    /// <summary>The file the preferences live in, whether or not it exists yet.</summary>
    string FilePath { get; }

    /// <summary>Reads the stored preferences.</summary>
    /// <returns>What was stored, or the defaults when there is nothing readable to read.</returns>
    AppSettings Load();

    /// <summary>Writes the preferences, replacing whatever was there.</summary>
    /// <param name="settings">What to store.</param>
    void Save(AppSettings settings);
}
