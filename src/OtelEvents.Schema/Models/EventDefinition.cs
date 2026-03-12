namespace OtelEvents.Schema.Models;

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

    /// <summary>
    /// Transaction event type: start, success, failure, or event (default).
    /// Controls code generation behavior for transaction lifecycle events.
    /// </summary>
    public EventType EventType { get; init; } = EventType.Event;

    /// <summary>
    /// References another event's name (for success/failure/event types).
    /// Must reference a valid "start" event name in the schema.
    /// </summary>
    public string? ParentEvent { get; init; }

    /// <summary>
    /// The raw event type string from YAML, preserved for validation when the type is invalid.
    /// </summary>
    public string? RawEventType { get; init; }

    /// <summary>
    /// Optional per-event prefix override. When set, overrides the schema-level prefix
    /// for this event's code string. When absent, the schema-level prefix is used.
    /// </summary>
    public string? Prefix { get; init; }
}
