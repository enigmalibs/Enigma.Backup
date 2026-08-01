using System.IO;
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

        string only = Assert.Single(editor.Inputs);
        Assert.Equal("/home/jo/Documents", only);
    }

    [Fact]
    public void AddInputs_DropsATrailingSeparator_SoOneFolderIsNotListedTwice()
    {
        ProfileEditorViewModel editor = Editor();
        string folder = Path.Combine(Path.DirectorySeparatorChar.ToString(), "home", "jo", "Documents");

        editor.AddInputs([folder + Path.DirectorySeparatorChar]);
        editor.AddInputs([folder]);

        string only = Assert.Single(editor.Inputs);
        Assert.Equal(folder, only);
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

        Assert.Equal(expectedInputs, editor.Inputs);
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

        editor.SelectedInput = "/a";
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

    private static ProfileEditorViewModel Editor() => new(new FakeFilePicker());
}
