using System;
using Avalonia.Controls;
using Enigma.Backup.Desktop.ViewModels;

namespace Enigma.Backup.Desktop.Views;

/// <summary>The prompt behind "Save as" and "Rename".</summary>
public partial class ProfileNameDialog : Window
{
    private ProfileNameDialogViewModel? _viewModel;

    // Parameterless constructor for the XAML previewer/designer.
    public ProfileNameDialog()
    {
        InitializeComponent();
    }

    /// <summary>Creates the dialog around its view model.</summary>
    /// <param name="viewModel">The prompt to show, which also holds the answer afterwards.</param>
    public ProfileNameDialog(ProfileNameDialogViewModel viewModel)
        : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
    }

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        NameInput.Focus();
        NameInput.SelectAll();
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
