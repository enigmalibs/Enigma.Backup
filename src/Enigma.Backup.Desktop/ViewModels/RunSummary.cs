using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Enigma.Backup.Core.Backups;
using Enigma.Backup.Core.Errors;
using Enigma.Backup.Core.Restoring;
using Enigma.Backup.Core.Verification;

namespace Enigma.Backup.Desktop.ViewModels;

/// <summary>
/// The end-of-run panel: one headline, a handful of labelled figures, and — prominently — everything
/// that was left out.
/// </summary>
/// <remarks>
/// The core already writes the sentences it wants shown (a <see cref="SkippedEntry.Reason"/>, a
/// <see cref="VerificationResult.Message"/>), so this does not paraphrase them; it arranges them.
/// The whole type is immutable and built by the static factories below, which is what makes "did the
/// summary come out right?" a test that needs no window.
/// </remarks>
public sealed class RunSummary
{
    private RunSummary(RunSummaryKind kind, string headline)
    {
        Kind = kind;
        Headline = headline;
    }

    /// <summary>How the run ended.</summary>
    public RunSummaryKind Kind { get; }

    /// <summary>The one sentence that says what happened.</summary>
    public string Headline { get; }

    /// <summary>The labelled figures, in the order they should be read.</summary>
    public IReadOnlyList<SummaryDetail> Details { get; private init; } = [];

    /// <summary>Everything left out of the run, each with the reason — the part that must not be missed.</summary>
    public IReadOnlyList<string> Skipped { get; private init; } = [];

    /// <summary>Adjustments the run made on its own and that the user should know about.</summary>
    public IReadOnlyList<string> Warnings { get; private init; } = [];

    /// <summary>Whether anything was left out.</summary>
    public bool HasSkipped => Skipped.Count > 0;

    /// <summary>Whether anything was adjusted.</summary>
    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>Whether there are any figures to show.</summary>
    public bool HasDetails => Details.Count > 0;

    /// <summary>The heading above the skipped list, which counts what is in it.</summary>
    public string SkippedHeader => $"Left out — {DisplayFormat.Count(Skipped.Count, "entry", "entries")}";

    /// <summary>The heading above the warnings list, which counts what is in it.</summary>
    public string WarningsHeader => $"Warnings — {DisplayFormat.Count(Warnings.Count, "note", "notes")}";

    /// <summary>Whether the run succeeded outright.</summary>
    public bool IsSuccess => Kind == RunSummaryKind.Success;

    /// <summary>Whether the run finished with something to report.</summary>
    public bool IsWarning => Kind == RunSummaryKind.Warning;

    /// <summary>Whether the run did not happen.</summary>
    public bool IsFailure => Kind == RunSummaryKind.Failure;

    /// <summary>Builds the summary of a completed backup.</summary>
    /// <param name="result">What the run produced.</param>
    public static RunSummary FromBackup(BackupResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        List<string> warnings =
        [
            .. result.ScanWarnings.Select(static warning => warning.Message),
            .. result.Warnings.Select(static warning => warning.Message),
        ];

        bool clean = result.Skipped.Count == 0;

        return new RunSummary(
            clean ? RunSummaryKind.Success : RunSummaryKind.Warning,
            clean
                ? $"Backed up {DisplayFormat.Count(result.FileCount, "file", "files")} to '{Path.GetFileName(result.OutputPath)}'."
                : $"Backed up {DisplayFormat.Count(result.FileCount, "file", "files")} to '{Path.GetFileName(result.OutputPath)}', " +
                  $"leaving out {DisplayFormat.Count(result.Skipped.Count, "entry", "entries")}.")
        {
            Details =
            [
                new SummaryDetail("Archive", result.OutputPath),
                new SummaryDetail("Contents", $"{DisplayFormat.Count(result.FileCount, "file", "files")} · {DisplayFormat.Count(result.DirectoryCount, "folder", "folders")}"),
                new SummaryDetail("Read", DisplayFormat.Bytes(result.BytesProcessed)),
                new SummaryDetail("Written", DisplayFormat.Bytes(result.ArchiveBytes)),
                new SummaryDetail("Took", DisplayFormat.Duration(result.Duration)),
                new SummaryDetail("SHA-256", result.Sha256),
                new SummaryDetail("Checksum file", Path.GetFileName(result.SidecarPath)),
            ],
            Skipped = [.. result.Skipped.Select(static entry => entry.Reason)],
            Warnings = warnings,
        };
    }

    /// <summary>Builds the summary of a restore.</summary>
    /// <param name="result">What the restore produced.</param>
    public static RunSummary FromRestore(RestoreResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        bool aborted = result.Outcome == RestoreOutcome.Aborted;
        bool clean = !aborted && result.Skipped.Count == 0;

        string headline = aborted
            ? $"The restore was stopped after {DisplayFormat.Count(result.FileCount, "file", "files")}; what had already been written was kept."
            : clean
                ? $"Restored {DisplayFormat.Count(result.FileCount, "file", "files")} into '{result.DestinationDirectory}'."
                : $"Restored {DisplayFormat.Count(result.FileCount, "file", "files")} into '{result.DestinationDirectory}', " +
                  $"leaving {DisplayFormat.Count(result.Skipped.Count, "entry", "entries")} untouched.";

        return new RunSummary(clean ? RunSummaryKind.Success : RunSummaryKind.Warning, headline)
        {
            Details =
            [
                new SummaryDetail("Archive", result.ArchivePath),
                new SummaryDetail("Restored into", result.DestinationDirectory),
                new SummaryDetail("Contents", $"{DisplayFormat.Count(result.FileCount, "file", "files")} · {DisplayFormat.Count(result.DirectoryCount, "folder", "folders")}"),
                new SummaryDetail("Written", DisplayFormat.Bytes(result.BytesRestored)),
                new SummaryDetail("Took", DisplayFormat.Duration(result.Duration)),
            ],
            Skipped = [.. result.Skipped.Select(static entry => entry.Reason)],
        };
    }

    /// <summary>Builds the summary of a verification.</summary>
    /// <param name="result">What the verification found.</param>
    public static RunSummary FromVerification(VerificationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        RunSummaryKind kind = result.Outcome switch
        {
            VerificationOutcome.Succeeded => RunSummaryKind.Success,
            VerificationOutcome.HashMismatch or VerificationOutcome.AuthenticationFailed => RunSummaryKind.Failure,
            _ => RunSummaryKind.Warning,
        };

        List<SummaryDetail> details =
        [
            new SummaryDetail("Archive", result.ArchivePath),
            new SummaryDetail("Read", DisplayFormat.Bytes(result.BytesRead)),
            new SummaryDetail("Took", DisplayFormat.Duration(result.Duration)),
        ];

        if (result.ExpectedSha256 is string expected)
        {
            details.Add(new SummaryDetail("Expected SHA-256", expected));
        }

        if (result.ActualSha256 is string actual)
        {
            details.Add(new SummaryDetail("Computed SHA-256", actual));
        }

        if (result.IsEncrypted)
        {
            details.Add(new SummaryDetail("Encrypted contents", result.PayloadAuthenticated switch
            {
                true => "every block passed its authentication check",
                false => "a block failed its authentication check",
                _ => "not checked — no password was given",
            }));
        }

        // The core writes this sentence for display; it says both what was found and what it means.
        return new RunSummary(kind, result.Message) { Details = details };
    }

    /// <summary>Builds the summary of a run the user cancelled.</summary>
    /// <param name="what">What was cancelled, e.g. "backup".</param>
    /// <param name="discarded">Whether a partial output file was discarded with it.</param>
    public static RunSummary Cancelled(string what, bool discarded) =>
        new(RunSummaryKind.Warning,
            discarded
                ? $"The {what} was cancelled; the partial archive was deleted."
                : $"The {what} was cancelled.");

    /// <summary>Builds the summary of a backup stopped by an Abort answer.</summary>
    /// <param name="reason">The core's explanation of what was being handled when it stopped.</param>
    public static RunSummary Aborted(string reason) =>
        new(RunSummaryKind.Warning, $"The backup was stopped and the partial archive deleted. {reason}");

    /// <summary>Builds the summary of a run that failed outright.</summary>
    /// <param name="what">What failed, e.g. "backup".</param>
    /// <param name="reason">The failure's message, ready to display.</param>
    public static RunSummary Failed(string what, string reason) =>
        new(RunSummaryKind.Failure, $"The {what} failed: {reason}");
}
