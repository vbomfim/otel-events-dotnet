using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace OtelEvents.HealthChecks.Tests;

/// <summary>
/// In-memory logger that captures log entries for test assertions.
/// Captures EventId, LogLevel, message, and structured state parameters.
/// </summary>
internal sealed class TestLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => _entries.ToList();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var parameters = new Dictionary<string, object?>();

        if (state is IReadOnlyList<KeyValuePair<string, object?>> structuredState)
        {
            foreach (var kvp in structuredState)
            {
                if (kvp.Key != "{OriginalFormat}")
                {
                    parameters[kvp.Key] = kvp.Value;
                }
            }
        }

        _entries.Enqueue(new LogEntry(
            LogLevel: logLevel,
            EventId: eventId,
            Message: formatter(state, exception),
            Parameters: parameters,
            Exception: exception));
    }

    /// <summary>
    /// Gets all log entries with the specified event name.
    /// </summary>
    public IReadOnlyList<LogEntry> GetEntriesByEventName(string eventName)
        => Entries.Where(e => e.EventId.Name == eventName).ToList();

    /// <summary>
    /// Clears all captured entries.
    /// </summary>
    public void Clear()
    {
        while (_entries.TryDequeue(out _)) { }
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();
        public void Dispose() { }
    }
}

/// <summary>
/// Immutable snapshot of a captured log entry.
/// </summary>
internal sealed record LogEntry(
    LogLevel LogLevel,
    EventId EventId,
    string Message,
    IReadOnlyDictionary<string, object?> Parameters,
    Exception? Exception);
