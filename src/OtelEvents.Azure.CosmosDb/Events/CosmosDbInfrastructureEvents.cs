using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace OtelEvents.Azure.CosmosDb.Events;

/// <summary>
/// Pre-compiled [LoggerMessage] methods and metrics for CosmosDB infrastructure events.
/// Maps to event IDs 10205–10207 (connection failures, auth failures, throttling).
/// </summary>
/// <remarks>
/// <para>
/// Infrastructure events are supplemental — they fire <em>in addition to</em> the
/// existing <c>cosmosdb.query.failed</c> event (10202), not instead of it.
/// They are gated by <see cref="OtelEventsCosmosDbOptions.EmitInfrastructureEvents"/>
/// (default: <c>false</c>) so existing consumers see no change unless they opt in.
/// </para>
/// <para>
/// This code is pre-compiled in the NuGet package — consumers do NOT need
/// OtelEvents.Schema at build time.
/// </para>
/// </remarks>
internal static partial class CosmosDbInfrastructureEvents
{
    // ─── Meter & Instruments ────────────────────────────────────────────

    private static readonly Meter s_meter = new("OtelEvents.Azure.CosmosDb", "1.0.0");

    /// <summary>Counter: total CosmosDB connection failures.</summary>
    internal static readonly Counter<long> ConnectionFailureCount =
        s_meter.CreateCounter<long>(
            "otel.cosmosdb.connection.failure.count", "errors",
            "Total CosmosDB connection failures");

    /// <summary>Counter: total CosmosDB authentication failures.</summary>
    internal static readonly Counter<long> AuthFailureCount =
        s_meter.CreateCounter<long>(
            "otel.cosmosdb.auth.failure.count", "errors",
            "Total CosmosDB authentication failures");

    /// <summary>Counter: total CosmosDB throttled requests (HTTP 429).</summary>
    internal static readonly Counter<long> ThrottledCount =
        s_meter.CreateCounter<long>(
            "otel.cosmosdb.throttled.count", "requests",
            "Total CosmosDB throttled requests");

    // ─── Event: cosmosdb.connection.failed (ID 10205) ───────────────────

    [LoggerMessage(
        EventId = 10205,
        EventName = "cosmosdb.connection.failed",
        Level = LogLevel.Error,
        Message = "CosmosDB connection failed to {endpoint} database={cosmosDatabase} after {durationMs}ms error={errorType} reason={failureReason} message={errorMessage}")]
    private static partial void LogConnectionFailed(
        ILogger logger,
        Exception? exception,
        string endpoint,
        string cosmosDatabase,
        double durationMs,
        string errorType,
        string errorMessage,
        string failureReason);

    /// <summary>
    /// Emits the <c>cosmosdb.connection.failed</c> event (ID 10205) and records metrics.
    /// Fires when a CosmosException indicates a connection-level failure.
    /// </summary>
    internal static void CosmosDbConnectionFailed(
        this ILogger logger,
        string endpoint,
        string cosmosDatabase,
        double durationMs,
        string errorType,
        string errorMessage,
        string failureReason,
        Exception? exception)
    {
        LogConnectionFailed(
            logger, exception, endpoint, cosmosDatabase,
            durationMs, errorType, errorMessage, failureReason);

        ConnectionFailureCount.Add(1,
            new KeyValuePair<string, object?>("cosmosDatabase", cosmosDatabase),
            new KeyValuePair<string, object?>("failureReason", failureReason));
    }

    // ─── Event: cosmosdb.auth.failed (ID 10206) ─────────────────────────

    [LoggerMessage(
        EventId = 10206,
        EventName = "cosmosdb.auth.failed",
        Level = LogLevel.Error,
        Message = "CosmosDB auth failed on database={cosmosDatabase} status={httpStatusCode} scheme={authScheme} identity={identityHint}")]
    private static partial void LogAuthFailed(
        ILogger logger,
        Exception? exception,
        int httpStatusCode,
        string cosmosDatabase,
        string authScheme,
        string identityHint);

    /// <summary>
    /// Emits the <c>cosmosdb.auth.failed</c> event (ID 10206) and records metrics.
    /// Fires when a CosmosException has HTTP status 401 or 403.
    /// </summary>
    internal static void CosmosDbAuthFailed(
        this ILogger logger,
        int httpStatusCode,
        string cosmosDatabase,
        string authScheme,
        string identityHint,
        Exception? exception)
    {
        LogAuthFailed(
            logger, exception, httpStatusCode, cosmosDatabase,
            authScheme, identityHint);

        AuthFailureCount.Add(1,
            new KeyValuePair<string, object?>("cosmosDatabase", cosmosDatabase),
            new KeyValuePair<string, object?>("httpStatusCode", httpStatusCode));
    }

    // ─── Event: cosmosdb.throttled (ID 10207) ───────────────────────────

    [LoggerMessage(
        EventId = 10207,
        EventName = "cosmosdb.throttled",
        Level = LogLevel.Warning,
        Message = "CosmosDB throttled on {cosmosDatabase}/{cosmosContainer} status={httpStatusCode} retryAfter={retryAfterMs}ms charge={cosmosRequestCharge} RU")]
    private static partial void LogThrottled(
        ILogger logger,
        Exception? exception,
        int httpStatusCode,
        string cosmosDatabase,
        string cosmosContainer,
        double retryAfterMs,
        double cosmosRequestCharge);

    /// <summary>
    /// Emits the <c>cosmosdb.throttled</c> event (ID 10207) and records metrics.
    /// Fires when a CosmosException has HTTP status 429 (Request Rate Too Large).
    /// </summary>
    internal static void CosmosDbThrottled(
        this ILogger logger,
        int httpStatusCode,
        string cosmosDatabase,
        string cosmosContainer,
        double retryAfterMs,
        double cosmosRequestCharge,
        Exception? exception)
    {
        LogThrottled(
            logger, exception, httpStatusCode, cosmosDatabase,
            cosmosContainer, retryAfterMs, cosmosRequestCharge);

        ThrottledCount.Add(1,
            new KeyValuePair<string, object?>("cosmosDatabase", cosmosDatabase),
            new KeyValuePair<string, object?>("cosmosContainer", cosmosContainer));
    }
}
