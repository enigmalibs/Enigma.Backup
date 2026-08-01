namespace Enigma.Backup.Desktop.ViewModels;

/// <summary>How a run ended, which decides how its summary is presented.</summary>
public enum RunSummaryKind
{
    /// <summary>Everything asked for happened.</summary>
    Success,

    /// <summary>It finished, but something was left out, stopped early, or could not be checked.</summary>
    Warning,

    /// <summary>It did not happen.</summary>
    Failure,
}
