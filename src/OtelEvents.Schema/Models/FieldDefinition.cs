namespace OtelEvents.Schema.Models;

/// <summary>
/// Defines a field within an event or as a reusable shared definition.
/// Fields carry the structured data attached to each event occurrence.
/// </summary>
public sealed class FieldDefinition
{
    /// <summary>Name of the field (key in the YAML fields map).</summary>
    public required string Name { get; init; }

    /// <summary>The data type of this field. Null when using a ref.</summary>
    public FieldType? Type { get; init; }

    /// <summary>Human-readable description of the field.</summary>
    public string? Description { get; init; }

    /// <summary>Whether this field is required when emitting the event.</summary>
    public bool Required { get; init; }

    /// <summary>Data sensitivity classification (defaults to Public).</summary>
    public Sensitivity Sensitivity { get; init; } = Sensitivity.Public;

    /// <summary>Maximum string length. Only valid for string-typed fields.</summary>
    public int? MaxLength { get; init; }

    /// <summary>Whether this field should be indexed for querying.</summary>
    public bool Index { get; init; }

    /// <summary>Reference to a reusable field or enum definition.</summary>
    public string? Ref { get; init; }

    /// <summary>Unit of measure (e.g., "ms", "bytes").</summary>
    public string? Unit { get; init; }

    /// <summary>Example values for documentation.</summary>
    public List<string>? Examples { get; init; }

    /// <summary>Inline enum values when type is enum.</summary>
    public List<string>? Values { get; init; }

    /// <summary>Raw type string from YAML (for validation of invalid types).</summary>
    internal string? RawType { get; init; }

    /// <summary>Raw sensitivity string from YAML (for validation of invalid values).</summary>
    internal string? RawSensitivity { get; init; }

    /// <summary>Raw maxLength string from YAML (for validation of non-numeric values).</summary>
    internal string? RawMaxLength { get; init; }
}
