using System;
using System.Collections.Generic;
using Enigma.Backup.Core.Backups;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Core.Restoring;
using Enigma.Backup.Core.Scanning;
using Enigma.Backup.Core.Verification;

namespace Enigma.Backup.Core.Tests.TestSupport;

/// <summary>
/// Builds the archive services the way <c>AddEnigmaBackupCore</c> does, for tests that exercise the
/// pipeline rather than the container.
/// </summary>
public static class Pipeline
{
    /// <summary>Creates a backup service.</summary>
    /// <param name="timeProvider">The clock to use; the system clock when omitted.</param>
    public static BackupService Backup(TimeProvider? timeProvider = null) =>
        new(new InputScanner(), timeProvider ?? TimeProvider.System);

    /// <summary>Creates a restore service.</summary>
    /// <param name="timeProvider">The clock to use; the system clock when omitted.</param>
    public static RestoreService Restore(TimeProvider? timeProvider = null) =>
        new(timeProvider ?? TimeProvider.System);

    /// <summary>Creates a verification service.</summary>
    /// <param name="timeProvider">The clock to use; the system clock when omitted.</param>
    public static VerificationService Verification(TimeProvider? timeProvider = null) =>
        new(timeProvider ?? TimeProvider.System);

    /// <summary>Builds a profile pointing at the given inputs.</summary>
    /// <param name="name">The profile name, which becomes the archive's name prefix.</param>
    /// <param name="outputDirectory">Where the archive is written.</param>
    /// <param name="compression">The compression to use.</param>
    /// <param name="inputs">The paths to back up.</param>
    public static BackupProfile Profile(string name, string outputDirectory, CompressionAlgorithm compression, params string[] inputs) =>
        new()
        {
            Name = name,
            OutputDirectory = outputDirectory,
            Compression = compression,
            Inputs = new List<string>(inputs),
        };
}
