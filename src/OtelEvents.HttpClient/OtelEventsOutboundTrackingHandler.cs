using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OtelEvents.HttpClient.Events;

namespace OtelEvents.HttpClient;

/// <summary>
/// A <see cref="DelegatingHandler"/> that emits structured events for outbound HTTP calls.
/// Emits up to three events per request: started (10010), completed (10011), failed (10012).
/// </summary>
/// <remarks>
/// The handler observes but never interferes — exceptions are always re-thrown.
/// User-provided delegates (<see cref="OtelEventsOutboundTrackingOptions.UrlRedactor"/>
/// and <see cref="OtelEventsOutboundTrackingOptions.IsFailure"/>) are wrapped defensively
/// so they can never kill the request or lose the response.
/// Register via <see cref="OtelEventsHttpClientExtensions.AddOtelEventsOutboundTracking"/>.
/// </remarks>
internal sealed class OtelEventsOutboundTrackingHandler : DelegatingHandler
{
    private readonly ILogger<OtelEventsHttpClientEventSource> _logger;
    private readonly OtelEventsOutboundTrackingOptions _options;
    private readonly string? _httpClientName;

    public OtelEventsOutboundTrackingHandler(
        ILogger<OtelEventsHttpClientEventSource> logger,
        IOptionsMonitor<OtelEventsOutboundTrackingOptions> options,
        string? httpClientName)
    {
        _logger = logger;
        _options = options.Get(httpClientName ?? Options.DefaultName);
        _httpClientName = httpClientName;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var method = request.Method.Method;
        var url = RedactUrl(request.RequestUri);

        // Emit http.outbound.started (10010)
        if (_options.EmitStartedEvent)
        {
            _logger.HttpOutboundStarted(method, url, _httpClientName);
        }

        var sw = Stopwatch.StartNew();
        HttpResponseMessage response;

        try
        {
            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            sw.Stop();

            // Emit http.outbound.failed (10012)
            _logger.HttpOutboundFailed(
                httpMethod: method,
                httpUrl: url,
                errorType: ex.GetType().Name,
                durationMs: sw.Elapsed.TotalMilliseconds,
                httpClientName: _httpClientName,
                exception: ex);

            throw;
        }

        sw.Stop();
        var durationMs = sw.Elapsed.TotalMilliseconds;

        // Check if response should be classified as failure
        var statusCode = (int)response.StatusCode;
        bool isFailure;

        if (_options.IsFailure is not null)
        {
            try
            {
                isFailure = _options.IsFailure(response);
            }
            catch
            {
                // Defensive: user-provided delegate must never lose the response.
                // Fall back to default classification (status >= 500).
                isFailure = statusCode >= 500;
            }
        }
        else
        {
            isFailure = statusCode >= 500;
        }

        if (isFailure)
        {
            // Emit http.outbound.failed (10012) for failure-classified responses
            _logger.HttpOutboundFailed(
                httpMethod: method,
                httpUrl: url,
                errorType: $"HTTP {(int)response.StatusCode}",
                durationMs: durationMs,
                httpClientName: _httpClientName);
        }
        else
        {
            // Emit http.outbound.completed (10011)
            _logger.HttpOutboundCompleted(
                httpMethod: method,
                httpUrl: url,
                httpStatusCode: (int)response.StatusCode,
                durationMs: durationMs,
                httpClientName: _httpClientName);
        }

        return response;
    }

    /// <summary>
    /// Applies the configured URL redactor, or returns the absolute URI as-is.
    /// Defensive: if the user-provided delegate throws, falls back to the raw URI.
    /// </summary>
    private string RedactUrl(Uri? requestUri)
    {
        if (requestUri is null)
        {
            return "<unknown>";
        }

        if (_options.UrlRedactor is not null)
        {
            try
            {
                return _options.UrlRedactor(requestUri);
            }
            catch
            {
                // Defensive: user-provided delegate must never kill the request.
                // Fall back to the raw absolute URI.
                return requestUri.AbsoluteUri;
            }
        }

        return requestUri.AbsoluteUri;
    }
}
