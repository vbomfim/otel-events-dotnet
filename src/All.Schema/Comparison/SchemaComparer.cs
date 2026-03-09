using All.Schema.Models;

namespace All.Schema.Comparison;

/// <summary>
/// Compares two <see cref="SchemaDocument"/> instances and reports structural differences.
/// Classifies each change as breaking or non-breaking.
/// </summary>
/// <remarks>
/// Breaking changes: removing an event, removing a field, changing a field type.
/// Non-breaking changes: adding an event, adding a field.
/// </remarks>
public sealed class SchemaComparer
{
    /// <summary>
    /// Compares an old schema to a new schema and returns all structural differences.
    /// </summary>
    /// <param name="oldSchema">The previous schema version.</param>
    /// <param name="newSchema">The new schema version.</param>
    /// <returns>A result containing all detected changes.</returns>
    public SchemaComparisonResult Compare(SchemaDocument oldSchema, SchemaDocument newSchema)
    {
        var changes = new List<SchemaChange>();

        CompareEvents(oldSchema.Events, newSchema.Events, changes);

        return new SchemaComparisonResult(changes);
    }

    private static void CompareEvents(
        List<EventDefinition> oldEvents,
        List<EventDefinition> newEvents,
        List<SchemaChange> changes)
    {
        var oldByName = oldEvents.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
        var newByName = newEvents.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

        // Events removed (breaking)
        foreach (var oldEvt in oldEvents)
        {
            if (!newByName.ContainsKey(oldEvt.Name))
            {
                changes.Add(new SchemaChange
                {
                    Kind = SchemaChangeKind.EventRemoved,
                    Name = oldEvt.Name,
                    Description = $"Event '{oldEvt.Name}' was removed."
                });
            }
        }

        // Events added (non-breaking)
        foreach (var newEvt in newEvents)
        {
            if (!oldByName.ContainsKey(newEvt.Name))
            {
                changes.Add(new SchemaChange
                {
                    Kind = SchemaChangeKind.EventAdded,
                    Name = newEvt.Name,
                    Description = $"Event '{newEvt.Name}' was added."
                });
            }
        }

        // Events in both — compare fields
        foreach (var oldEvt in oldEvents)
        {
            if (newByName.TryGetValue(oldEvt.Name, out var newEvt))
            {
                CompareFields(oldEvt.Name, oldEvt.Fields, newEvt.Fields, changes);
            }
        }
    }

    private static void CompareFields(
        string eventName,
        List<FieldDefinition> oldFields,
        List<FieldDefinition> newFields,
        List<SchemaChange> changes)
    {
        var oldByName = oldFields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
        var newByName = newFields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);

        // Fields removed (breaking)
        foreach (var oldField in oldFields)
        {
            if (!newByName.ContainsKey(oldField.Name))
            {
                changes.Add(new SchemaChange
                {
                    Kind = SchemaChangeKind.FieldRemoved,
                    Name = $"{eventName}.{oldField.Name}",
                    Description = $"Field '{oldField.Name}' was removed from event '{eventName}'."
                });
            }
        }

        // Fields added (non-breaking)
        foreach (var newField in newFields)
        {
            if (!oldByName.ContainsKey(newField.Name))
            {
                changes.Add(new SchemaChange
                {
                    Kind = SchemaChangeKind.FieldAdded,
                    Name = $"{eventName}.{newField.Name}",
                    Description = $"Field '{newField.Name}' was added to event '{eventName}'."
                });
            }
        }

        // Fields in both — check type changes
        foreach (var oldField in oldFields)
        {
            if (newByName.TryGetValue(oldField.Name, out var newField))
            {
                if (oldField.Type != newField.Type)
                {
                    changes.Add(new SchemaChange
                    {
                        Kind = SchemaChangeKind.FieldTypeChanged,
                        Name = $"{eventName}.{oldField.Name}",
                        Description = $"Field '{oldField.Name}' in event '{eventName}' changed type from '{oldField.Type}' to '{newField.Type}'."
                    });
                }
            }
        }
    }
}
