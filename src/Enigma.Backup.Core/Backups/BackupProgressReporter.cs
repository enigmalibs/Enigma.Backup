using System;

namespace Enigma.Backup.Core.Backups;

/// <summary>
/// Turns the pipeline's byte-by-byte activity into progress reports a UI can keep up with.
/// </summary>
/// <remarks>
/// Reports are forced at every file boundary and throttled in between: one further report per
/// <see cref="ReportInterval"/> of file content. The throttle is measured in bytes rather than time,
/// so a run's reports do not depend on how fast the machine is — and neither do the tests.
/// </remarks>
internal sealed class BackupProgressReporter
{
    /// <summary>How much file content passes between two mid-file reports.</summary>
    public const long ReportInterval = 1024 * 1024;

    private readonly IProgress<BackupProgress>? _progress;
    private long _bytesAtLastReport;
    private string? _currentPath;

    /// <summary>Creates a reporter feeding <paramref name="progress"/>.</summary>
    /// <param name="progress">The receiver, or <see langword="null"/> when nobody is listening.</param>
    public BackupProgressReporter(IProgress<BackupProgress>? progress)
    {
        _progress = progress;
    }

    /// <summary>How many files have been written.</summary>
    public int FilesProcessed { get; private set; }

    /// <summary>How many bytes of file content have been written.</summary>
    public long BytesProcessed { get; private set; }

    /// <summary>The number of files the scan found.</summary>
    public int FileCount { get; private set; }

    /// <summary>The number of content bytes the scan found.</summary>
    public long TotalBytes { get; private set; }

    /// <summary>Reports that the scan is running, before any total is known.</summary>
    public void ReportScanning() => Report(BackupPhase.Scanning, path: null);

    /// <summary>Records the scan's totals, which the percentage is measured against.</summary>
    /// <param name="fileCount">How many files the scan found.</param>
    /// <param name="totalBytes">How many content bytes the scan found.</param>
    public void SetTotals(int fileCount, long totalBytes)
    {
        FileCount = fileCount;
        TotalBytes = totalBytes;
    }

    /// <summary>Reports that a file's entry has started.</summary>
    /// <param name="path">The file being written.</param>
    public void StartFile(string path)
    {
        _currentPath = path;
        Report(BackupPhase.Archiving, path);
    }

    /// <summary>Adds bytes read from the current file, reporting when the interval has passed.</summary>
    /// <param name="count">How many bytes were read.</param>
    public void AddBytes(int count)
    {
        BytesProcessed += count;

        if (BytesProcessed - _bytesAtLastReport >= ReportInterval)
        {
            Report(BackupPhase.Archiving, _currentPath);
        }
    }

    /// <summary>Reports that the current file's entry is complete.</summary>
    public void CompleteFile()
    {
        FilesProcessed++;
        Report(BackupPhase.Archiving, _currentPath);
    }

    /// <summary>Reports that the run has finished.</summary>
    public void ReportCompleted()
    {
        _currentPath = null;
        Report(BackupPhase.Completed, path: null);
    }

    private void Report(BackupPhase phase, string? path)
    {
        _bytesAtLastReport = BytesProcessed;
        _progress?.Report(new BackupProgress(phase, path, FilesProcessed, FileCount, BytesProcessed, TotalBytes));
    }
}
