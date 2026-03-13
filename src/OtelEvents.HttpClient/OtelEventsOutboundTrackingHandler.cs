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
/// Register via <see cref="OtelEventsHttpClientExtensions.AddOtelEventsOutboundTracking"/>.
/// </remarks>
internal sealed class OtelEventsOutboundTrackingHandler : DelegatingHandler
{
    private readonly ILogger<OtelEventsOutboundTrackingHandler> _logger;
    private readonly OtelEventsOutboundTrackingOptions _options;
    private readonly string? _httpClientName;

    public OtelEventsOutboundTrackingHandler(
        ILogger<OtelEventsOutboundTrackingHandler> logger,
        IOptionsMonitor<OtelEventsOutboundTrackingOptions> options,
        string? httpClientName)
    {
        _logger = logger;
        _options = options.CurrentValue;
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
        var isFailure = _options.IsFailure is not null
            ? _options.IsFailure(response)
            : (int)response.StatusCode >= 500;

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
    /// </summary>
    private string RedactUrl(Uri? requestUri)
    {
        if (requestUri is null)
        {
            return "<unknown>";
        }

        return _options.UrlRedactor is not null
            ? _options.UrlRedactor(requestUri)
            : requestUri.AbsoluteUri;
    }
}
