namespace All.Schema.Comparison;

/// <summary>
/// Describes a single structural change between two schema versions.
/// </summary>
public sealed class SchemaChange
{
    /// <summary>The kind of change (added, removed, changed).</summary>
    public required SchemaChangeKind Kind { get; init; }

    /// <summary>
    /// Identifies what changed. For events: event name. For fields: "eventName.fieldName".
    /// </summary>
    public required string Name { get; init; }

    /// <summary>Human-readable description of the change.</summary>
    public required string Description { get; init; }

    /// <summary>Whether this change is a breaking change.</summary>
    public bool IsBreaking => Kind is SchemaChangeKind.EventRemoved
                           or SchemaChangeKind.FieldRemoved
                           or SchemaChangeKind.FieldTypeChanged;

    public override string ToString() => $"[{(IsBreaking ? "BREAKING" : "OK")}] {Kind}: {Description}";
}
