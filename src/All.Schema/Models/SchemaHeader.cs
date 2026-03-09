namespace All.Schema.Models;

/// <summary>
/// Schema header metadata (the "schema:" block in YAML).
/// </summary>
public sealed class SchemaHeader
{
    /// <summary>Schema name (required).</summary>
    public required string Name { get; init; }

    /// <summary>Semver version string (required).</summary>
    public required string Version { get; init; }

    /// <summary>C# namespace for generated code (required).</summary>
    public required string Namespace { get; init; }

    /// <summary>Human-readable description (optional).</summary>
    public string? Description { get; init; }

    /// <summary>OTEL Meter name. Defaults to Namespace if not specified.</summary>
    public string? MeterName { get; init; }
}
