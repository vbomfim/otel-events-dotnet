namespace OtelEvents.Grpc;

/// <summary>
/// Configuration for the OtelEvents.Grpc integration pack.
/// Controls which gRPC lifecycle events are emitted and what data they capture.
/// </summary>
public sealed class OtelEventsGrpcOptions
{
    /// <summary>
    /// Enable causal scope per gRPC call. When true, all events emitted during
    /// call processing share a parentEventId pointing to the grpc.call.started event.
    /// Default: true (requires All.Causality to be referenced; no-op otherwise).
    /// </summary>
    public bool EnableCausalScope { get; set; } = true;

    /// <summary>
    /// Enable the server-side gRPC interceptor.
    /// Default: true.
    /// </summary>
    public bool EnableServerInterceptor { get; set; } = true;

    /// <summary>
    /// Enable the client-side gRPC interceptor.
    /// Default: true.
    /// </summary>
    public bool EnableClientInterceptor { get; set; } = true;

    /// <summary>
    /// Capture serialized message size in request/response events.
    /// Default: true.
    /// </summary>
    public bool CaptureMessageSize { get; set; } = true;

    /// <summary>
    /// gRPC service names to exclude from event emission.
    /// Uses exact match on the fully qualified service name (e.g., "grpc.health.v1.Health").
    /// Default: empty.
    /// </summary>
    public IList<string> ExcludeServices { get; set; } = [];

    /// <summary>
    /// Fully qualified gRPC method paths to exclude from event emission.
    /// Format: "/package.ServiceName/MethodName".
    /// Default: empty.
    /// </summary>
    public IList<string> ExcludeMethods { get; set; } = [];

    /// <summary>
    /// Capture gRPC metadata (headers) in events.
    /// Default: false — opt-in only to avoid capturing sensitive headers.
    /// </summary>
    public bool CaptureMetadata { get; set; }
}
