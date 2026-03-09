using Microsoft.Extensions.Configuration;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace All.Exporter.Json;

/// <summary>
/// Extension methods for registering <see cref="AllJsonExporter"/> in the OTEL logging pipeline.
/// </summary>
public static class AllJsonExporterExtensions
{
    /// <summary>
    /// Adds the ALL JSON exporter to the logging pipeline.
    /// </summary>
    /// <param name="builder">The <see cref="LoggerProviderBuilder"/> to configure.</param>
    /// <param name="configure">Optional action to configure <see cref="AllJsonExporterOptions"/>.</param>
    /// <returns>The <see cref="LoggerProviderBuilder"/> for chaining.</returns>
    public static LoggerProviderBuilder AddAllJsonExporter(
        this LoggerProviderBuilder builder,
        Action<AllJsonExporterOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new AllJsonExporterOptions();
        configure?.Invoke(options);

        var exporter = new AllJsonExporter(options);
        var processor = new SimpleLogRecordExportProcessor(exporter);

        return builder.AddProcessor(processor);
    }

    /// <summary>
    /// Adds the ALL JSON exporter to the logging pipeline, binding options
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
    /// defaults to <see cref="AllEnvironmentProfile.Production"/> (most restrictive).
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
    /// builder.AddAllJsonExporter(configuration);
    /// </code>
    /// </example>
    public static LoggerProviderBuilder AddAllJsonExporter(
        this LoggerProviderBuilder builder,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection("All:Exporter");
        var options = new AllJsonExporterOptions();
        section.Bind(options);

        // Auto-detect EnvironmentProfile if not explicitly configured
        if (section["EnvironmentProfile"] is null)
        {
            options.EnvironmentProfile = EnvironmentProfileDetector.Detect();
        }

        var exporter = new AllJsonExporter(options);
        var processor = new SimpleLogRecordExportProcessor(exporter);

        return builder.AddProcessor(processor);
    }
}
