namespace OtelEvents.Azure.Storage.Events;

/// <summary>
/// Logger category marker for Azure Storage events.
/// Used as the type parameter for <c>ILogger&lt;OtelEventsStorageEventSource&gt;</c>
/// to provide a distinct logger category name.
/// </summary>
public sealed class OtelEventsStorageEventSource;
