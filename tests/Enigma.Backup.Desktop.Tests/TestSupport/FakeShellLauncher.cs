using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Enigma.Backup.Desktop.Services;

namespace Enigma.Backup.Desktop.Tests.TestSupport;

/// <summary>An <see cref="IShellLauncher"/> that records what it was asked to open instead of opening it.</summary>
/// <remarks>
/// The real one needs a window, so this stands in for it everywhere — including in the end-to-end
/// container, where nothing calls it but the seam still has to resolve to something harmless.
/// </remarks>
internal sealed class FakeShellLauncher : IShellLauncher
{
    /// <summary>Every folder it was asked to open, in order.</summary>
    public List<string> Folders { get; } = [];

    /// <summary>Every link it was asked to open, in order.</summary>
    public List<Uri> Uris { get; } = [];

    /// <inheritdoc />
    public Task OpenFolderAsync(string path)
    {
        Folders.Add(path);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OpenUriAsync(Uri uri)
    {
        Uris.Add(uri);

        return Task.CompletedTask;
    }
}
