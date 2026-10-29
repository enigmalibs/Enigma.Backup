using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Enigma.Backup.Desktop.ViewModels;

namespace Enigma.Backup.Desktop.Views;

/// <summary>The About dialog: the app's icon, what it is, which build is running, and its copyright.</summary>
/// <remarks>
/// The content of a <c>ContentDialog</c>, not a window — see <see cref="Services.DialogService"/>.
/// Deliberately logic-free: everything shown is fixed for the life of the dialog and comes from
/// <see cref="AboutDialogViewModel"/>.
/// </remarks>
public partial class AboutDialog : UserControl
{
    // Parameterless constructor for the XAML previewer/designer.
    public AboutDialog()
    {
        InitializeComponent();
    }

    /// <summary>Creates the dialog's content around its view model.</summary>
    /// <param name="viewModel">What to say about this build.</param>
    public AboutDialog(AboutDialogViewModel viewModel)
        : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        DataContext = viewModel;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Escape and Enter both need the focus inside the dialog, or they reach the window behind the
        // scrim. Posted because this content is attached while the dialog host is still collapsed, and
        // an invisible control cannot take the focus.
        Dispatcher.UIThread.Post(() => Focus(), DispatcherPriority.Loaded);
    }
}
