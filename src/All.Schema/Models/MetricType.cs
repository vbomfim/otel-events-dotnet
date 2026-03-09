namespace All.Schema.Models;

/// <summary>
/// Supported metric instrument types.
/// </summary>
public enum MetricType
{
    Counter,
    Histogram,
    Gauge
}

/// <summary>
/// Extension methods for MetricType parsing and validation.
/// </summary>
public static class MetricTypeExtensions
{
    private static readonly Dictionary<string, MetricType> MetricTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["counter"] = MetricType.Counter,
        ["histogram"] = MetricType.Histogram,
        ["gauge"] = MetricType.Gauge
    };

    /// <summary>
    /// Tries to parse a YAML metric type string into a <see cref="MetricType"/>.
    /// </summary>
    public static bool TryParseMetricType(string value, out MetricType metricType)
    {
        return MetricTypeMap.TryGetValue(value, out metricType);
    }

    /// <summary>
    /// Returns the set of valid metric type names.
    /// </summary>
    public static IReadOnlyCollection<string> ValidMetricTypeNames => MetricTypeMap.Keys;
}
