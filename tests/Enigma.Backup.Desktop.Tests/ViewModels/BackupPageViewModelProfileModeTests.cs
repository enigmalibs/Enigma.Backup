using System;
using System.Threading.Tasks;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Desktop.Services;
using Enigma.Backup.Desktop.Tests.TestSupport;
using Enigma.Backup.Desktop.ViewModels;
using Xunit;

namespace Enigma.Backup.Desktop.Tests.ViewModels;

/// <summary>
/// The Backup page backing up a saved profile: which mode it opens in, what it says the profile backs
/// up, what it hands the core, and how it follows the store.
/// </summary>
public sealed class BackupPageViewModelProfileModeTests
{
    [Fact]
    public async Task ThePage_OpensOnAOneOff_WhenThereIsNoProfile()
    {
        Harness harness = new();

        await harness.ProfilesPage.RefreshProfilesCommand.ExecuteAsync(null);

        Assert.False(harness.BackupPage.UseProfile);
        Assert.True(harness.BackupPage.IsSingleMode);
        Assert.False(harness.BackupPage.HasProfiles);
        Assert.Same(harness.BackupEditor, harness.BackupPage.ActiveEditor);
        Assert.True(harness.BackupPage.IsOutputEditable);
    }

    [Fact]
    public async Task ThePage_OpensOnTheFirstProfile_WhenThereAreSome()
    {
        Harness harness = new();
        harness.Profiles.Seed(Harness.Profile("Photos", inputs: ["/pics"]));
        harness.Profiles.Seed(Harness.Profile("Documents", inputs: ["/docs"]));

        await harness.ProfilesPage.RefreshProfilesCommand.ExecuteAsync(null);

        Assert.True(harness.BackupPage.UseProfile);
        Assert.Equal("Documents", harness.BackupPage.SelectedProfileName);
        Assert.Same(harness.FromProfileEditor, harness.BackupPage.ActiveEditor);
        Assert.False(harness.BackupPage.IsOutputEditable);
        Assert.Equal("/docs", Assert.Single(harness.FromProfileEditor.Inputs).Path);
    }

    [Fact]
    public async Task AModeTheUserChose_IsKept_WhenTheStoreLoads()
    {
        Harness harness = new();
        harness.Profiles.Seed(Harness.Profile("Documents"));
        harness.BackupPage.UseProfile = true;
        harness.BackupPage.IsSingleMode = true;

        await harness.ProfilesPage.RefreshProfilesCommand.ExecuteAsync(null);

        Assert.False(harness.BackupPage.UseProfile);
    }

    [Fact]
    public async Task TheSummary_ListsPathsPatternsAndExcludes()
    {
        Harness harness = new();
        BackupProfile profile = Harness.Profile("Documents", inputs: ["/docs", "/notes"], excludeGlobs: ["**/bin/**", "*.tmp"]);
        profile.PatternInputs = [new PatternInput("/var/log", "*.log")];
        harness.Profiles.Seed(profile);

        await harness.ProfilesPage.RefreshProfilesCommand.ExecuteAsync(null);

        Assert.Equal("Backs up 3 items, as last saved:", harness.BackupPage.SummaryLead);
        Assert.Equal(["/docs", "/notes", "*.log in /var/log"], harness.BackupPage.SummaryItems);
        Assert.Equal("Leaves out **/bin/**, *.tmp", harness.BackupPage.SummaryExcludes);
    }

    [Fact]
    public async Task TheSummary_OfAnEmptyProfile_SaysSo_AndBackUpWaits()
    {
        Harness harness = new();
        harness.Profiles.Seed(Harness.Profile("Empty", inputs: []));

        await harness.ProfilesPage.RefreshProfilesCommand.ExecuteAsync(null);

        Assert.Equal("This profile backs up nothing yet.", harness.BackupPage.SummaryLead);
        Assert.Empty(harness.BackupPage.SummaryItems);
        Assert.Null(harness.BackupPage.SummaryExcludes);
        Assert.False(harness.BackupPage.BackupCommand.CanExecute(null));
        Assert.Equal("This profile backs up nothing yet — add to it on the Profiles page.", harness.BackupPage.MissingForBackup);
    }

    [Fact]
    public void ProfileMode_WithoutAnyProfile_SaysWhereToMakeOne()
    {
        Harness harness = new();

        harness.BackupPage.UseProfile = true;

        Assert.False(harness.BackupPage.BackupCommand.CanExecute(null));
        Assert.Equal("Create a profile on the Profiles page first.", harness.BackupPage.MissingForBackup);
        Assert.Null(harness.BackupPage.SummaryLead);
    }

    [Fact]
    public async Task ProfileMode_WithNothingChosen_AsksForAChoice()
    {
        Harness harness = new();
        harness.Profiles.Seed(Harness.Profile("Documents"));
        await harness.ProfilesPage.RefreshProfilesCommand.ExecuteAsync(null);

        harness.BackupPage.SelectedProfileName = null;

        Assert.False(harness.BackupPage.BackupCommand.CanExecute(null));
        Assert.Equal("Choose the profile to back up.", harness.BackupPage.MissingForBackup);
        Assert.Empty(harness.FromProfileEditor.Inputs);
        Assert.Equal(string.Empty, harness.FromProfileEditor.OutputDirectory);
    }

    [Fact]
    public async Task AProfileWithoutAnOutputFolder_SaysWhereToSetOne()
    {
        Harness harness = new();
        harness.Profiles.Seed(Harness.Profile("Documents", outputDirectory: string.Empty));

        await harness.ProfilesPage.RefreshProfilesCommand.ExecuteAsync(null);

        Assert.False(harness.BackupPage.BackupCommand.CanExecute(null));
        Assert.Equal("This profile has no output folder — choose one on the Profiles page.", harness.BackupPage.MissingForBackup);
    }

    [Fact]
    public async Task ABackup_HandsTheCoreTheProfileAsSaved_UnderItsName()
    {
        Harness harness = new();
        harness.Profiles.Seed(Harness.Profile("Documents", inputs: ["/docs"], compression: CompressionAlgorithm.Gzip, excludeGlobs: ["*.tmp"]));
        await harness.ProfilesPage.RefreshProfilesCommand.ExecuteAsync(null);

        // An edit on the Profiles page that has not been saved is not what a profile backup runs.
        harness.ProfileEditor.AddInputs(["/unsaved"]);

        Assert.True(harness.BackupPage.BackupCommand.CanExecute(null));
        await harness.BackupPage.BackupCommand.ExecuteAsync(null);

        BackupProfile sent = Assert.IsType<BackupProfile>(harness.Backups.LastProfile);

        Assert.Equal("Documents", sent.Name);
        Assert.Equal("/docs", Assert.Single(sent.Inputs));
        Assert.Equal(CompressionAlgorithm.Gzip, sent.Compression);
        Assert.Equal("*.tmp", Assert.Single(sent.ExcludeGlobs));
        Assert.Equal("/backups", sent.OutputDirectory);
        Assert.True(harness.RunDialog.LastSummary?.IsSuccess);
    }

    [Fact]
    public async Task ABackupOfAnEncryptingProfile_AsksForThePasswordInItsName()
    {
        Harness harness = new();
        harness.Profiles.Seed(Harness.Profile("Documents", encrypt: true));
        await harness.ProfilesPage.RefreshProfilesCommand.ExecuteAsync(null);
        harness.Dialogs.Passwords.Enqueue("s3cret");

        await harness.BackupPage.BackupCommand.ExecuteAsync(null);

        (PasswordPurpose purpose, string subject) = Assert.Single(harness.Dialogs.PasswordRequests);

        Assert.Equal(PasswordPurpose.ProtectNewArchive, purpose);
        Assert.Equal("Documents", subject);
        Assert.Equal("s3cret", harness.Backups.LastPassword);
    }

    [Fact]
    public async Task SwitchingBack_ToAOneOff_RunsTheOneOff()
    {
        Harness harness = new();
        harness.Profiles.Seed(Harness.Profile("Documents"));
        await harness.ProfilesPage.RefreshProfilesCommand.ExecuteAsync(null);
        harness.FillEditor();

        harness.BackupPage.IsSingleMode = true;
        await harness.BackupPage.BackupCommand.ExecuteAsync(null);

        Assert.Equal(BackupPageViewModel.OneOffName, harness.Backups.LastProfile?.Name);
        Assert.Same(harness.BackupEditor, harness.BackupPage.ActiveEditor);
    }

    [Fact]
    public async Task AReload_ShowsTheChosenProfileAsItWasJustSaved()
    {
        Harness harness = new();
        harness.Profiles.Seed(Harness.Profile("Documents", inputs: ["/docs"]));
        await harness.ProfilesPage.RefreshProfilesCommand.ExecuteAsync(null);

        harness.ProfileEditor.AddInputs(["/more"]);
        await harness.ProfilesPage.SaveProfileCommand.ExecuteAsync(null);

        Assert.Equal("Documents", harness.BackupPage.SelectedProfileName);
        Assert.Equal(2, harness.FromProfileEditor.Inputs.Count);
        Assert.Equal("Backs up 2 items, as last saved:", harness.BackupPage.SummaryLead);
    }

    [Fact]
    public async Task AReload_LetsGoOfAProfileThatIsGone()
    {
        Harness harness = new();
        harness.Profiles.Seed(Harness.Profile("Documents"));
        harness.Profiles.Seed(Harness.Profile("Photos"));
        await harness.ProfilesPage.RefreshProfilesCommand.ExecuteAsync(null);
        Assert.Equal("Documents", harness.BackupPage.SelectedProfileName);

        harness.Dialogs.Confirmations.Enqueue(true);
        await harness.ProfilesPage.DeleteProfileCommand.ExecuteAsync(null);

        Assert.Null(harness.BackupPage.SelectedProfileName);
        Assert.Null(harness.BackupPage.SummaryLead);
        Assert.Equal(["Photos"], harness.BackupPage.ProfileNames);
        Assert.False(harness.BackupPage.BackupCommand.CanExecute(null));
    }

    [Fact]
    public void TheConstructor_RefusesOneEditorForBothJobs()
    {
        Harness harness = new();

        Assert.Throws<ArgumentException>(() => new BackupPageViewModel(
            harness.Backups,
            harness.BackupEditor,
            harness.BackupEditor,
            harness.Catalog,
            harness.Dialogs,
            harness.Runs,
            harness.Dispatcher));
    }
}
