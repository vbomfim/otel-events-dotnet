using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace OtelEvents.Subscriptions;

/// <summary>
/// Immutable snapshot of a <see cref="OpenTelemetry.Logs.LogRecord"/> delivered to subscription handlers.
/// <para>
/// OpenTelemetry recycles <see cref="OpenTelemetry.Logs.LogRecord"/> instances (they are pooled/mutable),
/// so this class captures all relevant fields at processing time, allowing safe async handler execution
/// after the OTEL pipeline has moved on.
/// </para>
/// </summary>
public sealed class OtelEventContext
{
    /// <summary>Gets the event name from <c>LogRecord.EventId.Name</c>.</summary>
    public string EventName { get; }

    /// <summary>Gets the log severity level.</summary>
    public LogLevel LogLevel { get; }

    /// <summary>Gets the formatted message, if any.</summary>
    public string? FormattedMessage { get; }

    /// <summary>
    /// Gets the captured attributes as an immutable dictionary.
    /// Empty dictionary if no attributes were present on the original record.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Attributes { get; }

    /// <summary>Gets the UTC timestamp of the log record.</summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>Gets the W3C trace ID as a hex string, or null if not present.</summary>
    public string? TraceId { get; }

    /// <summary>Gets the W3C span ID as a hex string, or null if not present.</summary>
    public string? SpanId { get; }

    /// <summary>Gets the exception associated with this log record, if any.</summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="OtelEventContext"/>.
    /// </summary>
    public OtelEventContext(
        string eventName,
        LogLevel logLevel,
        string? formattedMessage,
        IReadOnlyDictionary<string, object?> attributes,
        DateTimeOffset timestamp,
        string? traceId,
        string? spanId,
        Exception? exception)
    {
        EventName = eventName;
        LogLevel = logLevel;
        FormattedMessage = formattedMessage;
        Attributes = attributes;
        Timestamp = timestamp;
        TraceId = traceId;
        SpanId = spanId;
        Exception = exception;
    }

    /// <summary>
    /// Gets a typed attribute value by key.
    /// Returns <c>default</c> if the key is not found or cannot be cast to <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The expected attribute value type.</typeparam>
    /// <param name="key">The attribute key.</param>
    /// <returns>The attribute value cast to <typeparamref name="T"/>, or <c>default</c>.</returns>
    public T? GetAttribute<T>(string key)
    {
        if (Attributes.TryGetValue(key, out var value) && value is T typed)
        {
            return typed;
        }

        return default;
    }

    /// <summary>
    /// Creates an immutable snapshot from a live <see cref="OpenTelemetry.Logs.LogRecord"/>.
    /// Copies all fields so the snapshot is safe to use after the record is recycled.
    /// </summary>
    internal static OtelEventContext FromLogRecord(OpenTelemetry.Logs.LogRecord record)
    {
        var attributes = new Dictionary<string, object?>();
        if (record.Attributes is not null)
        {
            foreach (var kvp in record.Attributes)
            {
                attributes[kvp.Key] = kvp.Value;
            }
        }

        var traceId = record.TraceId != default(ActivityTraceId)
            ? record.TraceId.ToString()
            : null;

        var spanId = record.SpanId != default(ActivitySpanId)
            ? record.SpanId.ToString()
            : null;

        return new OtelEventContext(
            eventName: record.EventId.Name ?? string.Empty,
            logLevel: record.LogLevel,
            formattedMessage: record.FormattedMessage,
            attributes: attributes,
            timestamp: record.Timestamp != default
                ? new DateTimeOffset(record.Timestamp, TimeSpan.Zero)
                : DateTimeOffset.UtcNow,
            traceId: traceId,
            spanId: spanId,
            exception: record.Exception);
    }
}
