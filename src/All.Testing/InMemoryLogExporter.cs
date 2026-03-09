using System.Collections.Concurrent;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace All.Testing;

/// <summary>
/// In-memory OTEL LogRecord collector for testing.
/// Implements <see cref="BaseExporter{T}"/> and plugs directly into the OTEL pipeline.
/// <para>
/// Captured records are stored as <see cref="ExportedLogRecord"/> snapshots,
/// since OpenTelemetry recycles <see cref="LogRecord"/> instances.
/// Thread-safe for concurrent test scenarios.
/// </para>
/// </summary>
public sealed class InMemoryLogExporter : BaseExporter<LogRecord>
{
    private readonly ConcurrentQueue<ExportedLogRecord> _records = new();

    /// <summary>
    /// Gets all captured log records as a read-only list in chronological order.
    /// Returns a point-in-time snapshot of the collected records.
    /// </summary>
    public IReadOnlyList<ExportedLogRecord> LogRecords => _records.ToArray().ToList();

    /// <summary>
    /// Exports a batch of <see cref="LogRecord"/> instances by snapshotting them
    /// into <see cref="ExportedLogRecord"/> instances.
    /// </summary>
    /// <param name="batch">The batch of log records to export.</param>
    /// <returns><see cref="ExportResult.Success"/> always.</returns>
    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        foreach (var record in batch)
        {
            _records.Enqueue(ExportedLogRecord.From(record));
        }

        return ExportResult.Success;
    }

    /// <summary>
    /// Removes all captured records. Thread-safe.
    /// </summary>
    public void Clear()
    {
        while (_records.TryDequeue(out _)) { }
    }
}
