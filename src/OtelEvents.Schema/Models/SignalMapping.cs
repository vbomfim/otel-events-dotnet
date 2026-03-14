namespace OtelEvents.Schema.Models;

/// <summary>
/// Maps a signal (event) to a component's health evaluation.
/// Each signal references an event name and optional match filters.
/// </summary>
public sealed class SignalMapping
{
    /// <summary>Event name that this signal maps to (e.g., "http.request.failed").</summary>
    public required string Event { get; init; }

    /// <summary>Optional match filters as key-value pairs (e.g., httpRoute → "/api/orders/*").</summary>
    public Dictionary<string, string> Match { get; init; } = [];
}
