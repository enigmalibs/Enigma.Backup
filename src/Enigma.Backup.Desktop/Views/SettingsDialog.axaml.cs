using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Enigma.Backup.Desktop.ViewModels;

namespace Enigma.Backup.Desktop.Views;

/// <summary>The app's preferences.</summary>
/// <remarks>
/// The content of a <c>ContentDialog</c>, not a window — see <see cref="Services.DialogService"/>.
/// </remarks>
public partial class SettingsDialog : UserControl
{
    // Parameterless constructor for the XAML previewer/designer.
    public SettingsDialog()
    {
        InitializeComponent();
    }

    /// <summary>Creates the dialog's content around its view model.</summary>
    /// <param name="viewModel">The preferences to show, which apply as they are changed.</param>
    public SettingsDialog(SettingsDialogViewModel viewModel)
        : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        DataContext = viewModel;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // The root takes the focus rather than the drop-down: a focused ComboBox swallows Enter, and
        // Enter has to reach the key binding above. Posted because this content is attached while the
        // dialog host is still collapsed, and an invisible control cannot take the focus.
        Dispatcher.UIThread.Post(() => Focus(), DispatcherPriority.Loaded);
    }
}
