using Microsoft.Extensions.Configuration;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace OtelEvents.Exporter.Json;

/// <summary>
/// Extension methods for registering <see cref="OtelEventsJsonExporter"/> in the OTEL logging pipeline.
/// </summary>
public static class OtelEventsJsonExporterExtensions
{
    /// <summary>
    /// Adds the otel-events JSON exporter to the logging pipeline.
    /// </summary>
    /// <param name="builder">The <see cref="LoggerProviderBuilder"/> to configure.</param>
    /// <param name="configure">Optional action to configure <see cref="OtelEventsJsonExporterOptions"/>.</param>
    /// <returns>The <see cref="LoggerProviderBuilder"/> for chaining.</returns>
    public static LoggerProviderBuilder AddOtelEventsJsonExporter(
        this LoggerProviderBuilder builder,
        Action<OtelEventsJsonExporterOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new OtelEventsJsonExporterOptions();
        configure?.Invoke(options);

        var exporter = new OtelEventsJsonExporter(options);
        var processor = new SimpleLogRecordExportProcessor(exporter);

        return builder.AddProcessor(processor);
    }

    /// <summary>
    /// Adds the otel-events JSON exporter to the logging pipeline, binding options
    /// from the <c>All:Exporter</c> section of the provided <see cref="IConfiguration"/>.
    /// </summary>
    /// <param name="builder">The <see cref="LoggerProviderBuilder"/> to configure.</param>
    /// <param name="configuration">
    /// The configuration root (e.g., from <c>appsettings.json</c> or environment variables).
    /// Options are read from the <c>All:Exporter</c> section.
    /// </param>
    /// <returns>The <see cref="LoggerProviderBuilder"/> for chaining.</returns>
    /// <remarks>
    /// <para>
    /// If <c>EnvironmentProfile</c> is not explicitly set in configuration,
    /// the profile is auto-detected from the <c>ASPNETCORE_ENVIRONMENT</c> or
    /// <c>DOTNET_ENVIRONMENT</c> environment variable. If neither is set,
    /// defaults to <see cref="OtelEventsEnvironmentProfile.Production"/> (most restrictive).
    /// </para>
    /// <para>
    /// Environment variable overrides use the standard .NET double-underscore convention:
    /// <c>ALL__Exporter__Output=Stderr</c> maps to <c>All:Exporter:Output</c>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // In appsettings.json:
    /// // {
    /// //   "All": {
    /// //     "Exporter": {
    /// //       "Output": "Stdout",
    /// //       "EnvironmentProfile": "Production",
    /// //       "EmitHostInfo": false,
    /// //       "MaxAttributeValueLength": 4096,
    /// //       "SchemaVersion": "1.0.0"
    /// //     }
    /// //   }
    /// // }
    ///
    /// builder.AddOtelEventsJsonExporter(configuration);
    /// </code>
    /// </example>
    public static LoggerProviderBuilder AddOtelEventsJsonExporter(
        this LoggerProviderBuilder builder,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection("All:Exporter");
        var options = new OtelEventsJsonExporterOptions();
        section.Bind(options);

        // Auto-detect EnvironmentProfile if not explicitly configured
        if (section["EnvironmentProfile"] is null)
        {
            options.EnvironmentProfile = EnvironmentProfileDetector.Detect();
        }

        var exporter = new OtelEventsJsonExporter(options);
        var processor = new SimpleLogRecordExportProcessor(exporter);

        return builder.AddProcessor(processor);
    }
}
