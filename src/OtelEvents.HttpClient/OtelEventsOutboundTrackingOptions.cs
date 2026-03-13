namespace OtelEvents.HttpClient;

/// <summary>
/// Configuration for the OtelEvents.HttpClient outbound tracking handler.
/// Controls URL redaction, failure classification, and which events are emitted.
/// </summary>
public sealed class OtelEventsOutboundTrackingOptions
{
    /// <summary>
    /// Redact URLs before emitting (e.g., strip query params).
    /// Receives the request <see cref="Uri"/> and returns a sanitized string.
    /// Default: null (uses <see cref="Uri.AbsoluteUri"/> as-is).
    /// </summary>
    public Func<Uri, string>? UrlRedactor { get; set; }

    /// <summary>
    /// Custom failure classification.
    /// Receives the <see cref="HttpResponseMessage"/> and returns true if the response
    /// should be classified as a failure (emits <c>http.outbound.failed</c> instead of <c>http.outbound.completed</c>).
    /// Default: null (status &gt;= 500 is considered a failure).
    /// </summary>
    public Func<HttpResponseMessage, bool>? IsFailure { get; set; }

    /// <summary>
    /// Emit <c>http.outbound.started</c> event before sending the request.
    /// Default: true.
    /// </summary>
    public bool EmitStartedEvent { get; set; } = true;
}
