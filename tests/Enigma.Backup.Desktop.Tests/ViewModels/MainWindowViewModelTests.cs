using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Enigma.Backup.Desktop.Tests.TestSupport;
using Enigma.Backup.Desktop.ViewModels;
using Xunit;

namespace Enigma.Backup.Desktop.Tests.ViewModels;

/// <summary>The window: which page the rail shows, and where a drop lands.</summary>
public sealed class MainWindowViewModelTests
{
    [Fact]
    public void TheWindow_OpensOnTheBackupPage()
    {
        Harness harness = new();

        Assert.Equal(AppPage.Backup, harness.Shell.CurrentPage);
        Assert.True(harness.Shell.IsBackupPage);
        Assert.False(harness.Shell.IsVerifyRestorePage);
    }

    [Fact]
    public void SwitchingPages_ShowsExactlyOne()
    {
        Harness harness = new();
        List<string?> raised = [];
        harness.Shell.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        harness.Shell.CurrentPage = AppPage.VerifyRestore;

        Assert.False(harness.Shell.IsBackupPage);
        Assert.True(harness.Shell.IsVerifyRestorePage);
        Assert.Contains(nameof(MainWindowViewModel.IsBackupPage), raised);
        Assert.Contains(nameof(MainWindowViewModel.IsVerifyRestorePage), raised);
    }

    [Fact]
    public void TheSettingsPage_IsOneOfThePages()
    {
        Harness harness = new();

        harness.Shell.CurrentPage = AppPage.Settings;

        Assert.True(harness.Shell.IsSettingsPage);
        Assert.False(harness.Shell.IsBackupPage);
        Assert.False(harness.Shell.IsVerifyRestorePage);
        Assert.False(harness.Shell.CanAcceptDrop);
    }

    [Fact]
    public void SwitchingPages_KeepsWhatThePageHeld()
    {
        Harness harness = new();
        harness.FillEditor();

        harness.Shell.CurrentPage = AppPage.VerifyRestore;
        harness.Shell.CurrentPage = AppPage.Backup;

        Assert.Equal("/home/jo/Documents", Assert.Single(harness.Shell.BackupPage.Single.Inputs).Path);
    }

    [Fact]
    public void ADrop_OnTheBackupPage_AddsItsInputs()
    {
        Harness harness = new();

        Assert.True(harness.Shell.CanAcceptDrop);

        harness.Shell.AddDroppedInputs(["/home/jo/Pictures", "/home/jo/Music"]);

        Assert.Equal(2, harness.BackupEditor.Inputs.Count);
    }

    [Fact]
    public void ADrop_OnTheProfilesPage_AddsToTheProfile()
    {
        Harness harness = new();
        harness.Shell.CurrentPage = AppPage.Profiles;

        Assert.True(harness.Shell.IsProfilesPage);
        Assert.True(harness.Shell.CanAcceptDrop);

        harness.Shell.AddDroppedInputs(["/home/jo/Pictures"]);

        Assert.Equal("/home/jo/Pictures", Assert.Single(harness.ProfileEditor.Inputs).Path);
        Assert.Empty(harness.BackupEditor.Inputs);
    }

    [Fact]
    public void ADrop_OnTheBackupPage_IsRefusedWhileItBacksUpAProfile()
    {
        Harness harness = new();
        harness.BackupPage.UseProfile = true;

        Assert.False(harness.Shell.CanAcceptDrop);

        harness.Shell.AddDroppedInputs(["/home/jo/Pictures"]);

        Assert.Empty(harness.BackupEditor.Inputs);
        Assert.Empty(harness.FromProfileEditor.Inputs);
    }

    [Fact]
    public void ADrop_OnAPageWithoutInputs_IsRefused()
    {
        Harness harness = new();
        harness.Shell.CurrentPage = AppPage.VerifyRestore;

        Assert.False(harness.Shell.CanAcceptDrop);

        harness.Shell.AddDroppedInputs(["/home/jo/Pictures"]);

        Assert.Empty(harness.BackupEditor.Inputs);
    }

    [Fact]
    public async Task ADrop_WhileARunIsGoing_IsRefused()
    {
        Harness harness = new();
        harness.FillEditor();

        bool acceptedWhileRunning = true;
        harness.Backups.Behaviour = (_, _, _, _, _) =>
        {
            acceptedWhileRunning = harness.Shell.CanAcceptDrop;

            return Task.FromResult(Results.Backup());
        };

        await harness.BackupPage.BackupCommand.ExecuteAsync(null);

        Assert.False(acceptedWhileRunning);
        Assert.True(harness.Shell.CanAcceptDrop);
    }

    [Fact]
    public void TheConstructor_RefusesNulls()
    {
        Harness harness = new();

        Assert.Throws<ArgumentNullException>(() => new MainWindowViewModel(null!, harness.VerifyRestorePage, harness.ProfilesPage, harness.SettingsPage));
        Assert.Throws<ArgumentNullException>(() => new MainWindowViewModel(harness.BackupPage, null!, harness.ProfilesPage, harness.SettingsPage));
        Assert.Throws<ArgumentNullException>(() => new MainWindowViewModel(harness.BackupPage, harness.VerifyRestorePage, null!, harness.SettingsPage));
        Assert.Throws<ArgumentNullException>(() => new MainWindowViewModel(harness.BackupPage, harness.VerifyRestorePage, harness.ProfilesPage, null!));
    }
}
