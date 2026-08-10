using System;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Enigma.Backup.Core.Scanning;

/// <summary>
/// Matches what is inside a pattern input's base folder against its glob — the include direction of
/// the same machinery <see cref="GlobExcluder"/> uses to leave things out.
/// </summary>
/// <remarks>
/// <para>
/// One rulebook per window: the glob language and the case sensitivity are the exclude field's, so
/// <c>*</c> matches within one path segment, <c>**</c> at any depth, and two paths differing only in
/// case are the same entry exactly where the file system says they are
/// (<see cref="PathUtilities.Comparison"/>).
/// </para>
/// <para>
/// <see cref="Matches"/> is asked about directories as well as files, and the scan gives a matching
/// directory its whole subtree. That is the one documented departure from strict file globbing: it is
/// what a pattern such as <c>DirPrefix*</c> is asking for, where strict semantics would match nothing
/// at all when only directories carry the prefix.
/// </para>
/// </remarks>
internal sealed class PatternMatcher
{
    private readonly Matcher? _matcher;

    private PatternMatcher(Matcher? matcher) => _matcher = matcher;

    /// <summary>
    /// Whether the pattern can match anything at all. A blank pattern cannot, and is reported as a
    /// pattern that matched nothing rather than refused — the profile is the user's to fix.
    /// </summary>
    public bool MatchesAnything => _matcher is not null;

    /// <summary>Builds a matcher for one pattern.</summary>
    /// <param name="pattern">The glob, relative to the base folder; <c>\</c> is accepted for <c>/</c>.</param>
    /// <returns>A matcher, which matches nothing when the pattern is blank.</returns>
    public static PatternMatcher Create(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return new PatternMatcher(null);
        }

        Matcher matcher = new(PathUtilities.Comparison);
        matcher.AddInclude(pattern.Trim().Replace('\\', '/'));

        return new PatternMatcher(matcher);
    }

    /// <summary>Tells whether an entry is one the pattern asks for.</summary>
    /// <param name="relativePath">The entry's path relative to the base folder, with <c>/</c> separators.</param>
    public bool Matches(string relativePath) => _matcher is not null && _matcher.Match(relativePath).HasMatches;
}
