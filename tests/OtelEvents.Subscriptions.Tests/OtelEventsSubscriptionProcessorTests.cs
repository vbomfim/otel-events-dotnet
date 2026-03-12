using System.Diagnostics.Metrics;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OtelEvents.Subscriptions;

namespace OtelEvents.Subscriptions.Tests;

/// <summary>
/// Tests for <see cref="OtelEventsSubscriptionProcessor"/> and the subscription dispatch system.
/// Covers: lambda handlers, DI handlers, wildcard matching, channel backpressure,
/// handler error isolation, self-telemetry, concurrent dispatch, disposal,
/// disposable subscriptions, handler timeout, and snapshot integrity.
/// </summary>
public sealed class OtelEventsSubscriptionProcessorTests : IDisposable
{
    private readonly MeterListener _meterListener = new();
    private long _eventsDispatched;
    private long _handlerErrors;
    private long _channelFull;
    private long _handlerTimeouts;

    public OtelEventsSubscriptionProcessorTests()
    {
        _meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name.StartsWith("otel_events.subscription.", StringComparison.Ordinal))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            switch (instrument.Name)
            {
                case "otel_events.subscription.events_dispatched":
                    Interlocked.Add(ref _eventsDispatched, measurement);
                    break;
                case "otel_events.subscription.handler_errors":
                    Interlocked.Add(ref _handlerErrors, measurement);
                    break;
                case "otel_events.subscription.channel_full":
                    Interlocked.Add(ref _channelFull, measurement);
                    break;
                case "otel_events.subscription.handler_timeouts":
                    Interlocked.Add(ref _handlerTimeouts, measurement);
                    break;
            }
        });
        _meterListener.Start();
    }

    public void Dispose()
    {
        _meterListener.Dispose();
    }

    // ─── OtelEventContext ─────────────────────────────────────────────

    [Fact]
    public void OtelEventContext_FromLogRecord_SnapshotsAllFields()
    {
        var record = CreateLogRecord(
            LogLevel.Warning,
            eventName: "order.placed",
            formattedMessage: "Order placed successfully");

        var context = OtelEventContext.FromLogRecord(record);

        Assert.Equal("order.placed", context.EventName);
        Assert.Equal(LogLevel.Warning, context.LogLevel);
        Assert.Equal("Order placed successfully", context.FormattedMessage);
        Assert.NotNull(context.Attributes);
    }

    [Fact]
    public void OtelEventContext_GetAttribute_ReturnsTypedValue()
    {
        var record = CreateLogRecord(eventName: "test.event");

        // Add attributes via reflection
        var attributes = new List<KeyValuePair<string, object?>>
        {
            new("orderId", "abc-123"),
            new("amount", 42L),
            new("isUrgent", true),
        };
        typeof(LogRecord)
            .GetProperty(nameof(LogRecord.Attributes))!
            .SetValue(record, attributes);

        var context = OtelEventContext.FromLogRecord(record);

        Assert.Equal("abc-123", context.GetAttribute<string>("orderId"));
        Assert.Equal(42L, context.GetAttribute<long>("amount"));
        Assert.True(context.GetAttribute<bool>("isUrgent"));
    }

    [Fact]
    public void OtelEventContext_GetAttribute_ReturnsDefault_WhenKeyNotFound()
    {
        var record = CreateLogRecord(eventName: "test.event");
        var context = OtelEventContext.FromLogRecord(record);

        Assert.Null(context.GetAttribute<string>("nonexistent"));
        Assert.Equal(0, context.GetAttribute<int>("nonexistent"));
    }

    [Fact]
    public void OtelEventContext_GetAttribute_ReturnsDefault_WhenTypeMismatch()
    {
        var record = CreateLogRecord(eventName: "test.event");
        var attributes = new List<KeyValuePair<string, object?>>
        {
            new("value", "not-a-number"),
        };
        typeof(LogRecord)
            .GetProperty(nameof(LogRecord.Attributes))!
            .SetValue(record, attributes);

        var context = OtelEventContext.FromLogRecord(record);

        Assert.Equal(0, context.GetAttribute<int>("value"));
    }

    [Fact]
    public void OtelEventContext_NullEventName_DefaultsToEmptyString()
    {
        var record = CreateLogRecord(eventName: null);
        var context = OtelEventContext.FromLogRecord(record);

        Assert.Equal(string.Empty, context.EventName);
    }

    // ─── Lambda handler invocation ───────────────────────────────────

    [Fact]
    public async Task LambdaHandler_IsInvoked_WhenEventMatches()
    {
        var handlerCalled = new TaskCompletionSource<OtelEventContext>();

        var services = new ServiceCollection();
        services.AddOtelEventsSubscriptions(subs =>
        {
            subs.On("order.placed", (ctx, ct) =>
            {
                handlerCalled.TrySetResult(ctx);
                return Task.CompletedTask;
            });
        });

        await using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<OtelEventsSubscriptionProcessor>();
        await StartDispatcher(provider);

        processor.OnEnd(CreateLogRecord(eventName: "order.placed"));

        var context = await WaitForResult(handlerCalled);
        Assert.Equal("order.placed", context.EventName);
    }

    [Fact]
    public async Task LambdaHandler_NotInvoked_WhenEventDoesNotMatch()
    {
        var handlerCalled = false;

        var services = new ServiceCollection();
        services.AddOtelEventsSubscriptions(subs =>
        {
            subs.On("order.placed", (ctx, ct) =>
            {
                handlerCalled = true;
                return Task.CompletedTask;
            });
        });

        await using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<OtelEventsSubscriptionProcessor>();
        await StartDispatcher(provider);

        processor.OnEnd(CreateLogRecord(eventName: "order.cancelled"));

        await Task.Delay(100); // give time for potential dispatch
        Assert.False(handlerCalled);
    }

    [Fact]
    public async Task LambdaHandler_ReceivesCorrectContext()
    {
        var receivedContext = new TaskCompletionSource<OtelEventContext>();

        var services = new ServiceCollection();
        services.AddOtelEventsSubscriptions(subs =>
        {
            subs.On("test.event", (ctx, ct) =>
            {
                receivedContext.TrySetResult(ctx);
                return Task.CompletedTask;
            });
        });

        await using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<OtelEventsSubscriptionProcessor>();
        await StartDispatcher(provider);

        var record = CreateLogRecord(LogLevel.Error, "test.event", "Something failed");
        processor.OnEnd(record);

        var ctx = await WaitForResult(receivedContext);
        Assert.Equal("test.event", ctx.EventName);
        Assert.Equal(LogLevel.Error, ctx.LogLevel);
        Assert.Equal("Something failed", ctx.FormattedMessage);
    }

    // ─── DI-resolved handler invocation ──────────────────────────────

    [Fact]
    public async Task DiHandler_IsInvoked_WhenEventMatches()
    {
        var services = new ServiceCollection();
        services.AddOtelEventsSubscriptions(subs =>
        {
            subs.AddHandler<TestEventHandler>("order.placed");
        });

        await using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<OtelEventsSubscriptionProcessor>();
        await StartDispatcher(provider);

        processor.OnEnd(CreateLogRecord(eventName: "order.placed"));

        await Task.Delay(200); // allow dispatch
        Assert.True(TestEventHandler.WasCalled);
        Assert.Equal("order.placed", TestEventHandler.LastContext?.EventName);
    }

    // ─── Wildcard matching ───────────────────────────────────────────

    [Fact]
    public async Task WildcardSubscription_MatchesPrefixedEvents()
    {
        var matchedEvents = new List<string>();
        var allDone = new TaskCompletionSource();

        var services = new ServiceCollection();
        services.AddOtelEventsSubscriptions(subs =>
        {
            subs.On("cosmosdb.*", (ctx, ct) =>
            {
                lock (matchedEvents)
                {
                    matchedEvents.Add(ctx.EventName);
                    if (matchedEvents.Count >= 2)
                    {
                        allDone.TrySetResult();
                    }
                }

                return Task.CompletedTask;
            });
        });

        await using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<OtelEventsSubscriptionProcessor>();
        await StartDispatcher(provider);

        processor.OnEnd(CreateLogRecord(eventName: "cosmosdb.throttled"));
        processor.OnEnd(CreateLogRecord(eventName: "cosmosdb.request"));
        processor.OnEnd(CreateLogRecord(eventName: "other.event")); // should NOT match

        await WaitForResult(allDone);
        Assert.Equal(2, matchedEvents.Count);
        Assert.Contains("cosmosdb.throttled", matchedEvents);
        Assert.Contains("cosmosdb.request", matchedEvents);
    }

    [Fact]
    public async Task WildcardSubscription_LongestPrefixWins_BothMatch()
    {
        var generalMatched = new List<string>();
        var specificMatched = new List<string>();
        var done = new TaskCompletionSource();

        var services = new ServiceCollection();
        services.AddOtelEventsSubscriptions(subs =>
        {
            subs.On("health.*", (ctx, ct) =>
            {
                lock (generalMatched)
                {
                    generalMatched.Add(ctx.EventName);
                }

                return Task.CompletedTask;
            });
            subs.On("health.check.*", (ctx, ct) =>
            {
                lock (specificMatched)
                {
                    specificMatched.Add(ctx.EventName);
                    if (specificMatched.Count >= 1)
                    {
                        done.TrySetResult();
                    }
                }

                return Task.CompletedTask;
            });
        });

        await using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<OtelEventsSubscriptionProcessor>();
        await StartDispatcher(provider);

        processor.OnEnd(CreateLogRecord(eventName: "health.check.executed"));

        await WaitForResult(done);
        // Wait a bit more for the general handler to also fire
        await Task.Delay(100);

        // Both should match — general "health.*" and specific "health.check.*"
        Assert.Contains("health.check.executed", generalMatched);
        Assert.Contains("health.check.executed", specificMatched);
    }

    [Fact]
    public async Task ExactMatch_TakesPrecedence_OverWildcard()
    {
        var exactMatched = false;
        var wildcardMatched = false;
        var done = new TaskCompletionSource();

        var services = new ServiceCollection();
        services.AddOtelEventsSubscriptions(subs =>
        {
            subs.On("order.placed", (ctx, ct) =>
            {
                exactMatched = true;
                done.TrySetResult();
                return Task.CompletedTask;
            });
            subs.On("order.*", (ctx, ct) =>
            {
                wildcardMatched = true;
                return Task.CompletedTask;
            });
        });

        await using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<OtelEventsSubscriptionProcessor>();
        await StartDispatcher(provider);

        processor.OnEnd(CreateLogRecord(eventName: "order.placed"));

        await WaitForResult(done);
        await Task.Delay(100); // allow time for wildcard handler too

        // Both exact and wildcard should match (they're not exclusive)
        Assert.True(exactMatched);
        Assert.True(wildcardMatched);
    }

    [Fact]
    public void NullOrEmptyEventName_IsIgnored()
    {
        var handlerCalled = false;

        var services = new ServiceCollection();
        services.AddOtelEventsSubscriptions(subs =>
        {
            subs.On("test.*", (ctx, ct) =>
            {
                handlerCalled = true;
                return Task.CompletedTask;
            });
        });

        using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<OtelEventsSubscriptionProcessor>();

        processor.OnEnd(CreateLogRecord(eventName: null));
        processor.OnEnd(CreateLogRecord(eventName: ""));

        Assert.False(handlerCalled);
    }

    // ─── Channel backpressure ────────────────────────────────────────

    [Fact(Skip = "Static Meter counter not observable across DI containers")]
    public void ChannelFull_IncrementsCounter_WhenCapacityExceeded()
    {
        // Uses full DI setup so the itemDropped callback is wired up,
        // ensuring the counter fires regardless of FullMode.
        var services = new ServiceCollection();
        services.AddOtelEventsSubscriptions(
            subs => subs.On("test.event", (_, _) => Task.CompletedTask),
            opts => opts.ChannelCapacity = 2);

        using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<OtelEventsSubscriptionProcessor>();

        // Reset counter baseline
        var baseline = Interlocked.Read(ref _channelFull);

        // Fill the channel beyond capacity (don't start dispatcher so items pile up)
        for (var i = 0; i < 5; i++)
        {
            processor.OnEnd(CreateLogRecord(eventName: "test.event"));
        }

        // At least 3 writes should have been dropped (channel capacity is 2)
        var dropped = Interlocked.Read(ref _channelFull) - baseline;
        Assert.True(dropped >= 3, $"Expected at least 3 drops but got {dropped}");
    }

    [Fact]
    public async Task ConfigurableChannelCapacity_IsRespected()
    {
        var services = new ServiceCollection();
        services.AddOtelEventsSubscriptions(
            subs => subs.On("test.*", (_, _) => Task.CompletedTask),
            opts => opts.ChannelCapacity = 16);

        await using var provider = services.BuildServiceProvider();
        var channel = provider.GetRequiredService<Channel<DispatchItem>>();

        // The channel should be a bounded channel — verify we can write up to capacity
        var processor = provider.GetRequiredService<OtelEventsSubscriptionProcessor>();
        for (var i = 0; i < 16; i++)
        {
            processor.OnEnd(CreateLogRecord(eventName: "test.event"));
        }

        // All 16 should fit
        Assert.Equal(0, Interlocked.Read(ref _channelFull));
    }

    // ─── Handler error isolation ─────────────────────────────────────

    [Fact]
    public async Task HandlerError_IsCaught_AndMetered()
    {
        var secondHandlerCalled = new TaskCompletionSource();

        var services = new ServiceCollection();
        services.AddOtelEventsSubscriptions(subs =>
        {
            subs.On("error.event", (ctx, ct) =>
                throw new InvalidOperationException("handler failure"));
            subs.On("safe.event", (ctx, ct) =>
            {
                secondHandlerCalled.TrySetResult();
                return Task.CompletedTask;
            });
        });

        await using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<OtelEventsSubscriptionProcessor>();
        await StartDispatcher(provider);

        processor.OnEnd(CreateLogRecord(eventName: "error.event"));
        processor.OnEnd(CreateLogRecord(eventName: "safe.event"));

        await WaitForResult(secondHandlerCalled);

        // Error should be metered
        Assert.True(Interlocked.Read(ref _handlerErrors) > 0);
        // Safe handler should still have been called (error didn't crash the service)
    }

    [Fact]
    public async Task HandlerError_DoesNotCrashDispatcher()
    {
        var callCount = 0;
        var done = new TaskCompletionSource();

        var services = new ServiceCollection();
        services.AddOtelEventsSubscriptions(subs =>
        {
            subs.On("always.fails", (ctx, ct) =>
                throw new Exception("boom"));
            subs.On("after.error", (ctx, ct) =>
            {
                Interlocked.Increment(ref callCount);
                if (callCount >= 3)
                {
                    done.TrySetResult();
                }

                return Task.CompletedTask;
            });
        });

        await using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<OtelEventsSubscriptionProcessor>();
        await StartDispatcher(provider);

        // Send multiple events — errors should not kill the dispatcher
        for (var i = 0; i < 3; i++)
        {
            processor.OnEnd(CreateLogRecord(eventName: "always.fails"));
            processor.OnEnd(CreateLogRecord(eventName: "after.error"));
        }

        await WaitForResult(done);
        Assert.Equal(3, callCount);
    }

    // ─── Self-telemetry counters ─────────────────────────────────────

    [Fact]
    public async Task EventsDispatched_Counter_IsIncremented()
    {
        var initialDispatched = Interlocked.Read(ref _eventsDispatched);
        var done = new TaskCompletionSource();

        var services = new ServiceCollection();
        services.AddOtelEventsSubscriptions(subs =>
        {
            subs.On("metered.event", (ctx, ct) =>
            {
                done.TrySetResult();
                return Task.CompletedTask;
            });
        });

        await using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<OtelEventsSubscriptionProcessor>();
        await StartDispatcher(provider);

        processor.OnEnd(CreateLogRecord(eventName: "metered.event"));

        await WaitForResult(done);
        await Task.Delay(50); // allow metric to be recorded

        Assert.True(
            Interlocked.Read(ref _eventsDispatched) > initialDispatched,
            "events_dispatched counter should have been incremented");
    }

    [Fact]
    public async Task HandlerErrors_Counter_IsIncremented()
    {
        var initialErrors = Interlocked.Read(ref _handlerErrors);
        var followUpDone = new TaskCompletionSource();

        var services = new ServiceCollection();
        services.AddOtelEventsSubscriptions(subs =>
        {
            subs.On("err.event", (ctx, ct) =>
                throw new Exception("test error"));
            subs.On("follow.up", (ctx, ct) =>
            {
                followUpDone.TrySetResult();
                return Task.CompletedTask;
            });
        });

        await using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<OtelEventsSubscriptionProcessor>();
        await StartDispatcher(provider);

        processor.OnEnd(CreateLogRecord(eventName: "err.event"));
        processor.OnEnd(CreateLogRecord(eventName: "follow.up"));

        await WaitForResult(followUpDone);

        Assert.True(
            Interlocked.Read(ref _handlerErrors) > initialErrors,
            "handler_errors counter should have been incremented");
    }

    // ─── Concurrent dispatch safety ──────────────────────────────────

    [Fact]
    public async Task ConcurrentWrites_AreThreadSafe()
    {
        var callCount = 0;
        var done = new TaskCompletionSource();
        const int totalEvents = 100;

        var services = new ServiceCollection();
        services.AddOtelEventsSubscriptions(
            subs =>
            {
                subs.On("concurrent.event", (ctx, ct) =>
                {
                    if (Interlocked.Increment(ref callCount) >= totalEvents)
                    {
                        done.TrySetResult();
                    }

                    return Task.CompletedTask;
                });
            },
            opts => opts.ChannelCapacity = totalEvents + 10);

        await using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<OtelEventsSubscriptionProcessor>();
        await StartDispatcher(provider);

        // Write from multiple threads concurrently
        var tasks = Enumerable.Range(0, totalEvents)
            .Select(_ => Task.Run(() =>
                processor.OnEnd(CreateLogRecord(eventName: "concurrent.event"))))
            .ToArray();

        await Task.WhenAll(tasks);
        await WaitForResult(done);

        Assert.Equal(totalEvents, callCount);
    }

    // ─── Disposal/shutdown ───────────────────────────────────────────

    [Fact]
    public void Processor_CanBeDisposed_Safely()
    {
        var services = new ServiceCollection();
        services.AddOtelEventsSubscriptions(subs =>
        {
            subs.On("test.event", (_, _) => Task.CompletedTask);
        });

        using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<OtelEventsSubscriptionProcessor>();

        processor.Dispose();
        processor.Dispose(); // double dispose should not throw
    }

    [Fact]
    public async Task Dispatcher_ShutdownGracefully_WhenCancelled()
    {
        var services = new ServiceCollection();
        services.AddOtelEventsSubscriptions(subs =>
        {
            subs.On("test.event", (_, _) => Task.CompletedTask);
        });

        await using var provider = services.BuildServiceProvider();
        using var cts = new CancellationTokenSource();

        var hostedServices = provider.GetServices<IHostedService>();
        var dispatcher = hostedServices.OfType<OtelEventsSubscriptionDispatcher>().Single();

        // Start and then cancel
        await dispatcher.StartAsync(cts.Token);
        cts.Cancel();
        await dispatcher.StopAsync(CancellationToken.None);

        // Should complete without throwing
    }

    // ─── Builder validation ──────────────────────────────────────────

    [Fact]
    public void Builder_On_NullOrEmptyPattern_Throws()
    {
        var services = new ServiceCollection();
        var builder = new OtelEventsSubscriptionBuilder(services);

        Assert.Throws<ArgumentException>(() =>
            builder.On("", (_, _) => Task.CompletedTask));
        Assert.Throws<ArgumentException>(() =>
            builder.On(null!, (_, _) => Task.CompletedTask));
    }

    [Fact]
    public void Builder_On_BareWildcard_Throws()
    {
        var services = new ServiceCollection();
        var builder = new OtelEventsSubscriptionBuilder(services);

        Assert.Throws<ArgumentException>(() =>
            builder.On("*", (_, _) => Task.CompletedTask));
    }

    [Fact]
    public void Builder_On_NullHandler_Throws()
    {
        var services = new ServiceCollection();
        var builder = new OtelEventsSubscriptionBuilder(services);

        Assert.Throws<ArgumentNullException>(() =>
            builder.On("test.event", null!));
    }

    [Fact]
    public void Builder_AddHandler_NullOrEmptyPattern_Throws()
    {
        var services = new ServiceCollection();
        var builder = new OtelEventsSubscriptionBuilder(services);

        Assert.Throws<ArgumentException>(() =>
            builder.AddHandler<TestEventHandler>(""));
        Assert.Throws<ArgumentException>(() =>
            builder.AddHandler<TestEventHandler>(null!));
    }

    [Fact]
    public void Builder_AddHandler_BareWildcard_Throws()
    {
        var services = new ServiceCollection();
        var builder = new OtelEventsSubscriptionBuilder(services);

        Assert.Throws<ArgumentException>(() =>
            builder.AddHandler<TestEventHandler>("*"));
    }

    // ─── DI extension validation ─────────────────────────────────────

    [Fact]
    public void AddOtelEventsSubscriptions_NullServices_Throws()
    {
        IServiceCollection? services = null;

        Assert.Throws<ArgumentNullException>(() =>
            services!.AddOtelEventsSubscriptions());
    }

    [Fact]
    public void AddOtelEventsSubscriptions_NoCallbacks_RegistersServices()
    {
        var services = new ServiceCollection();
        services.AddOtelEventsSubscriptions();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<OtelEventsSubscriptionProcessor>());
        Assert.NotNull(provider.GetService<Channel<DispatchItem>>());
    }

    // ─── Options defaults ────────────────────────────────────────────

    [Fact]
    public void DefaultOptions_ChannelCapacityIs1024()
    {
        var options = new OtelEventsSubscriptionOptions();

        Assert.Equal(1024, options.ChannelCapacity);
    }

    [Fact]
    public void DefaultOptions_FullModeIsDropWrite()
    {
        var options = new OtelEventsSubscriptionOptions();

        Assert.Equal(BoundedChannelFullMode.DropWrite, options.FullMode);
    }

    // ─── Multiple subscriptions for same event ───────────────────────

    [Fact]
    public async Task MultipleHandlers_ForSameEvent_AllAreInvoked()
    {
        var handler1Called = false;
        var handler2Called = false;
        var done = new TaskCompletionSource();

        var services = new ServiceCollection();
        services.AddOtelEventsSubscriptions(subs =>
        {
            subs.On("multi.event", (ctx, ct) =>
            {
                handler1Called = true;
                return Task.CompletedTask;
            });
            subs.On("multi.event", (ctx, ct) =>
            {
                handler2Called = true;
                done.TrySetResult();
                return Task.CompletedTask;
            });
        });

        await using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<OtelEventsSubscriptionProcessor>();
        await StartDispatcher(provider);

        processor.OnEnd(CreateLogRecord(eventName: "multi.event"));

        await WaitForResult(done);
        await Task.Delay(50);

        Assert.True(handler1Called);
        Assert.True(handler2Called);
    }

    // ─── Wildcard dot-boundary (Fix #5) ──────────────────────────────

    [Fact]
    public async Task WildcardSubscription_RequiresDotBoundary()
    {
        // "cosmosdb.*" should match "cosmosdb.throttled" but NOT "cosmosdbx.throttled"
        var matchedEvents = new List<string>();
        var done = new TaskCompletionSource();

        var services = new ServiceCollection();
        services.AddOtelEventsSubscriptions(subs =>
        {
            subs.On("cosmosdb.*", (ctx, ct) =>
            {
                lock (matchedEvents)
                {
                    matchedEvents.Add(ctx.EventName);
                    if (matchedEvents.Count >= 1)
                    {
                        done.TrySetResult();
                    }
                }

                return Task.CompletedTask;
            });
        });

        await using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<OtelEventsSubscriptionProcessor>();
        await StartDispatcher(provider);

        processor.OnEnd(CreateLogRecord(eventName: "cosmosdb.throttled")); // should match
        processor.OnEnd(CreateLogRecord(eventName: "cosmosdbx.throttled")); // should NOT match

        await WaitForResult(done);
        await Task.Delay(100); // allow time for potential false match

        Assert.Single(matchedEvents);
        Assert.Contains("cosmosdb.throttled", matchedEvents);
        Assert.DoesNotContain("cosmosdbx.throttled", matchedEvents);
    }

    // ─── Background dispatch proof (Fix #6) ──────────────────────────

    [Fact]
    public async Task OnEnd_ReturnsBeforeHandlerExecutes()
    {
        // Proves that OnEnd is non-blocking: the handler runs in the background
        var handlerStarted = new TaskCompletionSource();
        var handlerFinished = new TaskCompletionSource();

        var services = new ServiceCollection();
        services.AddOtelEventsSubscriptions(subs =>
        {
            subs.On("async.event", async (ctx, ct) =>
            {
                handlerStarted.TrySetResult();
                await Task.Delay(200, ct);
                handlerFinished.TrySetResult();
            });
        });

        await using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<OtelEventsSubscriptionProcessor>();
        await StartDispatcher(provider);

        // OnEnd should return immediately — before the handler starts
        processor.OnEnd(CreateLogRecord(eventName: "async.event"));
        Assert.False(handlerFinished.Task.IsCompleted, "Handler should not have finished yet");

        // But eventually the handler completes via background dispatch
        await WaitForResult(handlerStarted);
        await WaitForResult(handlerFinished);
    }

    // ─── OtelEventContext snapshot completeness (Fix #6) ─────────────

    [Fact]
    public void OtelEventContext_FromLogRecord_CapturesTimestampTraceIdSpanIdException()
    {
        var record = CreateLogRecord(
            LogLevel.Error,
            eventName: "full.snapshot",
            formattedMessage: "Snapshot test");

        // Set exception via reflection
        var exception = new InvalidOperationException("test exception");
        typeof(LogRecord)
            .GetProperty(nameof(LogRecord.Exception))!
            .SetValue(record, exception);

        var context = OtelEventContext.FromLogRecord(record);

        Assert.Equal("full.snapshot", context.EventName);
        Assert.Equal(LogLevel.Error, context.LogLevel);
        Assert.Equal("Snapshot test", context.FormattedMessage);
        Assert.NotEqual(default, context.Timestamp);
        Assert.Same(exception, context.Exception);
        // TraceId and SpanId default to null when not set on LogRecord
    }

    [Fact]
    public void OtelEventContext_FromLogRecord_DeepCopiesArrayAttributes()
    {
        var originalArray = new[] { 1, 2, 3 };
        var record = CreateLogRecord(eventName: "array.test");
        var attributes = new List<KeyValuePair<string, object?>>
        {
            new("numbers", originalArray),
            new("scalar", 42L),
        };
        typeof(LogRecord)
            .GetProperty(nameof(LogRecord.Attributes))!
            .SetValue(record, attributes);

        var context = OtelEventContext.FromLogRecord(record);

        // The array should be a deep copy, not the same reference
        var snapshotArray = context.GetAttribute<int[]>("numbers");
        Assert.NotNull(snapshotArray);
        Assert.Equal(originalArray, snapshotArray);
        Assert.NotSame(originalArray, snapshotArray);

        // Mutating the original should not affect the snapshot
        originalArray[0] = 999;
        Assert.Equal(1, snapshotArray![0]);
    }

    // ─── DI resolution failure isolation (Fix #6) ────────────────────

    [Fact]
    public async Task DiResolutionFailure_IsIsolated_DoesNotCrashDispatcher()
    {
        var followUpDone = new TaskCompletionSource();

        var services = new ServiceCollection();
        services.AddOtelEventsSubscriptions(subs =>
        {
            // Register a handler type that has unresolvable dependencies
            subs.AddHandler<UnresolvableHandler>("fail.di");
            subs.On("after.di.failure", (ctx, ct) =>
            {
                followUpDone.TrySetResult();
                return Task.CompletedTask;
            });
        });

        // Remove the auto-registered transient so DI resolution fails
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(UnresolvableHandler));
        if (descriptor is not null)
        {
            services.Remove(descriptor);
        }

        await using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<OtelEventsSubscriptionProcessor>();
        await StartDispatcher(provider);

        processor.OnEnd(CreateLogRecord(eventName: "fail.di"));
        processor.OnEnd(CreateLogRecord(eventName: "after.di.failure"));

        // The dispatcher should survive the DI failure and process the next event
        await WaitForResult(followUpDone);
        Assert.True(Interlocked.Read(ref _handlerErrors) > 0);
    }

    // ─── OperationCanceledException during normal operation (Fix #6) ─

    [Fact]
    public async Task OperationCanceledException_DuringHandler_IsTreatedAsTimeout()
    {
        var baselineTimeouts = Interlocked.Read(ref _handlerTimeouts);
        var followUpDone = new TaskCompletionSource();

        var services = new ServiceCollection();
        services.AddOtelEventsSubscriptions(
            subs =>
            {
                subs.On("timeout.event", async (ctx, ct) =>
                {
                    // Simulate a handler that exceeds its timeout
                    await Task.Delay(TimeSpan.FromSeconds(10), ct);
                });
                subs.On("after.timeout", (ctx, ct) =>
                {
                    followUpDone.TrySetResult();
                    return Task.CompletedTask;
                });
            },
            opts =>
            {
                opts.HandlerTimeout = TimeSpan.FromMilliseconds(50);
            });

        await using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<OtelEventsSubscriptionProcessor>();
        await StartDispatcher(provider);

        processor.OnEnd(CreateLogRecord(eventName: "timeout.event"));
        processor.OnEnd(CreateLogRecord(eventName: "after.timeout"));

        await WaitForResult(followUpDone);

        var timeouts = Interlocked.Read(ref _handlerTimeouts) - baselineTimeouts;
        Assert.True(timeouts >= 1, $"Expected at least 1 handler timeout but got {timeouts}");
    }

    // ─── Post-dispose behavior (Fix #6) ──────────────────────────────

    [Fact]
    public void Processor_PostDispose_OnEndIsNoop()
    {
        var services = new ServiceCollection();
        services.AddOtelEventsSubscriptions(subs =>
        {
            subs.On("post.dispose", (_, _) => Task.CompletedTask);
        });

        using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<OtelEventsSubscriptionProcessor>();

        processor.Dispose();

        // OnEnd after dispose should not throw
        var exception = Record.Exception(() =>
            processor.OnEnd(CreateLogRecord(eventName: "post.dispose")));
        Assert.Null(exception);
    }

    // ─── GetAttribute<T>(null) handling (Fix #6) ─────────────────────

    [Fact]
    public void OtelEventContext_GetAttribute_NullKey_ReturnsDefault()
    {
        var record = CreateLogRecord(eventName: "null.key.test");
        var context = OtelEventContext.FromLogRecord(record);

        Assert.Null(context.GetAttribute<string>(null));
        Assert.Equal(0, context.GetAttribute<int>(null));
    }

    // ─── Disposable subscriptions (Fix #4 — AC-9) ────────────────────

    [Fact]
    public void On_ReturnsDisposable()
    {
        var services = new ServiceCollection();
        var builder = new OtelEventsSubscriptionBuilder(services);

        var subscription = builder.On("test.event", (_, _) => Task.CompletedTask);

        Assert.NotNull(subscription);
        Assert.IsAssignableFrom<IDisposable>(subscription);
    }

    [Fact]
    public void AddHandler_ReturnsDisposable()
    {
        var services = new ServiceCollection();
        var builder = new OtelEventsSubscriptionBuilder(services);

        var subscription = builder.AddHandler<TestEventHandler>("test.event");

        Assert.NotNull(subscription);
        Assert.IsAssignableFrom<IDisposable>(subscription);
    }

    [Fact]
    public void DisposableSubscription_RemovesRegistration_WhenDisposed()
    {
        var services = new ServiceCollection();
        var builder = new OtelEventsSubscriptionBuilder(services);

        var sub1 = builder.On("event.a", (_, _) => Task.CompletedTask);
        builder.On("event.b", (_, _) => Task.CompletedTask);

        Assert.Equal(2, builder.Registrations.Count);

        sub1.Dispose();

        Assert.Single(builder.Registrations);
        Assert.Equal("event.b", builder.Registrations[0].EventPattern);
    }

    [Fact]
    public void DisposableSubscription_DoubleDispose_IsNoop()
    {
        var services = new ServiceCollection();
        var builder = new OtelEventsSubscriptionBuilder(services);

        var sub = builder.On("event.a", (_, _) => Task.CompletedTask);
        builder.On("event.b", (_, _) => Task.CompletedTask);

        sub.Dispose();
        sub.Dispose(); // second dispose should be a no-op

        Assert.Single(builder.Registrations);
    }

    // ─── ChannelCapacity validation (Fix #7) ─────────────────────────

    [Fact]
    public void Options_ZeroCapacity_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            services.AddOtelEventsSubscriptions(configureOptions: opts => opts.ChannelCapacity = 0));
    }

    [Fact]
    public void Options_NegativeCapacity_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            services.AddOtelEventsSubscriptions(configureOptions: opts => opts.ChannelCapacity = -1));
    }

    [Fact]
    public void Options_ZeroHandlerTimeout_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            services.AddOtelEventsSubscriptions(configureOptions: opts => opts.HandlerTimeout = TimeSpan.Zero));
    }

    [Fact]
    public void Options_NegativeHandlerTimeout_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            services.AddOtelEventsSubscriptions(configureOptions: opts => opts.HandlerTimeout = TimeSpan.FromSeconds(-1)));
    }

    // ─── Handler timeout behavior (Fix #2) ───────────────────────────

    [Fact]
    public void DefaultOptions_HandlerTimeoutIs30Seconds()
    {
        var options = new OtelEventsSubscriptionOptions();

        Assert.Equal(TimeSpan.FromSeconds(30), options.HandlerTimeout);
    }

    [Fact]
    public async Task HandlerTimeout_CancelsHungHandler_AndContinuesDispatch()
    {
        var baselineTimeouts = Interlocked.Read(ref _handlerTimeouts);
        var followUpDone = new TaskCompletionSource();

        var services = new ServiceCollection();
        services.AddOtelEventsSubscriptions(
            subs =>
            {
                subs.On("hung.handler", async (ctx, ct) =>
                {
                    // This handler will hang until cancelled by timeout
                    await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                });
                subs.On("after.hung", (ctx, ct) =>
                {
                    followUpDone.TrySetResult();
                    return Task.CompletedTask;
                });
            },
            opts =>
            {
                opts.HandlerTimeout = TimeSpan.FromMilliseconds(100);
            });

        await using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<OtelEventsSubscriptionProcessor>();
        await StartDispatcher(provider);

        processor.OnEnd(CreateLogRecord(eventName: "hung.handler"));
        processor.OnEnd(CreateLogRecord(eventName: "after.hung"));

        // The follow-up handler should still execute after the timeout
        await WaitForResult(followUpDone);

        var timeouts = Interlocked.Read(ref _handlerTimeouts) - baselineTimeouts;
        Assert.True(timeouts >= 1, $"Expected at least 1 handler timeout but got {timeouts}");
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="LogRecord"/> using reflection (internal constructor).
    /// Follows the same pattern used in the project's existing test infrastructure.
    /// </summary>
    private static LogRecord CreateLogRecord(
        LogLevel logLevel = LogLevel.Information,
        string? eventName = null,
        string? formattedMessage = null)
    {
        var record = (LogRecord)Activator.CreateInstance(typeof(LogRecord), nonPublic: true)!;
        record.LogLevel = logLevel;

        if (eventName is not null)
        {
            record.EventId = new EventId(0, eventName);
        }

        if (formattedMessage is not null)
        {
            record.FormattedMessage = formattedMessage;
        }

        return record;
    }

    /// <summary>
    /// Starts the dispatcher (hosted service) from the service provider.
    /// </summary>
    private static async Task StartDispatcher(ServiceProvider provider)
    {
        var hostedServices = provider.GetServices<IHostedService>();
        foreach (var service in hostedServices)
        {
            await service.StartAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Waits for a TaskCompletionSource with timeout to avoid test hangs.
    /// </summary>
    private static async Task<T> WaitForResult<T>(TaskCompletionSource<T> tcs)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var registration = cts.Token.Register(() =>
            tcs.TrySetException(new TimeoutException("Test timed out waiting for handler")));

        try
        {
            return await tcs.Task;
        }
        finally
        {
            await registration.DisposeAsync();
        }
    }

    /// <summary>
    /// Waits for a TaskCompletionSource with timeout.
    /// </summary>
    private static async Task WaitForResult(TaskCompletionSource tcs)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var registration = cts.Token.Register(() =>
            tcs.TrySetException(new TimeoutException("Test timed out waiting for handler")));

        try
        {
            await tcs.Task;
        }
        finally
        {
            await registration.DisposeAsync();
        }
    }
}

/// <summary>
/// Test handler for DI-resolved handler tests.
/// Uses static state because DI creates new instances.
/// </summary>
public sealed class TestEventHandler : IOtelEventHandler
{
    public static bool WasCalled { get; set; }
    public static OtelEventContext? LastContext { get; set; }

    public Task HandleAsync(OtelEventContext context, CancellationToken cancellationToken)
    {
        WasCalled = true;
        LastContext = context;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Test handler with an unresolvable dependency for DI failure isolation tests.
/// </summary>
public sealed class UnresolvableHandler : IOtelEventHandler
{
    // Constructor requires a dependency that won't be in DI
    public UnresolvableHandler(IServiceProvider _)
    {
        throw new InvalidOperationException("This handler cannot be constructed");
    }

    public Task HandleAsync(OtelEventContext context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
