using System;
using System.Runtime.CompilerServices;

namespace Enigma.Backup.Desktop.Tests.TestSupport;

/// <summary>Exceptions that were really thrown, so they carry a stack trace like the ones the app sees.</summary>
internal static class Thrown
{
    /// <summary>
    /// An <see cref="InvalidOperationException"/> thrown and caught at one fixed place: two calls with the
    /// same message give two instances with the same type, message and stack trace — "the same error again".
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The caught exception.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static InvalidOperationException Error(string message = "Something broke.")
    {
        try
        {
            throw new InvalidOperationException(message);
        }
        catch (InvalidOperationException exception)
        {
            return exception;
        }
    }

    /// <summary>The same error, thrown from a second place: same type and message, a different stack trace.</summary>
    /// <param name="message">The message.</param>
    /// <returns>The caught exception.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static InvalidOperationException ErrorElsewhere(string message = "Something broke.")
    {
        try
        {
            throw new InvalidOperationException(message);
        }
        catch (InvalidOperationException exception)
        {
            return exception;
        }
    }
}
