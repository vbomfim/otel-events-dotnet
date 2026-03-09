using Microsoft.Extensions.DependencyInjection;
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
    // ─── AddAllSeverityFilter with inner processor ───────────────────

    [Fact]
    public void AddAllSeverityFilter_WithInnerProcessor_FiltersCorrectly()
    {
        // Arrange — full OTEL pipeline with filter wrapping the exporter
        var exportedRecords = new List<LogLevel>();

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace));
        services.AddOpenTelemetry()
            .WithLogging(builder =>
            {
                var exporter = new InMemoryLogExporter(exportedRecords);
                var exportProcessor = new SimpleLogRecordExportProcessor(exporter);

                builder.AddAllSeverityFilter(
                    configure: filterOpts =>
                    {
                        filterOpts.MinSeverity = LogLevel.Warning;
                    },
                    innerProcessor: exportProcessor);
            });

        using var sp = services.BuildServiceProvider();
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

        // Act
        var logger = loggerFactory.CreateLogger("test");
        logger.LogDebug("debug");
        logger.LogInformation("info");
        logger.LogWarning("warning");
        logger.LogError("error");

        loggerFactory.Dispose();

        // Assert — only Warning and above pass through
        Assert.Equal(2, exportedRecords.Count);
    }

    [Fact]
    public void AddAllSeverityFilter_DefaultConfig_AppliesInformationThreshold()
    {
        // Arrange — pipeline with default filter options wrapping exporter
        var exportedRecords = new List<LogLevel>();

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace));
        services.AddOpenTelemetry()
            .WithLogging(builder =>
            {
                var exporter = new InMemoryLogExporter(exportedRecords);
                var exportProcessor = new SimpleLogRecordExportProcessor(exporter);

                builder.AddAllSeverityFilter(
                    configure: null,
                    innerProcessor: exportProcessor);
            });

        using var sp = services.BuildServiceProvider();
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

        // Act
        var logger = loggerFactory.CreateLogger("test");
        logger.LogTrace("trace");
        logger.LogDebug("debug");
        logger.LogInformation("info");
        logger.LogWarning("warning");

        loggerFactory.Dispose();

        // Assert — only Information and above pass through (default min)
        Assert.Equal(2, exportedRecords.Count);
    }

    [Fact]
    public void AddAllSeverityFilter_WithEventNameOverrides_FiltersPerEventName()
    {
        // Arrange — pipeline with event name overrides
        var exportedRecords = new List<LogLevel>();

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace));
        services.AddOpenTelemetry()
            .WithLogging(builder =>
            {
                var exporter = new InMemoryLogExporter(exportedRecords);
                var exportProcessor = new SimpleLogRecordExportProcessor(exporter);

                builder.AddAllSeverityFilter(
                    configure: filterOpts =>
                    {
                        filterOpts.MinSeverity = LogLevel.Warning;
                        filterOpts.EventNameOverrides["health.*"] = LogLevel.Debug;
                    },
                    innerProcessor: exportProcessor);
            });

        using var sp = services.BuildServiceProvider();
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

        // Act
        var logger = loggerFactory.CreateLogger("test");
        logger.LogDebug("plain debug");
        logger.Log(LogLevel.Debug, new EventId(0, "health.check"), "health check");
        logger.LogWarning("plain warning");

        loggerFactory.Dispose();

        // Assert — override event and warning pass
        Assert.Equal(2, exportedRecords.Count);
    }

    // ─── Null guard tests ────────────────────────────────────────────

    [Fact]
    public void AddAllSeverityFilter_NullBuilder_ThrowsArgumentNullException()
    {
        LoggerProviderBuilder? nullBuilder = null;

        Assert.Throws<ArgumentNullException>(() =>
            nullBuilder!.AddAllSeverityFilter(
                configure: opts => opts.MinSeverity = LogLevel.Warning,
                innerProcessor: new InMemoryLogRecordProcessor()));
    }

    [Fact]
    public void AddAllSeverityFilter_NullInnerProcessor_ThrowsArgumentNullException()
    {
        // Verify via constructor — the extension delegates to it
        Assert.Throws<ArgumentNullException>(() =>
            new AllSeverityFilterProcessor(
                new AllSeverityFilterOptions(),
                null!));
    }

    // ─── Direct processor construction (recommended pattern) ─────────

    [Fact]
    public void DirectConstruction_WithinPipeline_FiltersCorrectly()
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
