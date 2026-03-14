namespace OtelEvents.Schema.Models;

/// <summary>
/// Response time thresholds for a component's health policy.
/// Defines percentile and degradation/unhealthy cutoffs.
/// </summary>
public sealed class ResponseTimeConfig
{
    /// <summary>Percentile to evaluate (e.g., 0.95 for p95).</summary>
    public double Percentile { get; init; }

    /// <summary>Response time (in milliseconds) after which the component is considered degraded.</summary>
    public double DegradedAfterMs { get; init; }

    /// <summary>Response time (in milliseconds) after which the component is considered unhealthy.</summary>
    public double UnhealthyAfterMs { get; init; }
}
