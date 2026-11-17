using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Enigma.Avalonia.Desktop.Controls.Navigation;
using Enigma.Backup.Desktop.ViewModels;
using Enigma.Icons.Avalonia;
using Enigma.Icons.Phosphor;

namespace Enigma.Backup.Desktop.Views;

/// <summary>The application's only real window.</summary>
/// <remarks>
/// The code-behind holds exactly what a view model cannot: the rail's items, which are controls; dropped
/// files, which arrive as platform storage items and leave as plain paths; and the first read of the
/// profile store, which waits until the window is on screen.
/// </remarks>
public partial class MainWindow : Window
{
    /// <summary>Every rail item, by the page it shows.</summary>
    private readonly Dictionary<AppPage, NavigationItem> _items = [];

    private MainWindowViewModel? _viewModel;

    // Parameterless constructor for the XAML previewer/designer.
    public MainWindow()
    {
        InitializeComponent();

        // The pages in the rail's order, and the app's own page at its foot.
        Rail.Items =
        [
            Item(AppPage.Backup, "Backup", PhosphorIcon.Archive),
            Item(AppPage.VerifyRestore, "Verify & Restore", PhosphorIcon.ClockCounterClockwise),
            Item(AppPage.Profiles, "Profiles", PhosphorIcon.FileText),
        ];
        Rail.FooterItems = [Item(AppPage.Settings, "Settings", PhosphorIcon.Gear)];
        Rail.PropertyChanged += OnRailPropertyChanged;

        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    /// <summary>Creates the window around its view model.</summary>
    /// <param name="viewModel">The window's state and actions, resolved from the container.</param>
    public MainWindow(MainWindowViewModel viewModel)
        : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        DataContext = viewModel;
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as MainWindowViewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            ShowCurrentPageOnRail();
        }
    }

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // Reading the store is deferred to here so a slow disk cannot hold up the window appearing.
        if (_viewModel?.ProfilesPage.RefreshProfilesCommand is { } refresh && refresh.CanExecute(null))
        {
            refresh.Execute(null);
        }
    }

    private NavigationItem Item(AppPage page, string header, PhosphorIcon icon)
    {
        NavigationItem item = new()
        {
            Header = header,
            IconData = PhosphorIconSet.Instance.GetGlyph(icon, PhosphorWeight.Regular).ToGeometry(),
        };

        _items[page] = item;

        return item;
    }

    private void OnRailPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        // The rail drops its selection while it rebuilds its lists; a page is always shown, so a null
        // — or an item that is not ours — is not a choice and is ignored.
        if (e.Property != NavigationView.SelectedItemProperty || _viewModel is null || Rail.SelectedItem is not NavigationItem selected)
        {
            return;
        }

        foreach ((AppPage page, NavigationItem item) in _items)
        {
            if (ReferenceEquals(item, selected))
            {
                _viewModel.CurrentPage = page;

                return;
            }
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.CurrentPage))
        {
            ShowCurrentPageOnRail();
        }
    }

    private void ShowCurrentPageOnRail()
    {
        if (_viewModel is not null && _items.TryGetValue(_viewModel.CurrentPage, out NavigationItem? item))
        {
            Rail.SelectedItem = item;
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = _viewModel is { CanAcceptDrop: true } && e.DataTransfer.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;

        if (_viewModel is not { CanAcceptDrop: true } viewModel || e.DataTransfer.TryGetFiles() is not IEnumerable<IStorageItem> items)
        {
            return;
        }

        // A location without a local path is not something a backup could read, so it is dropped.
        List<string> paths = [];
        foreach (IStorageItem item in items)
        {
            if (item.TryGetLocalPath() is string path)
            {
                paths.Add(path);
            }
        }

        if (paths.Count > 0)
        {
            viewModel.AddDroppedInputs(paths);
        }
    }
}
