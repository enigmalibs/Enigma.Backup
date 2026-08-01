using System;
using System.Collections.Generic;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Enigma.Backup.Core.Scanning;

/// <summary>
/// Applies a profile's exclude globs to paths relative to the input they were found under.
/// </summary>
/// <remarks>
/// A pattern ending in <c>/**</c> also excludes the directory it names — <c>**/bin/**</c> is meant
/// to drop <c>bin</c> and everything in it, not to leave an empty <c>bin</c> behind in the archive.
/// Matching is case-insensitive only where the file system is.
/// </remarks>
internal sealed class GlobExcluder
{
    private const string RecursiveSuffix = "/**";

    private static readonly GlobExcluder EmptyExcluder = new(null);

    private readonly Matcher? _matcher;

    private GlobExcluder(Matcher? matcher) => _matcher = matcher;

    /// <summary>Builds an excluder from a profile's patterns.</summary>
    /// <param name="globs">The patterns; blank entries are ignored, <c>\</c> is accepted for <c>/</c>.</param>
    /// <returns>An excluder that matches nothing when no usable pattern was supplied.</returns>
    public static GlobExcluder Create(IEnumerable<string>? globs)
    {
        if (globs is null)
        {
            return EmptyExcluder;
        }

        Matcher matcher = new(PathUtilities.Comparison);
        bool any = false;

        foreach (string glob in globs)
        {
            if (string.IsNullOrWhiteSpace(glob))
            {
                continue;
            }

            string pattern = glob.Trim().Replace('\\', '/');
            matcher.AddInclude(pattern);
            any = true;

            if (pattern.Length > RecursiveSuffix.Length && pattern.EndsWith(RecursiveSuffix, StringComparison.Ordinal))
            {
                matcher.AddInclude(pattern[..^RecursiveSuffix.Length]);
            }
        }

        return any ? new GlobExcluder(matcher) : EmptyExcluder;
    }

    /// <summary>Tells whether an entry is excluded.</summary>
    /// <param name="relativePath">The entry's path relative to its input root, with <c>/</c> separators.</param>
    public bool IsExcluded(string relativePath) => _matcher is not null && _matcher.Match(relativePath).HasMatches;
}
