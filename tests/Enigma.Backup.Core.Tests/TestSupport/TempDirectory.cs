using System;
using System.IO;

namespace Enigma.Backup.Core.Tests.TestSupport;

/// <summary>A throw-away directory tree for one test, removed again on dispose.</summary>
public sealed class TempDirectory : IDisposable
{
    /// <summary>Creates and enters a uniquely named directory under the system temp folder.</summary>
    public TempDirectory()
    {
        FullPath = Path.Combine(Path.GetTempPath(), "enigma-backup-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(FullPath);
    }

    /// <summary>The absolute path of the temporary directory.</summary>
    public string FullPath { get; }

    /// <summary>Turns a <c>/</c>-separated relative path into an absolute path inside this directory.</summary>
    /// <param name="relativePath">The relative path.</param>
    public string Resolve(string relativePath) =>
        Path.Combine(FullPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Creates a directory (and its parents) inside this one.</summary>
    /// <param name="relativePath">The relative path to create.</param>
    /// <returns>The absolute path of the created directory.</returns>
    public string CreateDirectory(string relativePath)
    {
        string path = Resolve(relativePath);
        Directory.CreateDirectory(path);

        return path;
    }

    /// <summary>Writes a file (creating its parents) inside this directory.</summary>
    /// <param name="relativePath">The relative path to write.</param>
    /// <param name="contents">The text to write.</param>
    /// <returns>The absolute path of the written file.</returns>
    public string WriteFile(string relativePath, string contents = "x")
    {
        string path = Resolve(relativePath);
        string? parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        File.WriteAllText(path, contents);

        return path;
    }

    /// <summary>Removes the directory and everything in it.</summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(FullPath, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a test over.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
