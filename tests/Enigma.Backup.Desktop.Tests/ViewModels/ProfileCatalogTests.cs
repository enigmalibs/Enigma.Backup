using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;
using Enigma.Backup.Core.Profiles;
using Enigma.Backup.Desktop.Services;
using Enigma.Backup.Desktop.Tests.TestSupport;
using Enigma.Backup.Desktop.ViewModels;
using Xunit;

namespace Enigma.Backup.Desktop.Tests.ViewModels;

/// <summary>
/// The profiles every page lists: what a reload reads, what it leaves out, and how little it disturbs a
/// list that is bound to more than one drop-down.
/// </summary>
public sealed class ProfileCatalogTests
{
    [Fact]
    public async Task Reload_ListsEveryProfileInNameOrder_AndKeepsWhatEachHolds()
    {
        Harness harness = new();
        harness.Profiles.Seed(Harness.Profile("photos", inputs: ["/pics"]));
        harness.Profiles.Seed(Harness.Profile("Documents", inputs: ["/docs"]));
        harness.Profiles.Seed(Harness.Profile("archive", inputs: ["/old"]));
        int reloads = 0;
        harness.Catalog.Reloaded += (_, _) => reloads++;

        Assert.True(await harness.Catalog.ReloadAsync());

        Assert.Equal(["archive", "Documents", "photos"], harness.Catalog.Names);
        Assert.True(harness.Catalog.TryGet("DOCUMENTS", out BackupProfile? documents));
        Assert.Equal("/docs", Assert.Single(documents.Inputs));
        Assert.True(harness.Catalog.Contains("photos"));
        Assert.False(harness.Catalog.Contains("missing"));
        Assert.Equal(1, reloads);
    }

    [Fact]
    public async Task Reload_LeavesOutAProfileItCannotRead_AndSaysSo()
    {
        Harness harness = new();
        harness.Profiles.Seed(Harness.Profile("Documents"));
        harness.Profiles.Seed(Harness.Profile("Broken"));
        harness.Profiles.Unreadable.Add("Broken");

        Assert.True(await harness.Catalog.ReloadAsync());

        Assert.Equal(["Documents"], harness.Catalog.Names);
        (string _, string message, MessageSeverity severity) = Assert.Single(harness.Dialogs.Messages);
        Assert.Equal(MessageSeverity.Warning, severity);
        Assert.Contains("'Broken'", message, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reload_OfAStoreThatCannotBeListed_ChangesNothing_AndSaysSo()
    {
        Harness harness = new();
        harness.Profiles.Seed(Harness.Profile("Documents"));
        await harness.Catalog.ReloadAsync();
        int reloads = 0;
        harness.Catalog.Reloaded += (_, _) => reloads++;

        harness.Profiles.FailNextList = new IOException("The disk went away.");

        Assert.False(await harness.Catalog.ReloadAsync());

        Assert.Equal(["Documents"], harness.Catalog.Names);
        Assert.True(harness.Catalog.Contains("Documents"));
        Assert.Equal(0, reloads);
        Assert.Equal(MessageSeverity.Error, Assert.Single(harness.Dialogs.Messages).Severity);
    }

    /// <remarks>
    /// The list is bound to a drop-down on more than one page. Rebuilding it from scratch would make
    /// every one of them drop its selection; touching only what changed keeps a selection whose profile
    /// is still there.
    /// </remarks>
    [Fact]
    public async Task Reload_ChangesOnlyTheNamesThatChanged()
    {
        Harness harness = new();
        harness.Profiles.Seed(Harness.Profile("Documents"));
        harness.Profiles.Seed(Harness.Profile("Photos"));
        await harness.Catalog.ReloadAsync();

        List<NotifyCollectionChangedAction> changes = [];
        harness.Catalog.Names.CollectionChanged += (_, e) => changes.Add(e.Action);

        harness.Profiles.Seed(Harness.Profile("Music"));
        await harness.Catalog.ReloadAsync();

        Assert.Equal([NotifyCollectionChangedAction.Add], changes);
        Assert.Equal(["Documents", "Music", "Photos"], harness.Catalog.Names);

        changes.Clear();
        await harness.Profiles.DeleteAsync("Documents", TestContext.Current.CancellationToken);
        await harness.Catalog.ReloadAsync();

        Assert.Equal([NotifyCollectionChangedAction.Remove], changes);
        Assert.Equal(["Music", "Photos"], harness.Catalog.Names);
    }

    [Fact]
    public async Task Reload_TreatsANameChangedOnlyInCaseAsTheNewName()
    {
        Harness harness = new();
        harness.Profiles.Seed(Harness.Profile("docs"));
        await harness.Catalog.ReloadAsync();

        await harness.Profiles.RenameAsync("docs", "Docs", TestContext.Current.CancellationToken);
        await harness.Catalog.ReloadAsync();

        Assert.Equal(["Docs"], harness.Catalog.Names);
    }
}
