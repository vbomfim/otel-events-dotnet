using System.Text;
using System.Text.Json;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace OtelEvents.Exporter.Json.Tests;

/// <summary>
/// Helper to create an <see cref="OtelEventsJsonExporter"/> that writes to an in-memory stream
/// and captures the JSONL output for assertions.
/// </summary>
internal sealed class TestExporterHarness : IDisposable
{
    private readonly MemoryStream _stream;
    private readonly OtelEventsJsonExporter _exporter;

    public TestExporterHarness(OtelEventsJsonExporterOptions? options = null)
    {
        _stream = new MemoryStream();
        options ??= new OtelEventsJsonExporterOptions();
        _exporter = new OtelEventsJsonExporter(options, _stream);
    }

    public OtelEventsJsonExporter Exporter => _exporter;

    /// <summary>
    /// Creates a LogRecord using reflection (internal constructor).
    /// </summary>
    public static LogRecord CreateLogRecord(
        Microsoft.Extensions.Logging.LogLevel logLevel = Microsoft.Extensions.Logging.LogLevel.Information,
        string? eventName = null,
        string? message = null,
        List<KeyValuePair<string, object?>>? attributes = null,
        Exception? exception = null,
        DateTime? timestamp = null,
        System.Diagnostics.ActivityTraceId traceId = default,
        System.Diagnostics.ActivitySpanId spanId = default)
    {
        var lr = (LogRecord)Activator.CreateInstance(typeof(LogRecord), nonPublic: true)!;
        lr.Timestamp = timestamp ?? DateTime.UtcNow;
        lr.LogLevel = logLevel;
        lr.EventId = new Microsoft.Extensions.Logging.EventId(1, eventName);
        lr.FormattedMessage = message;
        lr.Attributes = attributes;
        lr.Exception = exception;
        lr.TraceId = traceId;
        lr.SpanId = spanId;
        return lr;
    }

    /// <summary>
    /// Exports a single LogRecord and returns the parsed JSON document.
    /// </summary>
    public JsonDocument ExportSingle(LogRecord logRecord)
    {
        var batch = new Batch<LogRecord>([logRecord], 1);
        var result = _exporter.Export(batch);
        if (result != ExportResult.Success)
        {
            throw new InvalidOperationException($"Export failed with result: {result}");
        }

        return GetLastJsonDocument();
    }

    /// <summary>
    /// Exports a batch of LogRecords and returns all parsed JSON documents.
    /// </summary>
    public List<JsonDocument> ExportBatch(LogRecord[] logRecords)
    {
        var batch = new Batch<LogRecord>(logRecords, logRecords.Length);
        var result = _exporter.Export(batch);
        if (result != ExportResult.Success)
        {
            throw new InvalidOperationException($"Export failed with result: {result}");
        }

        return GetAllJsonDocuments();
    }

    /// <summary>
    /// Exports a batch and returns the raw ExportResult (for testing failure scenarios).
    /// </summary>
    public ExportResult ExportRaw(LogRecord[] logRecords)
    {
        var batch = new Batch<LogRecord>(logRecords, logRecords.Length);
        return _exporter.Export(batch);
    }

    /// <summary>Gets the raw UTF-8 output as a string.</summary>
    public string GetRawOutput()
    {
        _stream.Position = 0;
        return Encoding.UTF8.GetString(_stream.ToArray());
    }

    private JsonDocument GetLastJsonDocument()
    {
        var output = GetRawOutput();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return JsonDocument.Parse(lines[^1]);
    }

    private List<JsonDocument> GetAllJsonDocuments()
    {
        var output = GetRawOutput();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return lines.Select(line => JsonDocument.Parse(line)).ToList();
    }

    public void Dispose()
    {
        _exporter.Dispose();
        _stream.Dispose();
    }
}
