namespace OtelEvents.Schema.Models;

/// <summary>
/// Represents a fully parsed YAML schema document.
/// One instance per .otel.yaml file before merging.
/// </summary>
public sealed class SchemaDocument
{
    /// <summary>Schema header metadata.</summary>
    public required SchemaHeader Schema { get; init; }

    /// <summary>Import paths to other schema files.</summary>
    public List<string> Imports { get; init; } = [];

    /// <summary>Reusable field definitions (top-level "fields:" block).</summary>
    public List<FieldDefinition> Fields { get; init; } = [];

    /// <summary>Enum type definitions (top-level "enums:" block).</summary>
    public List<EnumDefinition> Enums { get; init; } = [];

    /// <summary>Event definitions (top-level "events:" block).</summary>
    public List<EventDefinition> Events { get; init; } = [];
}
