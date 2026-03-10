namespace OtelEvents.Azure.Storage;

/// <summary>
/// Configuration for the OtelEvents.Azure.Storage integration pack.
/// Controls which storage events are emitted and what operations are excluded.
/// </summary>
public sealed class OtelEventsAzureStorageOptions
{
    /// <summary>
    /// Enable blob storage event emission (uploaded, downloaded, deleted, failed).
    /// Default: true.
    /// </summary>
    public bool EnableBlobEvents { get; set; } = true;

    /// <summary>
    /// Enable queue storage event emission (sent, received, failed).
    /// Default: true.
    /// </summary>
    public bool EnableQueueEvents { get; set; } = true;

    /// <summary>
    /// Enable causal scope per storage operation. When true, events emitted during
    /// the operation share a parentEventId for causal linking.
    /// Default: true (requires OtelEvents.Causality; no-op otherwise).
    /// </summary>
    public bool EnableCausalScope { get; set; } = true;

    /// <summary>
    /// Blob container names to exclude from event emission.
    /// Exact match, case-insensitive.
    /// Default: empty.
    /// </summary>
    public IList<string> ExcludeContainers { get; set; } = [];

    /// <summary>
    /// Queue names to exclude from event emission.
    /// Exact match, case-insensitive.
    /// Default: empty.
    /// </summary>
    public IList<string> ExcludeQueues { get; set; } = [];

    /// <summary>
    /// Enable infrastructure event emission (connection.failed, auth.failed, throttled).
    /// These events provide deeper diagnostics for Azure Storage failures
    /// by classifying <c>RequestFailedException</c> into connection, authentication,
    /// and throttling categories.
    /// Default: true.
    /// </summary>
    public bool EmitInfrastructureEvents { get; set; } = true;
}
