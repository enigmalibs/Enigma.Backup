using System.Collections.Generic;
using Enigma.Backup.Desktop.ViewModels;
using Xunit;

namespace Enigma.Backup.Desktop.Tests.ViewModels;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Title_DefaultsToTheProductName()
    {
        MainWindowViewModel viewModel = new();

        Assert.Equal("Enigma.Backup", viewModel.Title);
    }

    [Fact]
    public void Title_RaisesPropertyChanged_WhenTheValueChanges()
    {
        MainWindowViewModel viewModel = new();
        List<string?> changed = [];
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.Title = "Enigma.Backup — Documents";

        Assert.Single(changed);
        Assert.Equal(nameof(MainWindowViewModel.Title), changed[0]);
    }

    [Fact]
    public void Title_DoesNotRaisePropertyChanged_WhenTheValueIsUnchanged()
    {
        MainWindowViewModel viewModel = new();
        List<string?> changed = [];
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        string unchanged = viewModel.Title;
        viewModel.Title = unchanged;

        Assert.Empty(changed);
    }
}
