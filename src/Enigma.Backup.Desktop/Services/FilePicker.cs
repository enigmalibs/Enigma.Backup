using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Enigma.Avalonia.Desktop.Services;

namespace Enigma.Backup.Desktop.Services;

/// <summary>The <see cref="IFilePicker"/> over the library's file and folder dialog services.</summary>
/// <remarks>
/// <para>
/// The string-path overloads are used throughout: they turn every result back into a local path with
/// <c>TryGetLocalPath</c> and drop anything without one, which is exactly the rule this app needs —
/// the core works on the file system, so a location that is not on it (a cloud document provider,
/// say) is not something a backup could read anyway.
/// </para>
/// <para>
/// This is one of the two places allowed to name the control library's services; the view models see
/// only <see cref="IFilePicker"/> and deal in plain paths.
/// </para>
/// </remarks>
public sealed class FilePicker : IFilePicker
{
    private static readonly FilePickerFileType ArchiveFileType = new("Enigma.Backup archives")
    {
        Patterns = ["*.tar.zst", "*.tar.gz", "*.zip", "*.tar.zst.enc", "*.tar.gz.enc", "*.enc"],
    };

    private static readonly FilePickerFileType ProfileFileType = new("Backup profiles")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"],
    };

    private readonly IFileDialogService _files;
    private readonly IFolderDialogService _folders;

    /// <summary>Creates a picker over the library's dialog services.</summary>
    /// <param name="files">Opens and saves files; given its storage provider by <see cref="App"/>.</param>
    /// <param name="folders">Picks folders; given its storage provider by <see cref="App"/>.</param>
    public FilePicker(IFileDialogService files, IFolderDialogService folders)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(folders);

        _files = files;
        _folders = folders;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> PickInputFilesAsync(CancellationToken cancellationToken = default)
    {
        if (!IsReady)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<string> paths = await _files.ShowOpenFileDialogAsync(
            title: "Add files to back up",
            allowMultiple: true);

        return [.. paths];
    }

    /// <inheritdoc />
    public async Task<string?> PickFolderAsync(string title, string? startAt = null, CancellationToken cancellationToken = default)
    {
        if (!IsReady)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<string> paths = await _folders.ShowOpenFolderDialogAsync(
            title: title,
            allowMultiple: false,
            suggestedStartLocation: AsStartLocation(startAt));

        return paths.FirstOrDefault();
    }

    /// <inheritdoc />
    public Task<string?> PickArchiveAsync(string title, CancellationToken cancellationToken = default) =>
        PickOneFileAsync(title, ArchiveFileType, cancellationToken);

    /// <inheritdoc />
    public Task<string?> PickProfileToImportAsync(CancellationToken cancellationToken = default) =>
        PickOneFileAsync("Import a backup profile", ProfileFileType, cancellationToken);

    /// <inheritdoc />
    public async Task<string?> PickProfileExportPathAsync(string suggestedFileName, CancellationToken cancellationToken = default)
    {
        if (!IsReady)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        return await _files.ShowSaveFileDialogAsync(
            title: "Export the backup profile",
            suggestedFileName: suggestedFileName,
            defaultExtension: "json",
            showOverwritePrompt: true,
            fileTypeChoices: [ProfileFileType]);
    }

    /// <summary>
    /// Whether the pickers have a window behind them yet. Both services throw until <see cref="App"/>
    /// hands them the window's storage provider, and this is the readiness they expose for it; before
    /// that moment there is nothing on screen to have asked for a file.
    /// </summary>
    private bool IsReady => _files.StorageProvider is not null && _folders.StorageProvider is not null;

    private async Task<string?> PickOneFileAsync(string title, FilePickerFileType fileType, CancellationToken cancellationToken)
    {
        if (!IsReady)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<string> paths = await _files.ShowOpenFileDialogAsync(
            title: title,
            allowMultiple: false,
            fileTypeFilter: [fileType, FilePickerFileTypes.All]);

        return paths.FirstOrDefault();
    }

    /// <summary>Keeps a hint that the platform would refuse from failing the picker.</summary>
    /// <param name="path">Where the picker should open, as the user last typed it.</param>
    /// <returns>The path when it can be resolved, or <see langword="null"/> to open at the default.</returns>
    /// <remarks>
    /// The library resolves the hint with <c>TryGetFolderFromPathAsync</c>, which throws on a relative
    /// path rather than shrugging — and the folder this is called with is whatever is in the
    /// destination box. Where the picker opens is not worth an exception.
    /// </remarks>
    private static string? AsStartLocation(string? path) =>
        !string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path) ? path : null;
}
