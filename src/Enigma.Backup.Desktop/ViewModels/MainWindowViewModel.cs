using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Enigma.Backup.Desktop.ViewModels;

/// <summary>
/// The window: which page the rail shows, and the pages themselves.
/// </summary>
/// <remarks>
/// <para>
/// Every page is built once and kept for as long as the window is open; the rail only changes which one
/// is visible. So a page keeps its state when the user goes elsewhere and comes back — a half-edited
/// profile is still half-edited.
/// </para>
/// <para>
/// The rail's items are Avalonia controls, so the window builds them; this knows only
/// <see cref="CurrentPage"/>, which is what a test switches.
/// </para>
/// </remarks>
public sealed class MainWindowViewModel : ObservableObject
{
    /// <summary>Creates the window's view model, on the Backup page.</summary>
    /// <param name="backupPage">The Backup page.</param>
    /// <param name="verifyRestorePage">The Verify &amp; Restore page.</param>
    /// <param name="profilesPage">The Profiles page.</param>
    /// <param name="settingsPage">The Settings page.</param>
    public MainWindowViewModel(
        BackupPageViewModel backupPage,
        VerifyRestorePageViewModel verifyRestorePage,
        ProfilesPageViewModel profilesPage,
        SettingsPageViewModel settingsPage)
    {
        ArgumentNullException.ThrowIfNull(backupPage);
        ArgumentNullException.ThrowIfNull(verifyRestorePage);
        ArgumentNullException.ThrowIfNull(profilesPage);
        ArgumentNullException.ThrowIfNull(settingsPage);

        BackupPage = backupPage;
        VerifyRestorePage = verifyRestorePage;
        ProfilesPage = profilesPage;
        SettingsPage = settingsPage;
    }

    /// <summary>The Backup page.</summary>
    public BackupPageViewModel BackupPage { get; }

    /// <summary>The Verify &amp; Restore page.</summary>
    public VerifyRestorePageViewModel VerifyRestorePage { get; }

    /// <summary>The Profiles page.</summary>
    public ProfilesPageViewModel ProfilesPage { get; }

    /// <summary>The Settings page, at the foot of the rail.</summary>
    public SettingsPageViewModel SettingsPage { get; }

    /// <summary>The page the window shows.</summary>
    public AppPage CurrentPage
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(IsBackupPage));
                OnPropertyChanged(nameof(IsVerifyRestorePage));
                OnPropertyChanged(nameof(IsProfilesPage));
                OnPropertyChanged(nameof(IsSettingsPage));
            }
        }
    } = AppPage.Backup;

    /// <summary>Whether the Backup page is shown.</summary>
    public bool IsBackupPage => CurrentPage == AppPage.Backup;

    /// <summary>Whether the Verify &amp; Restore page is shown.</summary>
    public bool IsVerifyRestorePage => CurrentPage == AppPage.VerifyRestore;

    /// <summary>Whether the Profiles page is shown.</summary>
    public bool IsProfilesPage => CurrentPage == AppPage.Profiles;

    /// <summary>Whether the Settings page is shown.</summary>
    public bool IsSettingsPage => CurrentPage == AppPage.Settings;

    /// <summary>
    /// Whether paths dropped on the window have somewhere to go: the page shown has to be one with a list
    /// of inputs — the Profiles page, or the Backup page on a one-off — and nothing may be running.
    /// </summary>
    public bool CanAcceptDrop => IsBackupPage ? BackupPage.AcceptsDrops : IsProfilesPage && BackupPage.IsIdle;

    /// <summary>
    /// Hands paths dropped onto the window to the page shown — the drag-and-drop half of "add file /
    /// add folder". Nothing happens on a page without an input list.
    /// </summary>
    /// <param name="paths">The dropped paths.</param>
    public void AddDroppedInputs(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        if (!CanAcceptDrop)
        {
            return;
        }

        if (IsProfilesPage)
        {
            ProfilesPage.AddDroppedInputs(paths);
        }
        else
        {
            BackupPage.AddDroppedInputs(paths);
        }
    }
}
