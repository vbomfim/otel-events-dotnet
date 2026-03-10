namespace OtelEvents.Azure.CosmosDb;

/// <summary>
/// Configuration options for the OtelEvents.Azure.CosmosDb integration pack.
/// Controls which CosmosDB operation events are emitted and what data they capture.
/// </summary>
/// <remarks>
/// <para>
/// PII-sensitive fields (query text) default to <c>false</c> to comply with
/// security best practices. Query text is always sanitized (string literals
/// replaced with ? placeholders) even when capture is enabled.
/// </para>
/// <para>
/// Threshold options allow filtering to only emit events for expensive or slow
/// operations, reducing telemetry volume in high-throughput scenarios.
/// </para>
/// </remarks>
public sealed class OtelEventsCosmosDbOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to capture SQL query text
    /// in <c>cosmosdb.query.executed</c> events.
    /// When enabled, string literals are replaced with <c>?</c> placeholders
    /// to prevent PII leakage.
    /// Default: <c>false</c> — opt-in only (sensitivity: internal).
    /// </summary>
    public bool CaptureQueryText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to enable causal scope linking
    /// for CosmosDB operation events. When <c>true</c>, all events within a
    /// CosmosDB operation share a parentEventId.
    /// Default: <c>true</c>.
    /// </summary>
    public bool EnableCausalScope { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to capture the CosmosDB region
    /// that served each request.
    /// Default: <c>true</c>.
    /// </summary>
    public bool CaptureRegion { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum Request Units (RU) threshold for emitting events.
    /// When set to a value greater than 0, only operations consuming at least this
    /// many RUs will be logged. Useful for reducing telemetry volume.
    /// Default: <c>0</c> — emit for all operations.
    /// </summary>
    public double RuThreshold { get; set; }

    /// <summary>
    /// Gets or sets the minimum duration threshold (in milliseconds) for emitting events.
    /// When set to a value greater than 0, only operations taking at least this long
    /// will be logged. Useful for focusing on slow operations.
    /// Default: <c>0</c> — emit for all operations.
    /// </summary>
    public double LatencyThresholdMs { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to emit infrastructure events
    /// (<c>cosmosdb.connection.failed</c>, <c>cosmosdb.auth.failed</c>,
    /// <c>cosmosdb.throttled</c>) in addition to the standard operation events.
    /// Infrastructure events fire supplementally — the existing
    /// <c>cosmosdb.query.failed</c> event still fires for all failures.
    /// Default: <c>false</c> — opt-in only (backward-compatible).
    /// </summary>
    public bool EmitInfrastructureEvents { get; set; }
}
