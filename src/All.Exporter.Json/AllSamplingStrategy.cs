namespace All.Exporter.Json;

/// <summary>
/// Defines the sampling strategy used by <see cref="AllSamplingProcessor"/>.
/// </summary>
public enum AllSamplingStrategy
{
    /// <summary>
    /// Head sampling: decides at event arrival whether to sample,
    /// based on random probability. Fast and predictable — each event
    /// is independently sampled with the configured probability.
    /// </summary>
    Head,

    /// <summary>
    /// Tail sampling: decides after inspecting event properties.
    /// Error-level events are always sampled (when <see cref="AllSamplingOptions.AlwaysSampleErrors"/>
    /// is true), while non-error events are sampled at the configured probability.
    /// </summary>
    Tail,
}
