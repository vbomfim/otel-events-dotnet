using OtelEvents.Schema.Models;

namespace OtelEvents.Schema.CodeGen;

/// <summary>
/// Maps schema types to C# types and YAML severity to LogLevel.
/// All fields are now strings — type mapping is simplified.
/// </summary>
public static class TypeMapper
{
    /// <summary>
    /// Returns the C# type for any field. Always returns "string" since
    /// all schema fields are string-typed.
    /// </summary>
    public static string GetFieldCSharpType(FieldDefinition field) => "string";

    /// <summary>
    /// Maps a <see cref="Severity"/> to its C# LogLevel string.
    /// </summary>
    public static string ToLogLevel(Severity severity) => severity switch
    {
        Severity.Trace => "LogLevel.Trace",
        Severity.Debug => "LogLevel.Debug",
        Severity.Info => "LogLevel.Information",
        Severity.Warn => "LogLevel.Warning",
        Severity.Error => "LogLevel.Error",
        Severity.Fatal => "LogLevel.Critical",
        _ => "LogLevel.Information"
    };

    /// <summary>
    /// Returns the CLR type parameter for a metric instrument.
    /// Counters use long, Histograms and Gauges use double.
    /// </summary>
    public static string GetMetricClrType(MetricType metricType) => metricType switch
    {
        MetricType.Counter => "long",
        MetricType.Histogram => "double",
        MetricType.Gauge => "double",
        _ => "long"
    };

    /// <summary>
    /// Returns the System.Diagnostics.Metrics instrument creation method name.
    /// </summary>
    public static string GetInstrumentCreationMethod(MetricType metricType) => metricType switch
    {
        MetricType.Counter => "CreateCounter",
        MetricType.Histogram => "CreateHistogram",
        MetricType.Gauge => "CreateCounter", // Simplified: gauge as counter for now
        _ => "CreateCounter"
    };
}
