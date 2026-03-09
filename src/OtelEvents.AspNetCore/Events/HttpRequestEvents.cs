using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace OtelEvents.AspNetCore.Events;

/// <summary>
/// Pre-compiled [LoggerMessage] methods and metrics for HTTP request lifecycle events.
/// Maps to the aspnetcore.all.yaml schema (event IDs 10001–10003).
/// </summary>
/// <remarks>
/// This code is pre-compiled in the NuGet package — consumers do NOT need
/// All.Schema at build time. The YAML schema is embedded for documentation
/// and tooling inspection only.
/// </remarks>
internal static partial class HttpRequestEvents
{
    // ─── Meter & Instruments ────────────────────────────────────────────

    private static readonly Meter s_meter = new("OtelEvents.AspNetCore", "1.0.0");

    /// <summary>Counter: total HTTP requests received, labeled by httpMethod.</summary>
    internal static readonly Counter<long> RequestReceivedCount =
        s_meter.CreateCounter<long>(
            "otel.http.request.received.count", "requests", "Total HTTP requests received");

    /// <summary>Histogram: HTTP request processing duration in ms.</summary>
    internal static readonly Histogram<double> RequestDuration =
        s_meter.CreateHistogram<double>(
            "otel.http.request.duration", "ms", "HTTP request processing duration");

    /// <summary>Counter: total HTTP responses, labeled by httpMethod + httpStatusCode.</summary>
    internal static readonly Counter<long> ResponseCount =
        s_meter.CreateCounter<long>(
            "otel.http.response.count", "responses", "Total HTTP responses by status code");

    /// <summary>Histogram: HTTP response body size in bytes.</summary>
    internal static readonly Histogram<double> ResponseSize =
        s_meter.CreateHistogram<double>(
            "otel.http.response.size", "bytes", "HTTP response body size");

    /// <summary>Counter: total HTTP request errors, labeled by httpMethod + errorType.</summary>
    internal static readonly Counter<long> RequestErrorCount =
        s_meter.CreateCounter<long>(
            "otel.http.request.error.count", "errors", "Total HTTP request errors");

    // ─── Event: http.request.received (ID 10001) ────────────────────────

    [LoggerMessage(
        EventId = 10001,
        EventName = "http.request.received",
        Level = LogLevel.Information,
        Message = "HTTP {HttpMethod} {HttpPath} received from {ClientIp}")]
    private static partial void LogHttpRequestReceived(
        ILogger logger,
        string httpMethod,
        string httpPath,
        string? clientIp,
        string? userAgent,
        long? contentLength,
        string requestId);

    /// <summary>
    /// Emits the <c>http.request.received</c> event (ID 10001) and records metrics.
    /// </summary>
    internal static void HttpRequestReceived(
        this ILogger logger,
        string httpMethod,
        string httpPath,
        string? userAgent,
        string? clientIp,
        long? contentLength,
        string requestId)
    {
        LogHttpRequestReceived(logger, httpMethod, httpPath, clientIp, userAgent, contentLength, requestId);

        RequestReceivedCount.Add(1, new KeyValuePair<string, object?>("httpMethod", httpMethod));
    }

    // ─── Event: http.request.completed (ID 10002) ───────────────────────

    [LoggerMessage(
        EventId = 10002,
        EventName = "http.request.completed",
        Level = LogLevel.Information,
        Message = "HTTP {HttpMethod} {HttpPath} completed with {HttpStatusCode} in {DurationMs}ms")]
    private static partial void LogHttpRequestCompleted(
        ILogger logger,
        string httpMethod,
        string httpPath,
        string? httpRoute,
        int httpStatusCode,
        double durationMs,
        long? contentLength,
        string requestId);

    /// <summary>
    /// Emits the <c>http.request.completed</c> event (ID 10002) and records metrics.
    /// </summary>
    internal static void HttpRequestCompleted(
        this ILogger logger,
        string httpMethod,
        string httpPath,
        string? httpRoute,
        int httpStatusCode,
        double durationMs,
        long? contentLength,
        string requestId)
    {
        LogHttpRequestCompleted(logger, httpMethod, httpPath, httpRoute, httpStatusCode, durationMs, contentLength, requestId);

        var methodTag = new KeyValuePair<string, object?>("httpMethod", httpMethod);
        var statusTag = new KeyValuePair<string, object?>("httpStatusCode", httpStatusCode);

        RequestDuration.Record(durationMs, methodTag, statusTag);
        ResponseCount.Add(1, methodTag, statusTag);

        if (contentLength.HasValue)
        {
            ResponseSize.Record(contentLength.Value, methodTag);
        }
    }

    // ─── Event: http.request.failed (ID 10003) ──────────────────────────

    [LoggerMessage(
        EventId = 10003,
        EventName = "http.request.failed",
        Level = LogLevel.Error,
        Message = "HTTP {HttpMethod} {HttpPath} failed with {ErrorType} after {DurationMs}ms")]
    private static partial void LogHttpRequestFailed(
        ILogger logger,
        Exception exception,
        string httpMethod,
        string httpPath,
        string? httpRoute,
        int? httpStatusCode,
        double durationMs,
        string errorType,
        string requestId);

    /// <summary>
    /// Emits the <c>http.request.failed</c> event (ID 10003) and records metrics.
    /// </summary>
    internal static void HttpRequestFailed(
        this ILogger logger,
        string httpMethod,
        string httpPath,
        string? httpRoute,
        int? httpStatusCode,
        double durationMs,
        string errorType,
        string requestId,
        Exception exception)
    {
        LogHttpRequestFailed(logger, exception, httpMethod, httpPath, httpRoute, httpStatusCode, durationMs, errorType, requestId);

        RequestErrorCount.Add(1,
            new KeyValuePair<string, object?>("httpMethod", httpMethod),
            new KeyValuePair<string, object?>("errorType", errorType));
    }
}
