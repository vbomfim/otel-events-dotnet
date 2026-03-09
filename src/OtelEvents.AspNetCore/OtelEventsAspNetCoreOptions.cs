namespace OtelEvents.AspNetCore;

/// <summary>
/// Configuration for the OtelEvents.AspNetCore integration pack.
/// Controls which HTTP lifecycle events are emitted and what data they capture.
/// </summary>
/// <remarks>
/// PII-sensitive fields (<see cref="CaptureUserAgent"/> and <see cref="CaptureClientIp"/>)
/// default to <c>false</c> to comply with GDPR/CCPA by default.
/// </remarks>
public sealed class OtelEventsAspNetCoreOptions
{
    /// <summary>
    /// Enable causal scope per request. When true, all events emitted during
    /// request processing share a parentEventId pointing to the http.request.received event.
    /// Default: true (requires All.Causality to be referenced; no-op otherwise).
    /// </summary>
    public bool EnableCausalScope { get; set; } = true;

    /// <summary>
    /// Emit http.request.received event at the start of each request.
    /// Set to false if only request completion/failure events are needed.
    /// Default: true.
    /// </summary>
    public bool RecordRequestReceived { get; set; } = true;

    /// <summary>
    /// Capture User-Agent header in http.request.received events.
    /// Default: false — opt-in only (PII: GDPR/CCPA).
    /// </summary>
    public bool CaptureUserAgent { get; set; }

    /// <summary>
    /// Capture client IP address in http.request.received events.
    /// Default: false — opt-in only (PII: GDPR/CCPA).
    /// </summary>
    public bool CaptureClientIp { get; set; }

    /// <summary>
    /// Use route template (e.g., /api/orders/{id}) instead of raw path.
    /// Prevents cardinality explosion in metrics labels.
    /// Default: true.
    /// </summary>
    public bool UseRouteTemplate { get; set; } = true;

    /// <summary>
    /// Request paths to exclude from event emission.
    /// Supports exact match and prefix match (e.g., "/health" matches "/health" and "/health/ready").
    /// Default: empty.
    /// </summary>
    public IList<string> ExcludePaths { get; set; } = [];

    /// <summary>
    /// Maximum path length before truncation. Prevents unbounded attribute sizes.
    /// Default: 256.
    /// </summary>
    public int MaxPathLength { get; set; } = 256;
}
