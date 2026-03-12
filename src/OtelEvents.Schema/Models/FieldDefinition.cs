namespace OtelEvents.Schema.Models;

/// <summary>
/// Defines a field within an event or as a reusable shared definition.
/// All fields are strings — type information has been removed in favor of a simplified schema.
/// </summary>
public sealed class FieldDefinition
{
    /// <summary>Name of the field (key in the YAML fields map or list item).</summary>
    public required string Name { get; init; }

    /// <summary>Human-readable description of the field.</summary>
    public string? Description { get; init; }

    /// <summary>Whether this field is required when emitting the event.</summary>
    public bool Required { get; init; }

    /// <summary>Data sensitivity classification (defaults to Public).</summary>
    public Sensitivity Sensitivity { get; init; } = Sensitivity.Public;

    /// <summary>Maximum string length constraint.</summary>
    public int? MaxLength { get; init; }

    /// <summary>Whether this field should be indexed for querying.</summary>
    public bool Index { get; init; }

    /// <summary>Raw sensitivity string from YAML (for validation of invalid values).</summary>
    internal string? RawSensitivity { get; init; }

    /// <summary>Raw maxLength string from YAML (for validation of non-numeric values).</summary>
    internal string? RawMaxLength { get; init; }
}
