namespace OtelEvents.Schema.Models;

/// <summary>
/// Defines a metric instrument associated with an event.
/// Metrics are recorded whenever the event is emitted.
/// </summary>
public sealed class MetricDefinition
{
    /// <summary>Name of the metric (key in the YAML metrics map).</summary>
    public required string Name { get; init; }

    /// <summary>The instrument type (counter, histogram, gauge).</summary>
    public required MetricType Type { get; init; }

    /// <summary>Unit of measure for the metric.</summary>
    public string? Unit { get; init; }

    /// <summary>Human-readable description of the metric.</summary>
    public string? Description { get; init; }

    /// <summary>Histogram bucket boundaries (only for histogram type).</summary>
    public List<double>? Buckets { get; init; }

    /// <summary>Field names used as metric labels/dimensions.</summary>
    public List<string>? Labels { get; init; }

    /// <summary>Raw metric type string from YAML (for validation of invalid types).</summary>
    internal string? RawType { get; init; }
}
