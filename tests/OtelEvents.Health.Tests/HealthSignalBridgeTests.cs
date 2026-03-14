// <copyright file="HealthSignalBridgeTests.cs" company="OtelEvents">
// Copyright (c) OtelEvents. All rights reserved.
// </copyright>

using FluentAssertions;
using Microsoft.Extensions.Logging;
using OtelEvents.Health.Contracts;
using OtelEvents.Schema.Models;
using OtelEvents.Subscriptions;

namespace OtelEvents.Health.Tests;

/// <summary>
/// Unit tests for <see cref="HealthSignalBridge"/> — the auto-subscribe bridge
/// that connects otel-events subscriptions to the Health state machine.
/// Tests cover outcome classification, match filtering, latency extraction,
/// signal recording, and subscription registration.
/// </summary>
public sealed class HealthSignalBridgeTests
{
    // ═══════════════════════════════════════════════════════════════
    // Test infrastructure
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Simple recording stub for <see cref="ISignalRecorder"/>.
    /// Captures all recorded signals for assertion.
    /// </summary>
    private sealed class RecordingSignalRecorder : ISignalRecorder
    {
        public List<(DependencyId Id, HealthSignal Signal)> Recorded { get; } = [];

        public void RecordSignal(DependencyId id, HealthSignal signal)
        {
            Recorded.Add((id, signal));
        }
    }

    /// <summary>
    /// Creates an <see cref="OtelEventContext"/> for testing with the given parameters.
    /// </summary>
    private static OtelEventContext CreateContext(
        string eventName,
        Dictionary<string, object?>? attributes = null,
        DateTimeOffset? timestamp = null)
    {
        return new OtelEventContext(
            eventName: eventName,
            logLevel: LogLevel.Information,
            formattedMessage: null,
            attributes: attributes ?? new Dictionary<string, object?>(),
            timestamp: timestamp ?? DateTimeOffset.UtcNow,
            traceId: null,
            spanId: null,
            exception: null);
    }

    /// <summary>
    /// Creates a minimal <see cref="ComponentDefinition"/> for testing.
    /// </summary>
    private static ComponentDefinition CreateComponent(
        string name,
        params SignalMapping[] signals)
    {
        return new ComponentDefinition
        {
            Name = name,
            WindowSeconds = 300,
            HealthyAbove = 0.9,
            DegradedAbove = 0.5,
            MinimumSignals = 5,
            CooldownSeconds = 30,
            Signals = [.. signals],
        };
    }

    /// <summary>
    /// Creates a <see cref="SignalMapping"/> for testing.
    /// </summary>
    private static SignalMapping CreateSignal(
        string eventName,
        Dictionary<string, string>? match = null)
    {
        return new SignalMapping
        {
            Event = eventName,
            Match = match ?? [],
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // 1. Outcome classification — ClassifyOutcome
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("http.request.failed", SignalOutcome.Failure)]
    [InlineData("cosmosdb.query.failed", SignalOutcome.Failure)]
    [InlineData("storage.upload.FAILED", SignalOutcome.Failure)]
    public void ClassifyOutcome_FailedSuffix_ReturnsFailure(string eventName, SignalOutcome expected)
    {
        var result = HealthSignalBridge.ClassifyOutcome(eventName);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("cosmosdb.throttled", SignalOutcome.Failure)]
    [InlineData("redis.THROTTLED", SignalOutcome.Failure)]
    public void ClassifyOutcome_ThrottledSuffix_ReturnsFailure(string eventName, SignalOutcome expected)
    {
        var result = HealthSignalBridge.ClassifyOutcome(eventName);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("http.request.completed", SignalOutcome.Success)]
    [InlineData("order.processing.COMPLETED", SignalOutcome.Success)]
    public void ClassifyOutcome_CompletedSuffix_ReturnsSuccess(string eventName, SignalOutcome expected)
    {
        var result = HealthSignalBridge.ClassifyOutcome(eventName);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("sql.query.executed", SignalOutcome.Success)]
    [InlineData("batch.job.EXECUTED", SignalOutcome.Success)]
    public void ClassifyOutcome_ExecutedSuffix_ReturnsSuccess(string eventName, SignalOutcome expected)
    {
        var result = HealthSignalBridge.ClassifyOutcome(eventName);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("http.request.started")]
    [InlineData("batch.job.STARTED")]
    public void ClassifyOutcome_StartedSuffix_ReturnsNull(string eventName)
    {
        var result = HealthSignalBridge.ClassifyOutcome(eventName);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("unknown.event")]
    [InlineData("no.suffix")]
    [InlineData("")]
    public void ClassifyOutcome_UnknownSuffix_ReturnsNull(string eventName)
    {
        var result = HealthSignalBridge.ClassifyOutcome(eventName);

        result.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. Match filter — MatchesFilters
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void MatchesFilters_EmptyMatch_ReturnsTrue()
    {
        var ctx = CreateContext("http.request.completed");
        var match = new Dictionary<string, string>();

        var result = HealthSignalBridge.MatchesFilters(match, ctx);

        result.Should().BeTrue();
    }

    [Fact]
    public void MatchesFilters_ExactMatch_ReturnsTrue()
    {
        var ctx = CreateContext("http.request.completed", new Dictionary<string, object?>
        {
            ["httpRoute"] = "/api/orders",
        });
        var match = new Dictionary<string, string> { ["httpRoute"] = "/api/orders" };

        var result = HealthSignalBridge.MatchesFilters(match, ctx);

        result.Should().BeTrue();
    }

    [Fact]
    public void MatchesFilters_ExactMismatch_ReturnsFalse()
    {
        var ctx = CreateContext("http.request.completed", new Dictionary<string, object?>
        {
            ["httpRoute"] = "/api/users",
        });
        var match = new Dictionary<string, string> { ["httpRoute"] = "/api/orders" };

        var result = HealthSignalBridge.MatchesFilters(match, ctx);

        result.Should().BeFalse();
    }

    [Fact]
    public void MatchesFilters_WildcardMatch_ReturnsTrue()
    {
        var ctx = CreateContext("http.request.completed", new Dictionary<string, object?>
        {
            ["httpRoute"] = "/api/orders/123",
        });
        var match = new Dictionary<string, string> { ["httpRoute"] = "/api/orders/*" };

        var result = HealthSignalBridge.MatchesFilters(match, ctx);

        result.Should().BeTrue();
    }

    [Fact]
    public void MatchesFilters_WildcardMismatch_ReturnsFalse()
    {
        var ctx = CreateContext("http.request.completed", new Dictionary<string, object?>
        {
            ["httpRoute"] = "/api/users/456",
        });
        var match = new Dictionary<string, string> { ["httpRoute"] = "/api/orders/*" };

        var result = HealthSignalBridge.MatchesFilters(match, ctx);

        result.Should().BeFalse();
    }

    [Fact]
    public void MatchesFilters_MissingAttribute_ReturnsFalse()
    {
        var ctx = CreateContext("http.request.completed"); // no attributes
        var match = new Dictionary<string, string> { ["httpRoute"] = "/api/orders" };

        var result = HealthSignalBridge.MatchesFilters(match, ctx);

        result.Should().BeFalse();
    }

    [Fact]
    public void MatchesFilters_MultipleFilters_AllMustMatch()
    {
        var ctx = CreateContext("http.request.completed", new Dictionary<string, object?>
        {
            ["httpRoute"] = "/api/orders",
            ["httpMethod"] = "POST",
        });
        var match = new Dictionary<string, string>
        {
            ["httpRoute"] = "/api/orders",
            ["httpMethod"] = "POST",
        };

        var result = HealthSignalBridge.MatchesFilters(match, ctx);

        result.Should().BeTrue();
    }

    [Fact]
    public void MatchesFilters_MultipleFilters_OneMismatch_ReturnsFalse()
    {
        var ctx = CreateContext("http.request.completed", new Dictionary<string, object?>
        {
            ["httpRoute"] = "/api/orders",
            ["httpMethod"] = "GET",
        });
        var match = new Dictionary<string, string>
        {
            ["httpRoute"] = "/api/orders",
            ["httpMethod"] = "POST",
        };

        var result = HealthSignalBridge.MatchesFilters(match, ctx);

        result.Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. Pattern matching — MatchesPattern
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("/api/orders/123", "/api/orders/*", true)]
    [InlineData("/api/orders/", "/api/orders/*", true)]
    [InlineData("/api/users/123", "/api/orders/*", false)]
    [InlineData("/api/orders", "/api/orders", true)]
    [InlineData("/api/orders", "/api/users", false)]
    [InlineData("anything", "*", true)]
    public void MatchesPattern_VariousInputs_ReturnsExpected(
        string value, string pattern, bool expected)
    {
        var result = HealthSignalBridge.MatchesPattern(value, pattern);

        result.Should().Be(expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // 4. Latency extraction — ExtractLatency
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ExtractLatency_DoubleDurationMs_ReturnsTimeSpan()
    {
        var ctx = CreateContext("test.completed", new Dictionary<string, object?>
        {
            ["durationMs"] = 150.5,
        });

        var result = HealthSignalBridge.ExtractLatency(ctx);

        result.Should().Be(TimeSpan.FromMilliseconds(150.5));
    }

    [Fact]
    public void ExtractLatency_LongDurationMs_ReturnsTimeSpan()
    {
        var ctx = CreateContext("test.completed", new Dictionary<string, object?>
        {
            ["durationMs"] = 200L,
        });

        var result = HealthSignalBridge.ExtractLatency(ctx);

        result.Should().Be(TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    public void ExtractLatency_IntDurationMs_ReturnsTimeSpan()
    {
        var ctx = CreateContext("test.completed", new Dictionary<string, object?>
        {
            ["durationMs"] = 100,
        });

        var result = HealthSignalBridge.ExtractLatency(ctx);

        result.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void ExtractLatency_StringDurationMs_ReturnsTimeSpan()
    {
        var ctx = CreateContext("test.completed", new Dictionary<string, object?>
        {
            ["durationMs"] = "75.5",
        });

        var result = HealthSignalBridge.ExtractLatency(ctx);

        result.Should().Be(TimeSpan.FromMilliseconds(75.5));
    }

    [Fact]
    public void ExtractLatency_NoDurationMs_ReturnsNull()
    {
        var ctx = CreateContext("test.completed");

        var result = HealthSignalBridge.ExtractLatency(ctx);

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractLatency_NonNumericDurationMs_ReturnsNull()
    {
        var ctx = CreateContext("test.completed", new Dictionary<string, object?>
        {
            ["durationMs"] = "not-a-number",
        });

        var result = HealthSignalBridge.ExtractLatency(ctx);

        result.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════════════════
    // 5. HandleSignal — end-to-end signal recording
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void HandleSignal_MatchingEvent_RecordsSignal()
    {
        var recorder = new RecordingSignalRecorder();
        var component = CreateComponent("orders-db",
            CreateSignal("http.request.completed"));
        var bridge = new HealthSignalBridge([component], recorder);

        var depId = new DependencyId("orders-db");
        var ctx = CreateContext("http.request.completed");

        bridge.HandleSignal(depId, component.Signals[0], ctx);

        recorder.Recorded.Should().ContainSingle();
        recorder.Recorded[0].Id.Should().Be(depId);
        recorder.Recorded[0].Signal.Outcome.Should().Be(SignalOutcome.Success);
    }

    [Fact]
    public void HandleSignal_NonMatchingFilter_DoesNotRecord()
    {
        var recorder = new RecordingSignalRecorder();
        var component = CreateComponent("orders-db",
            CreateSignal("http.request.completed",
                new Dictionary<string, string> { ["httpRoute"] = "/api/orders/*" }));
        var bridge = new HealthSignalBridge([component], recorder);

        var depId = new DependencyId("orders-db");
        var ctx = CreateContext("http.request.completed", new Dictionary<string, object?>
        {
            ["httpRoute"] = "/api/users/123",
        });

        bridge.HandleSignal(depId, component.Signals[0], ctx);

        recorder.Recorded.Should().BeEmpty();
    }

    [Fact]
    public void HandleSignal_MatchingFilter_RecordsSignal()
    {
        var recorder = new RecordingSignalRecorder();
        var component = CreateComponent("orders-db",
            CreateSignal("http.request.failed",
                new Dictionary<string, string> { ["httpRoute"] = "/api/orders/*" }));
        var bridge = new HealthSignalBridge([component], recorder);

        var depId = new DependencyId("orders-db");
        var ctx = CreateContext("http.request.failed", new Dictionary<string, object?>
        {
            ["httpRoute"] = "/api/orders/456",
        });

        bridge.HandleSignal(depId, component.Signals[0], ctx);

        recorder.Recorded.Should().ContainSingle();
        recorder.Recorded[0].Signal.Outcome.Should().Be(SignalOutcome.Failure);
    }

    [Fact]
    public void HandleSignal_UnclassifiableEvent_DoesNotRecord()
    {
        var recorder = new RecordingSignalRecorder();
        var component = CreateComponent("orders-db",
            CreateSignal("http.request.started"));
        var bridge = new HealthSignalBridge([component], recorder);

        var depId = new DependencyId("orders-db");
        var ctx = CreateContext("http.request.started");

        bridge.HandleSignal(depId, component.Signals[0], ctx);

        recorder.Recorded.Should().BeEmpty();
    }

    [Fact]
    public void HandleSignal_WithLatency_IncludesLatencyInSignal()
    {
        var recorder = new RecordingSignalRecorder();
        var component = CreateComponent("orders-db",
            CreateSignal("http.request.completed"));
        var bridge = new HealthSignalBridge([component], recorder);

        var depId = new DependencyId("orders-db");
        var ctx = CreateContext("http.request.completed", new Dictionary<string, object?>
        {
            ["durationMs"] = 250.0,
        });

        bridge.HandleSignal(depId, component.Signals[0], ctx);

        recorder.Recorded.Should().ContainSingle();
        recorder.Recorded[0].Signal.Latency.Should().Be(TimeSpan.FromMilliseconds(250));
    }

    [Fact]
    public void HandleSignal_WithoutLatency_SignalHasNullLatency()
    {
        var recorder = new RecordingSignalRecorder();
        var component = CreateComponent("orders-db",
            CreateSignal("http.request.completed"));
        var bridge = new HealthSignalBridge([component], recorder);

        var depId = new DependencyId("orders-db");
        var ctx = CreateContext("http.request.completed");

        bridge.HandleSignal(depId, component.Signals[0], ctx);

        recorder.Recorded.Should().ContainSingle();
        recorder.Recorded[0].Signal.Latency.Should().BeNull();
    }

    [Fact]
    public void HandleSignal_PreservesTimestamp()
    {
        var recorder = new RecordingSignalRecorder();
        var component = CreateComponent("orders-db",
            CreateSignal("http.request.completed"));
        var bridge = new HealthSignalBridge([component], recorder);

        var depId = new DependencyId("orders-db");
        var fixedTime = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var ctx = CreateContext("http.request.completed", timestamp: fixedTime);

        bridge.HandleSignal(depId, component.Signals[0], ctx);

        recorder.Recorded.Should().ContainSingle();
        recorder.Recorded[0].Signal.Timestamp.Should().Be(fixedTime);
    }

    [Fact]
    public void HandleSignal_NullRecorder_DoesNotThrow()
    {
        // Bridge created without recorder (unbound state)
        var component = CreateComponent("orders-db",
            CreateSignal("http.request.completed"));
        var bridge = new HealthSignalBridge([component]);

        var depId = new DependencyId("orders-db");
        var ctx = CreateContext("http.request.completed");

        var act = () => bridge.HandleSignal(depId, component.Signals[0], ctx);

        act.Should().NotThrow();
    }

    // ═══════════════════════════════════════════════════════════════
    // 6. RegisterSubscriptions — subscription registration
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void RegisterSubscriptions_NoComponents_NoRegistrations()
    {
        var recorder = new RecordingSignalRecorder();
        var bridge = new HealthSignalBridge([], recorder);
        var builder = new OtelEventsSubscriptionBuilder(
            new Microsoft.Extensions.DependencyInjection.ServiceCollection());

        bridge.RegisterSubscriptions(builder);

        builder.Registrations.Should().BeEmpty();
    }

    [Fact]
    public void RegisterSubscriptions_SingleComponentMultipleSignals_RegistersAll()
    {
        var recorder = new RecordingSignalRecorder();
        var component = CreateComponent("orders-db",
            CreateSignal("http.request.completed"),
            CreateSignal("http.request.failed"));
        var bridge = new HealthSignalBridge([component], recorder);
        var builder = new OtelEventsSubscriptionBuilder(
            new Microsoft.Extensions.DependencyInjection.ServiceCollection());

        bridge.RegisterSubscriptions(builder);

        builder.Registrations.Should().HaveCount(2);
        builder.Registrations[0].EventPattern.Should().Be("http.request.completed");
        builder.Registrations[1].EventPattern.Should().Be("http.request.failed");
    }

    [Fact]
    public void RegisterSubscriptions_MultipleComponents_RegistersAll()
    {
        var recorder = new RecordingSignalRecorder();
        var comp1 = CreateComponent("orders-db",
            CreateSignal("order.completed"));
        var comp2 = CreateComponent("payments-api",
            CreateSignal("payment.completed"),
            CreateSignal("payment.failed"));
        var bridge = new HealthSignalBridge([comp1, comp2], recorder);
        var builder = new OtelEventsSubscriptionBuilder(
            new Microsoft.Extensions.DependencyInjection.ServiceCollection());

        bridge.RegisterSubscriptions(builder);

        builder.Registrations.Should().HaveCount(3);
    }

    // ═══════════════════════════════════════════════════════════════
    // 7. End-to-end via RegisterSubscriptions → fire event → signal recorded
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task RegisteredSubscription_EventFires_SignalRecordedInCorrectComponent()
    {
        var recorder = new RecordingSignalRecorder();
        var comp1 = CreateComponent("orders-db",
            CreateSignal("order.completed"));
        var comp2 = CreateComponent("payments-api",
            CreateSignal("payment.failed"));
        var bridge = new HealthSignalBridge([comp1, comp2], recorder);

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var builder = new OtelEventsSubscriptionBuilder(services);
        bridge.RegisterSubscriptions(builder);

        // Simulate event dispatch by invoking the registered lambda
        var orderCtx = CreateContext("order.completed");
        await builder.Registrations[0].LambdaHandler!(orderCtx, CancellationToken.None);

        recorder.Recorded.Should().ContainSingle();
        recorder.Recorded[0].Id.Value.Should().Be("orders-db");
        recorder.Recorded[0].Signal.Outcome.Should().Be(SignalOutcome.Success);
    }

    [Fact]
    public async Task RegisteredSubscription_FailedEvent_RecordsFailure()
    {
        var recorder = new RecordingSignalRecorder();
        var component = CreateComponent("payments-api",
            CreateSignal("payment.failed"));
        var bridge = new HealthSignalBridge([component], recorder);

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var builder = new OtelEventsSubscriptionBuilder(services);
        bridge.RegisterSubscriptions(builder);

        var ctx = CreateContext("payment.failed");
        await builder.Registrations[0].LambdaHandler!(ctx, CancellationToken.None);

        recorder.Recorded.Should().ContainSingle();
        recorder.Recorded[0].Signal.Outcome.Should().Be(SignalOutcome.Failure);
    }

    [Fact]
    public async Task RegisteredSubscription_MatchFilter_OnlyRecordsMatching()
    {
        var recorder = new RecordingSignalRecorder();
        var component = CreateComponent("orders-db",
            CreateSignal("http.request.completed",
                new Dictionary<string, string> { ["httpRoute"] = "/api/orders/*" }));
        var bridge = new HealthSignalBridge([component], recorder);

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var builder = new OtelEventsSubscriptionBuilder(services);
        bridge.RegisterSubscriptions(builder);

        // Matching event
        var matchCtx = CreateContext("http.request.completed", new Dictionary<string, object?>
        {
            ["httpRoute"] = "/api/orders/123",
        });
        await builder.Registrations[0].LambdaHandler!(matchCtx, CancellationToken.None);

        // Non-matching event
        var noMatchCtx = CreateContext("http.request.completed", new Dictionary<string, object?>
        {
            ["httpRoute"] = "/api/users/123",
        });
        await builder.Registrations[0].LambdaHandler!(noMatchCtx, CancellationToken.None);

        recorder.Recorded.Should().ContainSingle("only the matching event should be recorded");
    }

    // ═══════════════════════════════════════════════════════════════
    // 8. Bind — late binding for DI
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Bind_SetsRecorder_AllowsHandleSignal()
    {
        var recorder = new RecordingSignalRecorder();
        var component = CreateComponent("orders-db",
            CreateSignal("http.request.completed"));
        var bridge = new HealthSignalBridge([component]);

        // Before bind: signal is silently dropped
        var depId = new DependencyId("orders-db");
        var ctx1 = CreateContext("http.request.completed");
        bridge.HandleSignal(depId, component.Signals[0], ctx1);
        recorder.Recorded.Should().BeEmpty();

        // Bind recorder
        bridge.Bind(recorder);

        // After bind: signal is recorded
        var ctx2 = CreateContext("http.request.completed");
        bridge.HandleSignal(depId, component.Signals[0], ctx2);
        recorder.Recorded.Should().ContainSingle();
    }
}
