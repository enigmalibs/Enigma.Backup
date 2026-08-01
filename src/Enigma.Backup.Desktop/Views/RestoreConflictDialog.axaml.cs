using System;
using Avalonia.Controls;
using Enigma.Backup.Desktop.ViewModels;

namespace Enigma.Backup.Desktop.Views;

/// <summary>The Skip / Overwrite / Abort prompt raised for every file a restore would replace.</summary>
public partial class RestoreConflictDialog : Window
{
    private RestoreConflictDialogViewModel? _viewModel;

    // Parameterless constructor for the XAML previewer/designer.
    public RestoreConflictDialog()
    {
        InitializeComponent();
    }

    /// <summary>Creates the dialog around its view model.</summary>
    /// <param name="viewModel">The conflict to show, which also holds the decision afterwards.</param>
    public RestoreConflictDialog(RestoreConflictDialogViewModel viewModel)
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
