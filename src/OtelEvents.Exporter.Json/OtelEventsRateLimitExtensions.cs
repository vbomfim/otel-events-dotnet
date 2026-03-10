using OpenTelemetry;
using OpenTelemetry.Logs;

namespace OtelEvents.Exporter.Json;

/// <summary>
/// Extension methods for registering <see cref="OtelEventsRateLimitProcessor"/>
/// in the OTEL logging pipeline.
/// </summary>
public static class OtelEventsRateLimitExtensions
{
    /// <summary>
    /// Adds an <see cref="OtelEventsRateLimitProcessor"/> to the logging pipeline,
    /// wrapping the specified inner processor. The rate limiter conditionally forwards
    /// log records to <paramref name="innerProcessor"/> based on per-event-category limits.
    /// </summary>
    /// <param name="builder">The <see cref="LoggerProviderBuilder"/> to configure.</param>
    /// <param name="configure">
    /// Optional action to configure <see cref="OtelEventsRateLimitOptions"/>.
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
    /// var exporter = new OtelEventsJsonExporter(exporterOptions);
    /// var exportProcessor = new SimpleExportProcessor&lt;LogRecord&gt;(exporter);
    /// builder.AddOtelEventsRateLimiter(
    ///     configure: opts =>
    ///     {
    ///         opts.DefaultMaxEventsPerWindow = 1000;
    ///         opts.EventLimits["db.query.*"] = 100;
    ///     },
    ///     innerProcessor: exportProcessor);
    /// </code>
    /// </example>
    public static LoggerProviderBuilder AddOtelEventsRateLimiter(
        this LoggerProviderBuilder builder,
        Action<OtelEventsRateLimitOptions>? configure,
        BaseProcessor<LogRecord> innerProcessor)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(innerProcessor);

        var options = new OtelEventsRateLimitOptions();
        configure?.Invoke(options);

        return builder.AddProcessor(
            new OtelEventsRateLimitProcessor(options, innerProcessor));
    }
}
