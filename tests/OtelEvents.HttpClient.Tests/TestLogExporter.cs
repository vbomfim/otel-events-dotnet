using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace OtelEvents.HttpClient.Tests;

/// <summary>
/// In-memory OTEL LogRecord collector for testing.
/// Captures immutable snapshots of LogRecords for safe assertions.
/// </summary>
internal sealed class TestLogExporter : BaseExporter<LogRecord>
{
    private readonly ConcurrentQueue<TestLogRecord> _records = new();

    /// <summary>Gets all captured log records as a list in chronological order.</summary>
    public IReadOnlyList<TestLogRecord> LogRecords => _records.ToArray().ToList();

    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        foreach (var record in batch)
        {
            _records.Enqueue(TestLogRecord.From(record));
        }

        return ExportResult.Success;
    }

    /// <summary>Asserts that at least one record with the specified event name was emitted.</summary>
    public void AssertEventEmitted(string eventName)
    {
        var found = LogRecords.Any(r => r.EventName == eventName);
        if (!found)
        {
            var emittedEvents = LogRecords.Count > 0
                ? string.Join(", ", LogRecords.Select(r => $"'{r.EventName}'"))
                : "(none)";
            throw new Xunit.Sdk.XunitException(
                $"Expected event '{eventName}' to be emitted, but it was not found. Emitted events: {emittedEvents}");
        }
    }

    /// <summary>Asserts that no record with the specified event name was emitted.</summary>
    public void AssertEventNotEmitted(string eventName)
    {
        var found = LogRecords.Any(r => r.EventName == eventName);
        if (found)
        {
            throw new Xunit.Sdk.XunitException(
                $"Expected event '{eventName}' NOT to be emitted, but it was found.");
        }
    }

    /// <summary>Asserts exactly one record with the specified event name and returns it.</summary>
    public TestLogRecord AssertSingle(string eventName)
    {
        var matches = LogRecords.Where(r => r.EventName == eventName).ToList();
        if (matches.Count == 0)
        {
            var emittedEvents = LogRecords.Count > 0
                ? string.Join(", ", LogRecords.Select(r => $"'{r.EventName}'"))
                : "(none)";
            throw new Xunit.Sdk.XunitException(
                $"Expected exactly one '{eventName}' event, but found none. Emitted events: {emittedEvents}");
        }
        if (matches.Count > 1)
        {
            throw new Xunit.Sdk.XunitException(
                $"Expected exactly one '{eventName}' event, but found {matches.Count}.");
        }
        return matches[0];
    }
}

/// <summary>
/// Immutable snapshot of a LogRecord for test assertions.
/// </summary>
internal sealed class TestLogRecord
{
    public string? EventName { get; init; }
    public EventId EventId { get; init; }
    public LogLevel LogLevel { get; init; }
    public string? FormattedMessage { get; init; }
    public Exception? Exception { get; init; }
    public IReadOnlyDictionary<string, object?> Attributes { get; init; } = new Dictionary<string, object?>();

    public static TestLogRecord From(LogRecord record)
    {
        var attrs = new Dictionary<string, object?>();
        if (record.Attributes is not null)
        {
            foreach (var kvp in record.Attributes)
            {
                attrs[kvp.Key] = kvp.Value;
            }
        }

        return new TestLogRecord
        {
            EventName = record.EventId.Name,
            EventId = record.EventId,
            LogLevel = record.LogLevel,
            FormattedMessage = record.FormattedMessage,
            Exception = record.Exception,
            Attributes = attrs
        };
    }

    /// <summary>Asserts that the record contains an attribute with the specified key and expected value.</summary>
    public void AssertAttribute(string key, object? expected)
    {
        if (!Attributes.TryGetValue(key, out var actual))
        {
            var availableKeys = Attributes.Count > 0
                ? string.Join(", ", Attributes.Keys.Select(k => $"'{k}'"))
                : "(none)";
            throw new Xunit.Sdk.XunitException(
                $"Expected attribute '{key}' not found. Available attributes: {availableKeys}");
        }
        if (!Equals(actual, expected))
        {
            throw new Xunit.Sdk.XunitException(
                $"Attribute '{key}' expected value '{expected}' (type: {expected?.GetType().Name ?? "null"}) " +
                $"but found '{actual}' (type: {actual?.GetType().Name ?? "null"}).");
        }
    }
}
