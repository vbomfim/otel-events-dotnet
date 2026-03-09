namespace OtelEvents.Exporter.Json;

/// <summary>
/// Defines the sampling strategy used by <see cref="OtelEventsSamplingProcessor"/>.
/// </summary>
public enum OtelEventsSamplingStrategy
{
    /// <summary>
    /// Head sampling: decides at event arrival whether to sample,
    /// based on random probability. Fast and predictable — each event
    /// is independently sampled with the configured probability.
    /// </summary>
    Head,

    /// <summary>
    /// Tail sampling: decides after inspecting event properties.
    /// Error-level events are always sampled (when <see cref="OtelEventsSamplingOptions.AlwaysSampleErrors"/>
    /// is true), while non-error events are sampled at the configured probability.
    /// </summary>
    Tail,
}
