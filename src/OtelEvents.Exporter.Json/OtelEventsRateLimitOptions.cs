namespace OtelEvents.Exporter.Json;

/// <summary>
/// Configuration options for <see cref="OtelEventsRateLimitProcessor"/>.
/// Controls per-event-category rate limiting of log records.
/// </summary>
public sealed class OtelEventsRateLimitOptions
{
    /// <summary>
    /// Gets or sets the default maximum events per window for any event category.
    /// Default: <c>0</c> (unlimited — no rate limiting applied).
    /// </summary>
    /// <remarks>
    /// A value of <c>0</c> means unlimited (no rate limit).
    /// Positive values cap the number of events forwarded per <see cref="Window"/>.
    /// Per-event overrides in <see cref="EventLimits"/> take precedence over this default.
    /// </remarks>
    public int DefaultMaxEventsPerWindow { get; set; }

    /// <summary>
    /// Gets or sets per-event-name rate limits.
    /// Keys can be exact event names (e.g., <c>"db.query.executed"</c>) or
    /// wildcard patterns with a trailing <c>*</c> (e.g., <c>"db.query.*"</c>).
    /// Values are the maximum events allowed per <see cref="Window"/>.
    /// A value of <c>0</c> means unlimited for that event name.
    /// Exact matches take precedence over wildcard matches.
    /// </summary>
    /// <example>
    /// <code>
    /// options.EventLimits["db.query.*"] = 100;
    /// options.EventLimits["noisy.event"] = 10;
    /// </code>
    /// </example>
    public Dictionary<string, int> EventLimits { get; set; } = [];

    /// <summary>
    /// Gets or sets the time window for rate calculation.
    /// Default: <c>1 second</c>.
    /// </summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromSeconds(1);
}
