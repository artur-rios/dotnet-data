// The test project compiles with nullable reference types disabled; ILogger's signatures
// are annotated, so this file opts in to keep them matching without warnings.
#nullable enable
using Microsoft.Extensions.Logging;

namespace ArturRios.Data.Tests.TestSupport;

/// <summary>Captures log entries in memory so tests can assert on what was logged.</summary>
public sealed class ListLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception), exception));
}
