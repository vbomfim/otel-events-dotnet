using OpenTelemetry.Logs;

namespace All.Causality;

/// <summary>
/// Extension methods for registering the <see cref="AllCausalityProcessor"/>
/// in the OpenTelemetry logging pipeline.
/// </summary>
public static class AllCausalityExtensions
{
    /// <summary>
    /// Adds the <see cref="AllCausalityProcessor"/> to the OpenTelemetry logging pipeline.
    /// This processor enriches every LogRecord with <c>all.event_id</c> (UUID v7) and
    /// <c>all.parent_event_id</c> (when a causal scope is active).
    /// </summary>
    /// <param name="builder">The <see cref="LoggerProviderBuilder"/> to configure.</param>
    /// <returns>The <paramref name="builder"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Logging.AddOpenTelemetry(logging =>
    ///     logging.AddAllCausalityProcessor());
    /// </code>
    /// </example>
    public static LoggerProviderBuilder AddAllCausalityProcessor(this LoggerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddProcessor(new AllCausalityProcessor());
    }
}
