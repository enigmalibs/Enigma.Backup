using System;
using System.Collections.Generic;
using System.IO;

namespace Enigma.Backup.Core.Tests.TestSupport;

/// <summary>
/// The checked-in archive written by an earlier release, and the tree it was made from.
/// </summary>
/// <remarks>
/// The files live in <c>Fixtures/</c> and are copied beside the test assembly by the project's
/// <c>Content</c> item; <c>Fixtures/README.md</c> records how they were produced and why they must not
/// be regenerated. Everything here is deliberately public knowledge, password included.
/// </remarks>
public static class GoldenFixture
{
    /// <summary>The password the fixture was written with — a throwaway constant, secret from nobody.</summary>
    public const string Password = "golden-fixture-password";

    private const string ArchiveName = "Golden_2026-08-01_120000.tar.zst.enc";

    /// <summary>The directory holding the fixture, beside the test assembly.</summary>
    public static string Directory { get; } = Path.Combine(AppContext.BaseDirectory, "Fixtures");

    /// <summary>The encrypted archive.</summary>
    public static string ArchivePath { get; } = Path.Combine(Directory, ArchiveName);

    /// <summary>The <c>.sha256</c> sidecar written beside it.</summary>
    public static string SidecarPath { get; } = ArchivePath + ".sha256";

    /// <summary>
    /// The tree the archive was made from, in the shape <see cref="FileTree.Snapshot"/> produces, so a
    /// restore can be compared against it with one assertion.
    /// </summary>
    public static SortedDictionary<string, string> ExpectedTree()
    {
        SortedDictionary<string, string> tree = new(StringComparer.Ordinal);

        foreach (string line in File.ReadAllLines(Path.Combine(Directory, "source-tree.txt")))
        {
            if (line.Length == 0)
            {
                continue;
            }

            string[] parts = line.Split('\t', 2);
            tree[parts[0]] = parts[1];
        }

        return tree;
    }
}
