namespace OtelEvents.Schema.Models;

/// <summary>
/// Defines a component's health policy in the schema.
/// Components aggregate signals (events) to derive health status
/// using configurable thresholds and time windows.
/// </summary>
public sealed class ComponentDefinition
{
    /// <summary>Component name (key in the YAML components map, e.g., "orders-db").</summary>
    public required string Name { get; init; }

    /// <summary>Evaluation window in seconds (parsed from duration string like "300s").</summary>
    public double WindowSeconds { get; init; }

    /// <summary>Success ratio above which the component is considered healthy (0.0–1.0).</summary>
    public double HealthyAbove { get; init; }

    /// <summary>Success ratio above which the component is considered degraded (0.0–1.0).</summary>
    public double DegradedAbove { get; init; }

    /// <summary>Minimum number of signals required before evaluating health.</summary>
    public int MinimumSignals { get; init; }

    /// <summary>Cooldown period in seconds before re-evaluating health (parsed from duration string like "30s").</summary>
    public double CooldownSeconds { get; init; }

    /// <summary>Optional response time configuration for latency-based health evaluation.</summary>
    public ResponseTimeConfig? ResponseTime { get; init; }

    /// <summary>Signal mappings that feed this component's health evaluation.</summary>
    public List<SignalMapping> Signals { get; init; } = [];
}
