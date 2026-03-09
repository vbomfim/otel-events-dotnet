namespace All.Schema.Models;

/// <summary>
/// Defines a single observable event in the schema.
/// Events map to [LoggerMessage] source-generated methods.
/// </summary>
public sealed class EventDefinition
{
    /// <summary>Dot-namespaced event name (e.g., "http.request.received").</summary>
    public required string Name { get; init; }

    /// <summary>Unique numeric EventId for [LoggerMessage].</summary>
    public required int Id { get; init; }

    /// <summary>Log severity level.</summary>
    public required Severity Severity { get; init; }

    /// <summary>Human-readable description of the event.</summary>
    public string? Description { get; init; }

    /// <summary>Message template with {placeholder} tokens.</summary>
    public required string Message { get; init; }

    /// <summary>Whether this event accepts an Exception parameter.</summary>
    public bool Exception { get; init; }

    /// <summary>Fields carried by this event.</summary>
    public List<FieldDefinition> Fields { get; init; } = [];

    /// <summary>Metrics recorded when this event fires.</summary>
    public List<MetricDefinition> Metrics { get; init; } = [];

    /// <summary>Freeform tags for categorization.</summary>
    public List<string> Tags { get; init; } = [];
}
