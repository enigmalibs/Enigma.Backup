using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Enigma.Backup.Desktop.ViewModels;

namespace Enigma.Backup.Desktop.Views;

/// <summary>The prompt behind "Save as" and "Rename".</summary>
/// <remarks>
/// The content of a <c>ContentDialog</c>, not a window — see <see cref="Services.DialogService"/>.
/// </remarks>
public partial class ProfileNameDialog : UserControl
{
    // Parameterless constructor for the XAML previewer/designer.
    public ProfileNameDialog()
    {
        InitializeComponent();
    }

    /// <summary>Creates the dialog's content around its view model.</summary>
    /// <param name="viewModel">The prompt to show, which also holds the answer afterwards.</param>
    public ProfileNameDialog(ProfileNameDialogViewModel viewModel)
        : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        DataContext = viewModel;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Renaming starts from the current name, so it is selected and not just focused. Posted
        // because the dialog host is still collapsed while its content is being set.
        Dispatcher.UIThread.Post(
            () =>
            {
                NameInput.Focus();
                NameInput.SelectAll();
            },
            DispatcherPriority.Loaded);
    }
}
