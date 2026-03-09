using All.Schema.Comparison;
using All.Schema.Models;

namespace All.Schema.Tests;

/// <summary>
/// Tests for SchemaComparer — detects structural differences between two schema versions.
/// Classifies changes as breaking or non-breaking.
/// </summary>
public class SchemaComparerTests
{
    private readonly SchemaComparer _comparer = new();

    // ═══════════════════════════════════════════════════════════════
    // 1. IDENTICAL SCHEMAS
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Compare_IdenticalSchemas_ReportsNoChanges()
    {
        var schema = CreateSchema(events:
        [
            CreateEvent("http.request", 1, fields: [CreateField("method", FieldType.String)])
        ]);

        var result = _comparer.Compare(schema, schema);

        Assert.Empty(result.Changes);
        Assert.False(result.HasBreakingChanges);
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. EVENT CHANGES
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Compare_EventAdded_ReportsNonBreakingChange()
    {
        var oldSchema = CreateSchema(events:
        [
            CreateEvent("http.request", 1)
        ]);

        var newSchema = CreateSchema(events:
        [
            CreateEvent("http.request", 1),
            CreateEvent("http.response", 2)
        ]);

        var result = _comparer.Compare(oldSchema, newSchema);

        Assert.Single(result.Changes);
        var change = result.Changes[0];
        Assert.Equal(SchemaChangeKind.EventAdded, change.Kind);
        Assert.Equal("http.response", change.Name);
        Assert.False(change.IsBreaking);
    }

    [Fact]
    public void Compare_EventRemoved_ReportsBreakingChange()
    {
        var oldSchema = CreateSchema(events:
        [
            CreateEvent("http.request", 1),
            CreateEvent("http.response", 2)
        ]);

        var newSchema = CreateSchema(events:
        [
            CreateEvent("http.request", 1)
        ]);

        var result = _comparer.Compare(oldSchema, newSchema);

        Assert.Single(result.Changes);
        var change = result.Changes[0];
        Assert.Equal(SchemaChangeKind.EventRemoved, change.Kind);
        Assert.Equal("http.response", change.Name);
        Assert.True(change.IsBreaking);
    }

    [Fact]
    public void Compare_MultipleEventsAdded_ReportsAll()
    {
        var oldSchema = CreateSchema(events: []);
        var newSchema = CreateSchema(events:
        [
            CreateEvent("http.request", 1),
            CreateEvent("http.response", 2)
        ]);

        var result = _comparer.Compare(oldSchema, newSchema);

        Assert.Equal(2, result.Changes.Count);
        Assert.All(result.Changes, c => Assert.Equal(SchemaChangeKind.EventAdded, c.Kind));
        Assert.False(result.HasBreakingChanges);
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. FIELD CHANGES (within events)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Compare_FieldAdded_ReportsNonBreakingChange()
    {
        var oldSchema = CreateSchema(events:
        [
            CreateEvent("http.request", 1, fields:
            [
                CreateField("method", FieldType.String)
            ])
        ]);

        var newSchema = CreateSchema(events:
        [
            CreateEvent("http.request", 1, fields:
            [
                CreateField("method", FieldType.String),
                CreateField("path", FieldType.String)
            ])
        ]);

        var result = _comparer.Compare(oldSchema, newSchema);

        Assert.Single(result.Changes);
        var change = result.Changes[0];
        Assert.Equal(SchemaChangeKind.FieldAdded, change.Kind);
        Assert.Contains("path", change.Name);
        Assert.Contains("http.request", change.Name);
        Assert.False(change.IsBreaking);
    }

    [Fact]
    public void Compare_FieldRemoved_ReportsBreakingChange()
    {
        var oldSchema = CreateSchema(events:
        [
            CreateEvent("http.request", 1, fields:
            [
                CreateField("method", FieldType.String),
                CreateField("path", FieldType.String)
            ])
        ]);

        var newSchema = CreateSchema(events:
        [
            CreateEvent("http.request", 1, fields:
            [
                CreateField("method", FieldType.String)
            ])
        ]);

        var result = _comparer.Compare(oldSchema, newSchema);

        Assert.Single(result.Changes);
        var change = result.Changes[0];
        Assert.Equal(SchemaChangeKind.FieldRemoved, change.Kind);
        Assert.Contains("path", change.Name);
        Assert.True(change.IsBreaking);
    }

    [Fact]
    public void Compare_FieldTypeChanged_ReportsBreakingChange()
    {
        var oldSchema = CreateSchema(events:
        [
            CreateEvent("http.request", 1, fields:
            [
                CreateField("statusCode", FieldType.String)
            ])
        ]);

        var newSchema = CreateSchema(events:
        [
            CreateEvent("http.request", 1, fields:
            [
                CreateField("statusCode", FieldType.Int)
            ])
        ]);

        var result = _comparer.Compare(oldSchema, newSchema);

        Assert.Single(result.Changes);
        var change = result.Changes[0];
        Assert.Equal(SchemaChangeKind.FieldTypeChanged, change.Kind);
        Assert.Contains("statusCode", change.Name);
        Assert.True(change.IsBreaking);
    }

    // ═══════════════════════════════════════════════════════════════
    // 4. BREAKING VS NON-BREAKING CLASSIFICATION
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Compare_OnlyAdditions_HasNoBreakingChanges()
    {
        var oldSchema = CreateSchema(events:
        [
            CreateEvent("http.request", 1)
        ]);

        var newSchema = CreateSchema(events:
        [
            CreateEvent("http.request", 1, fields:
            [
                CreateField("method", FieldType.String)
            ]),
            CreateEvent("http.response", 2)
        ]);

        var result = _comparer.Compare(oldSchema, newSchema);

        Assert.False(result.HasBreakingChanges);
    }

    [Fact]
    public void Compare_MixedChanges_HasBreakingChanges()
    {
        var oldSchema = CreateSchema(events:
        [
            CreateEvent("http.request", 1, fields:
            [
                CreateField("method", FieldType.String)
            ]),
            CreateEvent("http.response", 2)
        ]);

        var newSchema = CreateSchema(events:
        [
            CreateEvent("http.request", 1, fields:
            [
                CreateField("method", FieldType.String),
                CreateField("path", FieldType.String) // added, non-breaking
            ])
            // http.response removed — breaking
        ]);

        var result = _comparer.Compare(oldSchema, newSchema);

        Assert.True(result.HasBreakingChanges);
        Assert.Contains(result.Changes, c => c.IsBreaking);
        Assert.Contains(result.Changes, c => !c.IsBreaking);
    }

    // ═══════════════════════════════════════════════════════════════
    // 5. RESULT PROPERTIES
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Compare_BreakingChanges_ReturnsCorrectCount()
    {
        var oldSchema = CreateSchema(events:
        [
            CreateEvent("http.request", 1),
            CreateEvent("http.response", 2),
            CreateEvent("http.error", 3)
        ]);

        var newSchema = CreateSchema(events:
        [
            CreateEvent("http.request", 1)
        ]);

        var result = _comparer.Compare(oldSchema, newSchema);

        Assert.Equal(2, result.BreakingChangeCount);
    }

    [Fact]
    public void Compare_ResultDescription_ContainsMeaningfulText()
    {
        var oldSchema = CreateSchema(events:
        [
            CreateEvent("http.request", 1, fields:
            [
                CreateField("method", FieldType.String)
            ])
        ]);

        var newSchema = CreateSchema(events:
        [
            CreateEvent("http.request", 1, fields:
            [
                CreateField("method", FieldType.Int) // type changed
            ])
        ]);

        var result = _comparer.Compare(oldSchema, newSchema);
        var change = result.Changes[0];

        Assert.NotEmpty(change.Description);
        Assert.Contains("string", change.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("int", change.Description, StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════
    // 6. EDGE CASES
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Compare_EmptyToPopulated_ReportsAllAsAdded()
    {
        var oldSchema = CreateSchema(events: []);
        var newSchema = CreateSchema(events:
        [
            CreateEvent("http.request", 1, fields:
            [
                CreateField("method", FieldType.String)
            ])
        ]);

        var result = _comparer.Compare(oldSchema, newSchema);

        Assert.Single(result.Changes);
        Assert.Equal(SchemaChangeKind.EventAdded, result.Changes[0].Kind);
        Assert.False(result.HasBreakingChanges);
    }

    [Fact]
    public void Compare_PopulatedToEmpty_ReportsAllAsRemoved()
    {
        var oldSchema = CreateSchema(events:
        [
            CreateEvent("http.request", 1),
            CreateEvent("http.response", 2)
        ]);
        var newSchema = CreateSchema(events: []);

        var result = _comparer.Compare(oldSchema, newSchema);

        Assert.Equal(2, result.Changes.Count);
        Assert.All(result.Changes, c => Assert.Equal(SchemaChangeKind.EventRemoved, c.Kind));
        Assert.True(result.HasBreakingChanges);
    }

    [Fact]
    public void Compare_FieldAddedAndRemoved_InSameEvent_ReportsBoth()
    {
        var oldSchema = CreateSchema(events:
        [
            CreateEvent("http.request", 1, fields:
            [
                CreateField("method", FieldType.String),
                CreateField("oldField", FieldType.String)
            ])
        ]);

        var newSchema = CreateSchema(events:
        [
            CreateEvent("http.request", 1, fields:
            [
                CreateField("method", FieldType.String),
                CreateField("newField", FieldType.String)
            ])
        ]);

        var result = _comparer.Compare(oldSchema, newSchema);

        Assert.Equal(2, result.Changes.Count);
        Assert.Contains(result.Changes, c => c.Kind == SchemaChangeKind.FieldRemoved);
        Assert.Contains(result.Changes, c => c.Kind == SchemaChangeKind.FieldAdded);
    }

    // ═══════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════

    private static SchemaDocument CreateSchema(
        List<EventDefinition>? events = null) => new()
    {
        Schema = new SchemaHeader
        {
            Name = "TestService",
            Version = "1.0.0",
            Namespace = "Test.Namespace"
        },
        Events = events ?? []
    };

    private static EventDefinition CreateEvent(
        string name,
        int id,
        List<FieldDefinition>? fields = null) => new()
    {
        Name = name,
        Id = id,
        Severity = Severity.Info,
        Message = "Test event",
        Fields = fields ?? []
    };

    private static FieldDefinition CreateField(
        string name,
        FieldType type) => new()
    {
        Name = name,
        Type = type
    };
}
