using System;
using System.IO;
using Xunit;

namespace Enigma.Backup.Desktop.Tests;

/// <summary>The icon written beside the executable, for shortcuts and launchers to point at.</summary>
/// <remarks>
/// The Desktop project copies it to its output, and a copied item flows on to every project that
/// references it — so it lands beside this test assembly exactly as it lands beside the app. Compared
/// with the source asset byte for byte: a copy that drifted from the embedded icon would put one icon on
/// a shortcut and another on the window.
/// </remarks>
public sealed class AppIconTests
{
    [Fact]
    public void TheIconSitsBesideTheExecutableAsAppIco()
    {
        string copy = Path.Combine(AppContext.BaseDirectory, "app.ico");

        Assert.True(File.Exists(copy), $"Expected the app icon at {copy}.");
        Assert.Equal(File.ReadAllBytes(SourceAsset()), File.ReadAllBytes(copy));
    }

    /// <summary>The asset the copy is made from, found by walking up to the repository root.</summary>
    private static string SourceAsset()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, "src", "Enigma.Backup.Desktop", "Assets", "enigma-backup.ico");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"No src/Enigma.Backup.Desktop/Assets/enigma-backup.ico above {AppContext.BaseDirectory}.");
    }
}
