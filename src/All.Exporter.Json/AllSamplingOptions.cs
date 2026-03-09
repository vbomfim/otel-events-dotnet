using Microsoft.Extensions.Logging;

namespace All.Exporter.Json;

/// <summary>
/// Configuration options for <see cref="AllSamplingProcessor"/>.
/// Controls probabilistic event sampling with per-event-name overrides.
/// </summary>
public sealed class AllSamplingOptions
{
    /// <summary>
    /// Gets or sets the sampling strategy. Default: <see cref="AllSamplingStrategy.Head"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="AllSamplingStrategy.Head"/>: pure probability-based sampling at event arrival.
    /// <see cref="AllSamplingStrategy.Tail"/>: error-aware sampling — errors always pass,
    /// non-errors sampled at the configured rate.
    /// </remarks>
    public AllSamplingStrategy Strategy { get; set; } = AllSamplingStrategy.Head;

    /// <summary>
    /// Gets or sets the default sampling rate as a probability between 0.0 and 1.0.
    /// Default: <c>1.0</c> (sample everything — no events dropped).
    /// </summary>
    /// <remarks>
    /// <c>0.0</c> means drop all events (except errors in tail mode).
    /// <c>1.0</c> means sample all events.
    /// <c>0.1</c> means sample approximately 10% of events.
    /// Per-event overrides in <see cref="EventRates"/> take precedence over this default.
    /// </remarks>
    public double DefaultSamplingRate { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets per-event-name sampling rate overrides.
    /// Keys can be exact event names (e.g., <c>"db.query.executed"</c>) or
    /// wildcard patterns with a trailing <c>*</c> (e.g., <c>"db.query.*"</c>).
    /// Values are probabilities between 0.0 and 1.0.
    /// Exact matches take precedence over wildcard matches.
    /// </summary>
    /// <example>
    /// <code>
    /// options.EventRates["db.query.*"] = 0.1;   // sample 10% of DB queries
    /// options.EventRates["health.check"] = 0.01; // sample 1% of health checks
    /// </code>
    /// </example>
    public Dictionary<string, double> EventRates { get; set; } = [];

    /// <summary>
    /// Gets or sets whether error-level events are always sampled regardless
    /// of the configured rate. Only applies when <see cref="Strategy"/> is
    /// <see cref="AllSamplingStrategy.Tail"/>. Default: <c>true</c>.
    /// </summary>
    public bool AlwaysSampleErrors { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum <see cref="LogLevel"/> that qualifies as an
    /// "error" for <see cref="AlwaysSampleErrors"/>. Events at or above this
    /// level bypass the sampling rate check in tail mode.
    /// Default: <see cref="LogLevel.Error"/>.
    /// </summary>
    public LogLevel ErrorMinLevel { get; set; } = LogLevel.Error;
}
