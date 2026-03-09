using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using All.Exporter.Json;

namespace All.Exporter.Json.Tests;

/// <summary>
/// Tests for <see cref="AllSeverityFilterProcessor"/>: severity-based log record filtering.
/// </summary>
public sealed class AllSeverityFilterProcessorTests : IDisposable
{
    private readonly InMemoryLogRecordProcessor _innerProcessor = new();
    private readonly MeterListener _meterListener = new();
    private long _droppedCount;

    public AllSeverityFilterProcessorTests()
    {
        _meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == "all.processor.severity_filter.events_dropped")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            Interlocked.Add(ref _droppedCount, measurement);
        });
        _meterListener.Start();
    }

    public void Dispose()
    {
        _meterListener.Dispose();
        _innerProcessor.Dispose();
    }

    // ─── Default behavior ─────────────────────────────────────────────

    [Fact]
    public void DefaultOptions_MinSeverityIsInformation()
    {
        var options = new AllSeverityFilterOptions();

        Assert.Equal(LogLevel.Information, options.MinSeverity);
    }

    [Fact]
    public void DefaultOptions_EventNameOverridesIsEmpty()
    {
        var options = new AllSeverityFilterOptions();

        Assert.NotNull(options.EventNameOverrides);
        Assert.Empty(options.EventNameOverrides);
    }

    // ─── Events at/above minimum severity pass through ────────────────

    [Theory]
    [InlineData(LogLevel.Information)]  // exactly at threshold
    [InlineData(LogLevel.Warning)]      // above threshold
    [InlineData(LogLevel.Error)]        // well above threshold
    [InlineData(LogLevel.Critical)]     // maximum severity
    public void EventAtOrAboveMinSeverity_PassesThrough(LogLevel logLevel)
    {
        var options = new AllSeverityFilterOptions { MinSeverity = LogLevel.Information };
        using var processor = new AllSeverityFilterProcessor(options, _innerProcessor);

        var record = CreateLogRecord(logLevel);
        processor.OnEnd(record);

        Assert.Single(_innerProcessor.ProcessedRecords);
    }

    // ─── Events below minimum severity are dropped ────────────────────

    [Theory]
    [InlineData(LogLevel.Trace)]
    [InlineData(LogLevel.Debug)]
    public void EventBelowMinSeverity_IsDropped(LogLevel logLevel)
    {
        var options = new AllSeverityFilterOptions { MinSeverity = LogLevel.Information };
        using var processor = new AllSeverityFilterProcessor(options, _innerProcessor);

        var record = CreateLogRecord(logLevel);
        processor.OnEnd(record);

        Assert.Empty(_innerProcessor.ProcessedRecords);
    }

    // ─── Custom minimum severity ──────────────────────────────────────

    [Fact]
    public void CustomMinSeverity_Debug_AllowsDebugAndAbove()
    {
        var options = new AllSeverityFilterOptions { MinSeverity = LogLevel.Debug };
        using var processor = new AllSeverityFilterProcessor(options, _innerProcessor);

        processor.OnEnd(CreateLogRecord(LogLevel.Trace));        // below Debug → dropped
        processor.OnEnd(CreateLogRecord(LogLevel.Debug));        // at Debug → passes
        processor.OnEnd(CreateLogRecord(LogLevel.Information));  // above → passes
        processor.OnEnd(CreateLogRecord(LogLevel.Error));        // above → passes

        Assert.Equal(3, _innerProcessor.ProcessedRecords.Count);
    }

    [Fact]
    public void CustomMinSeverity_Error_OnlyErrorAndAbove()
    {
        var options = new AllSeverityFilterOptions { MinSeverity = LogLevel.Error };
        using var processor = new AllSeverityFilterProcessor(options, _innerProcessor);

        processor.OnEnd(CreateLogRecord(LogLevel.Information));  // below → dropped
        processor.OnEnd(CreateLogRecord(LogLevel.Warning));      // below → dropped
        processor.OnEnd(CreateLogRecord(LogLevel.Error));        // at threshold → passes
        processor.OnEnd(CreateLogRecord(LogLevel.Critical));     // above → passes

        Assert.Equal(2, _innerProcessor.ProcessedRecords.Count);
    }

    [Fact]
    public void MinSeverity_Trace_AllowsEverything()
    {
        var options = new AllSeverityFilterOptions { MinSeverity = LogLevel.Trace };
        using var processor = new AllSeverityFilterProcessor(options, _innerProcessor);

        processor.OnEnd(CreateLogRecord(LogLevel.Trace));
        processor.OnEnd(CreateLogRecord(LogLevel.Debug));
        processor.OnEnd(CreateLogRecord(LogLevel.Information));
        processor.OnEnd(CreateLogRecord(LogLevel.Warning));
        processor.OnEnd(CreateLogRecord(LogLevel.Error));
        processor.OnEnd(CreateLogRecord(LogLevel.Critical));

        Assert.Equal(6, _innerProcessor.ProcessedRecords.Count);
    }

    // ─── Per-event-name overrides ─────────────────────────────────────

    [Fact]
    public void EventNameOverride_ExactMatch_UsesOverrideSeverity()
    {
        var options = new AllSeverityFilterOptions
        {
            MinSeverity = LogLevel.Information,
            EventNameOverrides = new Dictionary<string, LogLevel>
            {
                ["health.check.executed"] = LogLevel.Debug
            }
        };
        using var processor = new AllSeverityFilterProcessor(options, _innerProcessor);

        // Debug event with matching name → should pass (override allows Debug)
        processor.OnEnd(CreateLogRecord(LogLevel.Debug, eventName: "health.check.executed"));

        Assert.Single(_innerProcessor.ProcessedRecords);
    }

    [Fact]
    public void EventNameOverride_ExactMatch_StillDropsBelowOverride()
    {
        var options = new AllSeverityFilterOptions
        {
            MinSeverity = LogLevel.Information,
            EventNameOverrides = new Dictionary<string, LogLevel>
            {
                ["health.check.executed"] = LogLevel.Debug
            }
        };
        using var processor = new AllSeverityFilterProcessor(options, _innerProcessor);

        // Trace event with matching name → dropped (below Debug override)
        processor.OnEnd(CreateLogRecord(LogLevel.Trace, eventName: "health.check.executed"));

        Assert.Empty(_innerProcessor.ProcessedRecords);
    }

    [Fact]
    public void EventNameOverride_WildcardSuffix_MatchesPrefix()
    {
        var options = new AllSeverityFilterOptions
        {
            MinSeverity = LogLevel.Information,
            EventNameOverrides = new Dictionary<string, LogLevel>
            {
                ["health.check.*"] = LogLevel.Debug
            }
        };
        using var processor = new AllSeverityFilterProcessor(options, _innerProcessor);

        // Debug events with matching prefix → should pass
        processor.OnEnd(CreateLogRecord(LogLevel.Debug, eventName: "health.check.executed"));
        processor.OnEnd(CreateLogRecord(LogLevel.Debug, eventName: "health.check.failed"));

        Assert.Equal(2, _innerProcessor.ProcessedRecords.Count);
    }

    [Fact]
    public void EventNameOverride_WildcardSuffix_DoesNotMatchNonPrefix()
    {
        var options = new AllSeverityFilterOptions
        {
            MinSeverity = LogLevel.Information,
            EventNameOverrides = new Dictionary<string, LogLevel>
            {
                ["health.check.*"] = LogLevel.Debug
            }
        };
        using var processor = new AllSeverityFilterProcessor(options, _innerProcessor);

        // Debug event with non-matching name → uses global min (Info), dropped
        processor.OnEnd(CreateLogRecord(LogLevel.Debug, eventName: "http.request.completed"));

        Assert.Empty(_innerProcessor.ProcessedRecords);
    }

    [Fact]
    public void EventNameOverride_NoEventName_UsesGlobalMinSeverity()
    {
        var options = new AllSeverityFilterOptions
        {
            MinSeverity = LogLevel.Information,
            EventNameOverrides = new Dictionary<string, LogLevel>
            {
                ["health.check.*"] = LogLevel.Debug
            }
        };
        using var processor = new AllSeverityFilterProcessor(options, _innerProcessor);

        // Debug event with no event name → uses global min (Info), dropped
        processor.OnEnd(CreateLogRecord(LogLevel.Debug));

        Assert.Empty(_innerProcessor.ProcessedRecords);
    }

    [Fact]
    public void EventNameOverride_ExactMatchTakesPrecedenceOverWildcard()
    {
        var options = new AllSeverityFilterOptions
        {
            MinSeverity = LogLevel.Information,
            EventNameOverrides = new Dictionary<string, LogLevel>
            {
                ["health.check.*"] = LogLevel.Debug,
                ["health.check.executed"] = LogLevel.Warning
            }
        };
        using var processor = new AllSeverityFilterProcessor(options, _innerProcessor);

        // Info event for exact match → below Warning override → dropped
        processor.OnEnd(CreateLogRecord(LogLevel.Information, eventName: "health.check.executed"));

        // Debug event for wildcard match → at Debug override → passes
        processor.OnEnd(CreateLogRecord(LogLevel.Debug, eventName: "health.check.failed"));

        Assert.Single(_innerProcessor.ProcessedRecords);
    }

    [Fact]
    public void EventNameOverride_CanRaiseMinSeverityAboveGlobal()
    {
        var options = new AllSeverityFilterOptions
        {
            MinSeverity = LogLevel.Debug,
            EventNameOverrides = new Dictionary<string, LogLevel>
            {
                ["noisy.event"] = LogLevel.Error
            }
        };
        using var processor = new AllSeverityFilterProcessor(options, _innerProcessor);

        // Info event for overridden name → below Error → dropped
        processor.OnEnd(CreateLogRecord(LogLevel.Information, eventName: "noisy.event"));
        // Info event for other name → above Debug global → passes
        processor.OnEnd(CreateLogRecord(LogLevel.Information, eventName: "other.event"));

        Assert.Single(_innerProcessor.ProcessedRecords);
    }

    // ─── Self-telemetry counter ───────────────────────────────────────

    [Fact]
    public void DroppedEvent_IncrementsSelfTelemetryCounter()
    {
        var options = new AllSeverityFilterOptions { MinSeverity = LogLevel.Error };
        using var processor = new AllSeverityFilterProcessor(options, _innerProcessor);

        processor.OnEnd(CreateLogRecord(LogLevel.Debug));
        processor.OnEnd(CreateLogRecord(LogLevel.Information));
        processor.OnEnd(CreateLogRecord(LogLevel.Warning));

        _meterListener.RecordObservableInstruments();

        Assert.Equal(3, Interlocked.Read(ref _droppedCount));
    }

    [Fact]
    public void PassedEvent_DoesNotIncrementSelfTelemetryCounter()
    {
        var options = new AllSeverityFilterOptions { MinSeverity = LogLevel.Information };
        using var processor = new AllSeverityFilterProcessor(options, _innerProcessor);

        processor.OnEnd(CreateLogRecord(LogLevel.Information));
        processor.OnEnd(CreateLogRecord(LogLevel.Error));

        _meterListener.RecordObservableInstruments();

        Assert.Equal(0, Interlocked.Read(ref _droppedCount));
    }

    // ─── Processor lifecycle (ForceFlush, Shutdown) ───────────────────

    [Fact]
    public void ForceFlush_DelegatesToInnerProcessor()
    {
        var options = new AllSeverityFilterOptions();
        using var processor = new AllSeverityFilterProcessor(options, _innerProcessor);

        var result = processor.ForceFlush();

        Assert.True(result);
        Assert.True(_innerProcessor.ForceFlushCalled);
    }

    [Fact]
    public void Shutdown_DelegatesToInnerProcessor()
    {
        var options = new AllSeverityFilterOptions();
        using var processor = new AllSeverityFilterProcessor(options, _innerProcessor);

        var result = processor.Shutdown();

        Assert.True(result);
        Assert.True(_innerProcessor.ShutdownCalled);
    }

    // ─── Constructor validation ───────────────────────────────────────

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AllSeverityFilterProcessor(null!, new InMemoryLogRecordProcessor()));
    }

    [Fact]
    public void Constructor_NullInnerProcessor_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AllSeverityFilterProcessor(new AllSeverityFilterOptions(), null!));
    }

    // ─── Helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="LogRecord"/> using reflection (internal constructor).
    /// Follows the same pattern used in the project's TestExporterHarness.
    /// </summary>
    private static LogRecord CreateLogRecord(
        LogLevel logLevel = LogLevel.Information,
        string? eventName = null)
    {
        var record = (LogRecord)Activator.CreateInstance(typeof(LogRecord), nonPublic: true)!;
        record.LogLevel = logLevel;

        if (eventName is not null)
        {
            record.EventId = new EventId(0, eventName);
        }

        return record;
    }
}

/// <summary>
/// In-memory processor that records all LogRecords passed to it.
/// Used for testing the severity filter processor's wrapping behavior.
/// </summary>
internal sealed class InMemoryLogRecordProcessor : BaseProcessor<LogRecord>
{
    private readonly List<LogRecord> _processedRecords = [];

    public IReadOnlyList<LogRecord> ProcessedRecords => _processedRecords;

    public bool ForceFlushCalled { get; private set; }

    public bool ShutdownCalled { get; private set; }

    public override void OnEnd(LogRecord data)
    {
        _processedRecords.Add(data);
    }

    protected override bool OnForceFlush(int timeoutMilliseconds)
    {
        ForceFlushCalled = true;
        return true;
    }

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        ShutdownCalled = true;
        return true;
    }
}
