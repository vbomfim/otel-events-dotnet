namespace OtelEvents.AspNetCore.Events;

/// <summary>
/// Logger category marker for HTTP request lifecycle events.
/// Used as the type parameter for <c>ILogger&lt;OtelEventsAspNetCoreEventSource&gt;</c>
/// to provide a distinct logger category name.
/// </summary>
public sealed class OtelEventsAspNetCoreEventSource;
