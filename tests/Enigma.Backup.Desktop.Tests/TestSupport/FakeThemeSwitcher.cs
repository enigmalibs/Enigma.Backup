using System.Collections.Generic;
using Enigma.Backup.Desktop.Services;
using Enigma.Backup.Desktop.Settings;

namespace Enigma.Backup.Desktop.Tests.TestSupport;

/// <summary>An <see cref="IThemeSwitcher"/> that records what it was asked to paint the app in.</summary>
internal sealed class FakeThemeSwitcher : IThemeSwitcher
{
    /// <summary>Every theme that was applied, in order.</summary>
    public List<AppTheme> Applied { get; } = [];

    /// <inheritdoc />
    public void Apply(AppTheme theme) => Applied.Add(theme);
}
