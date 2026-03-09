using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace OtelEvents.Azure.CosmosDb.Events;

/// <summary>
/// Pre-compiled [LoggerMessage] methods and metrics for CosmosDB operation events.
/// Maps to the cosmosdb.all.yaml schema (event IDs 10201–10204).
/// </summary>
/// <remarks>
/// This code is pre-compiled in the NuGet package — consumers do NOT need
/// OtelEvents.Schema at build time. The YAML schema is embedded for documentation
/// and tooling inspection only.
/// </remarks>
internal static partial class CosmosDbEvents
{
    // ─── Meter & Instruments ────────────────────────────────────────────

    private static readonly Meter s_meter = new("OtelEvents.Azure.CosmosDb", "1.0.0");

    /// <summary>Histogram: CosmosDB query duration in ms.</summary>
    internal static readonly Histogram<double> QueryDuration =
        s_meter.CreateHistogram<double>(
            "otel.cosmosdb.query.duration", "ms", "CosmosDB query duration");

    /// <summary>Histogram: CosmosDB query RU consumption.</summary>
    internal static readonly Histogram<double> QueryRu =
        s_meter.CreateHistogram<double>(
            "otel.cosmosdb.query.ru", "RU", "CosmosDB query RU consumption");

    /// <summary>Histogram: CosmosDB query result item count.</summary>
    internal static readonly Histogram<double> QueryItemCount =
        s_meter.CreateHistogram<double>(
            "otel.cosmosdb.query.item.count", "items", "CosmosDB query result item count");

    /// <summary>Histogram: CosmosDB point operation duration in ms.</summary>
    internal static readonly Histogram<double> PointDuration =
        s_meter.CreateHistogram<double>(
            "otel.cosmosdb.point.duration", "ms", "CosmosDB point operation duration");

    /// <summary>Histogram: CosmosDB point operation RU consumption.</summary>
    internal static readonly Histogram<double> PointRu =
        s_meter.CreateHistogram<double>(
            "otel.cosmosdb.point.ru", "RU", "CosmosDB point operation RU consumption");

    /// <summary>Counter: total CosmosDB operation errors.</summary>
    internal static readonly Counter<long> ErrorCount =
        s_meter.CreateCounter<long>(
            "otel.cosmosdb.error.count", "errors", "Total CosmosDB operation errors");

    /// <summary>Counter: total CosmosDB operations by type.</summary>
    internal static readonly Counter<long> OperationCount =
        s_meter.CreateCounter<long>(
            "otel.cosmosdb.operation.count", "operations", "Total CosmosDB operations");

    // ─── Event: cosmosdb.query.executed (ID 10201) ──────────────────────

    [LoggerMessage(
        EventId = 10201,
        EventName = "cosmosdb.query.executed",
        Level = LogLevel.Debug,
        Message = "CosmosDB query on {cosmosDatabase}/{cosmosContainer} returned {cosmosItemCount} items in {durationMs}ms ({cosmosRequestCharge} RU) status={cosmosStatusCode} region={cosmosRegion} pk={cosmosPartitionKey} query={cosmosQueryText}")]
    private static partial void LogCosmosDbQueryExecuted(
        ILogger logger,
        string cosmosDatabase,
        string cosmosContainer,
        double cosmosRequestCharge,
        int cosmosItemCount,
        double durationMs,
        int cosmosStatusCode,
        string? cosmosRegion,
        string? cosmosPartitionKey,
        string? cosmosQueryText);

    /// <summary>
    /// Emits the <c>cosmosdb.query.executed</c> event (ID 10201) and records metrics.
    /// </summary>
    internal static void CosmosDbQueryExecuted(
        this ILogger logger,
        string cosmosDatabase,
        string cosmosContainer,
        double cosmosRequestCharge,
        int cosmosItemCount,
        double durationMs,
        int cosmosStatusCode,
        string? cosmosRegion,
        string? cosmosPartitionKey,
        string? cosmosQueryText)
    {
        LogCosmosDbQueryExecuted(
            logger, cosmosDatabase, cosmosContainer, cosmosRequestCharge,
            cosmosItemCount, durationMs, cosmosStatusCode, cosmosRegion,
            cosmosPartitionKey, cosmosQueryText);

        var dbTag = new KeyValuePair<string, object?>("cosmosDatabase", cosmosDatabase);
        var containerTag = new KeyValuePair<string, object?>("cosmosContainer", cosmosContainer);

        QueryDuration.Record(durationMs, dbTag, containerTag);
        QueryRu.Record(cosmosRequestCharge, dbTag, containerTag);
        QueryItemCount.Record(cosmosItemCount, dbTag, containerTag);
        OperationCount.Add(1, dbTag, containerTag,
            new KeyValuePair<string, object?>("cosmosOperationType", "Query"));
    }

    // ─── Event: cosmosdb.query.failed (ID 10202) ────────────────────────

    [LoggerMessage(
        EventId = 10202,
        EventName = "cosmosdb.query.failed",
        Level = LogLevel.Error,
        Message = "CosmosDB query on {cosmosDatabase}/{cosmosContainer} failed with {cosmosStatusCode} after {durationMs}ms ({cosmosRequestCharge} RU) substatus={cosmosSubStatusCode} error={errorType} pk={cosmosPartitionKey}")]
    private static partial void LogCosmosDbQueryFailed(
        ILogger logger,
        Exception? exception,
        string cosmosDatabase,
        string cosmosContainer,
        double cosmosRequestCharge,
        double durationMs,
        int cosmosStatusCode,
        int? cosmosSubStatusCode,
        string errorType,
        string? cosmosPartitionKey);

    /// <summary>
    /// Emits the <c>cosmosdb.query.failed</c> event (ID 10202) and records metrics.
    /// </summary>
    internal static void CosmosDbQueryFailed(
        this ILogger logger,
        string cosmosDatabase,
        string cosmosContainer,
        double cosmosRequestCharge,
        double durationMs,
        int cosmosStatusCode,
        int? cosmosSubStatusCode,
        string errorType,
        string? cosmosPartitionKey,
        Exception? exception)
    {
        LogCosmosDbQueryFailed(
            logger, exception, cosmosDatabase, cosmosContainer,
            cosmosRequestCharge, durationMs, cosmosStatusCode,
            cosmosSubStatusCode, errorType, cosmosPartitionKey);

        ErrorCount.Add(1,
            new KeyValuePair<string, object?>("cosmosDatabase", cosmosDatabase),
            new KeyValuePair<string, object?>("cosmosContainer", cosmosContainer),
            new KeyValuePair<string, object?>("cosmosStatusCode", cosmosStatusCode));
    }

    // ─── Event: cosmosdb.point.read (ID 10203) ──────────────────────────

    [LoggerMessage(
        EventId = 10203,
        EventName = "cosmosdb.point.read",
        Level = LogLevel.Debug,
        Message = "CosmosDB point read on {cosmosDatabase}/{cosmosContainer} [{cosmosPartitionKey}] in {durationMs}ms ({cosmosRequestCharge} RU) status={cosmosStatusCode} region={cosmosRegion}")]
    private static partial void LogCosmosDbPointRead(
        ILogger logger,
        string cosmosDatabase,
        string cosmosContainer,
        string cosmosPartitionKey,
        double cosmosRequestCharge,
        double durationMs,
        int cosmosStatusCode,
        string? cosmosRegion);

    /// <summary>
    /// Emits the <c>cosmosdb.point.read</c> event (ID 10203) and records metrics.
    /// </summary>
    internal static void CosmosDbPointRead(
        this ILogger logger,
        string cosmosDatabase,
        string cosmosContainer,
        string cosmosPartitionKey,
        double cosmosRequestCharge,
        double durationMs,
        int cosmosStatusCode,
        string? cosmosRegion)
    {
        LogCosmosDbPointRead(
            logger, cosmosDatabase, cosmosContainer, cosmosPartitionKey,
            cosmosRequestCharge, durationMs, cosmosStatusCode, cosmosRegion);

        var dbTag = new KeyValuePair<string, object?>("cosmosDatabase", cosmosDatabase);
        var containerTag = new KeyValuePair<string, object?>("cosmosContainer", cosmosContainer);

        PointDuration.Record(durationMs, dbTag, containerTag);
        PointRu.Record(cosmosRequestCharge, dbTag, containerTag);
        OperationCount.Add(1, dbTag, containerTag,
            new KeyValuePair<string, object?>("cosmosOperationType", "PointRead"));
    }

    // ─── Event: cosmosdb.point.write (ID 10204) ─────────────────────────

    [LoggerMessage(
        EventId = 10204,
        EventName = "cosmosdb.point.write",
        Level = LogLevel.Debug,
        Message = "CosmosDB point write on {cosmosDatabase}/{cosmosContainer} [{cosmosPartitionKey}] in {durationMs}ms ({cosmosRequestCharge} RU) status={cosmosStatusCode} region={cosmosRegion}")]
    private static partial void LogCosmosDbPointWrite(
        ILogger logger,
        string cosmosDatabase,
        string cosmosContainer,
        string cosmosPartitionKey,
        double cosmosRequestCharge,
        double durationMs,
        int cosmosStatusCode,
        string? cosmosRegion);

    /// <summary>
    /// Emits the <c>cosmosdb.point.write</c> event (ID 10204) and records metrics.
    /// </summary>
    internal static void CosmosDbPointWrite(
        this ILogger logger,
        string cosmosDatabase,
        string cosmosContainer,
        string cosmosPartitionKey,
        double cosmosRequestCharge,
        double durationMs,
        int cosmosStatusCode,
        string? cosmosRegion)
    {
        LogCosmosDbPointWrite(
            logger, cosmosDatabase, cosmosContainer, cosmosPartitionKey,
            cosmosRequestCharge, durationMs, cosmosStatusCode, cosmosRegion);

        var dbTag = new KeyValuePair<string, object?>("cosmosDatabase", cosmosDatabase);
        var containerTag = new KeyValuePair<string, object?>("cosmosContainer", cosmosContainer);

        PointDuration.Record(durationMs, dbTag, containerTag);
        PointRu.Record(cosmosRequestCharge, dbTag, containerTag);
        OperationCount.Add(1, dbTag, containerTag,
            new KeyValuePair<string, object?>("cosmosOperationType", "PointWrite"));
    }
}
