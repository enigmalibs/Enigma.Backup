using System;
using Avalonia.Controls;
using Enigma.Backup.Desktop.ViewModels;

namespace Enigma.Backup.Desktop.Views;

/// <summary>The password prompt, shown before an encrypted archive is written or opened.</summary>
public partial class PasswordDialog : Window
{
    private PasswordDialogViewModel? _viewModel;

    // Parameterless constructor for the XAML previewer/designer.
    public PasswordDialog()
    {
        InitializeComponent();
    }

    /// <summary>Creates the dialog around its view model.</summary>
    /// <param name="viewModel">The prompt to show, which also holds the answer afterwards.</param>
    public PasswordDialog(PasswordDialogViewModel viewModel)
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

        // Typing a password should not start with reaching for the mouse.
        PasswordInput.Focus();
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
