using System.Collections.Concurrent;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace OtelEvents.Causality.Tests;

/// <summary>
/// In-memory exporter that captures LogRecord snapshots for test assertions.
/// LogRecords are pooled/mutable in OTEL, so we snapshot the attributes on export.
/// </summary>
internal sealed class TestLogExporter : BaseExporter<LogRecord>
{
    private readonly ConcurrentBag<LogRecordSnapshot> _records = new();

    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        foreach (var record in batch)
        {
            _records.Add(LogRecordSnapshot.From(record));
        }

        return ExportResult.Success;
    }

    public IReadOnlyList<LogRecordSnapshot> GetRecords() => _records.ToList();

    public void Clear()
    {
        while (_records.TryTake(out _)) { }
    }
}

/// <summary>
/// Immutable snapshot of a LogRecord's attributes for assertions.
/// </summary>
internal sealed record LogRecordSnapshot
{
    public IReadOnlyDictionary<string, object?> Attributes { get; init; } = new Dictionary<string, object?>();

    public static LogRecordSnapshot From(LogRecord record)
    {
        var attrs = new Dictionary<string, object?>();

        if (record.Attributes is not null)
        {
            foreach (var kvp in record.Attributes)
            {
                attrs[kvp.Key] = kvp.Value;
            }
        }

        return new LogRecordSnapshot { Attributes = attrs };
    }
}
