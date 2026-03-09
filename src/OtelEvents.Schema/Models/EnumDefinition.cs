namespace OtelEvents.Schema.Models;

/// <summary>
/// Defines a reusable enum type in the schema.
/// Enums are serialized as string names in the JSON output.
/// </summary>
public sealed class EnumDefinition
{
    /// <summary>Name of the enum type (key in the YAML enums map).</summary>
    public required string Name { get; init; }

    /// <summary>Human-readable description of the enum.</summary>
    public string? Description { get; init; }

    /// <summary>The set of valid values for this enum. Must not be empty.</summary>
    public required List<string> Values { get; init; }
}
