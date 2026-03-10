using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OtelEvents.Exporter.Json;

namespace OtelEvents.Exporter.Json.Tests;

/// <summary>
/// Tests for <see cref="OtelEventsRateLimitProcessor"/>: per-event-category rate limiting.
/// </summary>
public sealed class OtelEventsRateLimitProcessorTests : IDisposable
{
    private readonly InMemoryLogRecordProcessor _innerProcessor = new();
    private readonly MeterListener _meterListener = new();
    private long _droppedCount;
    private long _passedCount;

    public OtelEventsRateLimitProcessorTests()
    {
        _meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == "otel_events.processor.rate_limit.events_dropped"
                || instrument.Name == "otel_events.processor.rate_limit.events_passed")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "otel_events.processor.rate_limit.events_dropped")
            {
                Interlocked.Add(ref _droppedCount, measurement);
            }
            else if (instrument.Name == "otel_events.processor.rate_limit.events_passed")
            {
                Interlocked.Add(ref _passedCount, measurement);
            }
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
    public void DefaultOptions_DefaultMaxEventsPerWindowIsZero()
    {
        var options = new OtelEventsRateLimitOptions();

        Assert.Equal(0, options.DefaultMaxEventsPerWindow);
    }

    [Fact]
    public void DefaultOptions_EventLimitsIsEmpty()
    {
        var options = new OtelEventsRateLimitOptions();

        Assert.NotNull(options.EventLimits);
        Assert.Empty(options.EventLimits);
    }

    [Fact]
    public void DefaultOptions_WindowIsOneSecond()
    {
        var options = new OtelEventsRateLimitOptions();

        Assert.Equal(TimeSpan.FromSeconds(1), options.Window);
    }

    // ─── No rate limit (default = 0 = unlimited) ──────────────────────

    [Fact]
    public void DefaultMaxEventsPerWindow_Zero_AllEventsPassThrough()
    {
        var options = new OtelEventsRateLimitOptions { DefaultMaxEventsPerWindow = 0 };
        using var processor = new OtelEventsRateLimitProcessor(options, _innerProcessor);

        for (var i = 0; i < 100; i++)
        {
            processor.OnEnd(CreateLogRecord(eventName: "some.event"));
        }

        Assert.Equal(100, _innerProcessor.ProcessedRecords.Count);
    }

    // ─── Events exceeding rate limit are dropped ──────────────────────

    [Fact]
    public void EventsExceedingDefaultLimit_AreDropped()
    {
        var timeProvider = new FakeTimeProvider();
        var options = new OtelEventsRateLimitOptions { DefaultMaxEventsPerWindow = 3 };
        using var processor = new OtelEventsRateLimitProcessor(options, _innerProcessor, timeProvider);

        // Send 5 events within the same window
        for (var i = 0; i < 5; i++)
        {
            processor.OnEnd(CreateLogRecord(eventName: "test.event"));
        }

        // Only 3 should pass
        Assert.Equal(3, _innerProcessor.ProcessedRecords.Count);
    }

    [Fact]
    public void EventsWithinLimit_AllPassThrough()
    {
        var timeProvider = new FakeTimeProvider();
        var options = new OtelEventsRateLimitOptions { DefaultMaxEventsPerWindow = 10 };
        using var processor = new OtelEventsRateLimitProcessor(options, _innerProcessor, timeProvider);

        for (var i = 0; i < 10; i++)
        {
            processor.OnEnd(CreateLogRecord(eventName: "test.event"));
        }

        Assert.Equal(10, _innerProcessor.ProcessedRecords.Count);
    }

    // ─── Sliding window resets correctly ──────────────────────────────

    [Fact]
    public void WindowExpiration_ResetsCounter_AllowsNewEvents()
    {
        var timeProvider = new FakeTimeProvider();
        var options = new OtelEventsRateLimitOptions
        {
            DefaultMaxEventsPerWindow = 2,
            Window = TimeSpan.FromSeconds(1)
        };
        using var processor = new OtelEventsRateLimitProcessor(options, _innerProcessor, timeProvider);

        // Window 1: send 3 events, only 2 pass
        processor.OnEnd(CreateLogRecord(eventName: "test.event"));
        processor.OnEnd(CreateLogRecord(eventName: "test.event"));
        processor.OnEnd(CreateLogRecord(eventName: "test.event"));
        Assert.Equal(2, _innerProcessor.ProcessedRecords.Count);

        // Advance past window
        timeProvider.Advance(TimeSpan.FromSeconds(1.1));

        // Window 2: send 3 more, only 2 more pass
        processor.OnEnd(CreateLogRecord(eventName: "test.event"));
        processor.OnEnd(CreateLogRecord(eventName: "test.event"));
        processor.OnEnd(CreateLogRecord(eventName: "test.event"));
        Assert.Equal(4, _innerProcessor.ProcessedRecords.Count);
    }

    [Fact]
    public void WindowNotExpired_ContinuesCountingFromPreviousWindow()
    {
        var timeProvider = new FakeTimeProvider();
        var options = new OtelEventsRateLimitOptions
        {
            DefaultMaxEventsPerWindow = 3,
            Window = TimeSpan.FromSeconds(1)
        };
        using var processor = new OtelEventsRateLimitProcessor(options, _innerProcessor, timeProvider);

        // Send 2 events
        processor.OnEnd(CreateLogRecord(eventName: "test.event"));
        processor.OnEnd(CreateLogRecord(eventName: "test.event"));

        // Advance within window (not expired)
        timeProvider.Advance(TimeSpan.FromMilliseconds(500));

        // Send 2 more — only 1 more should pass (total 3 in window)
        processor.OnEnd(CreateLogRecord(eventName: "test.event"));
        processor.OnEnd(CreateLogRecord(eventName: "test.event"));

        Assert.Equal(3, _innerProcessor.ProcessedRecords.Count);
    }

    // ─── Per-event-name limits with exact matching ────────────────────

    [Fact]
    public void ExactEventLimit_LimitsSpecificEvent()
    {
        var timeProvider = new FakeTimeProvider();
        var options = new OtelEventsRateLimitOptions
        {
            DefaultMaxEventsPerWindow = 100,
            EventLimits = new Dictionary<string, int>
            {
                ["db.query.executed"] = 2
            }
        };
        using var processor = new OtelEventsRateLimitProcessor(options, _innerProcessor, timeProvider);

        // Send 5 matching events — only 2 pass
        for (var i = 0; i < 5; i++)
        {
            processor.OnEnd(CreateLogRecord(eventName: "db.query.executed"));
        }

        Assert.Equal(2, _innerProcessor.ProcessedRecords.Count);
    }

    [Fact]
    public void ExactEventLimit_DoesNotAffectOtherEvents()
    {
        var timeProvider = new FakeTimeProvider();
        var options = new OtelEventsRateLimitOptions
        {
            DefaultMaxEventsPerWindow = 100,
            EventLimits = new Dictionary<string, int>
            {
                ["db.query.executed"] = 2
            }
        };
        using var processor = new OtelEventsRateLimitProcessor(options, _innerProcessor, timeProvider);

        // Limited event
        for (var i = 0; i < 5; i++)
        {
            processor.OnEnd(CreateLogRecord(eventName: "db.query.executed"));
        }

        // Unlimited event (uses default of 100)
        for (var i = 0; i < 10; i++)
        {
            processor.OnEnd(CreateLogRecord(eventName: "http.request.completed"));
        }

        // 2 from limited + 10 from unlimited = 12
        Assert.Equal(12, _innerProcessor.ProcessedRecords.Count);
    }

    [Fact]
    public void ExactEventLimit_ZeroMeansUnlimited()
    {
        var timeProvider = new FakeTimeProvider();
        var options = new OtelEventsRateLimitOptions
        {
            DefaultMaxEventsPerWindow = 2,
            EventLimits = new Dictionary<string, int>
            {
                ["db.query.executed"] = 0  // unlimited override
            }
        };
        using var processor = new OtelEventsRateLimitProcessor(options, _innerProcessor, timeProvider);

        // Send 50 matching events — all pass (0 = unlimited)
        for (var i = 0; i < 50; i++)
        {
            processor.OnEnd(CreateLogRecord(eventName: "db.query.executed"));
        }

        Assert.Equal(50, _innerProcessor.ProcessedRecords.Count);
    }

    // ─── Per-event-name limits with wildcard matching ─────────────────

    [Fact]
    public void WildcardEventLimit_MatchesPrefix()
    {
        var timeProvider = new FakeTimeProvider();
        var options = new OtelEventsRateLimitOptions
        {
            DefaultMaxEventsPerWindow = 100,
            EventLimits = new Dictionary<string, int>
            {
                ["db.query.*"] = 2
            }
        };
        using var processor = new OtelEventsRateLimitProcessor(options, _innerProcessor, timeProvider);

        // Different events matching the wildcard share the same limit
        // BUT each event name gets its own counter
        processor.OnEnd(CreateLogRecord(eventName: "db.query.executed"));
        processor.OnEnd(CreateLogRecord(eventName: "db.query.executed"));
        processor.OnEnd(CreateLogRecord(eventName: "db.query.executed")); // dropped

        processor.OnEnd(CreateLogRecord(eventName: "db.query.failed"));
        processor.OnEnd(CreateLogRecord(eventName: "db.query.failed"));
        processor.OnEnd(CreateLogRecord(eventName: "db.query.failed")); // dropped

        // 2 + 2 = 4 (each event name has its own counter with limit 2)
        Assert.Equal(4, _innerProcessor.ProcessedRecords.Count);
    }

    [Fact]
    public void WildcardEventLimit_DoesNotMatchNonPrefix()
    {
        var timeProvider = new FakeTimeProvider();
        var options = new OtelEventsRateLimitOptions
        {
            DefaultMaxEventsPerWindow = 100,
            EventLimits = new Dictionary<string, int>
            {
                ["db.query.*"] = 2
            }
        };
        using var processor = new OtelEventsRateLimitProcessor(options, _innerProcessor, timeProvider);

        // Non-matching event uses default limit (100)
        for (var i = 0; i < 10; i++)
        {
            processor.OnEnd(CreateLogRecord(eventName: "http.request.completed"));
        }

        Assert.Equal(10, _innerProcessor.ProcessedRecords.Count);
    }

    // ─── Exact match takes precedence over wildcard ───────────────────

    [Fact]
    public void ExactMatchTakesPrecedenceOverWildcard()
    {
        var timeProvider = new FakeTimeProvider();
        var options = new OtelEventsRateLimitOptions
        {
            EventLimits = new Dictionary<string, int>
            {
                ["db.query.*"] = 10,
                ["db.query.executed"] = 2
            }
        };
        using var processor = new OtelEventsRateLimitProcessor(options, _innerProcessor, timeProvider);

        // Exact match event gets limit 2
        for (var i = 0; i < 5; i++)
        {
            processor.OnEnd(CreateLogRecord(eventName: "db.query.executed"));
        }

        Assert.Equal(2, _innerProcessor.ProcessedRecords.Count);
    }

    // ─── Overlapping wildcards: longest-prefix-wins ──────────────────

    [Fact]
    public void OverlappingWildcards_LongestPrefixWins()
    {
        var timeProvider = new FakeTimeProvider();
        var options = new OtelEventsRateLimitOptions
        {
            EventLimits = new Dictionary<string, int>
            {
                ["db.*"] = 50,
                ["db.query.*"] = 2
            }
        };
        using var processor = new OtelEventsRateLimitProcessor(options, _innerProcessor, timeProvider);

        // "db.query.executed" matches both "db." and "db.query."
        // Longest prefix "db.query." (limit 2) wins
        for (var i = 0; i < 5; i++)
        {
            processor.OnEnd(CreateLogRecord(eventName: "db.query.executed"));
        }

        Assert.Equal(2, _innerProcessor.ProcessedRecords.Count);
    }

    [Fact]
    public void OverlappingWildcards_ShorterPrefix_UsedWhenLongerDoesNotMatch()
    {
        var timeProvider = new FakeTimeProvider();
        var options = new OtelEventsRateLimitOptions
        {
            EventLimits = new Dictionary<string, int>
            {
                ["db.*"] = 3,
                ["db.query.*"] = 1
            }
        };
        using var processor = new OtelEventsRateLimitProcessor(options, _innerProcessor, timeProvider);

        // "db.connect" matches only "db." (limit 3)
        for (var i = 0; i < 5; i++)
        {
            processor.OnEnd(CreateLogRecord(eventName: "db.connect"));
        }

        Assert.Equal(3, _innerProcessor.ProcessedRecords.Count);
    }

    // ─── Events with no name use default limit ────────────────────────

    [Fact]
    public void EventWithNoName_UsesDefaultLimit()
    {
        var timeProvider = new FakeTimeProvider();
        var options = new OtelEventsRateLimitOptions
        {
            DefaultMaxEventsPerWindow = 2,
            EventLimits = new Dictionary<string, int>
            {
                ["db.query.*"] = 100
            }
        };
        using var processor = new OtelEventsRateLimitProcessor(options, _innerProcessor, timeProvider);

        // Events without a name use default (2)
        for (var i = 0; i < 5; i++)
        {
            processor.OnEnd(CreateLogRecord());
        }

        Assert.Equal(2, _innerProcessor.ProcessedRecords.Count);
    }

    // ─── Case sensitivity ────────────────────────────────────────────

    [Fact]
    public void EventNameComparison_IsCaseSensitive_ExactMatch()
    {
        var timeProvider = new FakeTimeProvider();
        var options = new OtelEventsRateLimitOptions
        {
            DefaultMaxEventsPerWindow = 100,
            EventLimits = new Dictionary<string, int>
            {
                ["Db.Query.Executed"] = 1
            }
        };
        using var processor = new OtelEventsRateLimitProcessor(options, _innerProcessor, timeProvider);

        // Lowercase does NOT match — uses default (100)
        for (var i = 0; i < 10; i++)
        {
            processor.OnEnd(CreateLogRecord(eventName: "db.query.executed"));
        }

        Assert.Equal(10, _innerProcessor.ProcessedRecords.Count);
    }

    [Fact]
    public void EventNameComparison_IsCaseSensitive_WildcardMatch()
    {
        var timeProvider = new FakeTimeProvider();
        var options = new OtelEventsRateLimitOptions
        {
            DefaultMaxEventsPerWindow = 100,
            EventLimits = new Dictionary<string, int>
            {
                ["Db.*"] = 1
            }
        };
        using var processor = new OtelEventsRateLimitProcessor(options, _innerProcessor, timeProvider);

        // Lowercase does NOT match — uses default (100)
        for (var i = 0; i < 10; i++)
        {
            processor.OnEnd(CreateLogRecord(eventName: "db.query.executed"));
        }

        Assert.Equal(10, _innerProcessor.ProcessedRecords.Count);
    }

    // ─── Self-telemetry counters ──────────────────────────────────────

    [Fact]
    public void DroppedEvent_IncrementsSelfTelemetryCounter()
    {
        var timeProvider = new FakeTimeProvider();
        var options = new OtelEventsRateLimitOptions { DefaultMaxEventsPerWindow = 1 };
        using var processor = new OtelEventsRateLimitProcessor(options, _innerProcessor, timeProvider);

        processor.OnEnd(CreateLogRecord(eventName: "test.event")); // passes
        processor.OnEnd(CreateLogRecord(eventName: "test.event")); // dropped
        processor.OnEnd(CreateLogRecord(eventName: "test.event")); // dropped

        _meterListener.RecordObservableInstruments();

        Assert.Equal(2, Interlocked.Read(ref _droppedCount));
    }

    [Fact]
    public void PassedEvent_IncrementsPassedCounter()
    {
        var timeProvider = new FakeTimeProvider();
        var options = new OtelEventsRateLimitOptions { DefaultMaxEventsPerWindow = 5 };
        using var processor = new OtelEventsRateLimitProcessor(options, _innerProcessor, timeProvider);

        processor.OnEnd(CreateLogRecord(eventName: "test.event"));
        processor.OnEnd(CreateLogRecord(eventName: "test.event"));
        processor.OnEnd(CreateLogRecord(eventName: "test.event"));

        _meterListener.RecordObservableInstruments();

        Assert.Equal(3, Interlocked.Read(ref _passedCount));
    }

    [Fact]
    public void UnlimitedEvents_IncrementPassedCounter_NeverDroppedCounter()
    {
        var options = new OtelEventsRateLimitOptions { DefaultMaxEventsPerWindow = 0 };
        using var processor = new OtelEventsRateLimitProcessor(options, _innerProcessor);

        for (var i = 0; i < 10; i++)
        {
            processor.OnEnd(CreateLogRecord(eventName: "test.event"));
        }

        _meterListener.RecordObservableInstruments();

        Assert.Equal(10, Interlocked.Read(ref _passedCount));
        Assert.Equal(0, Interlocked.Read(ref _droppedCount));
    }

    // ─── Processor lifecycle (ForceFlush, Shutdown) ───────────────────

    [Fact]
    public void ForceFlush_DelegatesToInnerProcessor()
    {
        var options = new OtelEventsRateLimitOptions();
        using var processor = new OtelEventsRateLimitProcessor(options, _innerProcessor);

        var result = processor.ForceFlush();

        Assert.True(result);
        Assert.True(_innerProcessor.ForceFlushCalled);
    }

    [Fact]
    public void Shutdown_DelegatesToInnerProcessor()
    {
        var options = new OtelEventsRateLimitOptions();
        using var processor = new OtelEventsRateLimitProcessor(options, _innerProcessor);

        var result = processor.Shutdown();

        Assert.True(result);
        Assert.True(_innerProcessor.ShutdownCalled);
    }

    // ─── Thread safety under concurrent load ──────────────────────────

    [Fact]
    public async Task ConcurrentAccess_DoesNotThrow_AndRespectsApproximateLimit()
    {
        var timeProvider = new FakeTimeProvider();
        var options = new OtelEventsRateLimitOptions { DefaultMaxEventsPerWindow = 100 };
        var threadSafeInner = new ThreadSafeLogRecordProcessor();
        using var processor = new OtelEventsRateLimitProcessor(options, threadSafeInner, timeProvider);

        const int threadCount = 10;
        const int eventsPerThread = 50;
        var barrier = new Barrier(threadCount);

        var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < eventsPerThread; i++)
            {
                processor.OnEnd(CreateLogRecord(eventName: "concurrent.event"));
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // With limit of 100 and 500 total events, exactly 100 should pass.
        // Interlocked.Increment is precise; the thread-safe inner processor
        // captures all forwarded records without loss.
        Assert.Equal(100, threadSafeInner.Count);
    }

    [Fact]
    public async Task ConcurrentAccess_MultipleEventNames_IndependentCounters()
    {
        var timeProvider = new FakeTimeProvider();
        var options = new OtelEventsRateLimitOptions { DefaultMaxEventsPerWindow = 50 };
        var threadSafeInner = new ThreadSafeLogRecordProcessor();
        using var processor = new OtelEventsRateLimitProcessor(options, threadSafeInner, timeProvider);

        var tasks = new[]
        {
            Task.Run(() =>
            {
                for (var i = 0; i < 100; i++)
                    processor.OnEnd(CreateLogRecord(eventName: "event.a"));
            }),
            Task.Run(() =>
            {
                for (var i = 0; i < 100; i++)
                    processor.OnEnd(CreateLogRecord(eventName: "event.b"));
            })
        };

        await Task.WhenAll(tasks);

        // Each event name has its own counter with limit 50
        // Total should be exactly 100 (50 + 50)
        Assert.Equal(100, threadSafeInner.Count);
    }

    // ─── Options validation ──────────────────────────────────────────

    [Fact]
    public void NegativeDefaultMaxEventsPerWindow_ThrowsArgumentOutOfRangeException()
    {
        var options = new OtelEventsRateLimitOptions { DefaultMaxEventsPerWindow = -1 };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OtelEventsRateLimitProcessor(options, _innerProcessor));
    }

    [Fact]
    public void ZeroOrNegativeWindow_ThrowsArgumentOutOfRangeException()
    {
        var options = new OtelEventsRateLimitOptions { Window = TimeSpan.Zero };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OtelEventsRateLimitProcessor(options, _innerProcessor));
    }

    [Fact]
    public void NegativeWindow_ThrowsArgumentOutOfRangeException()
    {
        var options = new OtelEventsRateLimitOptions { Window = TimeSpan.FromSeconds(-1) };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OtelEventsRateLimitProcessor(options, _innerProcessor));
    }

    [Fact]
    public void EmptyEventLimitKey_ThrowsArgumentException()
    {
        var options = new OtelEventsRateLimitOptions
        {
            EventLimits = new Dictionary<string, int>
            {
                [""] = 10
            }
        };

        Assert.Throws<ArgumentException>(() =>
            new OtelEventsRateLimitProcessor(options, _innerProcessor));
    }

    [Fact]
    public void BareWildcard_ThrowsArgumentException()
    {
        var options = new OtelEventsRateLimitOptions
        {
            EventLimits = new Dictionary<string, int>
            {
                ["*"] = 10
            }
        };

        Assert.Throws<ArgumentException>(() =>
            new OtelEventsRateLimitProcessor(options, _innerProcessor));
    }

    [Fact]
    public void NegativeEventLimit_ThrowsArgumentOutOfRangeException()
    {
        var options = new OtelEventsRateLimitOptions
        {
            EventLimits = new Dictionary<string, int>
            {
                ["db.query.*"] = -5
            }
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OtelEventsRateLimitProcessor(options, _innerProcessor));
    }

    // ─── Constructor validation ───────────────────────────────────────

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new OtelEventsRateLimitProcessor(null!, new InMemoryLogRecordProcessor()));
    }

    [Fact]
    public void Constructor_NullInnerProcessor_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new OtelEventsRateLimitProcessor(new OtelEventsRateLimitOptions(), null!));
    }

    // ─── GetRateLimit resolution (internal) ───────────────────────────

    [Fact]
    public void GetRateLimit_ExactMatch_ReturnsExactLimit()
    {
        var options = new OtelEventsRateLimitOptions
        {
            DefaultMaxEventsPerWindow = 100,
            EventLimits = new Dictionary<string, int>
            {
                ["db.query.executed"] = 5
            }
        };
        using var processor = new OtelEventsRateLimitProcessor(options, _innerProcessor);

        var record = CreateLogRecord(eventName: "db.query.executed");
        Assert.Equal(5, processor.GetRateLimit(record));
    }

    [Fact]
    public void GetRateLimit_WildcardMatch_ReturnsWildcardLimit()
    {
        var options = new OtelEventsRateLimitOptions
        {
            DefaultMaxEventsPerWindow = 100,
            EventLimits = new Dictionary<string, int>
            {
                ["db.query.*"] = 10
            }
        };
        using var processor = new OtelEventsRateLimitProcessor(options, _innerProcessor);

        var record = CreateLogRecord(eventName: "db.query.slow");
        Assert.Equal(10, processor.GetRateLimit(record));
    }

    [Fact]
    public void GetRateLimit_NoMatch_ReturnsDefault()
    {
        var options = new OtelEventsRateLimitOptions
        {
            DefaultMaxEventsPerWindow = 42,
            EventLimits = new Dictionary<string, int>
            {
                ["db.query.*"] = 10
            }
        };
        using var processor = new OtelEventsRateLimitProcessor(options, _innerProcessor);

        var record = CreateLogRecord(eventName: "http.request.completed");
        Assert.Equal(42, processor.GetRateLimit(record));
    }

    [Fact]
    public void GetRateLimit_NoEventName_ReturnsDefault()
    {
        var options = new OtelEventsRateLimitOptions { DefaultMaxEventsPerWindow = 42 };
        using var processor = new OtelEventsRateLimitProcessor(options, _innerProcessor);

        var record = CreateLogRecord();
        Assert.Equal(42, processor.GetRateLimit(record));
    }

    // ─── Multiple windows over time ───────────────────────────────────

    [Fact]
    public void MultipleWindowCycles_EachWindowResetsIndependently()
    {
        var timeProvider = new FakeTimeProvider();
        var options = new OtelEventsRateLimitOptions
        {
            DefaultMaxEventsPerWindow = 2,
            Window = TimeSpan.FromSeconds(1)
        };
        using var processor = new OtelEventsRateLimitProcessor(options, _innerProcessor, timeProvider);

        // Window 1
        processor.OnEnd(CreateLogRecord(eventName: "test.event"));
        processor.OnEnd(CreateLogRecord(eventName: "test.event"));
        processor.OnEnd(CreateLogRecord(eventName: "test.event")); // dropped
        Assert.Equal(2, _innerProcessor.ProcessedRecords.Count);

        // Window 2
        timeProvider.Advance(TimeSpan.FromSeconds(1.1));
        processor.OnEnd(CreateLogRecord(eventName: "test.event"));
        processor.OnEnd(CreateLogRecord(eventName: "test.event"));
        processor.OnEnd(CreateLogRecord(eventName: "test.event")); // dropped
        Assert.Equal(4, _innerProcessor.ProcessedRecords.Count);

        // Window 3
        timeProvider.Advance(TimeSpan.FromSeconds(1.1));
        processor.OnEnd(CreateLogRecord(eventName: "test.event"));
        processor.OnEnd(CreateLogRecord(eventName: "test.event"));
        Assert.Equal(6, _innerProcessor.ProcessedRecords.Count);
    }

    // ─── Custom window duration ───────────────────────────────────────

    [Fact]
    public void CustomWindow_LongerDuration_SpansCorrectly()
    {
        var timeProvider = new FakeTimeProvider();
        var options = new OtelEventsRateLimitOptions
        {
            DefaultMaxEventsPerWindow = 5,
            Window = TimeSpan.FromSeconds(10)
        };
        using var processor = new OtelEventsRateLimitProcessor(options, _innerProcessor, timeProvider);

        // Send 5 events (at limit)
        for (var i = 0; i < 5; i++)
        {
            processor.OnEnd(CreateLogRecord(eventName: "test.event"));
        }
        Assert.Equal(5, _innerProcessor.ProcessedRecords.Count);

        // 6th event within 10-second window — dropped
        timeProvider.Advance(TimeSpan.FromSeconds(5));
        processor.OnEnd(CreateLogRecord(eventName: "test.event"));
        Assert.Equal(5, _innerProcessor.ProcessedRecords.Count);

        // After 10 seconds — window resets
        timeProvider.Advance(TimeSpan.FromSeconds(6));
        processor.OnEnd(CreateLogRecord(eventName: "test.event"));
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
    /// Fake <see cref="TimeProvider"/> for deterministic time control in tests.
    /// Timestamps use <see cref="TimeSpan.TicksPerSecond"/> as the frequency,
    /// so advancing by <c>TimeSpan.FromSeconds(1)</c> advances by exactly
    /// <c>TimeSpan.TicksPerSecond</c> timestamp units.
    /// </summary>
    private sealed class FakeTimeProvider : TimeProvider
    {
        private long _timestamp;

        public override long GetTimestamp() => _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public void Advance(TimeSpan duration) =>
            Interlocked.Add(ref _timestamp, duration.Ticks);
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
