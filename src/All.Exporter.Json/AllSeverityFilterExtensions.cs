using OpenTelemetry;
using OpenTelemetry.Logs;

namespace All.Exporter.Json;

/// <summary>
/// Extension methods for registering <see cref="AllSeverityFilterProcessor"/>
/// in the OTEL logging pipeline.
/// </summary>
public static class AllSeverityFilterExtensions
{
    /// <summary>
    /// Adds an <see cref="AllSeverityFilterProcessor"/> to the logging pipeline.
    /// The filter wraps a no-op inner processor by default.
    /// For production use, construct the processor directly to wrap your
    /// export processor (e.g., <see cref="SimpleExportProcessor{T}"/>).
    /// </summary>
    /// <param name="builder">The <see cref="LoggerProviderBuilder"/> to configure.</param>
    /// <param name="configure">
    /// Optional action to configure <see cref="AllSeverityFilterOptions"/>.
    /// If null, defaults are used (MinSeverity = Information).
    /// </param>
    /// <returns>The <paramref name="builder"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// // Standalone registration (no-op inner, for enrichment-only pipelines):
    /// builder.AddAllSeverityFilter(options =>
    /// {
    ///     options.MinSeverity = LogLevel.Warning;
    ///     options.EventNameOverrides["health.check.*"] = LogLevel.Debug;
    /// });
    ///
    /// // Wrapping the export processor (recommended for production):
    /// options.AddProcessor(
    ///     new AllSeverityFilterProcessor(
    ///         filterOptions,
    ///         new SimpleExportProcessor&lt;LogRecord&gt;(exporter)));
    /// </code>
    /// </example>
    public static LoggerProviderBuilder AddAllSeverityFilter(
        this LoggerProviderBuilder builder,
        Action<AllSeverityFilterOptions>? configure = null)
    {
        var options = new AllSeverityFilterOptions();
        configure?.Invoke(options);

        return builder.AddProcessor(
            new AllSeverityFilterProcessor(options, new NoOpProcessor()));
    }

    /// <summary>
    /// No-op processor used as the default inner processor when the filter
    /// is added standalone via the DI extension.
    /// </summary>
    private sealed class NoOpProcessor : BaseProcessor<LogRecord>;
}
