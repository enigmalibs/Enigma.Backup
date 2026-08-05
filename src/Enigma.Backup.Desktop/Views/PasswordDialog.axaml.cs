using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Enigma.Backup.Desktop.ViewModels;

namespace Enigma.Backup.Desktop.Views;

/// <summary>The password prompt, shown before an encrypted archive is written or opened.</summary>
/// <remarks>
/// The content of a <c>ContentDialog</c>, not a window: the buttons, the title and the closing all
/// belong to the dialog the service raises around this (see <see cref="Services.DialogService"/>).
/// </remarks>
public partial class PasswordDialog : UserControl
{
    // Parameterless constructor for the XAML previewer/designer.
    public PasswordDialog()
    {
        InitializeComponent();
    }

    /// <summary>Creates the dialog's content around its view model.</summary>
    /// <param name="viewModel">The prompt to show, which also holds the answer afterwards.</param>
    public PasswordDialog(PasswordDialogViewModel viewModel)
        : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        DataContext = viewModel;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Typing a password should not start with reaching for the mouse. The focus is posted rather
        // than taken here: this content is attached while the dialog host is still collapsed — it is
        // opened immediately afterwards — and an invisible control cannot take the focus.
        Dispatcher.UIThread.Post(() => PasswordInput.Focus(), DispatcherPriority.Loaded);
    }
}
