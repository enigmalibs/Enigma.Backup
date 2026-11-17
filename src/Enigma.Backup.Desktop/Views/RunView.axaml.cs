using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Enigma.Backup.Desktop.ViewModels;

namespace Enigma.Backup.Desktop.Views;

/// <summary>The run dialog's content: the run's progress while it goes, then its outcome.</summary>
public partial class RunView : UserControl
{
    // Parameterless constructor for the XAML previewer/designer.
    public RunView()
    {
        InitializeComponent();
    }

    /// <summary>Creates the content around the run it shows.</summary>
    /// <param name="viewModel">The run.</param>
    public RunView(RunViewModel viewModel)
        : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        DataContext = viewModel;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // The dialog has to hold the focus, or Enter and Escape go to whatever the window had focused —
        // behind the scrim, where they must not land. Posted because this content is attached while the
        // dialog host is still collapsed, and an invisible control cannot take the focus.
        Dispatcher.UIThread.Post(() => Focus(), DispatcherPriority.Loaded);
    }
}
