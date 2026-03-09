using System.Diagnostics.Metrics;

namespace OtelEvents.Exporter.Json;

/// <summary>
/// Self-telemetry counters for the otel-events JSON exporter.
/// Uses OTEL's native <see cref="Meter"/> for self-monitoring.
/// </summary>
internal static class ExporterMetrics
{
    internal static readonly Meter Meter = new("OtelEvents.Exporter.Json", "1.0.0");

    internal static readonly Counter<long> ExportErrors =
        Meter.CreateCounter<long>(
            "all.exporter.json.export_errors",
            description: "Errors during JSON export");

    internal static readonly Counter<long> BatchesDropped =
        Meter.CreateCounter<long>(
            "all.exporter.json.batches_dropped",
            description: "Batches dropped due to lock timeout");

    internal static readonly Counter<long> ValuesTruncated =
        Meter.CreateCounter<long>(
            "all.exporter.json.values_truncated",
            description: "Attribute values truncated to MaxAttributeValueLength");

    internal static readonly Counter<long> AttributesRedacted =
        Meter.CreateCounter<long>(
            "all.exporter.json.attributes_redacted",
            description: "Attribute values redacted by pattern matching");

    internal static readonly Counter<long> RegexTimeouts =
        Meter.CreateCounter<long>(
            "all.exporter.json.regex_timeouts",
            description: "Regex pattern matches that timed out (value redacted as fail-closed)");

    internal static readonly Counter<long> ReservedPrefixStripped =
        Meter.CreateCounter<long>(
            "all.exporter.json.reserved_prefix_stripped",
            description: "Attributes with reserved all.* prefix stripped");
}
