using System.Diagnostics;
using OtelEvents.Causality;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OtelEvents.AspNetCore.Events;

namespace OtelEvents.AspNetCore;

/// <summary>
/// ASP.NET Core middleware that emits schema-defined events for HTTP request lifecycle.
/// Emits three events per request: received (10001), completed (10002), failed (10003).
/// Registered at the outermost position in the pipeline via <see cref="OtelEventsAspNetCoreStartupFilter"/>
/// or manually via <c>app.UseOtelEventsAspNetCore()</c>.
/// </summary>
/// <remarks>
/// The middleware observes but never interferes — exceptions are always re-thrown.
/// Path exclusion, route template resolution, and causal scope creation are configurable
/// via <see cref="OtelEventsAspNetCoreOptions"/>.
/// </remarks>
internal sealed class OtelEventsAspNetCoreMiddleware : IMiddleware
{
    private readonly ILogger<OtelEventsAspNetCoreEventSource> _logger;
    private readonly OtelEventsAspNetCoreOptions _options;

    public OtelEventsAspNetCoreMiddleware(
        ILogger<OtelEventsAspNetCoreEventSource> logger,
        IOptions<OtelEventsAspNetCoreOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Processes the HTTP request, emitting lifecycle events and recording metrics.
    /// </summary>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (IsExcluded(context.Request.Path))
        {
            await next(context);
            return;
        }

        // For received event, always use raw path (routing hasn't run yet)
        var rawPath = GetRawPath(context);

        // Emit http.request.received (10001)
        if (_options.RecordRequestReceived)
        {
            _logger.HttpRequestReceived(
                httpMethod: context.Request.Method,
                httpPath: rawPath,
                userAgent: _options.CaptureUserAgent ? context.Request.Headers.UserAgent.ToString() : null,
                clientIp: _options.CaptureClientIp ? GetClientIp(context) : null,
                contentLength: context.Request.ContentLength,
                requestId: context.TraceIdentifier);
        }

        // Create causal scope — all events within this request share a parentEventId
        IDisposable? causalScope = null;
        if (_options.EnableCausalScope)
        {
            var parentEventId = GetLastEmittedEventId(context);
            if (parentEventId is not null)
            {
                causalScope = OtelEventsCausalityContext.SetParent(parentEventId);
            }
        }

        var sw = Stopwatch.StartNew();
        try
        {
            await next(context);
            sw.Stop();

            // After routing, resolve path (route template available now)
            var resolvedPath = GetPath(context);

            // Emit http.request.completed (10002) — for all responses (1xx–5xx)
            _logger.HttpRequestCompleted(
                httpMethod: context.Request.Method,
                httpPath: resolvedPath,
                httpRoute: GetRouteTemplate(context),
                httpStatusCode: context.Response.StatusCode,
                durationMs: sw.Elapsed.TotalMilliseconds,
                contentLength: context.Response.ContentLength,
                requestId: context.TraceIdentifier);

            // Emit supplemental infrastructure events based on response status code
            if (_options.EmitInfrastructureEvents)
            {
                EmitResponseInfrastructureEvents(context);
            }
        }
        catch (Exception ex)
        {
            sw.Stop();

            // After routing (may have run partially), resolve path
            var resolvedPath = GetPath(context);

            // Emit http.request.failed (10003) — only for unhandled exceptions
            _logger.HttpRequestFailed(
                httpMethod: context.Request.Method,
                httpPath: resolvedPath,
                httpRoute: GetRouteTemplate(context),
                httpStatusCode: context.Response.HasStarted ? context.Response.StatusCode : null,
                durationMs: sw.Elapsed.TotalMilliseconds,
                errorType: ex.GetType().Name,
                requestId: context.TraceIdentifier,
                exception: ex);

            // Emit supplemental infrastructure events for exceptions
            if (_options.EmitInfrastructureEvents)
            {
                EmitExceptionInfrastructureEvents(context, ex, sw.Elapsed.TotalMilliseconds);
            }

            throw; // Re-throw — middleware observes, never swallows
        }
        finally
        {
            causalScope?.Dispose();
        }
    }

    /// <summary>
    /// Determines whether the request path should be excluded from event emission.
    /// Supports exact match and prefix match.
    /// </summary>
    internal bool IsExcluded(PathString requestPath)
    {
        var pathValue = requestPath.Value;
        if (string.IsNullOrEmpty(pathValue))
        {
            return false;
        }

        for (var i = 0; i < _options.ExcludePaths.Count; i++)
        {
            var excluded = _options.ExcludePaths[i];
            if (pathValue.Equals(excluded, StringComparison.OrdinalIgnoreCase) ||
                pathValue.StartsWith(excluded, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the raw request path (before routing).
    /// Truncates to <see cref="OtelEventsAspNetCoreOptions.MaxPathLength"/>.
    /// Used for http.request.received which fires before routing runs.
    /// </summary>
    private string GetRawPath(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";

        if (path.Length > _options.MaxPathLength)
        {
            path = path[.._options.MaxPathLength];
        }

        return path;
    }

    /// <summary>
    /// Gets the request path, using route template when available and configured.
    /// Truncates to <see cref="OtelEventsAspNetCoreOptions.MaxPathLength"/>.
    /// Used for http.request.completed and http.request.failed (after routing).
    /// </summary>
    private string GetPath(HttpContext context)
    {
        string? path;

        if (_options.UseRouteTemplate)
        {
            path = GetRouteTemplate(context) ?? context.Request.Path.Value;
        }
        else
        {
            path = context.Request.Path.Value;
        }

        path ??= "/";

        if (path.Length > _options.MaxPathLength)
        {
            path = path[.._options.MaxPathLength];
        }

        return path;
    }

    /// <summary>
    /// Extracts the matched route template from the endpoint metadata (e.g., /api/orders/{id}).
    /// Returns null when no route template is available.
    /// </summary>
    private static string? GetRouteTemplate(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint is RouteEndpoint routeEndpoint)
        {
            return routeEndpoint.RoutePattern.RawText;
        }

        return null;
    }

    /// <summary>
    /// Gets the client IP address, checking X-Forwarded-For header first,
    /// then falling back to RemoteIpAddress.
    /// </summary>
    private static string? GetClientIp(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // Take the first IP in the chain (client IP)
            var commaIndex = forwardedFor.IndexOf(',', StringComparison.Ordinal);
            return commaIndex > 0 ? forwardedFor[..commaIndex].Trim() : forwardedFor.Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }

    /// <summary>
    /// Attempts to retrieve the last emitted event ID from the OTEL pipeline.
    /// Uses the current OtelEventsCausalityContext if a parent was already set,
    /// otherwise generates a new event ID for the causal scope root.
    /// </summary>
    private static string? GetLastEmittedEventId(HttpContext context)
    {
        // The received event was just logged via the OTEL pipeline.
        // The OtelEventsCausalityProcessor adds otel_events.event_id to each LogRecord.
        // We need a stable event ID for the causal scope root.
        // Generate a new UUID v7 event ID for the causal scope.
        return Uuid7.FormatEventId();
    }

    // ─── Infrastructure Event Helpers ───────────────────────────────────

    /// <summary>
    /// Emits supplemental infrastructure events based on the HTTP response status code.
    /// Handles 401/403 (auth failed) and 429 (throttled) responses.
    /// Defensive: never throws — wrapped in try-catch.
    /// </summary>
    private void EmitResponseInfrastructureEvents(HttpContext context)
    {
        try
        {
            var statusCode = context.Response.StatusCode;

            if (statusCode is 401 or 403)
            {
                HttpInfrastructureEvents.EmitAuthFailed(
                    _logger,
                    httpStatusCode: statusCode,
                    wwwAuthenticateHeader: context.Response.Headers["WWW-Authenticate"].ToString(),
                    authorizationHeader: context.Request.Headers.Authorization.ToString());
            }
            else if (statusCode == 429)
            {
                HttpInfrastructureEvents.EmitThrottled(
                    _logger,
                    httpStatusCode: statusCode,
                    retryAfterHeader: context.Response.Headers["Retry-After"].ToString(),
                    rateLimitHeader: context.Response.Headers["X-RateLimit-Limit"].ToString());
            }
        }
        catch
        {
            // Defensive: infrastructure event emission must NEVER throw
        }
    }

    /// <summary>
    /// Emits supplemental infrastructure events for exceptions.
    /// Only emits http.connection.failed for HttpRequestException.
    /// Defensive: never throws — wrapped in try-catch.
    /// </summary>
    private void EmitExceptionInfrastructureEvents(HttpContext context, Exception exception, double durationMs)
    {
        try
        {
            if (exception is HttpRequestException httpEx)
            {
                var endpoint = GetRawPath(context);
                HttpInfrastructureEvents.EmitConnectionFailed(
                    _logger,
                    endpoint: endpoint,
                    durationMs: durationMs,
                    exception: httpEx);
            }
        }
        catch
        {
            // Defensive: infrastructure event emission must NEVER throw
        }
    }
}
