using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace All.Testing.Tests;

/// <summary>
/// Tests for <see cref="ExportedLogRecord"/> — immutable snapshot of a LogRecord.
/// Verifies all LogRecord fields are captured correctly since OTEL recycles LogRecord instances.
/// </summary>
public sealed class ExportedLogRecordTests
{
    [Fact]
    public void From_CapturesEventName()
    {
        var lr = CreateLogRecord(eventName: "order.placed");

        var snapshot = ExportedLogRecord.From(lr);

        Assert.Equal("order.placed", snapshot.EventName);
    }

    [Fact]
    public void From_CapturesLogLevel()
    {
        var lr = CreateLogRecord(logLevel: LogLevel.Warning);

        var snapshot = ExportedLogRecord.From(lr);

        Assert.Equal(LogLevel.Warning, snapshot.LogLevel);
    }

    [Fact]
    public void From_CapturesFormattedMessage()
    {
        var lr = CreateLogRecord(message: "Order 123 placed by user 456");

        var snapshot = ExportedLogRecord.From(lr);

        Assert.Equal("Order 123 placed by user 456", snapshot.FormattedMessage);
    }

    [Fact]
    public void From_CapturesException()
    {
        var exception = new InvalidOperationException("Test exception");
        var lr = CreateLogRecord(exception: exception);

        var snapshot = ExportedLogRecord.From(lr);

        Assert.Same(exception, snapshot.Exception);
    }

    [Fact]
    public void From_CapturesAttributes()
    {
        var attributes = new List<KeyValuePair<string, object?>>
        {
            new("OrderId", "ORD-123"),
            new("Amount", 42.50),
        };
        var lr = CreateLogRecord(attributes: attributes);

        var snapshot = ExportedLogRecord.From(lr);

        Assert.Equal(2, snapshot.Attributes.Count);
        Assert.Equal("ORD-123", snapshot.Attributes["OrderId"]);
        Assert.Equal(42.50, snapshot.Attributes["Amount"]);
    }

    [Fact]
    public void From_CapturesTraceId()
    {
        var traceId = ActivityTraceId.CreateFromString("4bf92f3577b34da6a3ce929d0e0e4736");
        var lr = CreateLogRecord(traceId: traceId);

        var snapshot = ExportedLogRecord.From(lr);

        Assert.Equal(traceId, snapshot.TraceId);
    }

    [Fact]
    public void From_CapturesSpanId()
    {
        var spanId = ActivitySpanId.CreateFromString("00f067aa0ba902b7");
        var lr = CreateLogRecord(spanId: spanId);

        var snapshot = ExportedLogRecord.From(lr);

        Assert.Equal(spanId, snapshot.SpanId);
    }

    [Fact]
    public void From_CapturesTimestamp()
    {
        var timestamp = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var lr = CreateLogRecord(timestamp: timestamp);

        var snapshot = ExportedLogRecord.From(lr);

        Assert.Equal(timestamp, snapshot.Timestamp);
    }

    [Fact]
    public void From_NullEventName_PreservesNull()
    {
        var lr = CreateLogRecord(eventName: null);

        var snapshot = ExportedLogRecord.From(lr);

        Assert.Null(snapshot.EventName);
    }

    [Fact]
    public void From_NullAttributes_ReturnsEmptyDictionary()
    {
        var lr = CreateLogRecord(attributes: null);

        var snapshot = ExportedLogRecord.From(lr);

        Assert.NotNull(snapshot.Attributes);
        Assert.Empty(snapshot.Attributes);
    }

    [Fact]
    public void From_NullMessage_PreservesNull()
    {
        var lr = CreateLogRecord(message: null);

        var snapshot = ExportedLogRecord.From(lr);

        Assert.Null(snapshot.FormattedMessage);
    }

    [Fact]
    public void From_NullException_PreservesNull()
    {
        var lr = CreateLogRecord(exception: null);

        var snapshot = ExportedLogRecord.From(lr);

        Assert.Null(snapshot.Exception);
    }

    [Fact]
    public void From_DefaultTraceId_PreservesDefault()
    {
        var lr = CreateLogRecord();

        var snapshot = ExportedLogRecord.From(lr);

        Assert.Equal(default, snapshot.TraceId);
    }

    [Fact]
    public void From_AttributesAreIsolatedFromOriginal()
    {
        var attributes = new List<KeyValuePair<string, object?>>
        {
            new("Key1", "Value1"),
        };
        var lr = CreateLogRecord(attributes: attributes);

        var snapshot = ExportedLogRecord.From(lr);

        // Mutating the original list should not affect the snapshot
        attributes.Add(new("Key2", "Value2"));

        Assert.Single(snapshot.Attributes);
        Assert.True(snapshot.Attributes.ContainsKey("Key1"));
        Assert.False(snapshot.Attributes.ContainsKey("Key2"));
    }

    [Fact]
    public void From_CapturesEventId()
    {
        var lr = CreateLogRecord(eventName: "test.event");

        var snapshot = ExportedLogRecord.From(lr);

        Assert.Equal(1, snapshot.EventId.Id);
        Assert.Equal("test.event", snapshot.EventId.Name);
    }

    /// <summary>
    /// Creates a LogRecord using reflection (internal constructor).
    /// Mirrors the pattern from TestExporterHarness in All.Exporter.Json.Tests.
    /// </summary>
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
