using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace OtelEvents.AspNetCore.Events;

/// <summary>
/// Pre-compiled [LoggerMessage] methods and metrics for HTTP infrastructure events.
/// Maps to the aspnetcore.all.yaml schema (event IDs 10004–10006).
/// These are SUPPLEMENTAL events — they fire alongside the standard operation events.
/// </summary>
/// <remarks>
/// Event emission is defensive: all public methods catch exceptions internally
/// to guarantee the middleware pipeline is never disrupted by telemetry failures.
/// </remarks>
internal static partial class HttpInfrastructureEvents
{
    // ─── Constants ──────────────────────────────────────────────────────

    /// <summary>Maximum Retry-After value in milliseconds (1 hour).</summary>
    private const long MaxRetryAfterMs = 3_600_000L;

    /// <summary>Length of the identity hint hash prefix.</summary>
    private const int IdentityHintLength = 8;

    // ─── Meter & Instruments ────────────────────────────────────────────

    private static readonly Meter s_meter = new("OtelEvents.AspNetCore.Infra", "1.0.0");

    /// <summary>Counter: total HTTP connection failures.</summary>
    internal static readonly Counter<long> ConnectionFailedCount =
        s_meter.CreateCounter<long>(
            "otel.http.connection.failed.count", "errors", "Total HTTP connection failures");

    /// <summary>Counter: total HTTP authentication failures (401/403).</summary>
    internal static readonly Counter<long> AuthFailedCount =
        s_meter.CreateCounter<long>(
            "otel.http.auth.failed.count", "errors", "Total HTTP authentication failures");

    /// <summary>Counter: total HTTP throttle responses (429).</summary>
    internal static readonly Counter<long> ThrottledCount =
        s_meter.CreateCounter<long>(
            "otel.http.throttled.count", "responses", "Total HTTP throttle responses");

    // ─── Event: http.connection.failed (ID 10004) ───────────────────────

    [LoggerMessage(
        EventId = 10004,
        EventName = "http.connection.failed",
        Level = LogLevel.Error,
        Message = "HTTP connection to {Endpoint} failed after {DurationMs}ms: {FailureReason}")]
    private static partial void LogConnectionFailed(
        ILogger logger,
        string endpoint,
        double durationMs,
        string errorType,
        string errorMessage,
        string failureReason,
        int? port);

    /// <summary>
    /// Emits the <c>http.connection.failed</c> event (ID 10004) and records metrics.
    /// Defensive: never throws.
    /// </summary>
    internal static void EmitConnectionFailed(
        ILogger logger,
        string endpoint,
        double durationMs,
        HttpRequestException exception)
    {
        try
        {
            var failureReason = ClassifyFailureReason(exception);
            var errorType = exception.GetType().Name;
            var errorMessage = exception.Message;
            var port = ExtractPort(endpoint);

            LogConnectionFailed(logger, endpoint, durationMs, errorType, errorMessage, failureReason, port);

            ConnectionFailedCount.Add(1,
                new KeyValuePair<string, object?>("failureReason", failureReason));
        }
        catch
        {
            // Defensive: infrastructure event emission must NEVER throw
        }
    }

    // ─── Event: http.auth.failed (ID 10005) ─────────────────────────────

    [LoggerMessage(
        EventId = 10005,
        EventName = "http.auth.failed",
        Level = LogLevel.Error,
        Message = "HTTP authentication failed with {HttpStatusCode} using {AuthScheme}")]
    private static partial void LogAuthFailed(
        ILogger logger,
        int httpStatusCode,
        string authScheme,
        string? identityHint);

    /// <summary>
    /// Emits the <c>http.auth.failed</c> event (ID 10005) and records metrics.
    /// Defensive: never throws.
    /// </summary>
    internal static void EmitAuthFailed(
        ILogger logger,
        int httpStatusCode,
        string? wwwAuthenticateHeader,
        string? authorizationHeader)
    {
        try
        {
            var authScheme = ParseAuthScheme(wwwAuthenticateHeader);
            var identityHint = ComputeIdentityHint(authorizationHeader);

            LogAuthFailed(logger, httpStatusCode, authScheme, identityHint);

            AuthFailedCount.Add(1,
                new KeyValuePair<string, object?>("authScheme", authScheme),
                new KeyValuePair<string, object?>("httpStatusCode", httpStatusCode));
        }
        catch
        {
            // Defensive: infrastructure event emission must NEVER throw
        }
    }

    // ─── Event: http.throttled (ID 10006) ───────────────────────────────

    [LoggerMessage(
        EventId = 10006,
        EventName = "http.throttled",
        Level = LogLevel.Warning,
        Message = "HTTP request throttled with {HttpStatusCode}, retry after {RetryAfterMs}ms")]
    private static partial void LogThrottled(
        ILogger logger,
        int httpStatusCode,
        long? retryAfterMs,
        string? currentLimit);

    /// <summary>
    /// Emits the <c>http.throttled</c> event (ID 10006) and records metrics.
    /// Defensive: never throws.
    /// </summary>
    internal static void EmitThrottled(
        ILogger logger,
        int httpStatusCode,
        string? retryAfterHeader,
        string? rateLimitHeader)
    {
        try
        {
            var retryAfterMs = ParseRetryAfterMs(retryAfterHeader);
            var currentLimit = string.IsNullOrEmpty(rateLimitHeader) ? null : rateLimitHeader;

            LogThrottled(logger, httpStatusCode, retryAfterMs, currentLimit);

            ThrottledCount.Add(1);
        }
        catch
        {
            // Defensive: infrastructure event emission must NEVER throw
        }
    }

    // ─── Classification Helpers ─────────────────────────────────────────

    /// <summary>
    /// Classifies the failure reason from an HttpRequestException's inner exception.
    /// </summary>
    internal static string ClassifyFailureReason(HttpRequestException exception)
    {
        // Check inner exception for socket errors
        if (exception.InnerException is System.Net.Sockets.SocketException socketEx)
        {
            return socketEx.SocketErrorCode switch
            {
                System.Net.Sockets.SocketError.ConnectionRefused => "ConnectionRefused",
                System.Net.Sockets.SocketError.HostNotFound => "DnsResolutionFailed",
                System.Net.Sockets.SocketError.HostUnreachable => "ConnectionRefused",
                System.Net.Sockets.SocketError.TimedOut => "Timeout",
                _ => "Unknown"
            };
        }

        // Check for TLS/SSL exceptions
        if (exception.InnerException is System.Security.Authentication.AuthenticationException)
        {
            return "TlsHandshakeFailed";
        }

        // Check for timeout
        if (exception.InnerException is TaskCanceledException or OperationCanceledException)
        {
            return "Timeout";
        }

        // Check message hints
        var message = exception.Message;
        if (message.Contains("SSL", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("TLS", StringComparison.OrdinalIgnoreCase))
        {
            return "TlsHandshakeFailed";
        }

        if (message.Contains("DNS", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("name resolution", StringComparison.OrdinalIgnoreCase))
        {
            return "DnsResolutionFailed";
        }

        if (message.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return "Timeout";
        }

        return "Unknown";
    }

    /// <summary>
    /// Parses the auth scheme from WWW-Authenticate header.
    /// Returns "Unknown" when the header is absent or unrecognized.
    /// </summary>
    internal static string ParseAuthScheme(string? wwwAuthenticateHeader)
    {
        if (string.IsNullOrEmpty(wwwAuthenticateHeader))
        {
            return "Unknown";
        }

        // The header format is: scheme [realm="...", ...]
        // We only need the first token
        var spaceIndex = wwwAuthenticateHeader.IndexOf(' ', StringComparison.Ordinal);
        var scheme = spaceIndex > 0
            ? wwwAuthenticateHeader[..spaceIndex]
            : wwwAuthenticateHeader;

        return scheme switch
        {
            "Bearer" => "Bearer",
            "SharedKey" => "SharedKey",
            "Basic" => "Basic",
            _ => scheme // Return as-is for recognized non-standard schemes
        };
    }

    /// <summary>
    /// Computes a privacy-safe identity hint from the Authorization header.
    /// Returns the first 8 characters of a SHA-256 hash — NEVER the raw credential.
    /// Returns null when no Authorization header is present.
    /// </summary>
    internal static string? ComputeIdentityHint(string? authorizationHeader)
    {
        if (string.IsNullOrEmpty(authorizationHeader))
        {
            return null;
        }

        var bytes = Encoding.UTF8.GetBytes(authorizationHeader);
        var hash = SHA256.HashData(bytes);
        var hex = Convert.ToHexString(hash);

        return hex[..IdentityHintLength].ToLowerInvariant();
    }

    /// <summary>
    /// Parses the Retry-After header value into milliseconds.
    /// Supports both seconds (integer) and HTTP-date formats.
    /// Clamps result to [0, 3600000] (max 1 hour).
    /// Returns null when the header is absent or unparseable.
    /// </summary>
    internal static long? ParseRetryAfterMs(string? retryAfterHeader)
    {
        if (string.IsNullOrEmpty(retryAfterHeader))
        {
            return null;
        }

        // Try parsing as integer seconds first
        if (long.TryParse(retryAfterHeader, out var seconds))
        {
            var ms = seconds * 1000L;
            return ClampRetryAfterMs(ms);
        }

        // Try parsing as HTTP-date
        if (DateTimeOffset.TryParse(retryAfterHeader, out var retryDate))
        {
            var delta = retryDate - DateTimeOffset.UtcNow;
            var ms = (long)delta.TotalMilliseconds;
            return ClampRetryAfterMs(Math.Max(0, ms));
        }

        return null;
    }

    /// <summary>
    /// Clamps a retry-after value to the allowed range [0, 3600000].
    /// </summary>
    private static long ClampRetryAfterMs(long ms)
    {
        if (ms < 0) return 0;
        if (ms > MaxRetryAfterMs) return MaxRetryAfterMs;
        return ms;
    }

    /// <summary>
    /// Extracts a port number from an endpoint string, if present.
    /// </summary>
    private static int? ExtractPort(string endpoint)
    {
        // Try to find :port at the end of the endpoint
        var lastColon = endpoint.LastIndexOf(':');
        if (lastColon > 0 && lastColon < endpoint.Length - 1)
        {
            var portStr = endpoint[(lastColon + 1)..];
            // Remove any trailing path
            var slashIndex = portStr.IndexOf('/', StringComparison.Ordinal);
            if (slashIndex >= 0)
            {
                portStr = portStr[..slashIndex];
            }

            if (int.TryParse(portStr, out var port) && port > 0 && port <= 65535)
            {
                return port;
            }
        }

        return null;
    }
}
