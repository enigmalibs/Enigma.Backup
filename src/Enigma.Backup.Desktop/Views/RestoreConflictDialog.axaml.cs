using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Enigma.Backup.Desktop.ViewModels;

namespace Enigma.Backup.Desktop.Views;

/// <summary>The Skip / Overwrite / Abort prompt raised for every file a restore would replace.</summary>
/// <remarks>
/// The content of a <c>ContentDialog</c>, not a window — see <see cref="Services.DialogService"/>.
/// </remarks>
public partial class RestoreConflictDialog : UserControl
{
    // Parameterless constructor for the XAML previewer/designer.
    public RestoreConflictDialog()
    {
        InitializeComponent();
    }

    /// <summary>Creates the dialog's content around its view model.</summary>
    /// <param name="viewModel">The conflict to show, which also holds the decision afterwards.</param>
    public RestoreConflictDialog(RestoreConflictDialogViewModel viewModel)
        : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        DataContext = viewModel;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // A dialog with nothing to type in still has to hold the focus, or Enter and Escape go to
        // whatever the main window had focused — behind the scrim, where they must not land. Posted
        // because this content is attached while the dialog host is still collapsed, and an
        // invisible control cannot take the focus.
        Dispatcher.UIThread.Post(() => Focus(), DispatcherPriority.Loaded);
    }
}
