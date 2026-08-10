using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Desktop.Tests.TestSupport;
using Enigma.Backup.Desktop.ViewModels;
using Xunit;

namespace Enigma.Backup.Desktop.Tests.ViewModels;

public sealed class ProfileEditorViewModelTests
{
    [Fact]
    public void CanRun_IsFalse_WhenThereIsNothingToBackUp()
    {
        ProfileEditorViewModel editor = Editor();
        editor.OutputDirectory = "/backups";

        Assert.False(editor.CanRun);
    }

    [Fact]
    public void CanRun_IsFalse_WhenThereIsNowhereToPutIt()
    {
        ProfileEditorViewModel editor = Editor();
        editor.AddInputs(["/home/jo/Documents"]);

        Assert.False(editor.CanRun);
    }

    [Fact]
    public void CanRun_IsFalse_WhenTheOutputDirectoryIsOnlySpaces()
    {
        ProfileEditorViewModel editor = Editor();
        editor.AddInputs(["/home/jo/Documents"]);
        editor.OutputDirectory = "   ";

        Assert.False(editor.CanRun);
    }

    [Fact]
    public void CanRun_IsTrue_WithSomethingToBackUpAndSomewhereToPutIt()
    {
        ProfileEditorViewModel editor = Editor();
        editor.AddInputs(["/home/jo/Documents"]);
        editor.OutputDirectory = "/backups";

        Assert.True(editor.CanRun);
    }

    [Fact]
    public void AddInputs_IgnoresPathsTheProfileAlreadyLists()
    {
        ProfileEditorViewModel editor = Editor();

        Assert.Equal(1, editor.AddInputs(["/home/jo/Documents"]));
        Assert.Equal(0, editor.AddInputs(["/home/jo/Documents"]));

        InputItem only = Assert.Single(editor.Inputs);
        Assert.Equal("/home/jo/Documents", only.Path);
        Assert.False(only.IsPattern);
    }

    [Fact]
    public void AddInputs_DropsATrailingSeparator_SoOneFolderIsNotListedTwice()
    {
        ProfileEditorViewModel editor = Editor();
        string folder = Path.Combine(Path.DirectorySeparatorChar.ToString(), "home", "jo", "Documents");

        editor.AddInputs([folder + Path.DirectorySeparatorChar]);
        editor.AddInputs([folder]);

        InputItem only = Assert.Single(editor.Inputs);
        Assert.Equal(folder, only.Path);
    }

    [Fact]
    public void AddInputs_IgnoresBlanks()
    {
        ProfileEditorViewModel editor = Editor();

        Assert.Equal(0, editor.AddInputs(["", "   "]));
        Assert.Empty(editor.Inputs);
    }

    [Fact]
    public void LoadFrom_ShowsTheProfileAndLeavesItClean()
    {
        ProfileEditorViewModel editor = Editor();

        editor.LoadFrom(Harness.Profile(
            inputs: ["/a", "/b"],
            outputDirectory: "/backups",
            compression: CompressionAlgorithm.Gzip,
            encrypt: true,
            excludeGlobs: ["*.log"]));

        string[] expectedInputs = ["/a", "/b"];
        string[] expectedGlobs = ["*.log"];

        Assert.Equal(expectedInputs, editor.Inputs.Select(static input => input.Path));
        Assert.Equal(expectedGlobs, editor.ExcludeGlobs);
        Assert.Equal("/backups", editor.OutputDirectory);
        Assert.Equal(CompressionAlgorithm.Gzip, editor.SelectedCompression.Value);
        Assert.True(editor.Encrypt);
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void EditingAnything_MarksTheProfileDirtyAndAnnouncesIt()
    {
        ProfileEditorViewModel editor = Editor();
        editor.LoadFrom(Harness.Profile());

        int announcements = 0;
        editor.Changed += (_, _) => announcements++;

        editor.Encrypt = true;

        Assert.True(editor.IsDirty);
        Assert.Equal(1, announcements);
    }

    [Fact]
    public void MarkClean_ForgetsTheEdits()
    {
        ProfileEditorViewModel editor = Editor();
        editor.Encrypt = true;

        editor.MarkClean();

        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void ToProfile_ReadsBackWhatTheEditorShows()
    {
        ProfileEditorViewModel editor = Editor();
        editor.AddInputs(["/a", "/b"]);
        editor.OutputDirectory = "  /backups  ";
        editor.SelectedCompression = CompressionOption.For(CompressionAlgorithm.Gzip);
        editor.Encrypt = true;
        editor.NewExcludeGlob = "**/bin/**";
        editor.AddExcludeCommand.Execute(null);

        BackupProfile profile = editor.ToProfile("Documents");

        string[] expectedInputs = ["/a", "/b"];
        string[] expectedGlobs = ["**/bin/**"];

        Assert.Equal("Documents", profile.Name);
        Assert.Equal(expectedInputs, profile.Inputs);
        Assert.Equal("/backups", profile.OutputDirectory);
        Assert.Equal(CompressionAlgorithm.Gzip, profile.Compression);
        Assert.True(profile.Encrypt);
        Assert.Equal(expectedGlobs, profile.ExcludeGlobs);
    }

    [Fact]
    public void ToProfile_TakesACopy_SoLaterEditsDoNotReachTheProfile()
    {
        ProfileEditorViewModel editor = Editor();
        editor.AddInputs(["/a"]);

        BackupProfile profile = editor.ToProfile("Documents");
        editor.AddInputs(["/b"]);

        Assert.Single(profile.Inputs);
    }

    [Fact]
    public void Reset_EmptiesEverything()
    {
        ProfileEditorViewModel editor = Editor();
        editor.LoadFrom(Harness.Profile(inputs: ["/a"], excludeGlobs: ["*.log"]));

        editor.Reset();

        Assert.Empty(editor.Inputs);
        Assert.Empty(editor.ExcludeGlobs);
        Assert.Equal(string.Empty, editor.OutputDirectory);
        Assert.False(editor.Encrypt);
        Assert.Equal(CompressionAlgorithm.Zstd, editor.SelectedCompression.Value);
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void AddExclude_TrimsTheTypedPatternAndClearsTheBox()
    {
        ProfileEditorViewModel editor = Editor();
        editor.NewExcludeGlob = "  *.log  ";

        editor.AddExcludeCommand.Execute(null);

        string only = Assert.Single(editor.ExcludeGlobs);
        Assert.Equal("*.log", only);
        Assert.Equal(string.Empty, editor.NewExcludeGlob);
    }

    [Fact]
    public void AddExclude_IsDisabledWhileTheBoxIsBlank()
    {
        ProfileEditorViewModel editor = Editor();

        Assert.False(editor.AddExcludeCommand.CanExecute(null));

        editor.NewExcludeGlob = "   ";
        Assert.False(editor.AddExcludeCommand.CanExecute(null));

        editor.NewExcludeGlob = "*.log";
        Assert.True(editor.AddExcludeCommand.CanExecute(null));
    }

    [Fact]
    public void AddExclude_DoesNotListTheSamePatternTwice()
    {
        ProfileEditorViewModel editor = Editor();

        editor.NewExcludeGlob = "*.log";
        editor.AddExcludeCommand.Execute(null);
        editor.NewExcludeGlob = "*.log";
        editor.AddExcludeCommand.Execute(null);

        Assert.Single(editor.ExcludeGlobs);
    }

    [Fact]
    public void RemoveCommands_NeedASelection()
    {
        ProfileEditorViewModel editor = Editor();
        editor.AddInputs(["/a"]);
        editor.NewExcludeGlob = "*.log";
        editor.AddExcludeCommand.Execute(null);

        Assert.False(editor.RemoveInputCommand.CanExecute(null));
        Assert.False(editor.RemoveExcludeCommand.CanExecute(null));

        editor.SelectedInput = editor.Inputs[0];
        editor.SelectedExcludeGlob = "*.log";

        Assert.True(editor.RemoveInputCommand.CanExecute(null));
        Assert.True(editor.RemoveExcludeCommand.CanExecute(null));

        editor.RemoveInputCommand.Execute(null);
        editor.RemoveExcludeCommand.Execute(null);

        Assert.Empty(editor.Inputs);
        Assert.Empty(editor.ExcludeGlobs);
        Assert.Null(editor.SelectedInput);
        Assert.Null(editor.SelectedExcludeGlob);
    }

    [Fact]
    public void ClearInputs_NeedsSomethingToClear()
    {
        ProfileEditorViewModel editor = Editor();

        Assert.False(editor.ClearInputsCommand.CanExecute(null));

        editor.AddInputs(["/a", "/b"]);
        Assert.True(editor.ClearInputsCommand.CanExecute(null));

        editor.ClearInputsCommand.Execute(null);
        Assert.Empty(editor.Inputs);
    }

    [Fact]
    public async Task AddFiles_AddsWhateverThePickerReturned()
    {
        FakeFilePicker picker = new();
        picker.InputFiles.Enqueue(["/a", "/b"]);
        ProfileEditorViewModel editor = new(picker);

        await editor.AddFilesCommand.ExecuteAsync(null);

        Assert.Equal(2, editor.Inputs.Count);
    }

    [Fact]
    public async Task BrowseOutput_OffersTheCurrentFolderAsAStartingPoint()
    {
        FakeFilePicker picker = new();
        picker.Folders.Enqueue("/backups/new");
        ProfileEditorViewModel editor = new(picker);
        editor.OutputDirectory = "/backups";

        await editor.BrowseOutputCommand.ExecuteAsync(null);

        Assert.Equal("/backups", Assert.Single(picker.FolderStarts));
        Assert.Equal("/backups/new", editor.OutputDirectory);
    }

    [Fact]
    public async Task BrowseOutput_LeavesTheFolderAloneWhenThePickerIsDismissed()
    {
        FakeFilePicker picker = new();
        ProfileEditorViewModel editor = new(picker);
        editor.OutputDirectory = "/backups";

        await editor.BrowseOutputCommand.ExecuteAsync(null);

        Assert.Equal("/backups", editor.OutputDirectory);
    }

    [Fact]
    public void AddPatternInput_AddsARowThatReadsAsThePair()
    {
        ProfileEditorViewModel editor = Editor();

        Assert.True(editor.AddPatternInput("/var/log", "*.log"));

        InputItem only = Assert.Single(editor.Inputs);
        Assert.True(only.IsPattern);
        Assert.Equal("/var/log", only.Path);
        Assert.Equal("*.log", only.Pattern);
        Assert.Equal("/var/log ▸ *.log", only.Display);
        Assert.Contains("*.log", only.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AddPatternInput_StoresTheGlobWithForwardSeparators()
    {
        ProfileEditorViewModel editor = Editor();

        Assert.True(editor.AddPatternInput("/var/log", @"  sub\*.log  "));

        Assert.Equal("sub/*.log", Assert.Single(editor.Inputs).Pattern);
    }

    [Fact]
    public void AddPatternInput_IgnoresAPatternTheProfileAlreadyLists()
    {
        ProfileEditorViewModel editor = Editor();

        Assert.True(editor.AddPatternInput("/var/log", "*.log"));
        Assert.False(editor.AddPatternInput("/var/log/", @"*.log"));

        // A different pattern over the same folder is a different rule, so it is a second row.
        Assert.True(editor.AddPatternInput("/var/log", "*.txt"));

        Assert.Equal(2, editor.Inputs.Count);
    }

    [Fact]
    public void AddPatternInput_RefusesBlanks()
    {
        ProfileEditorViewModel editor = Editor();

        Assert.False(editor.AddPatternInput("   ", "*.log"));
        Assert.False(editor.AddPatternInput("/var/log", "   "));
        Assert.Empty(editor.Inputs);
    }

    [Fact]
    public void AFolderCanBeBothAPlainInputAndAPatternsBase()
    {
        ProfileEditorViewModel editor = Editor();

        editor.AddInputs(["/var/log"]);
        Assert.True(editor.AddPatternInput("/var/log", "*.log"));

        // "All of it" and "the logs in it" are different requests; neither hides the other.
        Assert.Equal(2, editor.Inputs.Count);
        Assert.Equal(1, editor.AddInputs(["/var/log/archive"]));
    }

    [Fact]
    public void CanRun_IsTrue_WithNothingButAPattern()
    {
        ProfileEditorViewModel editor = Editor();
        editor.AddPatternInput("/var/log", "*.log");
        editor.OutputDirectory = "/backups";

        Assert.True(editor.CanRun);
    }

    [Fact]
    public void ToProfile_SplitsThePathsFromThePatterns()
    {
        ProfileEditorViewModel editor = Editor();
        editor.AddInputs(["/a", "/b"]);
        editor.AddPatternInput("/var/log", "*.log");
        editor.OutputDirectory = "/backups";

        BackupProfile profile = editor.ToProfile("Mixed");

        string[] expectedInputs = ["/a", "/b"];
        Assert.Equal(expectedInputs, profile.Inputs);

        PatternInput patternInput = Assert.Single(profile.PatternInputs);
        Assert.Equal("/var/log", patternInput.BaseDirectory);
        Assert.Equal("*.log", patternInput.Pattern);
    }

    [Fact]
    public void LoadFrom_ShowsThePathsThenThePatterns_AndAMixedProfileRoundTrips()
    {
        ProfileEditorViewModel editor = Editor();
        BackupProfile profile = Harness.Profile(inputs: ["/a", "/b"]);
        profile.PatternInputs = [new PatternInput("/var/log", "*.log")];

        editor.LoadFrom(profile);

        string[] expectedRows = ["/a", "/b", "/var/log ▸ *.log"];
        Assert.Equal(expectedRows, editor.Inputs.Select(static input => input.Display));
        Assert.False(editor.IsDirty);

        BackupProfile written = editor.ToProfile(profile.Name);

        Assert.Equal(profile.Inputs, written.Inputs);
        Assert.Equal(profile.PatternInputs, written.PatternInputs);
    }

    private static ProfileEditorViewModel Editor() => new(new FakeFilePicker());
}
