using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Desktop.Services;
using Enigma.Backup.Desktop.Tests.TestSupport;
using Enigma.Backup.Desktop.ViewModels;
using Xunit;

namespace Enigma.Backup.Desktop.Tests.ViewModels;

/// <summary>The profile bar: what it lists, what it loads, and what it writes.</summary>
public sealed class MainWindowViewModelProfileTests
{
    [Fact]
    public void Title_IsJustTheProductName_BeforeAnythingIsLoaded()
    {
        Harness harness = new();

        Assert.Equal("Enigma.Backup", harness.ViewModel.Title);
        Assert.Equal("No profile loaded.", harness.ViewModel.ProfileStatus);
    }

    [Fact]
    public async Task Title_NamesTheProfileAndMarksUnsavedEdits()
    {
        Harness harness = new();
        harness.Profiles.Seed(Harness.Profile("Documents"));

        await harness.ViewModel.RefreshProfilesCommand.ExecuteAsync(null);
        Assert.Equal("Enigma.Backup — Documents", harness.ViewModel.Title);
        Assert.Equal("Saved.", harness.ViewModel.ProfileStatus);

        harness.Editor.Encrypt = true;

        Assert.Equal("Enigma.Backup — Documents *", harness.ViewModel.Title);
        Assert.Equal("Unsaved changes.", harness.ViewModel.ProfileStatus);
    }

    [Fact]
    public async Task Refresh_ListsEveryProfileInOrderAndOpensOntoTheFirst()
    {
        Harness harness = new();
        harness.Profiles.Seed(Harness.Profile("Photos", inputs: ["/pics"]));
        harness.Profiles.Seed(Harness.Profile("Documents", inputs: ["/docs"]));

        await harness.ViewModel.RefreshProfilesCommand.ExecuteAsync(null);

        string[] expected = ["Documents", "Photos"];

        Assert.Equal(expected, harness.ViewModel.ProfileNames);
        Assert.Equal("Documents", harness.ViewModel.SelectedProfileName);
        Assert.Equal("/docs", Assert.Single(harness.Editor.Inputs));
        Assert.False(harness.Editor.IsDirty);
    }

    [Fact]
    public async Task Refresh_LeavesTheEditorEmptyWhenTheStoreIs()
    {
        Harness harness = new();

        await harness.ViewModel.RefreshProfilesCommand.ExecuteAsync(null);

        Assert.Empty(harness.ViewModel.ProfileNames);
        Assert.Null(harness.ViewModel.SelectedProfileName);
        Assert.Empty(harness.Dialogs.Messages);
    }

    [Fact]
    public async Task Refresh_LeavesOutAProfileItCannotRead_AndSaysSo()
    {
        Harness harness = new();
        harness.Profiles.Seed(Harness.Profile("Documents"));
        harness.Profiles.Seed(Harness.Profile("Broken"));
        harness.Profiles.Unreadable.Add("Broken");

        await harness.ViewModel.RefreshProfilesCommand.ExecuteAsync(null);

        Assert.DoesNotContain("Broken", harness.ViewModel.ProfileNames);
        Assert.Contains("Documents", harness.ViewModel.ProfileNames);

        (string _, string message, MessageSeverity severity) = Assert.Single(harness.Dialogs.Messages);
        Assert.Equal(MessageSeverity.Warning, severity);
        Assert.Contains("Broken", message, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task SelectingAProfile_ShowsIt()
    {
        Harness harness = new();
        harness.Profiles.Seed(Harness.Profile("Documents", inputs: ["/docs"]));
        harness.Profiles.Seed(Harness.Profile("Photos", inputs: ["/pics"], compression: CompressionAlgorithm.Gzip));

        await harness.ViewModel.RefreshProfilesCommand.ExecuteAsync(null);
        harness.ViewModel.SelectedProfileName = "Photos";

        Assert.Equal("/pics", Assert.Single(harness.Editor.Inputs));
        Assert.Equal(CompressionAlgorithm.Gzip, harness.Editor.SelectedCompression.Value);
        Assert.False(harness.Editor.IsDirty);
    }

    [Fact]
    public void RenameDeleteAndExport_NeedAStoredProfile()
    {
        Harness harness = new();

        Assert.False(harness.ViewModel.RenameProfileCommand.CanExecute(null));
        Assert.False(harness.ViewModel.DeleteProfileCommand.CanExecute(null));
        Assert.False(harness.ViewModel.ExportProfileCommand.CanExecute(null));

        Assert.True(harness.ViewModel.NewProfileCommand.CanExecute(null));
        Assert.True(harness.ViewModel.SaveProfileCommand.CanExecute(null));
        Assert.True(harness.ViewModel.SaveProfileAsCommand.CanExecute(null));
        Assert.True(harness.ViewModel.ImportProfileCommand.CanExecute(null));
    }

    [Fact]
    public async Task Save_AsksForANameWhenThereIsNoProfileYet()
    {
        Harness harness = new();
        harness.FillEditor();
        harness.Dialogs.ProfileNames.Enqueue("Documents");

        await harness.ViewModel.SaveProfileCommand.ExecuteAsync(null);

        Assert.True(harness.Profiles.Contains("Documents"));
        Assert.Equal("Documents", harness.ViewModel.SelectedProfileName);
        Assert.False(harness.Editor.IsDirty);
        Assert.Equal("/backups", harness.Profiles.Stored("Documents").OutputDirectory);
    }

    [Fact]
    public async Task Save_WritesNothingWhenTheNamePromptIsCancelled()
    {
        Harness harness = new();
        harness.FillEditor();

        await harness.ViewModel.SaveProfileCommand.ExecuteAsync(null);

        Assert.Equal(0, harness.Profiles.Count);
        Assert.True(harness.Editor.IsDirty);
    }

    [Fact]
    public async Task Save_OverwritesTheSelectedProfileWithoutAsking()
    {
        Harness harness = new();
        harness.Profiles.Seed(Harness.Profile("Documents", inputs: ["/docs"]));

        await harness.ViewModel.RefreshProfilesCommand.ExecuteAsync(null);
        harness.Editor.Encrypt = true;
        await harness.ViewModel.SaveProfileCommand.ExecuteAsync(null);

        Assert.Empty(harness.Dialogs.ProfileNameRequests);
        Assert.True(harness.Profiles.Stored("Documents").Encrypt);
        Assert.False(harness.Editor.IsDirty);
    }

    [Fact]
    public async Task SaveAs_TreatsEveryExistingNameAsTaken()
    {
        Harness harness = new();
        harness.Profiles.Seed(Harness.Profile("Documents"));
        harness.Profiles.Seed(Harness.Profile("Photos"));

        await harness.ViewModel.RefreshProfilesCommand.ExecuteAsync(null);
        harness.Dialogs.ProfileNames.Enqueue("Documents copy");
        await harness.ViewModel.SaveProfileAsCommand.ExecuteAsync(null);

        ProfileNameRequest request = Assert.Single(harness.Dialogs.ProfileNameRequests);
        string[] expected = ["Documents", "Photos"];

        Assert.Equal(expected, request.TakenNames.Order(System.StringComparer.Ordinal));
        Assert.Equal("Documents", request.InitialName);
        Assert.True(harness.Profiles.Contains("Documents copy"));
        Assert.Equal("Documents copy", harness.ViewModel.SelectedProfileName);
    }

    [Fact]
    public async Task Rename_LeavesTheProfilesOwnNameAvailable()
    {
        Harness harness = new();
        harness.Profiles.Seed(Harness.Profile("Documents"));
        harness.Profiles.Seed(Harness.Profile("Photos"));

        await harness.ViewModel.RefreshProfilesCommand.ExecuteAsync(null);
        harness.Dialogs.ProfileNames.Enqueue("Papers");
        await harness.ViewModel.RenameProfileCommand.ExecuteAsync(null);

        ProfileNameRequest request = Assert.Single(harness.Dialogs.ProfileNameRequests);

        Assert.Equal("Photos", Assert.Single(request.TakenNames));
        Assert.False(harness.Profiles.Contains("Documents"));
        Assert.True(harness.Profiles.Contains("Papers"));
        Assert.Equal("Papers", harness.ViewModel.SelectedProfileName);
    }

    [Fact]
    public async Task Rename_DoesNothingWhenTheNameIsUnchanged()
    {
        Harness harness = new();
        harness.Profiles.Seed(Harness.Profile("Documents"));

        await harness.ViewModel.RefreshProfilesCommand.ExecuteAsync(null);
        harness.Dialogs.ProfileNames.Enqueue("Documents");
        await harness.ViewModel.RenameProfileCommand.ExecuteAsync(null);

        Assert.DoesNotContain(harness.Profiles.Calls, call => call.StartsWith("Rename", System.StringComparison.Ordinal));
    }

    [Fact]
    public async Task Delete_AsksFirstAndKeepsTheProfileWhenTheAnswerIsNo()
    {
        Harness harness = new();
        harness.Profiles.Seed(Harness.Profile("Documents"));

        await harness.ViewModel.RefreshProfilesCommand.ExecuteAsync(null);
        await harness.ViewModel.DeleteProfileCommand.ExecuteAsync(null);

        (string _, string _, string confirmLabel) = Assert.Single(harness.Dialogs.ConfirmationRequests);

        Assert.Equal("Delete", confirmLabel);
        Assert.True(harness.Profiles.Contains("Documents"));
        Assert.Equal("Documents", harness.ViewModel.SelectedProfileName);
    }

    [Fact]
    public async Task Delete_RemovesTheProfileAndEmptiesTheEditor()
    {
        Harness harness = new();
        harness.Profiles.Seed(Harness.Profile("Documents", inputs: ["/docs"]));

        await harness.ViewModel.RefreshProfilesCommand.ExecuteAsync(null);
        harness.Dialogs.Confirmations.Enqueue(true);
        await harness.ViewModel.DeleteProfileCommand.ExecuteAsync(null);

        Assert.False(harness.Profiles.Contains("Documents"));
        Assert.Null(harness.ViewModel.SelectedProfileName);
        Assert.Empty(harness.ViewModel.ProfileNames);
        Assert.Empty(harness.Editor.Inputs);
    }

    [Fact]
    public async Task Import_StoresTheProfileAndOpensOntoIt()
    {
        Harness harness = new();
        harness.FilePicker.ProfileImports.Enqueue(Path.Combine("elsewhere", "Photos.json"));
        harness.Profiles.ImportResult = Harness.Profile("Photos", inputs: ["/pics"]);

        await harness.ViewModel.ImportProfileCommand.ExecuteAsync(null);

        Assert.True(harness.Profiles.Contains("Photos"));
        Assert.Equal("Photos", harness.ViewModel.SelectedProfileName);
        Assert.Equal("/pics", Assert.Single(harness.Editor.Inputs));
    }

    [Fact]
    public async Task Import_DoesNothingWhenThePickerIsDismissed()
    {
        Harness harness = new();

        await harness.ViewModel.ImportProfileCommand.ExecuteAsync(null);

        Assert.Empty(harness.Profiles.Calls);
    }

    [Fact]
    public async Task Export_OffersTheProfilesNameAsTheFileName()
    {
        Harness harness = new();
        harness.Profiles.Seed(Harness.Profile("Documents"));

        await harness.ViewModel.RefreshProfilesCommand.ExecuteAsync(null);
        harness.FilePicker.ProfileExports.Enqueue(Path.Combine("elsewhere", "Documents.json"));
        await harness.ViewModel.ExportProfileCommand.ExecuteAsync(null);

        Assert.Equal("Documents.json", Assert.Single(harness.FilePicker.ExportSuggestions));
        Assert.Contains(harness.Profiles.Calls, call => call.StartsWith("Export:Documents", System.StringComparison.Ordinal));
    }

    [Fact]
    public async Task AStoreFailure_IsReportedAndChangesNothing()
    {
        Harness harness = new();
        harness.FillEditor();
        harness.Dialogs.ProfileNames.Enqueue("Documents");
        harness.Profiles.FailNextWrite = new IOException("The disk is full.");

        await harness.ViewModel.SaveProfileCommand.ExecuteAsync(null);

        (string title, string message, MessageSeverity severity) = Assert.Single(harness.Dialogs.Messages);

        Assert.Equal("The profile could not be saved", title);
        Assert.Equal("The disk is full.", message);
        Assert.Equal(MessageSeverity.Error, severity);
        Assert.Equal(0, harness.Profiles.Count);
        Assert.Null(harness.ViewModel.SelectedProfileName);
    }

    [Fact]
    public async Task NewProfile_ClearsTheSelectionAndTheEditor()
    {
        Harness harness = new();
        harness.Profiles.Seed(Harness.Profile("Documents", inputs: ["/docs"]));

        await harness.ViewModel.RefreshProfilesCommand.ExecuteAsync(null);
        harness.ViewModel.NewProfileCommand.Execute(null);

        Assert.Null(harness.ViewModel.SelectedProfileName);
        Assert.Empty(harness.Editor.Inputs);
        Assert.True(harness.Profiles.Contains("Documents"));
        Assert.Contains("Documents", harness.ViewModel.ProfileNames);
    }

    [Fact]
    public void DroppedPaths_LandInTheInputsAndAreCounted()
    {
        Harness harness = new();

        harness.ViewModel.AddDroppedInputs(["/a", "/b"]);

        Assert.Equal(2, harness.Editor.Inputs.Count);
        Assert.Equal("Added 2 inputs.", harness.ViewModel.StatusText);

        harness.ViewModel.AddDroppedInputs(["/a"]);

        Assert.Equal(2, harness.Editor.Inputs.Count);
        Assert.Equal("Nothing was added — those paths are already in the profile.", harness.ViewModel.StatusText);
    }
}
