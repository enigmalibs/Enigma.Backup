using System;
using Enigma.Backup.Core.Profiles;
using Xunit;

namespace Enigma.Backup.Core.Tests.Profiles;

public sealed class ProfileNameTests
{
    private const char Bell = (char)7;

    [Theory]
    [InlineData("Documents")]
    [InlineData("my backup 2026")]
    [InlineData("photos.2026")]
    [InlineData("_")]
    public void IsValid_AcceptsUsableNames(string name) => Assert.True(ProfileName.IsValid(name));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../evil")]
    [InlineData("sub/profile")]
    [InlineData(@"sub\profile")]
    [InlineData("C:profile")]
    [InlineData("what?")]
    [InlineData("star*")]
    [InlineData("pipe|name")]
    [InlineData("quote\"name")]
    [InlineData("trailing.")]
    [InlineData(" leading")]
    [InlineData("trailing ")]
    [InlineData("CON")]
    [InlineData("nul.txt")]
    [InlineData("com1")]
    public void IsValid_RejectsUnusableNames(string? name) => Assert.False(ProfileName.IsValid(name));

    [Fact]
    public void IsValid_RejectsControlCharacters() => Assert.False(ProfileName.IsValid("control" + Bell + "character"));

    [Fact]
    public void IsValid_RejectsOverlongNames() => Assert.False(ProfileName.IsValid(new string('a', ProfileName.MaxLength + 1)));

    [Fact]
    public void EnsureValid_ReturnsTheNameWhenItIsUsable() => Assert.Equal("Documents", ProfileName.EnsureValid("Documents"));

    [Fact]
    public void EnsureValid_ThrowsForAPathTraversalAttempt() =>
        Assert.Throws<ArgumentException>(() => ProfileName.EnsureValid("../../etc/passwd"));

    [Theory]
    [InlineData("Docs/2026", "Docs_2026")]
    [InlineData("what?", "what_")]
    [InlineData("  padded  ", "padded")]
    [InlineData("trailing.", "trailing")]
    [InlineData("CON", "_CON")]
    public void Sanitize_ProducesTheExpectedName(string input, string expected) =>
        Assert.Equal(expected, ProfileName.Sanitize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("...")]
    public void Sanitize_FallsBackWhenNothingUsableRemains(string? input) =>
        Assert.Equal(ProfileName.Fallback, ProfileName.Sanitize(input));

    [Fact]
    public void Sanitize_AlwaysProducesAValidName()
    {
        string[] inputs = ["", "?", new string('x', 500), "..", "LPT9", "  a  ", @"a/b\c:d*e", "bell" + Bell];

        foreach (string input in inputs)
        {
            Assert.True(ProfileName.IsValid(ProfileName.Sanitize(input)), $"'{input}' sanitized to an invalid name.");
        }
    }
}
