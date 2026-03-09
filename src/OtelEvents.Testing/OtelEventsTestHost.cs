using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace OtelEvents.Testing;

/// <summary>
/// Factory for creating test-configured OTEL logging pipelines with in-memory collection.
/// <para>
/// Creates an <see cref="ILoggerFactory"/> wired to an <see cref="InMemoryLogExporter"/>
/// via a <see cref="SimpleLogRecordExportProcessor"/>. All log levels are enabled by default
/// so tests can verify any severity without configuration.
/// </para>
/// </summary>
public static class OtelEventsTestHost
{
    /// <summary>
    /// Creates a test logging pipeline with an in-memory exporter.
    /// </summary>
    /// <returns>
    /// A tuple of the configured <see cref="ILoggerFactory"/> and the
    /// <see cref="InMemoryLogExporter"/> that captures all emitted records.
    /// </returns>
    /// <remarks>
    /// The caller is responsible for disposing the returned <see cref="ILoggerFactory"/>
    /// to ensure the OTEL pipeline is flushed and shut down.
    /// </remarks>
    public static (ILoggerFactory Factory, InMemoryLogExporter Exporter) Create()
    {
        var exporter = new InMemoryLogExporter();

        var factory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.ParseStateValues = true;
                options.AddProcessor(new SimpleLogRecordExportProcessor(exporter));
            });
        });

        return (factory, exporter);
    }
}
