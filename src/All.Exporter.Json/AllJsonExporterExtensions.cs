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
}
