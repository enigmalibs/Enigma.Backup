using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Enigma.Backup.Core.Encryption;
using Enigma.Backup.Core.Hosting;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Desktop.Hosting;
using Enigma.Backup.Desktop.Services;
using Enigma.Backup.Desktop.Tests.TestSupport;
using Enigma.Backup.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Enigma.Backup.Desktop.Tests.Integration;

/// <summary>
/// The window's acceptance criterion, minus the pixels: back up, verify and restore for real.
/// </summary>
/// <remarks>
/// Everything here is the shipping wiring — the container built by <c>AddEnigmaBackupCore</c> and
/// <c>AddEnigmaBackupDesktop</c>, the real profile store, the real streaming pipeline, real files on
/// disk. Only the seams that would need a window on screen are replaced: the pickers answer with
/// paths, the dialogs answer with a password, the run card and the outcome banner are recorded rather
/// than drawn, and the dispatcher runs work where it stands. What is left untested is the click itself.
/// </remarks>
public sealed class WindowEndToEndTests
{
    [Theory]
    [InlineData(CompressionAlgorithm.Zstd, false)]
    [InlineData(CompressionAlgorithm.Gzip, false)]
    [InlineData(CompressionAlgorithm.Zstd, true)]
    [InlineData(CompressionAlgorithm.Gzip, true)]
    public async Task AProfileIsSaved_BackedUp_Verified_AndRestoredByteForByte(CompressionAlgorithm compression, bool encrypt)
    {
        using TempDirectory temp = new();

        string source = temp.Combine("source");
        Directory.CreateDirectory(source);
        temp.WriteFile(Path.Combine("source", "notes", "a.txt"), "The quick brown fox.");
        temp.WriteFile(Path.Combine("source", "notes", "b.md"), "# A heading\n\nAnd a paragraph.\n");
        temp.WriteFile(Path.Combine("source", "deep", "nested", "c.dat"), new string('x', 40_000));
        temp.WriteFile(Path.Combine("source", "build", "junk.o"), "compiled junk");
        Directory.CreateDirectory(temp.Combine("source", "empty-folder"));

        string output = temp.Combine("out");
        string restored = temp.Combine("restored");
        Directory.CreateDirectory(output);

        Fixture fixture = new(temp.Combine("profiles"));

        // Build the profile the way the window does, then save it through the real store.
        fixture.Editor.AddInputs([source]);
        fixture.Editor.OutputDirectory = output;
        fixture.Editor.SelectedCompression = CompressionOption.For(compression);
        fixture.Editor.Encrypt = encrypt;
        fixture.Editor.NewExcludeGlob = "**/build/**";
        fixture.Editor.AddExcludeCommand.Execute(null);

        fixture.Dialogs.ProfileNames.Enqueue("Documents");
        await fixture.ViewModel.SaveProfileCommand.ExecuteAsync(null);

        Assert.True(File.Exists(Path.Combine(fixture.ProfilesDirectory, "Documents.json")));
        Assert.Equal("Documents", fixture.ViewModel.SelectedProfileName);
        Assert.False(fixture.Editor.IsDirty);

        // Back up.
        if (encrypt)
        {
            fixture.Dialogs.Passwords.Enqueue("correct horse battery staple");
        }

        await fixture.ViewModel.BackupCommand.ExecuteAsync(null);

        RunSummary backup = Assert.IsType<RunSummary>(fixture.ViewModel.Summary);
        Assert.True(backup.IsSuccess, backup.Headline);

        string archive = Assert.Single(Directory.GetFiles(output), static path => !path.EndsWith(".sha256", StringComparison.Ordinal));
        string expectedExtension = (compression == CompressionAlgorithm.Zstd ? ".tar.zst" : ".tar.gz") + (encrypt ? ".enc" : string.Empty);

        Assert.EndsWith(expectedExtension, archive, StringComparison.Ordinal);
        Assert.StartsWith("Documents_", Path.GetFileName(archive), StringComparison.Ordinal);
        Assert.True(File.Exists(archive + ".sha256"));

        // The exclude pattern was honoured: the archive is smaller than everything on disk, and the
        // restored tree below is what actually proves what is in it.
        Assert.Contains(backup.Details, detail => detail is { Label: "Archive" } && detail.Value == archive);

        // Verify.
        fixture.FilePicker.Archives.Enqueue(archive);
        if (encrypt)
        {
            fixture.Dialogs.Passwords.Enqueue("correct horse battery staple");
        }

        await fixture.ViewModel.VerifyCommand.ExecuteAsync(null);

        RunSummary verification = Assert.IsType<RunSummary>(fixture.ViewModel.Summary);
        Assert.True(verification.IsSuccess, verification.Headline);

        if (encrypt)
        {
            Assert.Contains(
                verification.Details,
                detail => detail is { Label: "Encrypted contents", Value: "every block passed its authentication check" });
        }

        // Restore.
        fixture.FilePicker.Archives.Enqueue(archive);
        fixture.FilePicker.Folders.Enqueue(restored);
        if (encrypt)
        {
            fixture.Dialogs.Passwords.Enqueue("correct horse battery staple");
        }

        await fixture.ViewModel.RestoreCommand.ExecuteAsync(null);

        RunSummary restore = Assert.IsType<RunSummary>(fixture.ViewModel.Summary);
        Assert.True(restore.IsSuccess, restore.Headline);

        // What came back is what went in, minus what the profile excluded.
        Dictionary<string, byte[]> before = ReadTree(source);
        Dictionary<string, byte[]> after = ReadTree(Path.Combine(restored, "source"));

        before.Remove(Path.Combine("build", "junk.o"));

        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
        foreach ((string relativePath, byte[] contents) in before)
        {
            Assert.Equal(contents, after[relativePath]);
        }

        Assert.True(Directory.Exists(Path.Combine(restored, "source", "empty-folder")));
        Assert.Empty(fixture.Dialogs.Messages);
    }

    [Fact]
    public async Task AnEncryptedArchiveWillNotOpenWithTheWrongPassword()
    {
        using TempDirectory temp = new();

        string source = temp.Combine("source");
        Directory.CreateDirectory(source);
        temp.WriteFile(Path.Combine("source", "a.txt"), "Something worth protecting.");

        string output = temp.Combine("out");
        Directory.CreateDirectory(output);

        Fixture fixture = new(temp.Combine("profiles"));
        fixture.Editor.AddInputs([source]);
        fixture.Editor.OutputDirectory = output;
        fixture.Editor.Encrypt = true;

        fixture.Dialogs.Passwords.Enqueue("the right one");
        await fixture.ViewModel.BackupCommand.ExecuteAsync(null);
        Assert.True(fixture.ViewModel.Summary?.IsSuccess);

        string archive = Assert.Single(Directory.GetFiles(output), static path => !path.EndsWith(".sha256", StringComparison.Ordinal));

        // Verifying with the wrong password: the file is untouched, so the only thing left is that
        // the password is wrong — and the core's message says exactly that.
        fixture.FilePicker.Archives.Enqueue(archive);
        fixture.Dialogs.Passwords.Enqueue("the wrong one");
        await fixture.ViewModel.VerifyCommand.ExecuteAsync(null);

        RunSummary verification = Assert.IsType<RunSummary>(fixture.ViewModel.Summary);
        Assert.True(verification.IsFailure);
        Assert.Contains("the password is wrong", verification.Headline, StringComparison.Ordinal);

        // Restoring with the wrong password writes nothing.
        string restored = temp.Combine("restored");
        Directory.CreateDirectory(restored);

        fixture.FilePicker.Archives.Enqueue(archive);
        fixture.FilePicker.Folders.Enqueue(restored);
        fixture.Dialogs.Passwords.Enqueue("the wrong one");
        await fixture.ViewModel.RestoreCommand.ExecuteAsync(null);

        Assert.True(fixture.ViewModel.Summary?.IsFailure);
        Assert.Empty(Directory.GetFileSystemEntries(restored));
    }

    [Fact]
    public async Task AProfileSurvivesBeingSaved_Reopened_ExportedAndImported()
    {
        using TempDirectory temp = new();

        Fixture fixture = new(temp.Combine("profiles"));
        fixture.Editor.AddInputs([temp.Path]);
        fixture.Editor.OutputDirectory = temp.Combine("out");
        fixture.Editor.SelectedCompression = CompressionOption.For(CompressionAlgorithm.Gzip);
        fixture.Editor.Encrypt = true;
        fixture.Editor.NewExcludeGlob = "*.tmp";
        fixture.Editor.AddExcludeCommand.Execute(null);

        fixture.Dialogs.ProfileNames.Enqueue("Documents");
        await fixture.ViewModel.SaveProfileCommand.ExecuteAsync(null);

        // A second window over the same folder sees the same profile.
        Fixture reopened = new(fixture.ProfilesDirectory);
        await reopened.ViewModel.RefreshProfilesCommand.ExecuteAsync(null);

        Assert.Equal("Documents", reopened.ViewModel.SelectedProfileName);
        Assert.Equal(temp.Path, Assert.Single(reopened.Editor.Inputs).Path);
        Assert.Equal(CompressionAlgorithm.Gzip, reopened.Editor.SelectedCompression.Value);
        Assert.True(reopened.Editor.Encrypt);
        Assert.Equal("*.tmp", Assert.Single(reopened.Editor.ExcludeGlobs));

        // Exported to an arbitrary place, and imported back into a fresh store.
        string exported = temp.Combine("exported", "Documents.json");
        reopened.FilePicker.ProfileExports.Enqueue(exported);
        await reopened.ViewModel.ExportProfileCommand.ExecuteAsync(null);

        Assert.True(File.Exists(exported));

        Fixture elsewhere = new(temp.Combine("other-profiles"));
        elsewhere.FilePicker.ProfileImports.Enqueue(exported);
        await elsewhere.ViewModel.ImportProfileCommand.ExecuteAsync(null);

        Assert.Equal("Documents", elsewhere.ViewModel.SelectedProfileName);
        Assert.Equal("*.tmp", Assert.Single(elsewhere.Editor.ExcludeGlobs));
        Assert.True(elsewhere.Editor.Encrypt);
    }

    /// <remarks>
    /// The pattern inputs' acceptance criterion, run rather than described: two patterns over one
    /// folder, through the real scan, the real archive and the real restore, so what comes back out is
    /// the proof of what went in.
    /// </remarks>
    [Fact]
    public async Task APatternInputBacksUpWhatItMatchesAndNothingElse()
    {
        using TempDirectory temp = new();

        string logs = temp.Combine("source", "logs");
        Directory.CreateDirectory(logs);
        temp.WriteFile(Path.Combine("source", "logs", "app.log"), "today's log");
        temp.WriteFile(Path.Combine("source", "logs", "keep.txt"), "not a log");
        temp.WriteFile(Path.Combine("source", "logs", "deep", "nested.log"), "a log, but not at this level");
        temp.WriteFile(Path.Combine("source", "logs", "DirPrefixOne", "inner.txt"), "inside a matching folder");
        temp.WriteFile(Path.Combine("source", "logs", "other", "x.txt"), "left behind");

        string output = temp.Combine("out");
        string restored = temp.Combine("restored");
        Directory.CreateDirectory(output);

        Fixture fixture = new(temp.Combine("profiles"));

        // "*.log" is one level deep; "DirPrefix*" matches a folder's name and so takes the folder.
        Assert.True(fixture.Editor.AddPatternInput(logs, "*.log"));
        Assert.True(fixture.Editor.AddPatternInput(logs, "DirPrefix*"));
        fixture.Editor.OutputDirectory = output;

        fixture.Dialogs.ProfileNames.Enqueue("Logs");
        await fixture.ViewModel.SaveProfileCommand.ExecuteAsync(null);

        // A profile that uses a 1.2.0 feature says so, and the older builds refuse it rather than read
        // it as less than it is.
        string profileJson = File.ReadAllText(Path.Combine(fixture.ProfilesDirectory, "Logs.json"));
        Assert.Contains("\"formatVersion\": 2", profileJson, StringComparison.Ordinal);

        await fixture.ViewModel.BackupCommand.ExecuteAsync(null);

        RunSummary backup = Assert.IsType<RunSummary>(fixture.ViewModel.Summary);
        Assert.True(backup.IsSuccess, backup.Headline);

        string archive = Assert.Single(Directory.GetFiles(output), static path => !path.EndsWith(".sha256", StringComparison.Ordinal));

        fixture.FilePicker.Archives.Enqueue(archive);
        fixture.FilePicker.Folders.Enqueue(restored);
        await fixture.ViewModel.RestoreCommand.ExecuteAsync(null);

        RunSummary restore = Assert.IsType<RunSummary>(fixture.ViewModel.Summary);
        Assert.True(restore.IsSuccess, restore.Headline);

        // The second pattern over the same folder gets its own top-level name rather than merging.
        string[] expected =
        [
            Path.Combine("logs", "app.log"),
            Path.Combine("logs_2", "DirPrefixOne", "inner.txt"),
        ];

        Assert.Equal(expected, ReadTree(restored).Keys.Order(StringComparer.Ordinal));
        Assert.Empty(fixture.Dialogs.Messages);
    }

    private static Dictionary<string, byte[]> ReadTree(string root)
    {
        Dictionary<string, byte[]> files = new(StringComparer.Ordinal);

        foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            files[Path.GetRelativePath(root, path)] = File.ReadAllBytes(path);
        }

        return files;
    }

    /// <summary>The shipping container, with only the seams that would open a window replaced.</summary>
    private sealed class Fixture
    {
        public Fixture(string profilesDirectory)
        {
            ProfilesDirectory = profilesDirectory;

            ServiceCollection services = [];

            // Registered before the two extensions, whose TryAdd calls then stand down. The overlay and
            // the notification banner are among them because the real ones reach for host controls that
            // only exist once a window has been shown.
            services.AddSingleton<IProfileStore>(new ProfileStore(profilesDirectory));
            services.AddSingleton<IFilePicker>(FilePicker);
            services.AddSingleton<IDialogService>(Dialogs);
            services.AddSingleton<IProgressOverlay>(Overlay);
            services.AddSingleton<INotificationService>(Notifications);
            services.AddSingleton<IUiDispatcher>(new ImmediateUiDispatcher());

            // Argon2id at its shipped cost would add most of a second to every encrypted case here;
            // the container format does not change with the parameters, and the header carries them.
            services.AddSingleton(new Ebk1Options { MemoryKiB = 8, Iterations = 1, Parallelism = 1 });

            services.AddLogging();
            services.AddEnigmaBackupCore();
            services.AddEnigmaBackupDesktop();

            ServiceProvider provider = services.BuildServiceProvider();

            ViewModel = provider.GetRequiredService<MainWindowViewModel>();
            Editor = ViewModel.Editor;
        }

        public string ProfilesDirectory { get; }

        public FakeFilePicker FilePicker { get; } = new();

        public FakeDialogService Dialogs { get; } = new();

        public FakeProgressOverlay Overlay { get; } = new();

        public FakeNotificationService Notifications { get; } = new();

        public MainWindowViewModel ViewModel { get; }

        public ProfileEditorViewModel Editor { get; }
    }
}
