using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace Enigma.Backup.Desktop.Views;

/// <summary>
/// The card the run overlay holds: what is running, how far it has got, what it is working on, and the
/// Cancel button that stops it.
/// </summary>
/// <remarks>
/// <para>
/// The control library ships no such control — <c>IOverlayService</c> takes any <see cref="Control"/>,
/// so this is ours. Its theme is <c>Views/ProgressOverlayCard.axaml</c>, merged from <c>App.axaml</c>
/// after the library's dictionary so it can read the same <c>Enigma*</c> brushes the library's own
/// controls use.
/// </para>
/// <para>
/// A <see cref="ContentControl"/> like the library's three hosts, but everything it shows is one of the
/// properties below: there is no arbitrary content to place, so the theme presents none.
/// </para>
/// </remarks>
public class ProgressOverlayCard : ContentControl
{
    /// <summary>Defines the <see cref="Title"/> property.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ProgressOverlayCard, string?>(nameof(Title));

    /// <summary>Defines the <see cref="Message"/> property.</summary>
    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<ProgressOverlayCard, string?>(nameof(Message));

    /// <summary>Defines the <see cref="CurrentItem"/> property.</summary>
    public static readonly StyledProperty<string?> CurrentItemProperty =
        AvaloniaProperty.Register<ProgressOverlayCard, string?>(nameof(CurrentItem));

    /// <summary>Defines the <see cref="IsIndeterminate"/> property.</summary>
    public static readonly StyledProperty<bool> IsIndeterminateProperty =
        AvaloniaProperty.Register<ProgressOverlayCard, bool>(nameof(IsIndeterminate), true);

    /// <summary>Defines the <see cref="Progress"/> property.</summary>
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<ProgressOverlayCard, double>(nameof(Progress));

    /// <summary>Defines the <see cref="CancelCommand"/> property.</summary>
    public static readonly StyledProperty<ICommand?> CancelCommandProperty =
        AvaloniaProperty.Register<ProgressOverlayCard, ICommand?>(nameof(CancelCommand));

    /// <summary>Gets or sets what is running, as the card's heading.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Gets or sets what is happening, in one line. Hidden when null or empty.</summary>
    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>Gets or sets the file being processed. Hidden when null or empty.</summary>
    public string? CurrentItem
    {
        get => GetValue(CurrentItemProperty);
        set => SetValue(CurrentItemProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the bar should simply animate, because how much there is to do is not
    /// known yet. Defaults to <see langword="true"/>: every run begins that way.
    /// </summary>
    public bool IsIndeterminate
    {
        get => GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    /// <summary>Gets or sets how far the run has got, from 0 to 100.</summary>
    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>Gets or sets what the Cancel button runs.</summary>
    public ICommand? CancelCommand
    {
        get => GetValue(CancelCommandProperty);
        set => SetValue(CancelCommandProperty, value);
    }
}
