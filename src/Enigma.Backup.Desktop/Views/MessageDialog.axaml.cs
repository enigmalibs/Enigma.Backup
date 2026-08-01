using System;
using Avalonia.Controls;
using Enigma.Backup.Desktop.ViewModels;

namespace Enigma.Backup.Desktop.Views;

/// <summary>The one-message dialog: something to acknowledge, or something to confirm.</summary>
public partial class MessageDialog : Window
{
    private MessageDialogViewModel? _viewModel;

    // Parameterless constructor for the XAML previewer/designer.
    public MessageDialog()
    {
        InitializeComponent();
    }

    /// <summary>Creates the dialog around its view model.</summary>
    /// <param name="viewModel">The message to show, which also holds the answer afterwards.</param>
    public MessageDialog(MessageDialogViewModel viewModel)
        : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
            _viewModel = null;
        }

        base.OnClosed(e);
    }

    private void OnCloseRequested(object? sender, EventArgs e) => Close();
}
