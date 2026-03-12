namespace OtelEvents.Schema.Models;

/// <summary>
/// Typed transaction event types.
/// Controls code generation behavior for transaction lifecycle events.
/// </summary>
public enum EventType
{
    /// <summary>Just an event — no transaction scope effect (default).</summary>
    Event,

    /// <summary>Creates a causal scope + starts timer. Generated code returns a transaction handle.</summary>
    Start,

    /// <summary>Closes parent scope as success, records duration.</summary>
    Success,

    /// <summary>Closes parent scope as failure, records duration.</summary>
    Failure
}

/// <summary>
/// Extension methods for EventType parsing and validation.
/// </summary>
public static class EventTypeExtensions
{
    private static readonly Dictionary<string, EventType> EventTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["event"] = EventType.Event,
        ["start"] = EventType.Start,
        ["success"] = EventType.Success,
        ["failure"] = EventType.Failure
    };

    /// <summary>
    /// Tries to parse a YAML event type string into an <see cref="EventType"/>.
    /// </summary>
    public static bool TryParseEventType(string value, out EventType eventType)
    {
        return EventTypeMap.TryGetValue(value, out eventType);
    }

    /// <summary>
    /// Returns the set of valid event type names.
    /// </summary>
    public static IReadOnlyCollection<string> ValidEventTypeNames => EventTypeMap.Keys;
}
