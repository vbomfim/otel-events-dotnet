namespace OtelEvents.HealthChecks;

/// <summary>
/// Configuration options for the OtelEvents health check publisher.
/// Controls which events are emitted during health check poll cycles.
/// </summary>
public sealed class OtelEventsHealthCheckOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to emit <c>health.check.executed</c>
    /// events for every health check poll cycle. Default: <c>true</c>.
    /// </summary>
    public bool EmitExecutedEvents { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to emit <c>health.state.changed</c>
    /// events when a health check component's status transitions. Default: <c>true</c>.
    /// </summary>
    public bool EmitStateChangedEvents { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to emit <c>health.report.completed</c>
    /// events for each completed health report cycle. Default: <c>true</c>.
    /// </summary>
    public bool EmitReportCompletedEvents { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to suppress <c>health.check.executed</c>
    /// events for healthy checks. When <c>true</c>, only non-Healthy check events are
    /// emitted. Default: <c>false</c>.
    /// </summary>
    public bool SuppressHealthyExecutedEvents { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to enable causal scope linking
    /// for health check events. Default: <c>true</c>.
    /// </summary>
    public bool EnableCausalScope { get; set; } = true;
}
