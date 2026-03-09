namespace OtelEvents.Grpc.Events;

/// <summary>
/// Logger category marker for gRPC call lifecycle events.
/// Used as the type parameter for <c>ILogger&lt;OtelEventsGrpcEventSource&gt;</c>
/// to provide a distinct logger category name.
/// </summary>
public sealed class OtelEventsGrpcEventSource;
