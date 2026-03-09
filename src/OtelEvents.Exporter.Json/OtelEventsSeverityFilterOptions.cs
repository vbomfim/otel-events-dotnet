using Microsoft.Extensions.Logging;

namespace OtelEvents.Exporter.Json;

/// <summary>
/// Configuration options for <see cref="OtelEventsSeverityFilterProcessor"/>.
/// Controls which log events are forwarded based on severity level.
/// </summary>
public sealed class OtelEventsSeverityFilterOptions
{
    /// <summary>
    /// Gets or sets the minimum severity level for events to pass through.
    /// Events below this level are dropped. Default: <see cref="LogLevel.Information"/>.
    /// </summary>
    /// <remarks>
    /// Maps to OTEL severity numbers:
    /// Trace(1-4), Debug(5-8), Info(9-12), Warn(13-16), Error(17-20), Fatal(21-24).
    /// </remarks>
    public LogLevel MinSeverity { get; set; } = LogLevel.Information;

    /// <summary>
    /// Gets or sets per-event-name severity overrides.
    /// Keys can be exact event names (e.g., "health.check.executed") or
    /// wildcard patterns with a trailing <c>*</c> (e.g., "health.check.*").
    /// Exact matches take precedence over wildcard matches.
    /// </summary>
    /// <example>
    /// <code>
    /// options.EventNameOverrides["health.check.*"] = LogLevel.Debug;
    /// options.EventNameOverrides["noisy.event"] = LogLevel.Error;
    /// </code>
    /// </example>
    public Dictionary<string, LogLevel> EventNameOverrides { get; set; } = [];
}
