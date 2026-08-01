using System;
using Avalonia.Controls;
using Enigma.Backup.Desktop.ViewModels;

namespace Enigma.Backup.Desktop.Views;

/// <summary>The Skip / Retry / Abort prompt raised for every file a backup cannot read.</summary>
public partial class ErrorDecisionDialog : Window
{
    private ErrorDecisionDialogViewModel? _viewModel;

    // Parameterless constructor for the XAML previewer/designer.
    public ErrorDecisionDialog()
    {
        InitializeComponent();
    }

    /// <summary>Creates the dialog around its view model.</summary>
    /// <param name="viewModel">The failure to show, which also holds the decision afterwards.</param>
    public ErrorDecisionDialog(ErrorDecisionDialogViewModel viewModel)
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
