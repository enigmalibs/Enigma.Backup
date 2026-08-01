using System;
using System.IO;

namespace Enigma.Backup.Desktop.Tests.TestSupport;

/// <summary>A directory that exists for the length of a test and is removed with it.</summary>
internal sealed class TempDirectory : IDisposable
{
    /// <summary>Creates a fresh directory under the system temporary folder.</summary>
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "enigma-backup-desktop-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>The directory's full path.</summary>
    public string Path { get; }

    /// <summary>Builds a path inside the directory, creating the parents it needs.</summary>
    /// <param name="segments">The path segments below this directory.</param>
    public string Combine(params string[] segments)
    {
        string combined = System.IO.Path.Combine([Path, .. segments]);
        string? parent = System.IO.Path.GetDirectoryName(combined);

        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        return combined;
    }

    /// <summary>Writes a file inside the directory, creating the folders above it.</summary>
    /// <param name="relativePath">Where to put it, relative to this directory.</param>
    /// <param name="contents">What to write.</param>
    /// <returns>The file's full path.</returns>
    public string WriteFile(string relativePath, string contents)
    {
        string path = Combine(relativePath);
        File.WriteAllText(path, contents);

        return path;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A leftover temporary directory is not worth failing a passing test over.
        }
    }
}
