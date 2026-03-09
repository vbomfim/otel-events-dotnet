using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

namespace All.Testing;

/// <summary>
/// Immutable snapshot of a <see cref="LogRecord"/> for test assertions.
/// <para>
/// OpenTelemetry recycles <see cref="LogRecord"/> instances (they are pooled/mutable),
/// so this class captures all relevant fields at export time, allowing safe assertions
/// after the pipeline has processed the record.
/// </para>
/// </summary>
public sealed class ExportedLogRecord
{
    /// <summary>Gets the event name from <see cref="LogRecord.EventId"/>.</summary>
    public string? EventName { get; }

    /// <summary>Gets the <see cref="EventId"/> (numeric ID and name).</summary>
    public EventId EventId { get; }

    /// <summary>Gets the log severity level.</summary>
    public LogLevel LogLevel { get; }

    /// <summary>Gets the formatted message, if any.</summary>
    public string? FormattedMessage { get; }

    /// <summary>Gets the exception associated with this log record, if any.</summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Gets the captured attributes as an immutable dictionary.
    /// Empty dictionary if no attributes were present on the original record.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Attributes { get; }

    /// <summary>Gets the W3C trace ID, if present.</summary>
    public ActivityTraceId TraceId { get; }

    /// <summary>Gets the W3C span ID, if present.</summary>
    public ActivitySpanId SpanId { get; }

    /// <summary>Gets the UTC timestamp of the log record.</summary>
    public DateTime Timestamp { get; }

    private ExportedLogRecord(
        string? eventName,
        EventId eventId,
        LogLevel logLevel,
        string? formattedMessage,
        Exception? exception,
        IReadOnlyDictionary<string, object?> attributes,
        ActivityTraceId traceId,
        ActivitySpanId spanId,
        DateTime timestamp)
    {
        EventName = eventName;
        EventId = eventId;
        LogLevel = logLevel;
        FormattedMessage = formattedMessage;
        Exception = exception;
        Attributes = attributes;
        TraceId = traceId;
        SpanId = spanId;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Creates an immutable snapshot from a live <see cref="LogRecord"/>.
    /// Copies all fields so the snapshot is safe to use after the record is recycled.
    /// </summary>
    /// <param name="record">The live LogRecord to snapshot.</param>
    /// <returns>An immutable <see cref="ExportedLogRecord"/>.</returns>
    public static ExportedLogRecord From(LogRecord record)
    {
        var attributes = new Dictionary<string, object?>();
        if (record.Attributes is not null)
        {
            foreach (var kvp in record.Attributes)
            {
                attributes[kvp.Key] = kvp.Value;
            }
        }

        return new ExportedLogRecord(
            eventName: record.EventId.Name,
            eventId: record.EventId,
            logLevel: record.LogLevel,
            formattedMessage: record.FormattedMessage,
            exception: record.Exception,
            attributes: attributes,
            traceId: record.TraceId,
            spanId: record.SpanId,
            timestamp: record.Timestamp);
    }
}
