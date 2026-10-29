namespace Enigma.Backup.Desktop.Services;

/// <summary>What the safety net does with one exception.</summary>
public enum UnhandledErrorDecision
{
    /// <summary>Nothing to show: a cancellation someone asked for, or an exception already offered.</summary>
    Ignore,

    /// <summary>Shown less than <see cref="UnhandledErrorPolicy.RepeatWindow"/> ago: logged, not shown again.</summary>
    Suppress,

    /// <summary>The first error of a new dialog.</summary>
    Queue,

    /// <summary>Counted into the dialog already waiting or on screen.</summary>
    Merge,
}
