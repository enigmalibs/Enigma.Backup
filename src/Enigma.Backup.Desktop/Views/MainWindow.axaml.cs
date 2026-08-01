using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Enigma.Backup.Desktop.ViewModels;

namespace Enigma.Backup.Desktop.Views;

/// <summary>The application's only real window.</summary>
/// <remarks>
/// The code-behind holds exactly the two things a view model cannot: dropped files, which arrive as
/// platform storage items and leave as plain paths, and the first read of the profile store, which
/// waits until the window is on screen.
/// </remarks>
public partial class MainWindow : Window
{
    // Parameterless constructor for the XAML previewer/designer.
    public MainWindow()
    {
        InitializeComponent();

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
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // Reading the store is deferred to here so a slow disk cannot hold up the window appearing.
        if (DataContext is MainWindowViewModel viewModel && viewModel.RefreshProfilesCommand.CanExecute(null))
        {
            viewModel.RefreshProfilesCommand.Execute(null);
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;

        if (DataContext is not MainWindowViewModel viewModel || e.DataTransfer.TryGetFiles() is not IEnumerable<IStorageItem> items)
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
