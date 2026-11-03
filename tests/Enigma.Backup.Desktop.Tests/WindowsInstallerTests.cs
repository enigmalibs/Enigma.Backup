using System;
using System.IO;
using Xunit;

namespace Enigma.Backup.Desktop.Tests;

/// <summary>The identity of the Windows installer that <c>packaging/windows/buildmsi.ps1</c> builds.</summary>
/// <remarks>
/// The script derives everything else — the install folder, the shortcuts, the publish folder and the
/// executable they start — from the three lines pinned here, and nothing in the build reads it: a typo in
/// any of them would only show once an MSI was built, or worse, once it was installed.
/// </remarks>
public sealed class WindowsInstallerTests
{
    /// <summary>
    /// The product line's identity in Windows Installer, drawn once for the first MSI (1.4.0). An MSI that
    /// carries the installed one's upgrade code replaces it; one that does not installs beside it, so every
    /// later MSI must keep this one.
    /// </summary>
    private const string UpgradeCode = "d60f30d6-32d0-4629-ba7d-f98eb9731a91";

    [Fact]
    public void TheInstallerKeepsTheUpgradeCodeOfTheProductLine() =>
        Assert.Contains($"$UpgradeCode = '{UpgradeCode}'", Script());

    [Fact]
    public void TheInstallerIsNamedEnigmaBackup() =>
        Assert.Contains("$ProductName = 'Enigma Backup'", Script());

    [Fact]
    public void TheInstallerPackagesAndStartsTheExecutableOfTheAssembly()
    {
        string? assembly = typeof(App).Assembly.GetName().Name;

        Assert.Contains($"$Assembly = '{assembly}'", Script());
    }

    private static string[] Script() =>
        File.ReadAllLines(Path.Combine(Root(), "packaging", "windows", "buildmsi.ps1"));

    /// <summary>The repository's root: the first directory above the test's output that holds the solution.</summary>
    private static string Root()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Enigma.Backup.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"No Enigma.Backup.slnx above {AppContext.BaseDirectory}.");
    }
}
