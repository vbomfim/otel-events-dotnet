namespace OtelEvents.Sample.WebApi.Events;

/// <summary>
/// Logger category marker for order lifecycle events.
/// Used as the type parameter for <c>ILogger&lt;OrderEventSource&gt;</c>
/// to provide a distinct logger category name.
/// </summary>
public sealed class OrderEventSource;
