namespace Enigma.Backup.Core.Errors;

/// <summary>Classifies why a run could not process one of its entries.</summary>
public enum BackupErrorKind
{
    /// <summary>An input path recorded in the profile does not exist (any more).</summary>
    MissingInput,

    /// <summary>The operating system refused access to a file or directory.</summary>
    AccessDenied,

    /// <summary>A file or directory exists but could not be read.</summary>
    ReadFailed,
}
