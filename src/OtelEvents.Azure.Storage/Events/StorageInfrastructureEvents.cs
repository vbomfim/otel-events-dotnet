using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

// Explicit aliases to avoid namespace collision with OtelEvents.Azure
using RequestFailedException = global::Azure.RequestFailedException;
using AzureRequest = global::Azure.Core.Request;
using AzureHttpMessage = global::Azure.Core.HttpMessage;

namespace OtelEvents.Azure.Storage.Events;

/// <summary>
/// Pre-compiled [LoggerMessage] methods and metrics for Azure Storage infrastructure events.
/// Maps to the azure-storage.all.yaml schema (event IDs 10308–10310).
/// </summary>
/// <remarks>
/// Infrastructure events provide deeper diagnostics by classifying
/// <see cref="Azure.RequestFailedException"/> into connection, authentication,
/// and throttling categories. They are supplemental — the regular error event
/// (e.g., storage.blob.failed) is still emitted alongside.
/// </remarks>
internal static partial class StorageInfrastructureEvents
{
    // ─── Meter & Instruments ────────────────────────────────────────────

    private static readonly Meter s_meter = new("OtelEvents.Azure.Storage.Infra", "1.0.0");

    /// <summary>Counter: total connection failures.</summary>
    internal static readonly Counter<long> ConnectionFailedCount =
        s_meter.CreateCounter<long>(
            "otel.storage.connection.failed.count", "failures", "Total connection failures");

    /// <summary>Counter: total auth failures.</summary>
    internal static readonly Counter<long> AuthFailedCount =
        s_meter.CreateCounter<long>(
            "otel.storage.auth.failed.count", "failures", "Total auth failures");

    /// <summary>Counter: total throttling events.</summary>
    internal static readonly Counter<long> ThrottledCount =
        s_meter.CreateCounter<long>(
            "otel.storage.throttled.count", "events", "Total throttling events");

    // ─── Event: storage.connection.failed (ID 10308) ────────────────────

    [LoggerMessage(
        EventId = 10308,
        EventName = "storage.connection.failed",
        Level = LogLevel.Error,
        Message = "Storage connection to {endpoint} failed after {durationMs}ms: {failureReason}")]
    private static partial void LogStorageConnectionFailed(
        ILogger logger,
        Exception? exception,
        string endpoint,
        string storageAccountName,
        double durationMs,
        string errorType,
        string errorMessage,
        string failureReason);

    /// <summary>
    /// Emits the <c>storage.connection.failed</c> event (ID 10308) and records metrics.
    /// </summary>
    internal static void StorageConnectionFailed(
        this ILogger logger,
        string endpoint,
        string storageAccountName,
        double durationMs,
        string errorType,
        string errorMessage,
        string failureReason,
        Exception? exception = null)
    {
        LogStorageConnectionFailed(logger, exception, endpoint, storageAccountName,
            durationMs, errorType, errorMessage, failureReason);

        ConnectionFailedCount.Add(1,
            new KeyValuePair<string, object?>("storageAccountName", storageAccountName));
    }

    // ─── Event: storage.auth.failed (ID 10309) ──────────────────────────

    [LoggerMessage(
        EventId = 10309,
        EventName = "storage.auth.failed",
        Level = LogLevel.Error,
        Message = "Storage auth failed for {storageAccountName} with {httpStatusCode} ({authScheme})")]
    private static partial void LogStorageAuthFailed(
        ILogger logger,
        Exception? exception,
        int httpStatusCode,
        string storageAccountName,
        string authScheme,
        string? identityHint);

    /// <summary>
    /// Emits the <c>storage.auth.failed</c> event (ID 10309) and records metrics.
    /// </summary>
    internal static void StorageAuthFailed(
        this ILogger logger,
        int httpStatusCode,
        string storageAccountName,
        string authScheme,
        string? identityHint,
        Exception? exception = null)
    {
        LogStorageAuthFailed(logger, exception, httpStatusCode, storageAccountName,
            authScheme, identityHint);

        AuthFailedCount.Add(1,
            new KeyValuePair<string, object?>("storageAccountName", storageAccountName),
            new KeyValuePair<string, object?>("authScheme", authScheme));
    }

    // ─── Event: storage.throttled (ID 10310) ────────────────────────────

    [LoggerMessage(
        EventId = 10310,
        EventName = "storage.throttled",
        Level = LogLevel.Warning,
        Message = "Storage request to {storageAccountName} was throttled ({httpStatusCode})")]
    private static partial void LogStorageThrottled(
        ILogger logger,
        Exception? exception,
        int httpStatusCode,
        string storageAccountName,
        long? retryAfterMs,
        string? currentLimit);

    /// <summary>
    /// Emits the <c>storage.throttled</c> event (ID 10310) and records metrics.
    /// </summary>
    internal static void StorageThrottled(
        this ILogger logger,
        int httpStatusCode,
        string storageAccountName,
        long? retryAfterMs,
        string? currentLimit,
        Exception? exception = null)
    {
        LogStorageThrottled(logger, exception, httpStatusCode, storageAccountName,
            retryAfterMs, currentLimit);

        ThrottledCount.Add(1,
            new KeyValuePair<string, object?>("storageAccountName", storageAccountName),
            new KeyValuePair<string, object?>("httpStatusCode", httpStatusCode));
    }

    // ─── Classification Helpers ─────────────────────────────────────────

    /// <summary>
    /// Determines whether a <see cref="RequestFailedException"/> represents
    /// a connection-level failure (DNS, socket, timeout) rather than an HTTP error.
    /// </summary>
    internal static bool IsConnectionError(RequestFailedException ex)
    {
        // Connection errors have Status == 0 (no HTTP response received)
        // and an inner exception indicating a network-level failure.
        if (ex.Status != 0)
        {
            return false;
        }

        return ex.InnerException is
            System.Net.Http.HttpRequestException or
            System.Net.Sockets.SocketException or
            TaskCanceledException or
            TimeoutException or
            IOException;
    }

    /// <summary>
    /// Determines whether a <see cref="RequestFailedException"/> represents
    /// an authentication or authorization failure (HTTP 401 or 403).
    /// </summary>
    internal static bool IsAuthError(RequestFailedException ex)
    {
        return ex.Status is 401 or 403;
    }

    /// <summary>
    /// Determines whether a <see cref="RequestFailedException"/> represents
    /// a throttling response (HTTP 429 or 503).
    /// </summary>
    internal static bool IsThrottlingError(RequestFailedException ex)
    {
        return ex.Status is 429 or 503;
    }

    /// <summary>
    /// Classifies the failure reason from the inner exception type.
    /// </summary>
    internal static string ClassifyFailureReason(RequestFailedException ex)
    {
        return ex.InnerException switch
        {
            System.Net.Http.HttpRequestException => "HttpRequestException",
            System.Net.Sockets.SocketException => "SocketException",
            TaskCanceledException => "Timeout",
            TimeoutException => "Timeout",
            IOException => "IOException",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Detects the authentication scheme from the HTTP request.
    /// </summary>
    internal static string DetectAuthScheme(AzureRequest request)
    {
        if (request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            if (authHeader.StartsWith("SharedKey", StringComparison.OrdinalIgnoreCase))
            {
                return "SharedKey";
            }
            if (authHeader.StartsWith("Bearer", StringComparison.OrdinalIgnoreCase))
            {
                return "Bearer";
            }
            return "Unknown";
        }

        // Check for SAS token in query string
        var uri = request.Uri.ToUri();
        var query = uri.Query;
        if (query.Contains("sig=", StringComparison.OrdinalIgnoreCase))
        {
            return "SAS";
        }

        return "Anonymous";
    }

    /// <summary>
    /// Extracts a privacy-safe identity hint (SHA-256 hash of the account portion)
    /// from the Authorization header. Returns null if not available.
    /// </summary>
    internal static string? ExtractIdentityHint(AzureRequest request)
    {
        if (!request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            return null;
        }

        // Extract the account/identity part before hashing
        var parts = authHeader.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return null;
        }

        // For "SharedKey account:signature" — hash the account part
        var credential = parts[1];
        var colonIndex = credential.IndexOf(':', StringComparison.Ordinal);
        var identity = colonIndex >= 0 ? credential[..colonIndex] : credential;

        var bytes = System.Text.Encoding.UTF8.GetBytes(identity);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash)[..16]; // First 16 hex chars for brevity
    }

    /// <summary>
    /// Parses the Retry-After header value and returns it in milliseconds,
    /// clamped to a maximum of 300,000 ms (5 minutes).
    /// Returns null if the header is not present or cannot be parsed.
    /// </summary>
    internal static long? ParseRetryAfterMs(RequestFailedException ex)
    {
        // RequestFailedException doesn't expose response headers directly.
        // The Retry-After info is sometimes in the message or headers.
        // Since we can't access response headers from the exception,
        // return null — callers can set this from the HttpMessage if available.
        return null;
    }

    /// <summary>
    /// Parses the Retry-After header from the HTTP message response,
    /// clamped to a maximum of 300,000 ms (5 minutes).
    /// </summary>
    internal static long? ParseRetryAfterMs(AzureHttpMessage? message)
    {
        const long maxRetryAfterMs = 300_000; // 5 minutes

        if (message is null)
        {
            return null;
        }

        // Response getter throws InvalidOperationException if no response was set
        // (transport-level failures). Guard with try-catch.
        string? retryAfter;
        try
        {
            if (message.Response is null)
            {
                return null;
            }

            if (!message.Response.Headers.TryGetValue("Retry-After", out retryAfter))
            {
                return null;
            }
        }
#pragma warning disable CA1031 // Response getter may throw when not set
        catch (InvalidOperationException)
        {
            return null;
        }
#pragma warning restore CA1031

        // Retry-After can be seconds (integer) or an HTTP-date
        if (long.TryParse(retryAfter, System.Globalization.CultureInfo.InvariantCulture, out var seconds))
        {
            var ms = seconds * 1000;
            return Math.Min(ms, maxRetryAfterMs);
        }

        // Try parsing as HTTP-date
        if (DateTimeOffset.TryParse(retryAfter, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var date))
        {
            var delay = (long)(date - DateTimeOffset.UtcNow).TotalMilliseconds;
            return Math.Clamp(delay, 0, maxRetryAfterMs);
        }

        return null;
    }
}
