using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace All.Testing.Tests;

/// <summary>
/// Tests for <see cref="InMemoryLogExporter"/> — thread-safe in-memory collector
/// that captures LogRecord snapshots for test assertions.
/// </summary>
public sealed class InMemoryLogExporterTests
{
    [Fact]
    public void Export_CapturesSingleLogRecord()
    {
        var exporter = new InMemoryLogExporter();
        var lr = CreateLogRecord(eventName: "test.event");
        var batch = new Batch<LogRecord>([lr], 1);

        var result = exporter.Export(batch);

        Assert.Equal(ExportResult.Success, result);
        Assert.Single(exporter.LogRecords);
    }

    [Fact]
    public void Export_CapturesMultipleLogRecords()
    {
        var exporter = new InMemoryLogExporter();
        var lr1 = CreateLogRecord(eventName: "event.one");
        var lr2 = CreateLogRecord(eventName: "event.two");
        var lr3 = CreateLogRecord(eventName: "event.three");
        var batch = new Batch<LogRecord>([lr1, lr2, lr3], 3);

        exporter.Export(batch);

        Assert.Equal(3, exporter.LogRecords.Count);
    }

    [Fact]
    public void Export_SnapshotsPreserveEventName()
    {
        var exporter = new InMemoryLogExporter();
        var lr = CreateLogRecord(eventName: "order.placed");
        var batch = new Batch<LogRecord>([lr], 1);

        exporter.Export(batch);

        Assert.Equal("order.placed", exporter.LogRecords[0].EventName);
    }

    [Fact]
    public void Export_SnapshotsPreserveLogLevel()
    {
        var exporter = new InMemoryLogExporter();
        var lr = CreateLogRecord(logLevel: LogLevel.Error);
        var batch = new Batch<LogRecord>([lr], 1);

        exporter.Export(batch);

        Assert.Equal(LogLevel.Error, exporter.LogRecords[0].LogLevel);
    }

    [Fact]
    public void Export_SnapshotsPreserveFormattedMessage()
    {
        var exporter = new InMemoryLogExporter();
        var lr = CreateLogRecord(message: "Test message content");
        var batch = new Batch<LogRecord>([lr], 1);

        exporter.Export(batch);

        Assert.Equal("Test message content", exporter.LogRecords[0].FormattedMessage);
    }

    [Fact]
    public void Export_SnapshotsPreserveException()
    {
        var exporter = new InMemoryLogExporter();
        var exception = new ArgumentException("bad arg");
        var lr = CreateLogRecord(exception: exception);
        var batch = new Batch<LogRecord>([lr], 1);

        exporter.Export(batch);

        Assert.Same(exception, exporter.LogRecords[0].Exception);
    }

    [Fact]
    public void Export_SnapshotsPreserveAttributes()
    {
        var exporter = new InMemoryLogExporter();
        var attrs = new List<KeyValuePair<string, object?>>
        {
            new("UserId", "usr-42"),
            new("StatusCode", 200),
        };
        var lr = CreateLogRecord(attributes: attrs);
        var batch = new Batch<LogRecord>([lr], 1);

        exporter.Export(batch);

        var record = exporter.LogRecords[0];
        Assert.Equal("usr-42", record.Attributes["UserId"]);
        Assert.Equal(200, record.Attributes["StatusCode"]);
    }

    [Fact]
    public void Export_SnapshotsPreserveTraceContext()
    {
        var exporter = new InMemoryLogExporter();
        var traceId = ActivityTraceId.CreateFromString("4bf92f3577b34da6a3ce929d0e0e4736");
        var spanId = ActivitySpanId.CreateFromString("00f067aa0ba902b7");
        var lr = CreateLogRecord(traceId: traceId, spanId: spanId);
        var batch = new Batch<LogRecord>([lr], 1);

        exporter.Export(batch);

        Assert.Equal(traceId, exporter.LogRecords[0].TraceId);
        Assert.Equal(spanId, exporter.LogRecords[0].SpanId);
    }

    [Fact]
    public void Export_MultipleBatches_AccumulatesRecords()
    {
        var exporter = new InMemoryLogExporter();

        var lr1 = CreateLogRecord(eventName: "batch.one");
        exporter.Export(new Batch<LogRecord>([lr1], 1));

        var lr2 = CreateLogRecord(eventName: "batch.two");
        exporter.Export(new Batch<LogRecord>([lr2], 1));

        Assert.Equal(2, exporter.LogRecords.Count);
    }

    [Fact]
    public void Clear_RemovesAllRecords()
    {
        var exporter = new InMemoryLogExporter();
        var lr = CreateLogRecord(eventName: "test.event");
        exporter.Export(new Batch<LogRecord>([lr], 1));

        Assert.Single(exporter.LogRecords);

        exporter.Clear();

        Assert.Empty(exporter.LogRecords);
    }

    [Fact]
    public void Clear_AllowsNewRecordsAfterClear()
    {
        var exporter = new InMemoryLogExporter();
        var lr1 = CreateLogRecord(eventName: "before.clear");
        exporter.Export(new Batch<LogRecord>([lr1], 1));

        exporter.Clear();

        var lr2 = CreateLogRecord(eventName: "after.clear");
        exporter.Export(new Batch<LogRecord>([lr2], 1));

        Assert.Single(exporter.LogRecords);
        Assert.Equal("after.clear", exporter.LogRecords[0].EventName);
    }

    [Fact]
    public void LogRecords_ReturnsReadOnlySnapshot()
    {
        var exporter = new InMemoryLogExporter();
        var lr = CreateLogRecord(eventName: "test.event");
        exporter.Export(new Batch<LogRecord>([lr], 1));

        var records = exporter.LogRecords;

        Assert.IsAssignableFrom<IReadOnlyList<ExportedLogRecord>>(records);
    }

    [Fact]
    public async Task Export_ConcurrentExports_AllRecordsCaptured()
    {
        var exporter = new InMemoryLogExporter();
        const int threadCount = 4;
        const int recordsPerThread = 50;
        var barrier = new Barrier(threadCount);

        var tasks = Enumerable.Range(0, threadCount).Select(t => Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (int r = 0; r < recordsPerThread; r++)
            {
                var lr = CreateLogRecord(eventName: $"thread{t}.record{r}");
                var batch = new Batch<LogRecord>([lr], 1);
                exporter.Export(batch);
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(threadCount * recordsPerThread, exporter.LogRecords.Count);
    }

    [Fact]
    public async Task Clear_DuringConcurrentExports_DoesNotThrow()
    {
        var exporter = new InMemoryLogExporter();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var exportTask = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                var lr = CreateLogRecord(eventName: "concurrent.event");
                var batch = new Batch<LogRecord>([lr], 1);
                exporter.Export(batch);
                await Task.Yield();
            }
        });

        var clearTask = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                exporter.Clear();
                await Task.Delay(10);
            }
        });

        await Task.WhenAll(exportTask, clearTask);

        // No exceptions thrown — thread safety verified
    }

    private static LogRecord CreateLogRecord(
        LogLevel logLevel = LogLevel.Information,
        string? eventName = null,
        string? message = null,
        List<KeyValuePair<string, object?>>? attributes = null,
        Exception? exception = null,
        DateTime? timestamp = null,
        ActivityTraceId traceId = default,
        ActivitySpanId spanId = default)
    {
        var lr = (LogRecord)Activator.CreateInstance(typeof(LogRecord), nonPublic: true)!;
        lr.Timestamp = timestamp ?? DateTime.UtcNow;
        lr.LogLevel = logLevel;
        lr.EventId = new EventId(1, eventName);
        lr.FormattedMessage = message;
        lr.Attributes = attributes;
        lr.Exception = exception;
        lr.TraceId = traceId;
        lr.SpanId = spanId;
        return lr;
    }
}
