using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace OtelEvents.Grpc.Events;

/// <summary>
/// Pre-compiled [LoggerMessage] methods and metrics for gRPC call lifecycle events.
/// Maps to the grpc.otel.yaml schema (event IDs 10101–10103).
/// </summary>
/// <remarks>
/// This code is pre-compiled in the NuGet package — consumers do NOT need
/// OtelEvents.Schema at build time. The YAML schema is embedded for documentation
/// and tooling inspection only.
/// </remarks>
internal static partial class GrpcCallEvents
{
    // ─── Meter & Instruments ────────────────────────────────────────────

    private static readonly Meter s_meter = new("OtelEvents.Grpc", "1.0.0");

    /// <summary>Counter: total gRPC calls started, labeled by grpcService + grpcMethod + grpcSide.</summary>
    internal static readonly Counter<long> CallStartedCount =
        s_meter.CreateCounter<long>(
            "otel.grpc.call.started.count", "calls", "Total gRPC calls started");

    /// <summary>Histogram: gRPC call duration in ms.</summary>
    internal static readonly Histogram<double> CallDuration =
        s_meter.CreateHistogram<double>(
            "otel.grpc.call.duration", "ms", "gRPC call duration");

    /// <summary>Counter: total gRPC calls completed by status.</summary>
    internal static readonly Counter<long> CallCompletedCount =
        s_meter.CreateCounter<long>(
            "otel.grpc.call.completed.count", "calls", "Total gRPC calls completed by status");

    /// <summary>Counter: total gRPC call errors.</summary>
    internal static readonly Counter<long> CallErrorCount =
        s_meter.CreateCounter<long>(
            "otel.grpc.call.error.count", "errors", "Total gRPC call errors");

    // ─── Event: grpc.call.started (ID 10101) ────────────────────────────

    [LoggerMessage(
        EventId = 10101,
        EventName = "grpc.call.started",
        Level = LogLevel.Information,
        Message = "gRPC {GrpcSide} {GrpcService}/{GrpcMethod} started")]
    private static partial void LogGrpcCallStarted(
        ILogger logger,
        string grpcService,
        string grpcMethod,
        string grpcSide,
        long? requestSize);

    /// <summary>
    /// Emits the <c>grpc.call.started</c> event (ID 10101) and records metrics.
    /// </summary>
    internal static void GrpcCallStarted(
        this ILogger logger,
        string grpcService,
        string grpcMethod,
        string grpcSide,
        long? requestSize)
    {
        LogGrpcCallStarted(logger, grpcService, grpcMethod, grpcSide, requestSize);

        CallStartedCount.Add(1,
            new KeyValuePair<string, object?>("grpcService", grpcService),
            new KeyValuePair<string, object?>("grpcMethod", grpcMethod),
            new KeyValuePair<string, object?>("grpcSide", grpcSide));
    }

    // ─── Event: grpc.call.completed (ID 10102) ──────────────────────────

    [LoggerMessage(
        EventId = 10102,
        EventName = "grpc.call.completed",
        Level = LogLevel.Information,
        Message = "gRPC {GrpcSide} {GrpcService}/{GrpcMethod} completed with status {GrpcStatusCode} in {DurationMs}ms")]
    private static partial void LogGrpcCallCompleted(
        ILogger logger,
        string grpcService,
        string grpcMethod,
        string grpcSide,
        int grpcStatusCode,
        string? grpcStatusDetail,
        double durationMs,
        long? requestSize,
        long? responseSize);

    /// <summary>
    /// Emits the <c>grpc.call.completed</c> event (ID 10102) and records metrics.
    /// </summary>
    internal static void GrpcCallCompleted(
        this ILogger logger,
        string grpcService,
        string grpcMethod,
        string grpcSide,
        int grpcStatusCode,
        string? grpcStatusDetail,
        double durationMs,
        long? requestSize,
        long? responseSize)
    {
        LogGrpcCallCompleted(logger, grpcService, grpcMethod, grpcSide,
            grpcStatusCode, grpcStatusDetail, durationMs, requestSize, responseSize);

        var serviceTag = new KeyValuePair<string, object?>("grpcService", grpcService);
        var methodTag = new KeyValuePair<string, object?>("grpcMethod", grpcMethod);
        var sideTag = new KeyValuePair<string, object?>("grpcSide", grpcSide);
        var statusTag = new KeyValuePair<string, object?>("grpcStatusCode", grpcStatusCode);

        CallDuration.Record(durationMs, serviceTag, methodTag, sideTag, statusTag);
        CallCompletedCount.Add(1, serviceTag, methodTag, sideTag, statusTag);
    }

    // ─── Event: grpc.call.failed (ID 10103) ─────────────────────────────

    [LoggerMessage(
        EventId = 10103,
        EventName = "grpc.call.failed",
        Level = LogLevel.Error,
        Message = "gRPC {GrpcSide} {GrpcService}/{GrpcMethod} failed with {ErrorType} after {DurationMs}ms")]
    private static partial void LogGrpcCallFailed(
        ILogger logger,
        Exception exception,
        string grpcService,
        string grpcMethod,
        string grpcSide,
        int grpcStatusCode,
        string? grpcStatusDetail,
        double durationMs,
        string errorType);

    /// <summary>
    /// Emits the <c>grpc.call.failed</c> event (ID 10103) and records metrics.
    /// </summary>
    internal static void GrpcCallFailed(
        this ILogger logger,
        string grpcService,
        string grpcMethod,
        string grpcSide,
        int grpcStatusCode,
        string? grpcStatusDetail,
        double durationMs,
        string errorType,
        Exception exception)
    {
        LogGrpcCallFailed(logger, exception, grpcService, grpcMethod, grpcSide,
            grpcStatusCode, grpcStatusDetail, durationMs, errorType);

        CallErrorCount.Add(1,
            new KeyValuePair<string, object?>("grpcService", grpcService),
            new KeyValuePair<string, object?>("grpcMethod", grpcMethod),
            new KeyValuePair<string, object?>("grpcSide", grpcSide),
            new KeyValuePair<string, object?>("grpcStatusCode", grpcStatusCode));
    }
}
