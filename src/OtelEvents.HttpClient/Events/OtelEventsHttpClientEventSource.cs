namespace OtelEvents.HttpClient.Events;

/// <summary>
/// Logger category marker for outbound HTTP call lifecycle events.
/// Used as the type parameter for <c>ILogger&lt;OtelEventsHttpClientEventSource&gt;</c>
/// to provide a stable, distinct logger category name.
/// </summary>
/// <remarks>
/// Follows the same pattern as <c>OtelEventsAspNetCoreEventSource</c> in the ASP.NET Core pack.
/// </remarks>
public sealed class OtelEventsHttpClientEventSource;
