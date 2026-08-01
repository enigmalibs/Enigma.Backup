using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace Enigma.Backup.Core.Tests.TestSupport;

/// <summary>
/// Describes a directory tree as comparable text, so "the restore reproduced the input" can be one
/// assertion with a readable diff instead of a walk full of individual checks.
/// </summary>
public static class FileTree
{
    /// <summary>
    /// Snapshots every file and directory under <paramref name="root"/>: the relative path (always
    /// with <c>/</c>) mapped to a description of the entry — <c>dir</c>, or a file's length and the
    /// digest of its contents.
    /// </summary>
    /// <param name="root">The directory to describe.</param>
    public static SortedDictionary<string, string> Snapshot(string root)
    {
        SortedDictionary<string, string> snapshot = new(StringComparer.Ordinal);
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

        foreach (string path in Directory.EnumerateDirectories(full, "*", SearchOption.AllDirectories))
        {
            snapshot[Relative(full, path)] = "dir";
        }

        foreach (string path in Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories))
        {
            byte[] contents = File.ReadAllBytes(path);
            snapshot[Relative(full, path)] = $"file {contents.Length} {Convert.ToHexStringLower(SHA256.HashData(contents))}";
        }

        return snapshot;
    }

    /// <summary>The last write time of a file or directory, in UTC.</summary>
    /// <param name="path">The path to read.</param>
    public static DateTimeOffset LastWriteTime(string path) =>
        Directory.Exists(path) ? Directory.GetLastWriteTimeUtc(path) : File.GetLastWriteTimeUtc(path);

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
}
