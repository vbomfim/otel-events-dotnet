using Microsoft.Extensions.Logging;

namespace OtelEvents.HttpClient.Events;

/// <summary>
/// Pre-compiled [LoggerMessage] methods for outbound HTTP call lifecycle events.
/// Event IDs 10010–10012 follow the integration pack convention (10000+ range).
/// </summary>
/// <remarks>
/// This code is pre-compiled in the NuGet package — consumers do NOT need
/// OtelEvents.Schema at build time.
/// </remarks>
internal static partial class HttpOutboundEvents
{
    // ─── Event: http.outbound.started (ID 10010) ────────────────────────

    [LoggerMessage(
        EventId = 10010,
        EventName = "http.outbound.started",
        Level = LogLevel.Debug,
        Message = "{httpMethod} {httpUrl} started")]
    private static partial void LogHttpOutboundStarted(
        ILogger logger,
        string httpMethod,
        string httpUrl,
        string? httpClientName);

    /// <summary>
    /// Emits the <c>http.outbound.started</c> event (ID 10010).
    /// </summary>
    internal static void HttpOutboundStarted(
        this ILogger logger,
        string httpMethod,
        string httpUrl,
        string? httpClientName)
    {
        LogHttpOutboundStarted(logger, httpMethod, httpUrl, httpClientName);
    }

    // ─── Event: http.outbound.completed (ID 10011) ──────────────────────

    [LoggerMessage(
        EventId = 10011,
        EventName = "http.outbound.completed",
        Level = LogLevel.Debug,
        Message = "{httpMethod} {httpUrl} → {httpStatusCode} in {durationMs}ms")]
    private static partial void LogHttpOutboundCompleted(
        ILogger logger,
        string httpMethod,
        string httpUrl,
        int httpStatusCode,
        double durationMs,
        string? httpClientName);

    /// <summary>
    /// Emits the <c>http.outbound.completed</c> event (ID 10011).
    /// </summary>
    internal static void HttpOutboundCompleted(
        this ILogger logger,
        string httpMethod,
        string httpUrl,
        int httpStatusCode,
        double durationMs,
        string? httpClientName)
    {
        LogHttpOutboundCompleted(logger, httpMethod, httpUrl, httpStatusCode, durationMs, httpClientName);
    }

    // ─── Event: http.outbound.failed (ID 10012) ─────────────────────────

    [LoggerMessage(
        EventId = 10012,
        EventName = "http.outbound.failed",
        Level = LogLevel.Error,
        Message = "{httpMethod} {httpUrl} failed: {errorType}")]
    private static partial void LogHttpOutboundFailed(
        ILogger logger,
        Exception? exception,
        string httpMethod,
        string httpUrl,
        string errorType,
        double durationMs,
        string? httpClientName);

    /// <summary>
    /// Emits the <c>http.outbound.failed</c> event (ID 10012).
    /// </summary>
    internal static void HttpOutboundFailed(
        this ILogger logger,
        string httpMethod,
        string httpUrl,
        string errorType,
        double durationMs,
        string? httpClientName,
        Exception? exception = null)
    {
        LogHttpOutboundFailed(logger, exception, httpMethod, httpUrl, errorType, durationMs, httpClientName);
    }
}
