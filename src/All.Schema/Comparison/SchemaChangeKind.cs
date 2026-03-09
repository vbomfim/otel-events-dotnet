namespace All.Schema.Comparison;

/// <summary>
/// The kind of structural change between two schema versions.
/// </summary>
public enum SchemaChangeKind
{
    /// <summary>A new event was added.</summary>
    EventAdded,

    /// <summary>An existing event was removed.</summary>
    EventRemoved,

    /// <summary>A new field was added to an event.</summary>
    FieldAdded,

    /// <summary>An existing field was removed from an event.</summary>
    FieldRemoved,

    /// <summary>A field's type was changed.</summary>
    FieldTypeChanged
}
