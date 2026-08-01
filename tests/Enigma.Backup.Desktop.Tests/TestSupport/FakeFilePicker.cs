using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Backup.Desktop.Services;

namespace Enigma.Backup.Desktop.Tests.TestSupport;

/// <summary>
/// An <see cref="IFilePicker"/> that answers from a queue and records what it was asked.
/// </summary>
/// <remarks>
/// An empty queue means "the picker was dismissed", so a test that forgets to script an answer sees
/// the command do nothing rather than quietly pick something.
/// </remarks>
internal sealed class FakeFilePicker : IFilePicker
{
    /// <summary>What <see cref="PickInputFilesAsync"/> returns, in order.</summary>
    public Queue<IReadOnlyList<string>> InputFiles { get; } = new();

    /// <summary>What <see cref="PickFolderAsync"/> returns, in order.</summary>
    public Queue<string?> Folders { get; } = new();

    /// <summary>What <see cref="PickArchiveAsync"/> returns, in order.</summary>
    public Queue<string?> Archives { get; } = new();

    /// <summary>What <see cref="PickProfileToImportAsync"/> returns, in order.</summary>
    public Queue<string?> ProfileImports { get; } = new();

    /// <summary>What <see cref="PickProfileExportPathAsync"/> returns, in order.</summary>
    public Queue<string?> ProfileExports { get; } = new();

    /// <summary>The titles the folder picker was opened with.</summary>
    public List<string> FolderTitles { get; } = [];

    /// <summary>The folders the folder picker was asked to start at.</summary>
    public List<string?> FolderStarts { get; } = [];

    /// <summary>The titles the archive picker was opened with.</summary>
    public List<string> ArchiveTitles { get; } = [];

    /// <summary>The file names the export picker was offered.</summary>
    public List<string> ExportSuggestions { get; } = [];

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> PickInputFilesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(InputFiles.Count > 0 ? InputFiles.Dequeue() : []);

    /// <inheritdoc />
    public Task<string?> PickFolderAsync(string title, string? startAt = null, CancellationToken cancellationToken = default)
    {
        FolderTitles.Add(title);
        FolderStarts.Add(startAt);

        return Task.FromResult(Folders.Count > 0 ? Folders.Dequeue() : null);
    }

    /// <inheritdoc />
    public Task<string?> PickArchiveAsync(string title, CancellationToken cancellationToken = default)
    {
        ArchiveTitles.Add(title);

        return Task.FromResult(Archives.Count > 0 ? Archives.Dequeue() : null);
    }

    /// <inheritdoc />
    public Task<string?> PickProfileToImportAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ProfileImports.Count > 0 ? ProfileImports.Dequeue() : null);

    /// <inheritdoc />
    public Task<string?> PickProfileExportPathAsync(string suggestedFileName, CancellationToken cancellationToken = default)
    {
        ExportSuggestions.Add(suggestedFileName);

        return Task.FromResult(ProfileExports.Count > 0 ? ProfileExports.Dequeue() : null);
    }
}
