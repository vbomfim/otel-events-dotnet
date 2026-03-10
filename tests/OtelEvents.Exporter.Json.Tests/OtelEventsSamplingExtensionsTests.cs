using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OtelEvents.Exporter.Json;

namespace OtelEvents.Exporter.Json.Tests;

/// <summary>
/// Tests for <see cref="OtelEventsSamplingExtensions"/> DI registration
/// and end-to-end pipeline sampling behavior.
/// </summary>
public sealed class OtelEventsSamplingExtensionsTests
{
    // ─── AddOtelEventsSampler with inner processor ──────────────────────────

    [Fact]
    public void AddOtelEventsSampler_Rate1_AllEventsPassThrough()
    {
        // Arrange — full OTEL pipeline with sampler wrapping the exporter
        var exportedRecords = new List<LogLevel>();

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace));
        services.AddOpenTelemetry()
            .WithLogging(builder =>
            {
                var exporter = new InMemoryLogExporter(exportedRecords);
                var exportProcessor = new SimpleLogRecordExportProcessor(exporter);

                builder.AddOtelEventsSampler(
                    configure: opts =>
                    {
                        opts.DefaultSamplingRate = 1.0;
                    },
                    innerProcessor: exportProcessor);
            });

        using var sp = services.BuildServiceProvider();
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

        // Act
        var logger = loggerFactory.CreateLogger("test");
        logger.LogInformation("msg1");
        logger.LogInformation("msg2");
        logger.LogInformation("msg3");

        loggerFactory.Dispose();

        // Assert — all pass through (rate = 1.0)
        Assert.Equal(3, exportedRecords.Count);
    }

    [Fact]
    public void AddOtelEventsSampler_Rate0_AllEventsDropped()
    {
        // Arrange
        var exportedRecords = new List<LogLevel>();

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace));
        services.AddOpenTelemetry()
            .WithLogging(builder =>
            {
                var exporter = new InMemoryLogExporter(exportedRecords);
                var exportProcessor = new SimpleLogRecordExportProcessor(exporter);

                builder.AddOtelEventsSampler(
                    configure: opts =>
                    {
                        opts.DefaultSamplingRate = 0.0;
                    },
                    innerProcessor: exportProcessor);
            });

        using var sp = services.BuildServiceProvider();
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

        // Act
        var logger = loggerFactory.CreateLogger("test");
        logger.LogInformation("msg1");
        logger.LogInformation("msg2");
        logger.LogInformation("msg3");

        loggerFactory.Dispose();

        // Assert — all dropped (rate = 0.0)
        Assert.Empty(exportedRecords);
    }

    [Fact]
    public void AddOtelEventsSampler_DefaultConfig_AllEventsPass()
    {
        // Arrange — pipeline with default options (rate = 1.0)
        var exportedRecords = new List<LogLevel>();

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace));
        services.AddOpenTelemetry()
            .WithLogging(builder =>
            {
                var exporter = new InMemoryLogExporter(exportedRecords);
                var exportProcessor = new SimpleLogRecordExportProcessor(exporter);

                builder.AddOtelEventsSampler(
                    configure: null,
                    innerProcessor: exportProcessor);
            });

        using var sp = services.BuildServiceProvider();
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

        // Act
        var logger = loggerFactory.CreateLogger("test");
        logger.LogInformation("msg1");
        logger.LogWarning("msg2");
        logger.LogError("msg3");

        loggerFactory.Dispose();

        // Assert — all pass through (default = 1.0)
        Assert.Equal(3, exportedRecords.Count);
    }

    [Fact]
    public void AddOtelEventsSampler_TailSampling_ErrorsAlwaysPass()
    {
        // Arrange — tail sampling with rate=0 but errors always sampled
        var exportedRecords = new List<LogLevel>();

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace));
        services.AddOpenTelemetry()
            .WithLogging(builder =>
            {
                var exporter = new InMemoryLogExporter(exportedRecords);
                var exportProcessor = new SimpleLogRecordExportProcessor(exporter);

                builder.AddOtelEventsSampler(
                    configure: opts =>
                    {
                        opts.Strategy = OtelEventsSamplingStrategy.Tail;
                        opts.DefaultSamplingRate = 0.0;
                        opts.AlwaysSampleErrors = true;
                    },
                    innerProcessor: exportProcessor);
            });

        using var sp = services.BuildServiceProvider();
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

        // Act
        var logger = loggerFactory.CreateLogger("test");
        logger.LogInformation("info");   // dropped
        logger.LogWarning("warning");     // dropped
        logger.LogError("error");         // sampled (error)
        logger.LogCritical("critical");   // sampled (error)

        loggerFactory.Dispose();

        // Assert — only errors pass
        Assert.Equal(2, exportedRecords.Count);
        Assert.All(exportedRecords, level =>
            Assert.True(level >= LogLevel.Error));
    }

    [Fact]
    public void AddOtelEventsSampler_WithPerEventRates_OverridesDefault()
    {
        // Arrange
        var exportedRecords = new List<LogLevel>();

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace));
        services.AddOpenTelemetry()
            .WithLogging(builder =>
            {
                var exporter = new InMemoryLogExporter(exportedRecords);
                var exportProcessor = new SimpleLogRecordExportProcessor(exporter);

                builder.AddOtelEventsSampler(
                    configure: opts =>
                    {
                        opts.DefaultSamplingRate = 1.0; // pass by default
                        opts.EventRates["blocked.event"] = 0.0; // block this one
                    },
                    innerProcessor: exportProcessor);
            });

        using var sp = services.BuildServiceProvider();
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

        // Act
        var logger = loggerFactory.CreateLogger("test");
        logger.Log(LogLevel.Information, new EventId(0, "blocked.event"), "blocked");
        logger.Log(LogLevel.Information, new EventId(0, "blocked.event"), "blocked");
        logger.Log(LogLevel.Information, new EventId(0, "allowed.event"), "allowed");

        loggerFactory.Dispose();

        // Assert — only allowed.event passes
        Assert.Single(exportedRecords);
    }

    // ─── Null guard tests ────────────────────────────────────────────

    [Fact]
    public void AddOtelEventsSampler_NullBuilder_ThrowsArgumentNullException()
    {
        LoggerProviderBuilder? nullBuilder = null;

        Assert.Throws<ArgumentNullException>(() =>
            nullBuilder!.AddOtelEventsSampler(
                configure: opts => opts.DefaultSamplingRate = 0.5,
                innerProcessor: new InMemoryLogRecordProcessor()));
    }

    [Fact]
    public void AddOtelEventsSampler_NullInnerProcessor_ThrowsArgumentNullException()
    {
        // Verify via constructor — the extension delegates to it
        Assert.Throws<ArgumentNullException>(() =>
            new OtelEventsSamplingProcessor(
                new OtelEventsSamplingOptions(),
                null!));
    }

    // ─── Direct processor construction ───────────────────────────────

    [Fact]
    public void DirectConstruction_WithinPipeline_SamplesCorrectly()
    {
        // Arrange — full OTEL pipeline with sampler wrapping the exporter
        var exportedRecords = new List<LogLevel>();

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddOpenTelemetry(options =>
            {
                var exporter = new InMemoryLogExporter(exportedRecords);
                options.AddProcessor(
                    new OtelEventsSamplingProcessor(
                        new OtelEventsSamplingOptions { DefaultSamplingRate = 0.0 },
                        new SimpleLogRecordExportProcessor(exporter)));
            });
        });

        // Act
        var logger = loggerFactory.CreateLogger("test");
        logger.LogInformation("msg1");
        logger.LogInformation("msg2");
        logger.LogInformation("msg3");

        // Assert — all dropped (rate = 0.0)
        Assert.Empty(exportedRecords);
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
