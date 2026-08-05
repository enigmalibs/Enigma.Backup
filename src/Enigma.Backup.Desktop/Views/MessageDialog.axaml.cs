using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Enigma.Backup.Desktop.ViewModels;

namespace Enigma.Backup.Desktop.Views;

/// <summary>The one-message dialog: something to acknowledge, or something to confirm.</summary>
/// <remarks>
/// The content of a <c>ContentDialog</c>, not a window — see <see cref="Services.DialogService"/>.
/// </remarks>
public partial class MessageDialog : UserControl
{
    // Parameterless constructor for the XAML previewer/designer.
    public MessageDialog()
    {
        InitializeComponent();
    }

    /// <summary>Creates the dialog's content around its view model.</summary>
    /// <param name="viewModel">The message to show, which also holds the answer afterwards.</param>
    public MessageDialog(MessageDialogViewModel viewModel)
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
