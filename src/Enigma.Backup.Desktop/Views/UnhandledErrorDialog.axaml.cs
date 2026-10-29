using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Enigma.Backup.Desktop.ViewModels;

namespace Enigma.Backup.Desktop.Views;

/// <summary>The safety net's dialog: something went wrong, the app is still running, and the details.</summary>
/// <remarks>
/// The content of a <c>ContentDialog</c>, not a window — see <see cref="Services.DialogService"/>.
/// </remarks>
public partial class UnhandledErrorDialog : UserControl
{
    // Parameterless constructor for the XAML previewer/designer.
    public UnhandledErrorDialog()
    {
        InitializeComponent();
    }

    /// <summary>Creates the dialog's content around its view model.</summary>
    /// <param name="viewModel">The errors to show.</param>
    public UnhandledErrorDialog(UnhandledErrorDialogViewModel viewModel)
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

    // The clipboard belongs to the window the dialog is in, which only the view can reach: copying from
    // here keeps a clipboard seam out of a view model that has nothing else to do with the platform.
    private async void OnCopyDetailsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is UnhandledErrorDialogViewModel viewModel && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(viewModel.Details).ConfigureAwait(true);
        }
    }
}
