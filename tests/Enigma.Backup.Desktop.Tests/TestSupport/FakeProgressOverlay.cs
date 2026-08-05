using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using Enigma.Backup.Desktop.Services;

namespace Enigma.Backup.Desktop.Tests.TestSupport;

/// <summary>An <see cref="IProgressOverlay"/> that records the card it would have drawn.</summary>
/// <remarks>
/// What matters about the overlay is not what it looks like but that it goes up once, is told what is
/// happening, and always comes down again — a scrim left up is an app nobody can click. All three are
/// countable from here.
/// </remarks>
internal sealed class FakeProgressOverlay : IProgressOverlay
{
    /// <summary>How many times the card was raised.</summary>
    public int Shows { get; private set; }

    /// <summary>How many times it was taken away.</summary>
    public int Hides { get; private set; }

    /// <summary>Whether it is on screen right now.</summary>
    public bool IsShowing => Shows > Hides;

    /// <summary>The heading of every card that was raised, in order.</summary>
    public List<string> Titles { get; } = [];

    /// <summary>The Cancel command the last card was given.</summary>
    public ICommand? CancelCommand { get; private set; }

    /// <summary>Every report the card was given, in order.</summary>
    public List<(string Message, string? CurrentItem, double Percent, bool IsIndeterminate)> Reports { get; } = [];

    /// <inheritdoc />
    public Task ShowAsync(string title, ICommand cancelCommand)
    {
        Shows++;
        Titles.Add(title);
        CancelCommand = cancelCommand;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Update(string message, string? currentItem, double percent, bool isIndeterminate) =>
        Reports.Add((message, currentItem, percent, isIndeterminate));

    /// <inheritdoc />
    public Task HideAsync()
    {
        Hides++;

        return Task.CompletedTask;
    }
}
