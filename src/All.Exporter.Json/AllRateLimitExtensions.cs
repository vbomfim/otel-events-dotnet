using OpenTelemetry;
using OpenTelemetry.Logs;

namespace All.Exporter.Json;

/// <summary>
/// Extension methods for registering <see cref="AllRateLimitProcessor"/>
/// in the OTEL logging pipeline.
/// </summary>
public static class AllRateLimitExtensions
{
    /// <summary>
    /// Adds an <see cref="AllRateLimitProcessor"/> to the logging pipeline,
    /// wrapping the specified inner processor. The rate limiter conditionally forwards
    /// log records to <paramref name="innerProcessor"/> based on per-event-category limits.
    /// </summary>
    /// <param name="builder">The <see cref="LoggerProviderBuilder"/> to configure.</param>
    /// <param name="configure">
    /// Optional action to configure <see cref="AllRateLimitOptions"/>.
    /// If null, defaults are used (no rate limiting — all events pass through).
    /// </param>
    /// <param name="innerProcessor">
    /// The downstream processor to forward passing records to (e.g., a
    /// <see cref="SimpleExportProcessor{T}"/> or <see cref="BatchExportProcessor{T}"/>).
    /// </param>
    /// <returns>The <paramref name="builder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/> or <paramref name="innerProcessor"/> is null.
    /// </exception>
    /// <example>
    /// <code>
    /// // Wrapping an export processor with rate limiting:
    /// var exporter = new AllJsonExporter(exporterOptions);
    /// var exportProcessor = new SimpleExportProcessor&lt;LogRecord&gt;(exporter);
    /// builder.AddAllRateLimiter(
    ///     configure: opts =>
    ///     {
    ///         opts.DefaultMaxEventsPerWindow = 1000;
    ///         opts.EventLimits["db.query.*"] = 100;
    ///     },
    ///     innerProcessor: exportProcessor);
    /// </code>
    /// </example>
    public static LoggerProviderBuilder AddAllRateLimiter(
        this LoggerProviderBuilder builder,
        Action<AllRateLimitOptions>? configure,
        BaseProcessor<LogRecord> innerProcessor)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(innerProcessor);

        var options = new AllRateLimitOptions();
        configure?.Invoke(options);

        return builder.AddProcessor(
            new AllRateLimitProcessor(options, innerProcessor));
    }
}
