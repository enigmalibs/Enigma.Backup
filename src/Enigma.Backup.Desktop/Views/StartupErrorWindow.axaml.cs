using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Enigma.Avalonia.Desktop.Controls.ContentDialog;
using Enigma.Backup.Desktop.ViewModels;
using Enigma.Icons.Avalonia;
using Enigma.Icons.Phosphor;

namespace Enigma.Backup.Desktop.Views;

/// <summary>The window shown when the start fails before the main window could open.</summary>
/// <remarks>
/// <para>
/// At that point there is no container to ask for <see cref="Services.IDialogService"/> — building it may
/// be what failed — so this window raises the safety net's dialog on its own host, configured as
/// <see cref="Services.DialogService"/> configures it. Closing the dialog closes the window, and with it
/// the app: it is the only window there is.
/// </para>
/// <para>
/// Nothing in here may fail on its own account: a failure in the fallback has nowhere left to go.
/// </para>
/// </remarks>
public partial class StartupErrorWindow : Window
{
    private readonly UnhandledErrorDialogViewModel? _viewModel;

    // Parameterless constructor for the XAML previewer/designer.
    public StartupErrorWindow()
    {
        InitializeComponent();
    }

    /// <summary>Creates the window for a failed start.</summary>
    /// <param name="exception">What failed.</param>
    public StartupErrorWindow(Exception exception)
        : this()
    {
        ArgumentNullException.ThrowIfNull(exception);

        _viewModel = UnhandledErrorDialogViewModel.ForStartupFailure(exception);
    }

    /// <inheritdoc />
    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (_viewModel is not { } viewModel)
        {
            return;
        }

        ContentDialog host = HostDialog;

        // Enter is the content's key binding; it asks to close, and here closing is the host's job.
        viewModel.CloseRequested += (_, _) => _ = host.HideAsync();

        host.Title = "Something went wrong";
        host.Content = new UnhandledErrorDialog(viewModel);
        host.IconData = PhosphorIconSet.Instance.GetGlyph(PhosphorIcon.Warning, PhosphorWeight.Regular).ToGeometry();

        if (Application.Current?.TryFindResource("EnigmaWarningBrush", out object? brush) == true && brush is IBrush warning)
        {
            host.IconBrush = warning;
        }

        host.CloseButtonText = "Close";
        host.DefaultButton = DefaultButton.Close;

        await host.ShowAsync().ConfigureAwait(true);

        Close();
    }
}
