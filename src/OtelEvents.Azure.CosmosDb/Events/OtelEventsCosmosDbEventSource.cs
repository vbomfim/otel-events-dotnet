namespace OtelEvents.Azure.CosmosDb.Events;

/// <summary>
/// Logger category marker for CosmosDB operation lifecycle events.
/// Used as the type parameter for <c>ILogger&lt;OtelEventsCosmosDbEventSource&gt;</c>
/// to provide a distinct logger category name.
/// </summary>
public sealed class OtelEventsCosmosDbEventSource;
