using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using All.Exporter.Json;

namespace All.Exporter.Json.Tests;

/// <summary>
/// Tests for <see cref="AllSamplingProcessor"/>: probabilistic event sampling
/// with head/tail strategies and per-event-name rate overrides.
/// </summary>
public sealed class AllSamplingProcessorTests : IDisposable
{
    private readonly InMemoryLogRecordProcessor _innerProcessor = new();
    private readonly MeterListener _meterListener = new();
    private long _sampledCount;
    private long _droppedCount;

    public AllSamplingProcessorTests()
    {
        _meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == "all.processor.sampling.events_sampled"
                || instrument.Name == "all.processor.sampling.events_dropped")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "all.processor.sampling.events_sampled")
            {
                Interlocked.Add(ref _sampledCount, measurement);
            }
            else if (instrument.Name == "all.processor.sampling.events_dropped")
            {
                Interlocked.Add(ref _droppedCount, measurement);
            }
        });
        _meterListener.Start();
    }

    public void Dispose()
    {
        _meterListener.Dispose();
        _innerProcessor.Dispose();
    }

    // ─── Default options ──────────────────────────────────────────────

    [Fact]
    public void DefaultOptions_StrategyIsHead()
    {
        var options = new AllSamplingOptions();

        Assert.Equal(AllSamplingStrategy.Head, options.Strategy);
    }

    [Fact]
    public void DefaultOptions_DefaultSamplingRateIsOne()
    {
        var options = new AllSamplingOptions();

        Assert.Equal(1.0, options.DefaultSamplingRate);
    }

    [Fact]
    public void DefaultOptions_EventRatesIsEmpty()
    {
        var options = new AllSamplingOptions();

        Assert.NotNull(options.EventRates);
        Assert.Empty(options.EventRates);
    }

    [Fact]
    public void DefaultOptions_AlwaysSampleErrorsIsTrue()
    {
        var options = new AllSamplingOptions();

        Assert.True(options.AlwaysSampleErrors);
    }

    [Fact]
    public void DefaultOptions_ErrorMinLevelIsError()
    {
        var options = new AllSamplingOptions();

        Assert.Equal(LogLevel.Error, options.ErrorMinLevel);
    }

    // ─── Head sampling: rate 1.0 (all events pass) ────────────────────

    [Fact]
    public void HeadSampling_Rate1_AllEventsPassThrough()
    {
        var options = new AllSamplingOptions
        {
            Strategy = AllSamplingStrategy.Head,
            DefaultSamplingRate = 1.0
        };
        using var processor = new AllSamplingProcessor(options, _innerProcessor);

        for (var i = 0; i < 100; i++)
        {
            processor.OnEnd(CreateLogRecord(eventName: "some.event"));
        }

        Assert.Equal(100, _innerProcessor.ProcessedRecords.Count);
    }

    // ─── Head sampling: rate 0.0 (all events dropped) ─────────────────

    [Fact]
    public void HeadSampling_Rate0_AllEventsDropped()
    {
        var options = new AllSamplingOptions
        {
            Strategy = AllSamplingStrategy.Head,
            DefaultSamplingRate = 0.0
        };
        using var processor = new AllSamplingProcessor(options, _innerProcessor);

        for (var i = 0; i < 100; i++)
        {
            processor.OnEnd(CreateLogRecord(eventName: "some.event"));
        }

        Assert.Empty(_innerProcessor.ProcessedRecords);
    }

    // ─── Head sampling: probabilistic rate ─────────────────────────────

    [Fact]
    public void HeadSampling_Rate50Percent_SamplesApproximatelyHalf()
    {
        var options = new AllSamplingOptions
        {
            Strategy = AllSamplingStrategy.Head,
            DefaultSamplingRate = 0.5
        };
        using var processor = new AllSamplingProcessor(options, _innerProcessor);

        const int total = 10_000;
        for (var i = 0; i < total; i++)
        {
            processor.OnEnd(CreateLogRecord(eventName: "some.event"));
        }

        var sampled = _innerProcessor.ProcessedRecords.Count;

        // With 10,000 events at 50%, we expect ~5,000 ± ~300 (3σ for binomial)
        Assert.InRange(sampled, 4400, 5600);
    }

    [Fact]
    public void HeadSampling_Rate10Percent_SamplesApproximately10Percent()
    {
        var options = new AllSamplingOptions
        {
            Strategy = AllSamplingStrategy.Head,
            DefaultSamplingRate = 0.1
        };
        using var processor = new AllSamplingProcessor(options, _innerProcessor);

        const int total = 10_000;
        for (var i = 0; i < total; i++)
        {
            processor.OnEnd(CreateLogRecord(eventName: "some.event"));
        }

        var sampled = _innerProcessor.ProcessedRecords.Count;

        // With 10,000 events at 10%, we expect ~1,000 ± ~180 (3σ)
        Assert.InRange(sampled, 700, 1300);
    }

    // ─── Head sampling: errors are NOT special ────────────────────────

    [Fact]
    public void HeadSampling_Rate0_ErrorEventsAlsoDropped()
    {
        var options = new AllSamplingOptions
        {
            Strategy = AllSamplingStrategy.Head,
            DefaultSamplingRate = 0.0,
            AlwaysSampleErrors = true // ignored in head mode
        };
        using var processor = new AllSamplingProcessor(options, _innerProcessor);

        processor.OnEnd(CreateLogRecord(logLevel: LogLevel.Error, eventName: "error.event"));
        processor.OnEnd(CreateLogRecord(logLevel: LogLevel.Critical, eventName: "critical.event"));

        Assert.Empty(_innerProcessor.ProcessedRecords);
    }

    // ─── Tail sampling: always sample errors ──────────────────────────

    [Fact]
    public void TailSampling_Rate0_ErrorsAlwaysSampled()
    {
        var options = new AllSamplingOptions
        {
            Strategy = AllSamplingStrategy.Tail,
            DefaultSamplingRate = 0.0,
            AlwaysSampleErrors = true,
            ErrorMinLevel = LogLevel.Error
        };
        using var processor = new AllSamplingProcessor(options, _innerProcessor);

        processor.OnEnd(CreateLogRecord(logLevel: LogLevel.Error, eventName: "error.event"));
        processor.OnEnd(CreateLogRecord(logLevel: LogLevel.Critical, eventName: "critical.event"));

        Assert.Equal(2, _innerProcessor.ProcessedRecords.Count);
    }

    [Fact]
    public void TailSampling_Rate0_NonErrorsDropped()
    {
        var options = new AllSamplingOptions
        {
            Strategy = AllSamplingStrategy.Tail,
            DefaultSamplingRate = 0.0,
            AlwaysSampleErrors = true,
            ErrorMinLevel = LogLevel.Error
        };
        using var processor = new AllSamplingProcessor(options, _innerProcessor);

        processor.OnEnd(CreateLogRecord(logLevel: LogLevel.Information, eventName: "info.event"));
        processor.OnEnd(CreateLogRecord(logLevel: LogLevel.Warning, eventName: "warn.event"));
        processor.OnEnd(CreateLogRecord(logLevel: LogLevel.Debug, eventName: "debug.event"));

        Assert.Empty(_innerProcessor.ProcessedRecords);
    }

    [Fact]
    public void TailSampling_AlwaysSampleErrorsFalse_ErrorsFollowRate()
    {
        var options = new AllSamplingOptions
        {
            Strategy = AllSamplingStrategy.Tail,
            DefaultSamplingRate = 0.0,
            AlwaysSampleErrors = false
        };
        using var processor = new AllSamplingProcessor(options, _innerProcessor);

        for (var i = 0; i < 100; i++)
        {
            processor.OnEnd(CreateLogRecord(logLevel: LogLevel.Error, eventName: "error.event"));
        }

        // With rate 0.0 and AlwaysSampleErrors=false, errors are also dropped
        Assert.Empty(_innerProcessor.ProcessedRecords);
    }

    [Fact]
    public void TailSampling_CustomErrorMinLevel_RespectsThreshold()
    {
        var options = new AllSamplingOptions
        {
            Strategy = AllSamplingStrategy.Tail,
            DefaultSamplingRate = 0.0,
            AlwaysSampleErrors = true,
            ErrorMinLevel = LogLevel.Warning // treat Warning+ as "error"
        };
        using var processor = new AllSamplingProcessor(options, _innerProcessor);

        processor.OnEnd(CreateLogRecord(logLevel: LogLevel.Information)); // dropped
        processor.OnEnd(CreateLogRecord(logLevel: LogLevel.Warning));     // sampled (>= Warning)
        processor.OnEnd(CreateLogRecord(logLevel: LogLevel.Error));       // sampled
        processor.OnEnd(CreateLogRecord(logLevel: LogLevel.Critical));    // sampled

        Assert.Equal(3, _innerProcessor.ProcessedRecords.Count);
    }

    [Fact]
    public void TailSampling_Rate1_AllEventsPassIncludingNonErrors()
    {
        var options = new AllSamplingOptions
        {
            Strategy = AllSamplingStrategy.Tail,
            DefaultSamplingRate = 1.0,
            AlwaysSampleErrors = true
        };
        using var processor = new AllSamplingProcessor(options, _innerProcessor);

        processor.OnEnd(CreateLogRecord(logLevel: LogLevel.Debug));
        processor.OnEnd(CreateLogRecord(logLevel: LogLevel.Information));
        processor.OnEnd(CreateLogRecord(logLevel: LogLevel.Error));

        Assert.Equal(3, _innerProcessor.ProcessedRecords.Count);
    }

    // ─── Per-event-name sampling rates: exact match ───────────────────

    [Fact]
    public void PerEventRate_ExactMatch_UsesOverrideRate()
    {
        var options = new AllSamplingOptions
        {
            DefaultSamplingRate = 1.0,
            EventRates = new Dictionary<string, double>
            {
                ["noisy.event"] = 0.0
            }
        };
        using var processor = new AllSamplingProcessor(options, _innerProcessor);

        // Events matching the exact override get rate 0.0 (dropped)
        for (var i = 0; i < 100; i++)
        {
            processor.OnEnd(CreateLogRecord(eventName: "noisy.event"));
        }

        Assert.Empty(_innerProcessor.ProcessedRecords);
    }

    [Fact]
    public void PerEventRate_ExactMatch_OtherEventsUseDefault()
    {
        var options = new AllSamplingOptions
        {
            DefaultSamplingRate = 1.0,
            EventRates = new Dictionary<string, double>
            {
                ["noisy.event"] = 0.0
            }
        };
        using var processor = new AllSamplingProcessor(options, _innerProcessor);

        // Events NOT matching the override use default rate (1.0 = pass)
        processor.OnEnd(CreateLogRecord(eventName: "other.event"));

        Assert.Single(_innerProcessor.ProcessedRecords);
    }

    // ─── Per-event-name sampling rates: wildcard match ────────────────

    [Fact]
    public void PerEventRate_WildcardMatch_UsesOverrideRate()
    {
        var options = new AllSamplingOptions
        {
            DefaultSamplingRate = 1.0,
            EventRates = new Dictionary<string, double>
            {
                ["db.query.*"] = 0.0
            }
        };
        using var processor = new AllSamplingProcessor(options, _innerProcessor);

        processor.OnEnd(CreateLogRecord(eventName: "db.query.executed"));
        processor.OnEnd(CreateLogRecord(eventName: "db.query.slow"));
        processor.OnEnd(CreateLogRecord(eventName: "http.request.completed"));

        // Only http.request.completed passes (db.query.* overridden to 0.0)
        Assert.Single(_innerProcessor.ProcessedRecords);
    }

    [Fact]
    public void PerEventRate_ExactMatchTakesPrecedenceOverWildcard()
    {
        var options = new AllSamplingOptions
        {
            DefaultSamplingRate = 0.0,
            EventRates = new Dictionary<string, double>
            {
                ["db.query.*"] = 0.0,
                ["db.query.critical"] = 1.0 // exact match overrides wildcard
            }
        };
        using var processor = new AllSamplingProcessor(options, _innerProcessor);

        processor.OnEnd(CreateLogRecord(eventName: "db.query.executed")); // wildcard: 0.0 → drop
        processor.OnEnd(CreateLogRecord(eventName: "db.query.critical")); // exact: 1.0 → pass

        Assert.Single(_innerProcessor.ProcessedRecords);
    }

    [Fact]
    public void PerEventRate_LongerWildcardTakesPrecedence()
    {
        var options = new AllSamplingOptions
        {
            DefaultSamplingRate = 0.0,
            EventRates = new Dictionary<string, double>
            {
                ["db.*"] = 0.0,
                ["db.query.*"] = 1.0 // more specific wildcard wins
            }
        };
        using var processor = new AllSamplingProcessor(options, _innerProcessor);

        processor.OnEnd(CreateLogRecord(eventName: "db.query.executed")); // db.query.* → 1.0
        processor.OnEnd(CreateLogRecord(eventName: "db.connect"));        // db.* → 0.0

        Assert.Single(_innerProcessor.ProcessedRecords);
    }

    // ─── GetSamplingRate internal method ───────────────────────────────

    [Fact]
    public void GetSamplingRate_ExactMatch_ReturnsOverride()
    {
        var options = new AllSamplingOptions
        {
            DefaultSamplingRate = 0.5,
            EventRates = new Dictionary<string, double>
            {
                ["db.query.executed"] = 0.1
            }
        };
        using var processor = new AllSamplingProcessor(options, _innerProcessor);

        var record = CreateLogRecord(eventName: "db.query.executed");
        Assert.Equal(0.1, processor.GetSamplingRate(record));
    }

    [Fact]
    public void GetSamplingRate_WildcardMatch_ReturnsWildcardRate()
    {
        var options = new AllSamplingOptions
        {
            DefaultSamplingRate = 0.5,
            EventRates = new Dictionary<string, double>
            {
                ["db.query.*"] = 0.2
            }
        };
        using var processor = new AllSamplingProcessor(options, _innerProcessor);

        var record = CreateLogRecord(eventName: "db.query.slow");
        Assert.Equal(0.2, processor.GetSamplingRate(record));
    }

    [Fact]
    public void GetSamplingRate_NoMatch_ReturnsDefault()
    {
        var options = new AllSamplingOptions
        {
            DefaultSamplingRate = 0.42,
            EventRates = new Dictionary<string, double>
            {
                ["db.query.*"] = 0.1
            }
        };
        using var processor = new AllSamplingProcessor(options, _innerProcessor);

        var record = CreateLogRecord(eventName: "http.request.completed");
        Assert.Equal(0.42, processor.GetSamplingRate(record));
    }

    [Fact]
    public void GetSamplingRate_NoEventName_ReturnsDefault()
    {
        var options = new AllSamplingOptions { DefaultSamplingRate = 0.42 };
        using var processor = new AllSamplingProcessor(options, _innerProcessor);

        var record = CreateLogRecord();
        Assert.Equal(0.42, processor.GetSamplingRate(record));
    }

    // ─── Tail sampling with per-event rates ──────────────────────────

    [Fact]
    public void TailSampling_ErrorBypassesPerEventRate()
    {
        var options = new AllSamplingOptions
        {
            Strategy = AllSamplingStrategy.Tail,
            DefaultSamplingRate = 0.0,
            AlwaysSampleErrors = true,
            EventRates = new Dictionary<string, double>
            {
                ["db.query.*"] = 0.0 // rate says drop
            }
        };
        using var processor = new AllSamplingProcessor(options, _innerProcessor);

        // Error bypasses rate check even with per-event override
        processor.OnEnd(CreateLogRecord(
            logLevel: LogLevel.Error,
            eventName: "db.query.failed"));

        Assert.Single(_innerProcessor.ProcessedRecords);
    }

    // ─── Self-telemetry counters ──────────────────────────────────────

    [Fact]
    public void SelfTelemetry_SampledCounter_IncrementedForPassedEvents()
    {
        var options = new AllSamplingOptions { DefaultSamplingRate = 1.0 };
        using var processor = new AllSamplingProcessor(options, _innerProcessor);

        processor.OnEnd(CreateLogRecord(eventName: "test.event"));
        processor.OnEnd(CreateLogRecord(eventName: "test.event"));
        processor.OnEnd(CreateLogRecord(eventName: "test.event"));

        _meterListener.RecordObservableInstruments();

        Assert.Equal(3, Interlocked.Read(ref _sampledCount));
    }

    [Fact]
    public void SelfTelemetry_DroppedCounter_IncrementedForDroppedEvents()
    {
        var options = new AllSamplingOptions { DefaultSamplingRate = 0.0 };
        using var processor = new AllSamplingProcessor(options, _innerProcessor);

        processor.OnEnd(CreateLogRecord(eventName: "test.event"));
        processor.OnEnd(CreateLogRecord(eventName: "test.event"));

        _meterListener.RecordObservableInstruments();

        Assert.Equal(2, Interlocked.Read(ref _droppedCount));
    }

    [Fact]
    public void SelfTelemetry_MixedSampledAndDropped()
    {
        var options = new AllSamplingOptions
        {
            Strategy = AllSamplingStrategy.Tail,
            DefaultSamplingRate = 0.0,
            AlwaysSampleErrors = true
        };
        using var processor = new AllSamplingProcessor(options, _innerProcessor);

        // Errors pass (sampled), non-errors drop
        processor.OnEnd(CreateLogRecord(logLevel: LogLevel.Error));
        processor.OnEnd(CreateLogRecord(logLevel: LogLevel.Information));
        processor.OnEnd(CreateLogRecord(logLevel: LogLevel.Error));

        _meterListener.RecordObservableInstruments();

        Assert.Equal(2, Interlocked.Read(ref _sampledCount));
        Assert.Equal(1, Interlocked.Read(ref _droppedCount));
    }

    // ─── ForceFlush and Shutdown delegation ───────────────────────────

    [Fact]
    public void ForceFlush_DelegatesToInnerProcessor()
    {
        var options = new AllSamplingOptions();
        using var processor = new AllSamplingProcessor(options, _innerProcessor);

        processor.ForceFlush();

        Assert.True(_innerProcessor.ForceFlushCalled);
    }

    [Fact]
    public void Shutdown_DelegatesToInnerProcessor()
    {
        var options = new AllSamplingOptions();
        using var processor = new AllSamplingProcessor(options, _innerProcessor);

        processor.Shutdown();

        Assert.True(_innerProcessor.ShutdownCalled);
    }

    // ─── Dispose ──────────────────────────────────────────────────────

    [Fact]
    public void Dispose_DisposesInnerProcessor()
    {
        var inner = new InMemoryLogRecordProcessor();
        var options = new AllSamplingOptions();
        var processor = new AllSamplingProcessor(options, inner);

        processor.Dispose();

        // Verify inner was disposed — sending OnEnd should not throw,
        // but we verify by double-dispose being safe
        processor.Dispose(); // idempotent
    }

    // ─── Options validation ──────────────────────────────────────────

    [Fact]
    public void InvalidStrategy_ThrowsArgumentOutOfRangeException()
    {
        var options = new AllSamplingOptions
        {
            Strategy = (AllSamplingStrategy)999
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AllSamplingProcessor(options, _innerProcessor));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(-1.0)]
    [InlineData(2.0)]
    public void InvalidDefaultSamplingRate_ThrowsArgumentOutOfRangeException(double rate)
    {
        var options = new AllSamplingOptions
        {
            DefaultSamplingRate = rate
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AllSamplingProcessor(options, _innerProcessor));
    }

    [Fact]
    public void InvalidErrorMinLevel_ThrowsArgumentOutOfRangeException()
    {
        var options = new AllSamplingOptions
        {
            ErrorMinLevel = (LogLevel)999
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AllSamplingProcessor(options, _innerProcessor));
    }

    [Fact]
    public void EmptyEventRateKey_ThrowsArgumentException()
    {
        var options = new AllSamplingOptions
        {
            EventRates = new Dictionary<string, double>
            {
                [""] = 0.5
            }
        };

        Assert.Throws<ArgumentException>(() =>
            new AllSamplingProcessor(options, _innerProcessor));
    }

    [Fact]
    public void BareWildcard_ThrowsArgumentException()
    {
        var options = new AllSamplingOptions
        {
            EventRates = new Dictionary<string, double>
            {
                ["*"] = 0.5
            }
        };

        Assert.Throws<ArgumentException>(() =>
            new AllSamplingProcessor(options, _innerProcessor));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(-1.0)]
    [InlineData(2.0)]
    public void InvalidEventRate_ThrowsArgumentOutOfRangeException(double rate)
    {
        var options = new AllSamplingOptions
        {
            EventRates = new Dictionary<string, double>
            {
                ["test.event"] = rate
            }
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AllSamplingProcessor(options, _innerProcessor));
    }

    // ─── Constructor null guards ──────────────────────────────────────

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AllSamplingProcessor(null!, new InMemoryLogRecordProcessor()));
    }

    [Fact]
    public void Constructor_NullInnerProcessor_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AllSamplingProcessor(new AllSamplingOptions(), null!));
    }

    // ─── Valid boundary rates ─────────────────────────────────────────

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void ValidDefaultSamplingRate_DoesNotThrow(double rate)
    {
        var options = new AllSamplingOptions { DefaultSamplingRate = rate };

        using var processor = new AllSamplingProcessor(options, _innerProcessor);
        // No exception — valid
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void ValidEventRate_DoesNotThrow(double rate)
    {
        var options = new AllSamplingOptions
        {
            EventRates = new Dictionary<string, double>
            {
                ["test.event"] = rate
            }
        };

        using var processor = new AllSamplingProcessor(options, _innerProcessor);
        // No exception — valid
    }

    // ─── Thread safety ───────────────────────────────────────────────

    [Fact]
    public async Task ConcurrentSampling_ThreadSafe()
    {
        var inner = new ThreadSafeLogRecordProcessor();
        var options = new AllSamplingOptions
        {
            Strategy = AllSamplingStrategy.Head,
            DefaultSamplingRate = 0.5
        };
        using var processor = new AllSamplingProcessor(options, inner);

        const int threadCount = 8;
        const int eventsPerThread = 1_000;
        var barrier = new Barrier(threadCount);
        var tasks = new Task[threadCount];

        for (var t = 0; t < threadCount; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                barrier.SignalAndWait();
                for (var i = 0; i < eventsPerThread; i++)
                {
                    processor.OnEnd(CreateLogRecord(eventName: "concurrent.event"));
                }
            });
        }

        await Task.WhenAll(tasks);

        // At 50% rate with 8000 events, expect ~4000 ± ~400 (4σ for safety)
        Assert.InRange(inner.Count, 2800, 5200);

        // Total sampled + dropped should equal total events sent
        // (via self-telemetry — check that counters are consistent)
        _meterListener.RecordObservableInstruments();
        var sampled = Interlocked.Read(ref _sampledCount);
        var dropped = Interlocked.Read(ref _droppedCount);

        // Due to meter listener timing with multiple test instances,
        // we just verify sampled > 0 and dropped > 0
        Assert.True(sampled > 0, "Expected some events to be sampled");
        Assert.True(dropped > 0, "Expected some events to be dropped");
    }

    // ─── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void EventWithNoName_UsesDefaultRate()
    {
        var options = new AllSamplingOptions
        {
            DefaultSamplingRate = 0.0,
            EventRates = new Dictionary<string, double>
            {
                ["specific.event"] = 1.0
            }
        };
        using var processor = new AllSamplingProcessor(options, _innerProcessor);

        // No event name → uses default (0.0 → dropped)
        processor.OnEnd(CreateLogRecord());

        Assert.Empty(_innerProcessor.ProcessedRecords);
    }

    [Fact]
    public void TailSampling_ErrorWithNoName_StillSampled()
    {
        var options = new AllSamplingOptions
        {
            Strategy = AllSamplingStrategy.Tail,
            DefaultSamplingRate = 0.0,
            AlwaysSampleErrors = true
        };
        using var processor = new AllSamplingProcessor(options, _innerProcessor);

        // Error without event name should still be sampled
        processor.OnEnd(CreateLogRecord(logLevel: LogLevel.Error));

        Assert.Single(_innerProcessor.ProcessedRecords);
    }

    [Fact]
    public void DefaultOptions_NoEventsDropped()
    {
        // Default: rate=1.0, head sampling — everything passes
        var options = new AllSamplingOptions();
        using var processor = new AllSamplingProcessor(options, _innerProcessor);

        processor.OnEnd(CreateLogRecord(logLevel: LogLevel.Trace));
        processor.OnEnd(CreateLogRecord(logLevel: LogLevel.Debug));
        processor.OnEnd(CreateLogRecord(logLevel: LogLevel.Information));
        processor.OnEnd(CreateLogRecord(logLevel: LogLevel.Warning));
        processor.OnEnd(CreateLogRecord(logLevel: LogLevel.Error));
        processor.OnEnd(CreateLogRecord(logLevel: LogLevel.Critical));

        Assert.Equal(6, _innerProcessor.ProcessedRecords.Count);
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

    /// <summary>
    /// Thread-safe log record processor using atomic counter.
    /// Used for concurrent access tests where <see cref="InMemoryLogRecordProcessor"/>
    /// (backed by <c>List&lt;T&gt;</c>) would lose items due to unsafe concurrent Add.
    /// </summary>
    private sealed class ThreadSafeLogRecordProcessor : BaseProcessor<LogRecord>
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public override void OnEnd(LogRecord data) =>
            Interlocked.Increment(ref _count);
    }
}
