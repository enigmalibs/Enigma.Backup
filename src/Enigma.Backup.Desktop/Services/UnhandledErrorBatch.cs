using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Enigma.Backup.Desktop.Services;

/// <summary>The errors one dialog shows: the first, and how many more came while it waited or was open.</summary>
/// <remarks>
/// Every member is called on the UI thread: errors are counted in by <see cref="UnhandledErrorPolicy"/>,
/// which the reporter only ever consults there.
/// </remarks>
public sealed class UnhandledErrorBatch
{
    /// <summary>How deep the details follow inner exceptions before they stop.</summary>
    private const int MaximumDepth = 8;

    private readonly List<Exception> _errors = [];

    /// <summary>Opens a batch on its first error.</summary>
    /// <param name="first">The error that opens it.</param>
    public UnhandledErrorBatch(Exception first)
    {
        ArgumentNullException.ThrowIfNull(first);

        _errors.Add(first);
    }

    /// <summary>Raised when another error is counted into the batch, so a dialog on screen can follow.</summary>
    public event EventHandler? Changed;

    /// <summary>The error that opened the batch.</summary>
    public Exception First => _errors[0];

    /// <summary>Every error of the batch, the first one first.</summary>
    public IReadOnlyList<Exception> Errors => _errors;

    /// <summary>What the dialog says went wrong: the first error's message.</summary>
    public string Message => Summarise(First);

    /// <summary>How many errors came after the first.</summary>
    public int More => _errors.Count - 1;

    /// <summary>
    /// The details of every error, for a bug report: type, message, stack trace and inner exceptions.
    /// </summary>
    public string Details
    {
        get
        {
            if (_errors.Count == 1)
            {
                return BuildDetails(First);
            }

            StringBuilder details = new();

            for (int index = 0; index < _errors.Count; index++)
            {
                if (index > 0)
                {
                    details.AppendLine().AppendLine();
                }

                details.Append("Error ").Append(index + 1).Append(" of ").Append(_errors.Count).AppendLine();
                details.Append(BuildDetails(_errors[index]));
            }

            return details.ToString();
        }
    }

    /// <summary>Says what went wrong in one line: the message of the exception that carries the meaning.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The sentence.</returns>
    public static string Summarise(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        Exception meaningful = exception;

        // Wrappers say "one or more errors occurred" or "an invocation threw": the one inside says what.
        while (meaningful is AggregateException { InnerExceptions.Count: 1 } or TargetInvocationException { InnerException: not null })
        {
            meaningful = meaningful.InnerException!;
        }

        string message = meaningful.Message.Trim();

        return message.Length > 0 ? message : meaningful.GetType().Name;
    }

    /// <summary>
    /// Builds the details of an exception for a bug report: for it and every exception inside it, the
    /// type, the message and the stack trace — nothing else.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The text.</returns>
    /// <remarks>
    /// Never <see cref="Exception.Data"/>, never <see cref="Exception.ToString"/>, never a property of a
    /// derived type: those are where whatever an exception was carrying travels, and a bug report is
    /// pasted somewhere public.
    /// </remarks>
    public static string BuildDetails(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        StringBuilder details = new();
        Append(details, exception, 0);

        return details.ToString().TrimEnd();
    }

    /// <summary>Counts another error into the batch.</summary>
    /// <param name="error">The error.</param>
    internal void Add(Exception error)
    {
        _errors.Add(error);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static void Append(StringBuilder details, Exception exception, int depth)
    {
        details.Append(exception.GetType().FullName).Append(": ").AppendLine(exception.Message);

        if (exception.StackTrace is { Length: > 0 } stack)
        {
            details.AppendLine(stack);
        }

        if (depth >= MaximumDepth)
        {
            return;
        }

        if (exception is AggregateException aggregate)
        {
            for (int index = 0; index < aggregate.InnerExceptions.Count; index++)
            {
                details.Append("--- Inner exception ").Append(index + 1).Append(" of ")
                    .Append(aggregate.InnerExceptions.Count).AppendLine(" ---");
                Append(details, aggregate.InnerExceptions[index], depth + 1);
            }
        }
        else if (exception.InnerException is { } inner)
        {
            details.AppendLine("--- Inner exception ---");
            Append(details, inner, depth + 1);
        }
    }
}
