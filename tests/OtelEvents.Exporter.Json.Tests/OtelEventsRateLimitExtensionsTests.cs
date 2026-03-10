using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OtelEvents.Exporter.Json;

namespace OtelEvents.Exporter.Json.Tests;

/// <summary>
/// Tests for <see cref="OtelEventsRateLimitExtensions"/> DI registration
/// and end-to-end pipeline rate limiting behavior.
/// </summary>
public sealed class OtelEventsRateLimitExtensionsTests
{
    // ─── AddOtelEventsRateLimiter with inner processor ─────────────────────

    [Fact]
    public void AddOtelEventsRateLimiter_WithInnerProcessor_RateLimitsCorrectly()
    {
        // Arrange — full OTEL pipeline with rate limiter wrapping the exporter
        var exportedRecords = new List<LogLevel>();

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace));
        services.AddOpenTelemetry()
            .WithLogging(builder =>
            {
                var exporter = new InMemoryLogExporter(exportedRecords);
                var exportProcessor = new SimpleLogRecordExportProcessor(exporter);

                builder.AddOtelEventsRateLimiter(
                    configure: opts =>
                    {
                        opts.DefaultMaxEventsPerWindow = 2;
                    },
                    innerProcessor: exportProcessor);
            });

        using var sp = services.BuildServiceProvider();
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

        // Act — send 5 events, only 2 should pass
        var logger = loggerFactory.CreateLogger("test");
        logger.LogInformation("msg1");
        logger.LogInformation("msg2");
        logger.LogInformation("msg3");
        logger.LogInformation("msg4");
        logger.LogInformation("msg5");

        loggerFactory.Dispose();

        // Assert — only 2 pass through (rate limited)
        Assert.Equal(2, exportedRecords.Count);
    }

    [Fact]
    public void AddOtelEventsRateLimiter_DefaultConfig_NoRateLimiting()
    {
        // Arrange — pipeline with default options (unlimited)
        var exportedRecords = new List<LogLevel>();

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace));
        services.AddOpenTelemetry()
            .WithLogging(builder =>
            {
                var exporter = new InMemoryLogExporter(exportedRecords);
                var exportProcessor = new SimpleLogRecordExportProcessor(exporter);

                builder.AddOtelEventsRateLimiter(
                    configure: null,
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

        // Assert — all pass through (default = unlimited)
        Assert.Equal(3, exportedRecords.Count);
    }

    [Fact]
    public void AddOtelEventsRateLimiter_WithEventLimits_RateLimitsPerEventName()
    {
        // Arrange — pipeline with per-event limits
        var exportedRecords = new List<LogLevel>();

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace));
        services.AddOpenTelemetry()
            .WithLogging(builder =>
            {
                var exporter = new InMemoryLogExporter(exportedRecords);
                var exportProcessor = new SimpleLogRecordExportProcessor(exporter);

                builder.AddOtelEventsRateLimiter(
                    configure: opts =>
                    {
                        opts.EventLimits["limited.event"] = 1;
                    },
                    innerProcessor: exportProcessor);
            });

        using var sp = services.BuildServiceProvider();
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

        // Act
        var logger = loggerFactory.CreateLogger("test");
        logger.Log(LogLevel.Information, new EventId(0, "limited.event"), "msg1");
        logger.Log(LogLevel.Information, new EventId(0, "limited.event"), "msg2");
        logger.Log(LogLevel.Information, new EventId(0, "unlimited.event"), "msg3");

        loggerFactory.Dispose();

        // Assert — 1 from limited + 1 from unlimited = 2
        Assert.Equal(2, exportedRecords.Count);
    }

    // ─── Null guard tests ────────────────────────────────────────────

    [Fact]
    public void AddOtelEventsRateLimiter_NullBuilder_ThrowsArgumentNullException()
    {
        LoggerProviderBuilder? nullBuilder = null;

        Assert.Throws<ArgumentNullException>(() =>
            nullBuilder!.AddOtelEventsRateLimiter(
                configure: opts => opts.DefaultMaxEventsPerWindow = 10,
                innerProcessor: new InMemoryLogRecordProcessor()));
    }

    [Fact]
    public void AddOtelEventsRateLimiter_NullInnerProcessor_ThrowsArgumentNullException()
    {
        // Verify via constructor — the extension delegates to it
        Assert.Throws<ArgumentNullException>(() =>
            new OtelEventsRateLimitProcessor(
                new OtelEventsRateLimitOptions(),
                null!));
    }

    // ─── Direct processor construction ───────────────────────────────

    [Fact]
    public void DirectConstruction_WithinPipeline_RateLimitsCorrectly()
    {
        // Arrange — full OTEL pipeline with rate limiter wrapping the exporter
        var exportedRecords = new List<LogLevel>();

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddOpenTelemetry(options =>
            {
                var exporter = new InMemoryLogExporter(exportedRecords);
                options.AddProcessor(
                    new OtelEventsRateLimitProcessor(
                        new OtelEventsRateLimitOptions { DefaultMaxEventsPerWindow = 2 },
                        new SimpleLogRecordExportProcessor(exporter)));
            });
        });

        // Act
        var logger = loggerFactory.CreateLogger("test");
        logger.LogInformation("msg1");
        logger.LogInformation("msg2");
        logger.LogInformation("msg3");

        // Assert — only 2 pass through
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
