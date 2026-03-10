using OpenTelemetry.Logs;

namespace OtelEvents.Causality;

/// <summary>
/// Extension methods for registering the <see cref="OtelEventsCausalityProcessor"/>
/// in the OpenTelemetry logging pipeline.
/// </summary>
public static class OtelEventsCausalityExtensions
{
    /// <summary>
    /// Adds the <see cref="OtelEventsCausalityProcessor"/> to the OpenTelemetry logging pipeline.
    /// This processor enriches every LogRecord with <c>otel_events.event_id</c> (UUID v7) and
    /// <c>otel_events.parent_event_id</c> (when a causal scope is active).
    /// </summary>
    /// <param name="builder">The <see cref="LoggerProviderBuilder"/> to configure.</param>
    /// <returns>The <paramref name="builder"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Logging.AddOpenTelemetry(logging =>
    ///     logging.AddOtelEventsCausalityProcessor());
    /// </code>
    /// </example>
    public static LoggerProviderBuilder AddOtelEventsCausalityProcessor(this LoggerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddProcessor(new OtelEventsCausalityProcessor());
    }
}
