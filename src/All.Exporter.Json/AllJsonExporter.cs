using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace All.Exporter.Json;

/// <summary>
/// Custom OTEL Log Exporter that writes LogRecords as AI-optimized single-line JSONL.
/// Plugs into the standard OTEL log pipeline alongside OTLP, Console, or any other exporter.
/// </summary>
public sealed class AllJsonExporter : BaseExporter<LogRecord>
{
    private const int StreamWriterBufferSize = 32 * 1024; // 32 KB
    private const int MaxExceptionDepth = 5;
    private const string TruncationSuffix = "…[truncated]";
    private const string RedactedValue = "[REDACTED]";
    private const string RedactedPatternValue = "[REDACTED:pattern]";
    private const string RedactedTimeoutValue = "[REDACTED:timeout]";
    private const string FallbackEventName = "dotnet.ilogger";

    /// <summary>Known ALL-component attribute keys that are NOT stripped.</summary>
    private static readonly HashSet<string> AllowedAllPrefixKeys = new(StringComparer.Ordinal)
    {
        "all.event_id",
        "all.parent_event_id",
        "all.tags",
    };

    /// <summary>Defense-in-depth patterns (always active, non-configurable).</summary>
    private static readonly Regex[] DefaultDefensePatterns =
    [
        new(@"Server=.*;(User Id|Password|Pwd)=.*", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50)),
        new(@"Data Source=.*;(User ID|Password)=.*", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50)),
        new(@"Bearer\s+[A-Za-z0-9\-._~+/]+=*", RegexOptions.Compiled, TimeSpan.FromMilliseconds(50)),
        new(@"(api[_\-]?key|apikey|access[_\-]?token|secret[_\-]?key)\s*[=:]\s*\S{16,}", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50)),
    ];

    private readonly AllJsonExporterOptions _options;
    private readonly StreamWriter _writer;
    private readonly object _lock = new();
    private readonly Regex[] _userRedactPatterns;

    private long _seq;

    /// <summary>
    /// Initializes a new instance of <see cref="AllJsonExporter"/> writing to the given stream.
    /// </summary>
    internal AllJsonExporter(AllJsonExporterOptions options, Stream output)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _writer = new StreamWriter(output ?? throw new ArgumentNullException(nameof(output)),
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StreamWriterBufferSize, leaveOpen: true);
        _userRedactPatterns = CompileRedactPatterns(options.RedactPatterns);
    }

    /// <summary>
    /// Initializes a new instance of <see cref="AllJsonExporter"/> using the configured output target.
    /// </summary>
    public AllJsonExporter(AllJsonExporterOptions options)
        : this(options, ResolveOutputStream(options))
    {
    }

    /// <inheritdoc />
    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        bool lockTaken = false;
        try
        {
            Monitor.TryEnter(_lock, _options.LockTimeout, ref lockTaken);
            if (!lockTaken)
            {
                ExporterMetrics.BatchesDropped.Add(1);
                return ExportResult.Success; // Backpressure — don't report as failure
            }

            string? serviceName = null;
            string? environment = null;
            TryReadResource(ref serviceName, ref environment);

            foreach (var logRecord in batch)
            {
                var seq = Interlocked.Increment(ref _seq);
                WriteJsonLine(logRecord, seq, serviceName, environment);
            }

            _writer.Flush();
            return ExportResult.Success;
        }
        catch (IOException)
        {
            ExporterMetrics.ExportErrors.Add(1);
            return ExportResult.Failure;
        }
        finally
        {
            if (lockTaken) Monitor.Exit(_lock);
        }
    }

    /// <inheritdoc />
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        try
        {
            _writer.Flush();
        }
        catch (IOException)
        {
            // Best-effort flush on shutdown
        }

        return base.OnShutdown(timeoutMilliseconds);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _writer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void WriteJsonLine(LogRecord logRecord, long seq, string? serviceName, string? environment)
    {
        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { SkipValidation = false }))
        {
            writer.WriteStartObject();

            // timestamp
            WriteTimestamp(writer, logRecord);

            // event
            WriteEventName(writer, logRecord);

            // severity + severityNumber
            WriteSeverity(writer, logRecord);

            // message
            WriteMessage(writer, logRecord);

            // service, environment
            WriteServiceInfo(writer, serviceName, environment);

            // traceId, spanId
            WriteTraceContext(writer, logRecord);

            // Process attributes: extract eventId, parentEventId, tags, and build attr
            WriteAttributes(writer, logRecord);

            // exception
            WriteException(writer, logRecord);

            // metadata: all.v, all.seq, all.host, all.pid
            WriteMetadata(writer, seq);

            writer.WriteEndObject();
        }

        _writer.Write(System.Text.Encoding.UTF8.GetString(
            stream.GetBuffer(), 0, (int)stream.Length));
        _writer.Write('\n');
    }

    private static void WriteTimestamp(Utf8JsonWriter writer, LogRecord logRecord)
    {
        var timestamp = logRecord.Timestamp != default
            ? logRecord.Timestamp.ToUniversalTime()
            : DateTime.UtcNow;

        writer.WriteString("timestamp", timestamp.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ"));
    }

    private static void WriteEventName(Utf8JsonWriter writer, LogRecord logRecord)
    {
        var eventName = logRecord.EventId.Name;
        writer.WriteString("event", string.IsNullOrEmpty(eventName) ? FallbackEventName : eventName);
    }

    private static void WriteSeverity(Utf8JsonWriter writer, LogRecord logRecord)
    {
        var (severityString, severityNumber) = MapSeverity(logRecord.LogLevel);
        writer.WriteString("severity", severityString);
        writer.WriteNumber("severityNumber", severityNumber);
    }

    private static void WriteMessage(Utf8JsonWriter writer, LogRecord logRecord)
    {
        var message = logRecord.FormattedMessage ?? logRecord.Body;
        if (message is not null)
        {
            writer.WriteString("message", message);
        }
    }

    private static void WriteServiceInfo(Utf8JsonWriter writer, string? serviceName, string? environment)
    {
        if (serviceName is not null)
        {
            writer.WriteString("service", serviceName);
        }

        if (environment is not null)
        {
            writer.WriteString("environment", environment);
        }
    }

    private static void WriteTraceContext(Utf8JsonWriter writer, LogRecord logRecord)
    {
        if (logRecord.TraceId != default)
        {
            writer.WriteString("traceId", logRecord.TraceId.ToString());
        }

        if (logRecord.SpanId != default)
        {
            writer.WriteString("spanId", logRecord.SpanId.ToString());
        }
    }

    private void WriteAttributes(Utf8JsonWriter writer, LogRecord logRecord)
    {
        string? eventId = null;
        string? parentEventId = null;
        object? tagsValue = null;
        var attrs = new List<KeyValuePair<string, object?>>();
        var strippedCount = 0;

        logRecord.ForEachScope(ProcessScope, attrs);

        // Process LogRecord attributes
        if (logRecord.Attributes is not null)
        {
            foreach (var attr in logRecord.Attributes)
            {
                if (attr.Key == "all.event_id")
                {
                    eventId = attr.Value?.ToString();
                    continue;
                }

                if (attr.Key == "all.parent_event_id")
                {
                    parentEventId = attr.Value?.ToString();
                    continue;
                }

                if (attr.Key == "all.tags")
                {
                    tagsValue = attr.Value;
                    continue;
                }

                // Strip reserved all.* prefix from non-ALL attributes
                if (attr.Key.StartsWith("all.", StringComparison.Ordinal)
                    && !AllowedAllPrefixKeys.Contains(attr.Key))
                {
                    strippedCount++;
                    continue;
                }

                // Apply denylist
                if (_options.AttributeDenylist.Contains(attr.Key))
                {
                    continue;
                }

                // Apply allowlist (null = allow all)
                if (_options.AttributeAllowlist is not null
                    && !_options.AttributeAllowlist.Contains(attr.Key))
                {
                    continue;
                }

                // Don't include {OriginalFormat} in attr
                if (attr.Key == "{OriginalFormat}")
                {
                    continue;
                }

                attrs.Add(attr);
            }
        }

        if (strippedCount > 0)
        {
            ExporterMetrics.ReservedPrefixStripped.Add(strippedCount);
        }

        // Write eventId
        if (eventId is not null)
        {
            writer.WriteString("eventId", eventId);
        }

        // Write parentEventId
        if (parentEventId is not null)
        {
            writer.WriteString("parentEventId", parentEventId);
        }

        // Write attr object
        if (attrs.Count > 0)
        {
            writer.WriteStartObject("attr");
            foreach (var attr in attrs)
            {
                WriteAttributeValue(writer, attr.Key, attr.Value);
            }

            writer.WriteEndObject();
        }

        // Write tags
        WriteTags(writer, tagsValue);
    }

    private void WriteAttributeValue(Utf8JsonWriter writer, string key, object? value)
    {
        if (value is null)
        {
            return; // Omit null values per envelope rules
        }

        // Write typed values directly — redaction/truncation only applies to strings
        switch (value)
        {
            case int i:
                writer.WriteNumber(key, i);
                return;
            case long l:
                writer.WriteNumber(key, l);
                return;
            case float f:
                writer.WriteNumber(key, f);
                return;
            case double d:
                writer.WriteNumber(key, d);
                return;
            case decimal dec:
                writer.WriteNumber(key, dec);
                return;
            case bool b:
                writer.WriteBoolean(key, b);
                return;
        }

        // String path: apply redaction and truncation
        var stringValue = value.ToString() ?? string.Empty;
        stringValue = ApplyRedaction(stringValue);
        stringValue = ApplyTruncation(stringValue);
        writer.WriteString(key, stringValue);
    }

    private string ApplyRedaction(string value)
    {
        // Defense-in-depth patterns (always active)
        foreach (var pattern in DefaultDefensePatterns)
        {
            try
            {
                if (pattern.IsMatch(value))
                {
                    ExporterMetrics.AttributesRedacted.Add(1);
                    return RedactedPatternValue;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Fail-closed: redact on timeout to prevent credential leaks
                ExporterMetrics.RegexTimeouts.Add(1);
                return RedactedTimeoutValue;
            }
        }

        // User-configured redact patterns
        foreach (var pattern in _userRedactPatterns)
        {
            try
            {
                if (pattern.IsMatch(value))
                {
                    ExporterMetrics.AttributesRedacted.Add(1);
                    return RedactedValue;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Fail-closed: redact on timeout to prevent credential leaks
                ExporterMetrics.RegexTimeouts.Add(1);
                return RedactedTimeoutValue;
            }
        }

        return value;
    }

    private string ApplyTruncation(string value)
    {
        if (value.Length > _options.MaxAttributeValueLength)
        {
            ExporterMetrics.ValuesTruncated.Add(1);
            return string.Concat(value.AsSpan(0, _options.MaxAttributeValueLength), TruncationSuffix);
        }

        return value;
    }

    private static void WriteTags(Utf8JsonWriter writer, object? tagsValue)
    {
        if (tagsValue is null)
        {
            return;
        }

        writer.WriteStartArray("tags");

        if (tagsValue is IEnumerable<string> tags)
        {
            // Handles both string[] and IEnumerable<string> (string[] implements IEnumerable<string>)
            foreach (var tag in tags)
            {
                writer.WriteStringValue(tag);
            }
        }
        else
        {
            // Single tag value
            writer.WriteStringValue(tagsValue.ToString());
        }

        writer.WriteEndArray();
    }

    private void WriteException(Utf8JsonWriter writer, LogRecord logRecord)
    {
        if (logRecord.Exception is null)
        {
            return;
        }

        writer.WriteStartObject("exception");
        WriteExceptionObject(writer, logRecord.Exception, _options.ResolvedExceptionDetailLevel,
            _options.EnvironmentProfile, depth: 0);
        writer.WriteEndObject();
    }

    internal static void WriteExceptionObject(
        Utf8JsonWriter writer,
        Exception exception,
        ExceptionDetailLevel detailLevel,
        AllEnvironmentProfile profile,
        int depth)
    {
        writer.WriteString("type", exception.GetType().FullName);

        if (detailLevel is ExceptionDetailLevel.Full or ExceptionDetailLevel.TypeAndMessage)
        {
            writer.WriteString("message", exception.Message);
        }

        if (detailLevel == ExceptionDetailLevel.Full && exception.StackTrace is not null)
        {
            WriteStackTrace(writer, exception, profile);
        }

        if (exception.InnerException is not null)
        {
            if (depth >= MaxExceptionDepth - 1)
            {
                writer.WriteStartObject("inner");
                writer.WriteBoolean("truncated", true);
                writer.WriteEndObject();
            }
            else
            {
                writer.WriteStartObject("inner");
                WriteExceptionObject(writer, exception.InnerException, detailLevel, profile, depth + 1);
                writer.WriteEndObject();
            }
        }
    }

    private static void WriteStackTrace(Utf8JsonWriter writer, Exception exception, AllEnvironmentProfile profile)
    {
        var stackTrace = new StackTrace(exception, fNeedFileInfo: profile == AllEnvironmentProfile.Development);
        var frames = stackTrace.GetFrames();

        if (frames is null || frames.Length == 0)
        {
            return;
        }

        writer.WriteStartArray("stackTrace");

        foreach (var frame in frames)
        {
            var method = frame.GetMethod();
            if (method is null)
            {
                continue;
            }

            writer.WriteStartObject();

            var declaringType = method.DeclaringType?.Name ?? string.Empty;
            var methodName = method.Name;
            writer.WriteString("method", $"{declaringType}.{methodName}()");

            if (profile == AllEnvironmentProfile.Development)
            {
                var fileName = frame.GetFileName();
                if (fileName is not null)
                {
                    writer.WriteString("file", System.IO.Path.GetFileName(fileName));
                    var lineNumber = frame.GetFileLineNumber();
                    if (lineNumber > 0)
                    {
                        writer.WriteNumber("line", lineNumber);
                    }
                }
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private void WriteMetadata(Utf8JsonWriter writer, long seq)
    {
        writer.WriteString("all.v", _options.SchemaVersion);
        writer.WriteNumber("all.seq", seq);

        if (_options.EmitHostInfo)
        {
            writer.WriteString("all.host", Environment.MachineName);
            writer.WriteNumber("all.pid", Environment.ProcessId);
        }
    }

    private void TryReadResource(ref string? serviceName, ref string? environment)
    {
        var resource = ParentProvider?.GetResource();
        if (resource == Resource.Empty || resource is null)
        {
            return;
        }

        foreach (var attr in resource.Attributes)
        {
            if (attr.Key == "service.name")
            {
                serviceName = attr.Value?.ToString();
            }
            else if (attr.Key == "deployment.environment")
            {
                environment = attr.Value?.ToString();
            }
        }
    }

    private static (string Severity, int Number) MapSeverity(Microsoft.Extensions.Logging.LogLevel logLevel) =>
        logLevel switch
        {
            Microsoft.Extensions.Logging.LogLevel.Trace => ("TRACE", 1),
            Microsoft.Extensions.Logging.LogLevel.Debug => ("DEBUG", 5),
            Microsoft.Extensions.Logging.LogLevel.Information => ("INFO", 9),
            Microsoft.Extensions.Logging.LogLevel.Warning => ("WARN", 13),
            Microsoft.Extensions.Logging.LogLevel.Error => ("ERROR", 17),
            Microsoft.Extensions.Logging.LogLevel.Critical => ("FATAL", 21),
            _ => ("INFO", 9),
        };

    private static Stream ResolveOutputStream(AllJsonExporterOptions options) =>
        options.Output switch
        {
            AllJsonOutput.Stdout => Console.OpenStandardOutput(),
            AllJsonOutput.Stderr => Console.OpenStandardError(),
            AllJsonOutput.File => new FileStream(
                options.FilePath ?? throw new InvalidOperationException("FilePath must be set when Output is File."),
                FileMode.Append, FileAccess.Write, FileShare.Read),
            _ => Console.OpenStandardOutput(),
        };

    private static Regex[] CompileRedactPatterns(IList<string> patterns)
    {
        var compiled = new Regex[patterns.Count];
        for (int i = 0; i < patterns.Count; i++)
        {
            compiled[i] = new Regex(patterns[i], RegexOptions.Compiled, TimeSpan.FromMilliseconds(50));
        }

        return compiled;
    }

    private static void ProcessScope(LogRecordScope scope, List<KeyValuePair<string, object?>> attrs)
    {
        // Scoped values are not currently extracted into the envelope attr
        // Future: extract scope values if needed
    }
}
