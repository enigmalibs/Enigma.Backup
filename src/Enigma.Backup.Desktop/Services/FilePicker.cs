using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Enigma.Backup.Desktop.Services;

/// <summary>The <see cref="IFilePicker"/> over Avalonia's storage provider.</summary>
/// <remarks>
/// Every result is turned back into a local path with <c>TryGetLocalPath</c>, and anything without
/// one is dropped: the core works on the file system, so a location that is not on it — a cloud
/// document provider, say — is not something a backup could read anyway.
/// </remarks>
public sealed class FilePicker : IFilePicker
{
    private static readonly FilePickerFileType ArchiveFileType = new("Enigma.Backup archives")
    {
        Patterns = ["*.tar.zst", "*.tar.gz", "*.tar.zst.enc", "*.tar.gz.enc", "*.enc"],
    };

    private static readonly FilePickerFileType ProfileFileType = new("Backup profiles")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"],
    };

    private readonly IMainWindowProvider _windows;

    /// <summary>Creates a picker that opens over the app's main window.</summary>
    /// <param name="windows">Supplies the window the pickers belong to.</param>
    public FilePicker(IMainWindowProvider windows)
    {
        ArgumentNullException.ThrowIfNull(windows);

        _windows = windows;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> PickInputFilesAsync(CancellationToken cancellationToken = default)
    {
        if (GetStorageProvider() is not IStorageProvider storage)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add files to back up",
            AllowMultiple = true,
        });

        List<string> paths = [];
        foreach (IStorageFile file in files)
        {
            if (file.TryGetLocalPath() is string path)
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    /// <inheritdoc />
    public async Task<string?> PickFolderAsync(string title, string? startAt = null, CancellationToken cancellationToken = default)
    {
        if (GetStorageProvider() is not IStorageProvider storage)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<IStorageFolder> folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = await TryResolveFolderAsync(storage, startAt),
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
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
        if (GetStorageProvider() is not IStorageProvider storage)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        IStorageFile? file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export the backup profile",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "json",
            ShowOverwritePrompt = true,
            FileTypeChoices = [ProfileFileType],
        });

        return file?.TryGetLocalPath();
    }

    private async Task<string?> PickOneFileAsync(string title, FilePickerFileType fileType, CancellationToken cancellationToken)
    {
        if (GetStorageProvider() is not IStorageProvider storage)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [fileType, FilePickerFileTypes.All],
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private static async Task<IStorageFolder?> TryResolveFolderAsync(IStorageProvider storage, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return await storage.TryGetFolderFromPathAsync(path);
        }
        catch (Exception exception) when (exception is ArgumentException or UriFormatException)
        {
            // Only a hint about where to open: a path the platform will not take is not worth
            // failing the picker over.
            return null;
        }
    }

    private IStorageProvider? GetStorageProvider() => _windows.Window?.StorageProvider;
}
