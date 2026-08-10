using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Enigma.Backup.Desktop.ViewModels;

namespace Enigma.Backup.Desktop.Views;

/// <summary>The prompt behind "Add pattern…".</summary>
/// <remarks>
/// The content of a <c>ContentDialog</c>, not a window — see <see cref="Services.DialogService"/>.
/// </remarks>
public partial class PatternInputDialog : UserControl
{
    // Parameterless constructor for the XAML previewer/designer.
    public PatternInputDialog()
    {
        InitializeComponent();
    }

    /// <summary>Creates the dialog's content around its view model.</summary>
    /// <param name="viewModel">The prompt to show, which also holds the answer afterwards.</param>
    public PatternInputDialog(PatternInputDialogViewModel viewModel)
        : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        DataContext = viewModel;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // The pattern is the field that is typed — the folder beside it comes from the picker — so it
        // is the one that takes the focus. Posted because the dialog host is still collapsed while its
        // content is being set; without it Enter and Escape reach the window behind the scrim.
        Dispatcher.UIThread.Post(() => PatternBox.Focus(), DispatcherPriority.Loaded);
    }
}
