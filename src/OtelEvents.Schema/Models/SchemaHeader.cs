namespace OtelEvents.Schema.Models;

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

    /// <summary>
    /// Meter lifecycle mode. Defaults to <see cref="MeterLifecycle.Static"/>.
    /// When set to <see cref="MeterLifecycle.DI"/>, generates IMeterFactory-injected metrics class.
    /// </summary>
    public MeterLifecycle MeterLifecycle { get; init; } = MeterLifecycle.Static;

    /// <summary>
    /// Optional event code prefix. When set, event codes become "{Prefix}-{Id}" (e.g., "ORDER-1000").
    /// When absent, event codes are just the numeric ID as a string.
    /// </summary>
    public string? Prefix { get; init; }
}
