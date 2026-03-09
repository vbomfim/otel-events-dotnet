using All.Schema.Models;

namespace All.Schema.CodeGen;

/// <summary>
/// Maps YAML schema types to C# types and YAML severity to LogLevel.
/// </summary>
public static class TypeMapper
{
    /// <summary>
    /// Maps a <see cref="FieldType"/> to its C# type name.
    /// </summary>
    public static string ToCSharpType(FieldType fieldType) => fieldType switch
    {
        FieldType.String => "string",
        FieldType.Int => "int",
        FieldType.Long => "long",
        FieldType.Double => "double",
        FieldType.Bool => "bool",
        FieldType.DateTime => "DateTimeOffset",
        FieldType.Duration => "TimeSpan",
        FieldType.Guid => "Guid",
        FieldType.StringArray => "string[]",
        FieldType.IntArray => "int[]",
        FieldType.Map => "Dictionary<string, string>",
        FieldType.Enum => "string", // Resolved separately via GetFieldCSharpType
        _ => "object"
    };

    /// <summary>
    /// Gets the C# type for a field, handling enum references and inline enums.
    /// </summary>
    public static string GetFieldCSharpType(FieldDefinition field)
    {
        if (field.Type == FieldType.Enum)
        {
            if (field.Ref is not null)
                return NamingHelper.ToPascalCase(field.Ref);

            if (field.Values is { Count: > 0 })
                return NamingHelper.ToPascalCase(field.Name);

            return "string";
        }

        if (field.Type is not null)
            return ToCSharpType(field.Type.Value);

        return "object";
    }

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

    /// <summary>
    /// Checks whether a field type is numeric (can be recorded in a Histogram).
    /// </summary>
    public static bool IsNumericType(FieldType fieldType) => fieldType switch
    {
        FieldType.Int => true,
        FieldType.Long => true,
        FieldType.Double => true,
        _ => false
    };
}
