using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace OtelEvents.Grpc.Events;

/// <summary>
/// Pre-compiled [LoggerMessage] methods and metrics for gRPC infrastructure events.
/// Maps to the grpc.otel.yaml schema (event IDs 10104–10106).
/// These events are supplemental — they fire alongside <c>grpc.call.failed</c> (10103)
/// to provide domain-specific detail about connection, authentication, and throttling failures.
/// </summary>
/// <remarks>
/// Infrastructure events are opt-in via <see cref="OtelEventsGrpcOptions.EmitInfrastructureEvents"/>
/// (default: true). They are always wrapped in a defensive try-catch so they never
/// interfere with the main call lifecycle.
/// </remarks>
internal static partial class GrpcInfrastructureEvents
{
    // ─── Meter & Instruments ────────────────────────────────────────────

    private static readonly Meter s_meter = new("OtelEvents.Grpc", "1.0.0");

    /// <summary>Counter: gRPC connection failures (StatusCode.Unavailable).</summary>
    internal static readonly Counter<long> ConnectionFailedCount =
        s_meter.CreateCounter<long>(
            "otel.grpc.connection.failed.count", "errors", "gRPC connection failures");

    /// <summary>Counter: gRPC authentication/authorization failures.</summary>
    internal static readonly Counter<long> AuthFailedCount =
        s_meter.CreateCounter<long>(
            "otel.grpc.auth.failed.count", "errors", "gRPC authentication failures");

    /// <summary>Counter: gRPC throttled requests (StatusCode.ResourceExhausted).</summary>
    internal static readonly Counter<long> ThrottledCount =
        s_meter.CreateCounter<long>(
            "otel.grpc.throttled.count", "errors", "gRPC throttled requests");

    // ─── Event: grpc.connection.failed (ID 10104) ───────────────────────

    [LoggerMessage(
        EventId = 10104,
        EventName = "grpc.connection.failed",
        Level = LogLevel.Error,
        Message = "gRPC {GrpcSide} connection to {GrpcService}/{GrpcMethod} failed: {FailureReason}")]
    private static partial void LogGrpcConnectionFailed(
        ILogger logger,
        Exception exception,
        string grpcService,
        string grpcMethod,
        string grpcSide,
        string? endpoint,
        double durationMs,
        string errorType,
        string? errorMessage,
        string? failureReason);

    /// <summary>
    /// Emits the <c>grpc.connection.failed</c> event (ID 10104) and records metrics.
    /// Triggered when an RpcException has StatusCode.Unavailable.
    /// </summary>
    internal static void GrpcConnectionFailed(
        this ILogger logger,
        string grpcService,
        string grpcMethod,
        string grpcSide,
        string? endpoint,
        double durationMs,
        string errorType,
        string? errorMessage,
        string? failureReason,
        Exception exception)
    {
        LogGrpcConnectionFailed(logger, exception, grpcService, grpcMethod, grpcSide,
            endpoint, durationMs, errorType, errorMessage, failureReason);

        ConnectionFailedCount.Add(1,
            new KeyValuePair<string, object?>("grpcService", grpcService),
            new KeyValuePair<string, object?>("grpcMethod", grpcMethod),
            new KeyValuePair<string, object?>("grpcSide", grpcSide));
    }

    // ─── Event: grpc.auth.failed (ID 10105) ─────────────────────────────

    [LoggerMessage(
        EventId = 10105,
        EventName = "grpc.auth.failed",
        Level = LogLevel.Error,
        Message = "gRPC {GrpcSide} auth failed for {GrpcService}/{GrpcMethod} with HTTP {HttpStatusCode}")]
    private static partial void LogGrpcAuthFailed(
        ILogger logger,
        Exception exception,
        string grpcService,
        string grpcMethod,
        string grpcSide,
        int httpStatusCode,
        string? authScheme,
        string? identityHint);

    /// <summary>
    /// Emits the <c>grpc.auth.failed</c> event (ID 10105) and records metrics.
    /// Triggered when an RpcException has StatusCode.Unauthenticated or PermissionDenied.
    /// Maps gRPC codes to HTTP: Unauthenticated → 401, PermissionDenied → 403.
    /// </summary>
    internal static void GrpcAuthFailed(
        this ILogger logger,
        string grpcService,
        string grpcMethod,
        string grpcSide,
        int httpStatusCode,
        string? authScheme,
        string? identityHint,
        Exception exception)
    {
        LogGrpcAuthFailed(logger, exception, grpcService, grpcMethod, grpcSide,
            httpStatusCode, authScheme, identityHint);

        AuthFailedCount.Add(1,
            new KeyValuePair<string, object?>("grpcService", grpcService),
            new KeyValuePair<string, object?>("grpcMethod", grpcMethod),
            new KeyValuePair<string, object?>("grpcSide", grpcSide),
            new KeyValuePair<string, object?>("httpStatusCode", httpStatusCode));
    }

    // ─── Event: grpc.throttled (ID 10106) ───────────────────────────────

    [LoggerMessage(
        EventId = 10106,
        EventName = "grpc.throttled",
        Level = LogLevel.Warning,
        Message = "gRPC {GrpcSide} {GrpcService}/{GrpcMethod} throttled")]
    private static partial void LogGrpcThrottled(
        ILogger logger,
        Exception exception,
        string grpcService,
        string grpcMethod,
        string grpcSide,
        long? retryAfterMs);

    /// <summary>
    /// Emits the <c>grpc.throttled</c> event (ID 10106) and records metrics.
    /// Triggered when an RpcException has StatusCode.ResourceExhausted.
    /// </summary>
    internal static void GrpcThrottled(
        this ILogger logger,
        string grpcService,
        string grpcMethod,
        string grpcSide,
        long? retryAfterMs,
        Exception exception)
    {
        LogGrpcThrottled(logger, exception, grpcService, grpcMethod, grpcSide, retryAfterMs);

        ThrottledCount.Add(1,
            new KeyValuePair<string, object?>("grpcService", grpcService),
            new KeyValuePair<string, object?>("grpcMethod", grpcMethod),
            new KeyValuePair<string, object?>("grpcSide", grpcSide));
    }

    // ─── Infrastructure Event Classification ────────────────────────────

    /// <summary>
    /// Classifies an RpcException by StatusCode and emits the appropriate
    /// infrastructure event. Wrapped in defensive try-catch — never throws.
    /// </summary>
    /// <param name="logger">The logger to emit events through.</param>
    /// <param name="options">Options controlling whether infra events are enabled.</param>
    /// <param name="serviceName">The gRPC service name.</param>
    /// <param name="methodName">The gRPC method name.</param>
    /// <param name="grpcSide">Whether this is "Client" or "Server".</param>
    /// <param name="ex">The RpcException to classify.</param>
    /// <param name="durationMs">Call duration in milliseconds.</param>
    /// <param name="endpoint">The endpoint involved (host for client, peer for server).</param>
    /// <param name="requestMetadata">Request metadata for auth info extraction.</param>
    internal static void TryEmitInfrastructureEvent(
        ILogger logger,
        OtelEventsGrpcOptions options,
        string serviceName,
        string methodName,
        string grpcSide,
        RpcException ex,
        double durationMs,
        string? endpoint,
        Metadata? requestMetadata)
    {
        if (!options.EmitInfrastructureEvents)
        {
            return;
        }

        try
        {
            switch (ex.StatusCode)
            {
                case StatusCode.Unavailable:
                    logger.GrpcConnectionFailed(
                        grpcService: serviceName,
                        grpcMethod: methodName,
                        grpcSide: grpcSide,
                        endpoint: endpoint,
                        durationMs: durationMs,
                        errorType: ex.GetType().Name,
                        errorMessage: ex.Message,
                        failureReason: ex.Status.Detail,
                        exception: ex);
                    break;

                case StatusCode.Unauthenticated:
                case StatusCode.PermissionDenied:
                    var httpStatusCode = ex.StatusCode == StatusCode.Unauthenticated ? 401 : 403;
                    var (authScheme, identityHint) = ExtractAuthInfo(requestMetadata);
                    logger.GrpcAuthFailed(
                        grpcService: serviceName,
                        grpcMethod: methodName,
                        grpcSide: grpcSide,
                        httpStatusCode: httpStatusCode,
                        authScheme: authScheme,
                        identityHint: identityHint,
                        exception: ex);
                    break;

                case StatusCode.ResourceExhausted:
                    var retryAfterMs = ExtractRetryAfterMs(ex.Trailers);
                    logger.GrpcThrottled(
                        grpcService: serviceName,
                        grpcMethod: methodName,
                        grpcSide: grpcSide,
                        retryAfterMs: retryAfterMs,
                        exception: ex);
                    break;
            }
        }
#pragma warning disable CA1031 // Defensive: infrastructure events are supplemental — never interfere with call handling
        catch
        {
            // Intentionally empty — infrastructure event failure must not affect the gRPC call.
        }
#pragma warning restore CA1031
    }

    // ─── Helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts retry-after-ms from gRPC trailing metadata.
    /// Returns null if the key is absent or the value is not a valid long.
    /// </summary>
    internal static long? ExtractRetryAfterMs(Metadata? trailers)
    {
        if (trailers is null)
        {
            return null;
        }

        foreach (var entry in trailers)
        {
            if (string.Equals(entry.Key, "retry-after-ms", StringComparison.OrdinalIgnoreCase)
                && long.TryParse(entry.Value, out var ms))
            {
                return ms;
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts authentication scheme and identity hint from request metadata.
    /// The identity hint is a truncated SHA-256 hash — never the raw credential.
    /// </summary>
    internal static (string? AuthScheme, string? IdentityHint) ExtractAuthInfo(Metadata? metadata)
    {
        if (metadata is null)
        {
            return (null, null);
        }

        string? authValue = null;
        foreach (var entry in metadata)
        {
            if (string.Equals(entry.Key, "authorization", StringComparison.OrdinalIgnoreCase))
            {
                authValue = entry.Value;
                break;
            }
        }

        if (authValue is null)
        {
            return (null, null);
        }

        var spaceIndex = authValue.IndexOf(' ', StringComparison.Ordinal);
        if (spaceIndex <= 0)
        {
            return (authValue, null); // Scheme only, no token part
        }

        var scheme = authValue[..spaceIndex];
        var token = authValue[(spaceIndex + 1)..];
        var hint = HashIdentity(token);

        return (scheme, hint);
    }

    /// <summary>
    /// Produces a truncated SHA-256 hash of the input (first 8 bytes as 16 hex chars).
    /// Used for identity hints — never log raw credentials.
    /// </summary>
    internal static string HashIdentity(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
#pragma warning disable CA1308 // Lowercase hex is intentional for identity hashing
        return Convert.ToHexString(bytes).ToLowerInvariant()[..16];
#pragma warning restore CA1308
    }
}
