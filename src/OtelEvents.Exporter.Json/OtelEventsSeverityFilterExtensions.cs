using Microsoft.Extensions.Configuration;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace OtelEvents.Exporter.Json;

/// <summary>
/// Extension methods for registering <see cref="OtelEventsSeverityFilterProcessor"/>
/// in the OTEL logging pipeline.
/// </summary>
public static class OtelEventsSeverityFilterExtensions
{
    /// <summary>
    /// Adds an <see cref="OtelEventsSeverityFilterProcessor"/> to the logging pipeline,
    /// wrapping the specified inner processor. The filter conditionally forwards
    /// log records to <paramref name="innerProcessor"/> based on severity level.
    /// </summary>
    /// <param name="builder">The <see cref="LoggerProviderBuilder"/> to configure.</param>
    /// <param name="configure">
    /// Optional action to configure <see cref="OtelEventsSeverityFilterOptions"/>.
    /// If null, defaults are used (MinSeverity = Information).
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
    /// // Wrapping an export processor with severity filtering:
    /// var exporter = new OtelEventsJsonExporter(exporterOptions);
    /// var exportProcessor = new SimpleExportProcessor&lt;LogRecord&gt;(exporter);
    /// builder.AddOtelEventsSeverityFilter(
    ///     configure: opts => opts.MinSeverity = LogLevel.Warning,
    ///     innerProcessor: exportProcessor);
    /// </code>
    /// </example>
    public static LoggerProviderBuilder AddOtelEventsSeverityFilter(
        this LoggerProviderBuilder builder,
        Action<OtelEventsSeverityFilterOptions>? configure,
        BaseProcessor<LogRecord> innerProcessor)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(innerProcessor);

        var options = new OtelEventsSeverityFilterOptions();
        configure?.Invoke(options);

        return builder.AddProcessor(
            new OtelEventsSeverityFilterProcessor(options, innerProcessor));
    }

    /// <summary>
    /// Adds an <see cref="OtelEventsSeverityFilterProcessor"/> to the logging pipeline,
    /// binding filter options from the <c>OtelEvents:Filter</c> section of the provided
    /// <see cref="IConfiguration"/>.
    /// </summary>
    /// <param name="builder">The <see cref="LoggerProviderBuilder"/> to configure.</param>
    /// <param name="configuration">
    /// The configuration root (e.g., from <c>appsettings.json</c> or environment variables).
    /// Options are read from the <c>OtelEvents:Filter</c> section.
    /// </param>
    /// <param name="innerProcessor">
    /// The downstream processor to forward passing records to.
    /// </param>
    /// <returns>The <paramref name="builder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/>, <paramref name="configuration"/>,
    /// or <paramref name="innerProcessor"/> is null.
    /// </exception>
    /// <remarks>
    /// Environment variable overrides use the standard .NET double-underscore convention:
    /// <c>OTELEVENTS__Filter__MinSeverity=Warning</c> maps to <c>OtelEvents:Filter:MinSeverity</c>.
    /// </remarks>
    /// <example>
    /// <code>
    /// // In appsettings.json:
    /// // {
    /// //   "All": {
    /// //     "Filter": {
    /// //       "MinSeverity": "Warning"
    /// //     }
    /// //   }
    /// // }
    ///
    /// builder.AddOtelEventsSeverityFilter(configuration, exportProcessor);
    /// </code>
    /// </example>
    public static LoggerProviderBuilder AddOtelEventsSeverityFilter(
        this LoggerProviderBuilder builder,
        IConfiguration configuration,
        BaseProcessor<LogRecord> innerProcessor)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(innerProcessor);

        var options = new OtelEventsSeverityFilterOptions();
        configuration.GetSection("OtelEvents:Filter").Bind(options);

        return builder.AddProcessor(
            new OtelEventsSeverityFilterProcessor(options, innerProcessor));
    }
}
