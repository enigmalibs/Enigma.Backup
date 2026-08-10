using System;
using Xunit;

namespace Enigma.Backup.Desktop.Tests;

/// <summary>What the About section reads off the assembly.</summary>
/// <remarks>
/// The version cannot be asserted as a literal — it is whatever <c>Directory.Build.props</c> says at
/// the moment of the build, and pinning it here would mean a failing test every release. What is
/// pinned instead is the shape: the build metadata the SDK appends is gone, and nothing here throws
/// when an attribute is missing, because the About section is shown while a dialog is opening.
/// </remarks>
public sealed class AppInfoTests
{
    [Theory]
    [InlineData("1.2.0+0f6b505aa50d88f4f7483125569bcb9da7d79afd", "1.2.0")]
    [InlineData("1.2.0", "1.2.0")]
    [InlineData("1.2.0-rc.1+abc123", "1.2.0-rc.1")]
    [InlineData(" 1.2.0+abc ", "1.2.0")]
    public void TheBuildMetadataIsStrippedFromTheVersion(string informationalVersion, string expected) =>
        Assert.Equal(expected, AppInfo.ShortVersion(informationalVersion));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AVersionThatIsNotThereReadsAsNothing_RatherThanThrowing(string? informationalVersion) =>
        Assert.Empty(AppInfo.ShortVersion(informationalVersion));

    /// <remarks>
    /// A <c>+</c> with nothing before it is not a version, but it is also not a crash: the caller falls
    /// back to the assembly version, which is what "never throws on a missing attribute" means in
    /// practice.
    /// </remarks>
    [Fact]
    public void AVersionThatIsOnlyMetadataReadsAsNothing() =>
        Assert.Empty(AppInfo.ShortVersion("+abc123"));

    [Fact]
    public void TheVersionIsReadFromTheAssemblyAndCarriesNoMetadata()
    {
        Assert.NotEmpty(AppInfo.Version);

        // The whole point of the property: "1.1.0+0f6b505…" is not something to show a user.
        Assert.DoesNotContain('+', AppInfo.Version);

        // Not pinned to a literal, but it does have to look like a version — "unknown" would mean the
        // attribute chain fell through, which no shipped build does.
        Assert.StartsWith("1.", AppInfo.Version, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCopyrightIsReadFromTheAssembly()
    {
        Assert.NotEmpty(AppInfo.Copyright);
        Assert.Contains("Copyright", AppInfo.Copyright, StringComparison.Ordinal);
    }

    [Fact]
    public void TheProjectPageIsTheRepository()
    {
        Assert.Equal(Uri.UriSchemeHttps, AppInfo.ProjectPage.Scheme);
        Assert.Equal("github.com", AppInfo.ProjectPage.Host);
        Assert.Equal("/enigmalibs/Enigma.Backup", AppInfo.ProjectPage.AbsolutePath);
    }

    [Fact]
    public void TheNameIsTheOneTheWindowUses() => Assert.Equal("Enigma.Backup", AppInfo.Name);
}
