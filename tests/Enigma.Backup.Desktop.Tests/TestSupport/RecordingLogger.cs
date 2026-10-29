using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Enigma.Backup.Desktop.Tests.TestSupport;

/// <summary>An <see cref="ILogger{TCategoryName}"/> that keeps every entry for a test to read.</summary>
/// <typeparam name="T">The category.</typeparam>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    /// <summary>Every entry written, in order.</summary>
    public List<(LogLevel Level, Exception? Exception, string Message)> Entries { get; } = [];

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, exception, formatter(state, exception)));
}
