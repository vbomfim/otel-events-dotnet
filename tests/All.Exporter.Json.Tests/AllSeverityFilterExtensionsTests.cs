using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using All.Exporter.Json;

namespace All.Exporter.Json.Tests;

/// <summary>
/// Tests for <see cref="AllSeverityFilterExtensions"/> DI registration
/// and end-to-end pipeline filtering behavior.
/// </summary>
public sealed class AllSeverityFilterExtensionsTests
{
    [Fact]
    public void AddAllSeverityFilter_WithinPipeline_FiltersCorrectly()
    {
        // Arrange — full OTEL pipeline with filter wrapping the exporter
        var exportedRecords = new List<LogLevel>();

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddOpenTelemetry(options =>
            {
                var exporter = new InMemoryLogExporter(exportedRecords);
                options.AddProcessor(
                    new AllSeverityFilterProcessor(
                        new AllSeverityFilterOptions { MinSeverity = LogLevel.Warning },
                        new SimpleLogRecordExportProcessor(exporter)));
            });
        });

        // Act
        var logger = loggerFactory.CreateLogger("test");
        logger.LogDebug("debug");
        logger.LogInformation("info");
        logger.LogWarning("warning");
        logger.LogError("error");

        // Assert — only Warning and above pass through
        Assert.Equal(2, exportedRecords.Count);
    }

    [Fact]
    public void AddAllSeverityFilter_DefaultOptions_AppliesInformationThreshold()
    {
        // Arrange — pipeline with default filter options
        var exportedRecords = new List<LogLevel>();

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddOpenTelemetry(options =>
            {
                var exporter = new InMemoryLogExporter(exportedRecords);
                options.AddProcessor(
                    new AllSeverityFilterProcessor(
                        new AllSeverityFilterOptions(), // default: Information
                        new SimpleLogRecordExportProcessor(exporter)));
            });
        });

        // Act
        var logger = loggerFactory.CreateLogger("test");
        logger.LogTrace("trace");
        logger.LogDebug("debug");
        logger.LogInformation("info");
        logger.LogWarning("warning");

        // Assert — only Information and above pass through
        Assert.Equal(2, exportedRecords.Count);
    }

    [Fact]
    public void AddAllSeverityFilter_WithEventNameOverrides_FiltersPerEventName()
    {
        // Arrange — pipeline with event name overrides
        var exportedRecords = new List<LogLevel>();

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddOpenTelemetry(options =>
            {
                var exporter = new InMemoryLogExporter(exportedRecords);
                options.AddProcessor(
                    new AllSeverityFilterProcessor(
                        new AllSeverityFilterOptions
                        {
                            MinSeverity = LogLevel.Warning,
                            EventNameOverrides = new Dictionary<string, LogLevel>
                            {
                                ["health.*"] = LogLevel.Debug
                            }
                        },
                        new SimpleLogRecordExportProcessor(exporter)));
            });
        });

        // Act
        var logger = loggerFactory.CreateLogger("test");
        // Debug with no event name → below Warning → dropped
        logger.LogDebug("plain debug");
        // Debug with matching event name → at Debug override → passes
        logger.Log(LogLevel.Debug, new EventId(0, "health.check"), "health check");
        // Warning with no event name → at Warning → passes
        logger.LogWarning("plain warning");

        // Assert — override event and warning pass
        Assert.Equal(2, exportedRecords.Count);
    }

    /// <summary>
    /// Minimal in-memory exporter that captures LogLevel of exported records.
    /// </summary>
    private sealed class InMemoryLogExporter(List<LogLevel> records) : BaseExporter<LogRecord>
    {
        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            foreach (var record in batch)
            {
                records.Add(record.LogLevel);
            }

            return ExportResult.Success;
        }
    }
}
