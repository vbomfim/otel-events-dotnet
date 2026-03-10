using OtelEvents.Causality;
using Grpc.Core;
using Grpc.Core.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OtelEvents.Grpc;
using OtelEvents.Grpc.Events;

namespace OtelEvents.Grpc.Tests;

/// <summary>
/// Unit tests for OtelEventsGrpcServerInterceptor.
/// Uses Grpc.Core.Testing.TestServerCallContext to create mock contexts
/// and verifies all three gRPC lifecycle events are emitted correctly.
/// </summary>
public class OtelEventsGrpcServerInterceptorTests
{
    // ─── Test Infrastructure ────────────────────────────────────────────

    private static (OtelEventsGrpcServerInterceptor Interceptor, TestLogExporter Exporter)
        CreateInterceptor(Action<OtelEventsGrpcOptions>? configure = null)
    {
        var exporter = new TestLogExporter();
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.ParseStateValues = true;
                options.AddProcessor(new OtelEventsCausalityProcessor());
                options.AddProcessor(new SimpleLogRecordExportProcessor(exporter));
            });
        });

        if (configure is not null)
        {
            services.AddOtelEventsGrpc(configure);
        }
        else
        {
            services.AddOtelEventsGrpc();
        }

        var provider = services.BuildServiceProvider();
        var interceptor = provider.GetRequiredService<OtelEventsGrpcServerInterceptor>();

        return (interceptor, exporter);
    }

    private static ServerCallContext CreateMockContext(string method = "/greet.Greeter/SayHello")
    {
        return TestServerCallContext.Create(
            method: method,
            host: "localhost",
            deadline: DateTime.UtcNow.AddMinutes(1),
            requestHeaders: new Metadata(),
            cancellationToken: CancellationToken.None,
            peer: "ipv4:127.0.0.1:50051",
            authContext: null,
            contextPropagationToken: null,
            writeHeadersFunc: _ => Task.CompletedTask,
            writeOptionsGetter: () => new WriteOptions(),
            writeOptionsSetter: _ => { });
    }

    /// <summary>OtelEvents event names for filtering.</summary>
    private static readonly HashSet<string> GrpcEventNames =
    [
        "grpc.call.started",
        "grpc.call.completed",
        "grpc.call.failed"
    ];

    private static List<TestLogRecord> GetGrpcEvents(TestLogExporter exporter) =>
        exporter.LogRecords.Where(r => r.EventName is not null && GrpcEventNames.Contains(r.EventName)).ToList();

    // ─── Event Emission Tests ───────────────────────────────────────────

    [Fact]
    public async Task UnaryServerHandler_EmitsStartedEvent()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateMockContext();

        // Act
        await interceptor.UnaryServerHandler<string, string>(
            "request",
            context,
            (req, ctx) => Task.FromResult("response"));

        // Assert
        exporter.AssertEventEmitted("grpc.call.started");
        var record = exporter.AssertSingle("grpc.call.started");
        Assert.Equal(LogLevel.Information, record.LogLevel);
    }

    [Fact]
    public async Task UnaryServerHandler_EmitsCompletedEvent()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateMockContext();

        // Act
        await interceptor.UnaryServerHandler<string, string>(
            "request",
            context,
            (req, ctx) => Task.FromResult("response"));

        // Assert
        exporter.AssertEventEmitted("grpc.call.completed");
        var record = exporter.AssertSingle("grpc.call.completed");
        Assert.Equal(LogLevel.Information, record.LogLevel);
    }

    [Fact]
    public async Task UnaryServerHandler_EmitsFailedEvent_OnRpcException()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateMockContext();

        // Act & Assert
        await Assert.ThrowsAsync<RpcException>(async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "request",
                context,
                (_, _) => throw new RpcException(new Status(StatusCode.NotFound, "Not found"))));

        // Assert
        exporter.AssertEventEmitted("grpc.call.failed");
        var record = exporter.AssertSingle("grpc.call.failed");
        Assert.Equal(LogLevel.Error, record.LogLevel);
    }

    [Fact]
    public async Task UnaryServerHandler_EmitsFailedEvent_OnGenericException()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateMockContext();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "request",
                context,
                (_, _) => throw new InvalidOperationException("Test error")));

        // Assert
        exporter.AssertEventEmitted("grpc.call.failed");
        var record = exporter.AssertSingle("grpc.call.failed");
        record.AssertAttribute("ErrorType", "InvalidOperationException");
    }

    [Fact]
    public async Task UnaryServerHandler_RethrowsException()
    {
        // Arrange
        var (interceptor, _) = CreateInterceptor();
        var context = CreateMockContext();

        // Act & Assert — exception must be re-thrown, not swallowed
        var ex = await Assert.ThrowsAsync<RpcException>(async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "request",
                context,
                (_, _) => throw new RpcException(new Status(StatusCode.Internal, "Server error"))));

        Assert.Equal(StatusCode.Internal, ex.StatusCode);
    }

    // ─── Event Field Tests ──────────────────────────────────────────────

    [Fact]
    public async Task StartedEvent_CapturesServiceName()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateMockContext("/greet.Greeter/SayHello");

        // Act
        await interceptor.UnaryServerHandler<string, string>(
            "request", context, (_, _) => Task.FromResult("response"));

        // Assert
        var record = exporter.AssertSingle("grpc.call.started");
        record.AssertAttribute("GrpcService", "greet.Greeter");
    }

    [Fact]
    public async Task StartedEvent_CapturesMethodName()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateMockContext("/greet.Greeter/SayHello");

        // Act
        await interceptor.UnaryServerHandler<string, string>(
            "request", context, (_, _) => Task.FromResult("response"));

        // Assert
        var record = exporter.AssertSingle("grpc.call.started");
        record.AssertAttribute("GrpcMethod", "SayHello");
    }

    [Fact]
    public async Task StartedEvent_CapturesServerSide()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateMockContext();

        // Act
        await interceptor.UnaryServerHandler<string, string>(
            "request", context, (_, _) => Task.FromResult("response"));

        // Assert
        var record = exporter.AssertSingle("grpc.call.started");
        record.AssertAttribute("GrpcSide", "Server");
    }

    [Fact]
    public async Task CompletedEvent_CapturesDuration()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateMockContext();

        // Act
        await interceptor.UnaryServerHandler<string, string>(
            "request", context, (_, _) => Task.FromResult("response"));

        // Assert
        var record = exporter.AssertSingle("grpc.call.completed");
        Assert.True(record.Attributes.ContainsKey("DurationMs"),
            "Should capture call duration");
        var durationMs = (double)record.Attributes["DurationMs"]!;
        Assert.True(durationMs >= 0, $"Duration should be non-negative, was {durationMs}");
    }

    [Fact]
    public async Task CompletedEvent_CapturesStatusCode()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateMockContext();

        // Act
        await interceptor.UnaryServerHandler<string, string>(
            "request", context, (_, _) => Task.FromResult("response"));

        // Assert
        var record = exporter.AssertSingle("grpc.call.completed");
        Assert.True(record.Attributes.ContainsKey("GrpcStatusCode"),
            "Should capture gRPC status code");
    }

    [Fact]
    public async Task FailedEvent_CapturesRpcStatusCode()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateMockContext();

        // Act
        await Assert.ThrowsAsync<RpcException>(async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "request", context,
                (_, _) => throw new RpcException(new Status(StatusCode.NotFound, "Not found"))));

        // Assert
        var record = exporter.AssertSingle("grpc.call.failed");
        record.AssertAttribute("grpcStatusCode", (int)StatusCode.NotFound);
    }

    [Fact]
    public async Task FailedEvent_CapturesStatusDetail()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateMockContext();

        // Act
        await Assert.ThrowsAsync<RpcException>(async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "request", context,
                (_, _) => throw new RpcException(new Status(StatusCode.NotFound, "Item not found"))));

        // Assert
        var record = exporter.AssertSingle("grpc.call.failed");
        record.AssertAttribute("grpcStatusDetail", "Item not found");
    }

    [Fact]
    public async Task FailedEvent_CapturesException()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateMockContext();

        // Act
        await Assert.ThrowsAsync<RpcException>(async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "request", context,
                (_, _) => throw new RpcException(new Status(StatusCode.Internal, "Server error"))));

        // Assert
        var record = exporter.AssertSingle("grpc.call.failed");
        Assert.NotNull(record.Exception);
        Assert.IsType<RpcException>(record.Exception);
    }

    [Fact]
    public async Task FailedEvent_CapturesDuration()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateMockContext();

        // Act
        await Assert.ThrowsAsync<RpcException>(async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "request", context,
                (_, _) => throw new RpcException(new Status(StatusCode.Internal, "error"))));

        // Assert
        var record = exporter.AssertSingle("grpc.call.failed");
        Assert.True(record.Attributes.ContainsKey("DurationMs"));
        var durationMs = (double)record.Attributes["DurationMs"]!;
        Assert.True(durationMs >= 0, $"Duration should be non-negative, was {durationMs}");
    }

    // ─── Event ID Tests ─────────────────────────────────────────────────

    [Fact]
    public async Task Events_HaveCorrectEventIds()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateMockContext();

        // Act
        await interceptor.UnaryServerHandler<string, string>(
            "request", context, (_, _) => Task.FromResult("response"));

        // Assert
        var started = exporter.AssertSingle("grpc.call.started");
        Assert.Equal(10101, started.EventId.Id);
        Assert.Equal("grpc.call.started", started.EventId.Name);

        var completed = exporter.AssertSingle("grpc.call.completed");
        Assert.Equal(10102, completed.EventId.Id);
        Assert.Equal("grpc.call.completed", completed.EventId.Name);
    }

    [Fact]
    public async Task FailedEvent_HasCorrectEventId()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateMockContext();

        // Act
        await Assert.ThrowsAsync<RpcException>(async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "request", context,
                (_, _) => throw new RpcException(new Status(StatusCode.Internal, "error"))));

        // Assert
        var failed = exporter.AssertSingle("grpc.call.failed");
        Assert.Equal(10103, failed.EventId.Id);
        Assert.Equal("grpc.call.failed", failed.EventId.Name);
    }

    // ─── ExcludeServices Tests ──────────────────────────────────────────

    [Fact]
    public async Task ExcludeServices_SkipsExcludedService()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor(options =>
        {
            options.ExcludeServices = ["grpc.health.v1.Health"];
        });
        var context = CreateMockContext("/grpc.health.v1.Health/Check");

        // Act
        await interceptor.UnaryServerHandler<string, string>(
            "request", context, (_, _) => Task.FromResult("response"));

        // Assert — no events for excluded service
        Assert.Empty(GetGrpcEvents(exporter));
    }

    [Fact]
    public async Task ExcludeServices_DoesNotAffectNonExcludedService()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor(options =>
        {
            options.ExcludeServices = ["grpc.health.v1.Health"];
        });
        var context = CreateMockContext("/greet.Greeter/SayHello");

        // Act
        await interceptor.UnaryServerHandler<string, string>(
            "request", context, (_, _) => Task.FromResult("response"));

        // Assert — events should be emitted for non-excluded service
        exporter.AssertEventEmitted("grpc.call.started");
        exporter.AssertEventEmitted("grpc.call.completed");
    }

    // ─── ExcludeMethods Tests ───────────────────────────────────────────

    [Fact]
    public async Task ExcludeMethods_SkipsExcludedMethod()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor(options =>
        {
            options.ExcludeMethods = ["/greet.Greeter/SayHello"];
        });
        var context = CreateMockContext("/greet.Greeter/SayHello");

        // Act
        await interceptor.UnaryServerHandler<string, string>(
            "request", context, (_, _) => Task.FromResult("response"));

        // Assert — no events for excluded method
        Assert.Empty(GetGrpcEvents(exporter));
    }

    // ─── Causal Scope Tests ─────────────────────────────────────────────

    [Fact]
    public async Task CausalScope_WhenEnabled_CompletedEventHasParentEventId()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor(options =>
        {
            options.EnableCausalScope = true;
        });
        var context = CreateMockContext();

        // Act
        await interceptor.UnaryServerHandler<string, string>(
            "request", context, (_, _) => Task.FromResult("response"));

        // Assert
        var completed = exporter.AssertSingle("grpc.call.completed");
        Assert.True(completed.Attributes.ContainsKey("otel_events.parent_event_id"),
            "Completed event should have otel_events.parent_event_id when causal scope is enabled");
    }

    [Fact]
    public async Task CausalScope_WhenDisabled_CompletedEventHasNoParentEventId()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor(options =>
        {
            options.EnableCausalScope = false;
        });
        var context = CreateMockContext();

        // Act
        await interceptor.UnaryServerHandler<string, string>(
            "request", context, (_, _) => Task.FromResult("response"));

        // Assert
        var completed = exporter.AssertSingle("grpc.call.completed");
        Assert.False(completed.Attributes.ContainsKey("otel_events.parent_event_id"),
            "Completed event should not have otel_events.parent_event_id when causal scope is disabled");
    }

    [Fact]
    public async Task CausalScope_StartedEventHasEventId()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateMockContext();

        // Act
        await interceptor.UnaryServerHandler<string, string>(
            "request", context, (_, _) => Task.FromResult("response"));

        // Assert — started event should have otel_events.event_id (from OtelEventsCausalityProcessor)
        var started = exporter.AssertSingle("grpc.call.started");
        Assert.True(started.Attributes.ContainsKey("otel_events.event_id"),
            "Started event should have otel_events.event_id");
        var eventId = started.Attributes["otel_events.event_id"] as string;
        Assert.NotNull(eventId);
        Assert.StartsWith("evt_", eventId);
    }

    // ─── Formatted Message Tests ────────────────────────────────────────

    [Fact]
    public async Task StartedEvent_HasFormattedMessage()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateMockContext("/greet.Greeter/SayHello");

        // Act
        await interceptor.UnaryServerHandler<string, string>(
            "request", context, (_, _) => Task.FromResult("response"));

        // Assert
        var record = exporter.AssertSingle("grpc.call.started");
        Assert.NotNull(record.FormattedMessage);
        Assert.Contains("Server", record.FormattedMessage);
        Assert.Contains("greet.Greeter", record.FormattedMessage);
        Assert.Contains("SayHello", record.FormattedMessage);
    }

    [Fact]
    public async Task CompletedEvent_HasFormattedMessage()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateMockContext();

        // Act
        await interceptor.UnaryServerHandler<string, string>(
            "request", context, (_, _) => Task.FromResult("response"));

        // Assert
        var record = exporter.AssertSingle("grpc.call.completed");
        Assert.NotNull(record.FormattedMessage);
        Assert.Contains("completed", record.FormattedMessage);
    }

    [Fact]
    public async Task FailedEvent_HasFormattedMessage()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateMockContext();

        // Act
        await Assert.ThrowsAsync<RpcException>(async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "request", context,
                (_, _) => throw new RpcException(new Status(StatusCode.Internal, "error"))));

        // Assert
        var record = exporter.AssertSingle("grpc.call.failed");
        Assert.NotNull(record.FormattedMessage);
        Assert.Contains("RpcException", record.FormattedMessage);
    }

    // ─── Streaming Handler Tests ────────────────────────────────────────

    [Fact]
    public async Task ServerStreamingHandler_EmitsStartedAndCompleted()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateMockContext("/greet.Greeter/SayHellos");

        // Act
        await interceptor.ServerStreamingServerHandler<string, string>(
            "request",
            new TestServerStreamWriter<string>(),
            context,
            (req, writer, ctx) => Task.CompletedTask);

        // Assert
        exporter.AssertEventEmitted("grpc.call.started");
        exporter.AssertEventEmitted("grpc.call.completed");
    }

    [Fact]
    public async Task ServerStreamingHandler_EmitsFailedOnException()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateMockContext();

        // Act
        await Assert.ThrowsAsync<RpcException>(async () =>
            await interceptor.ServerStreamingServerHandler<string, string>(
                "request",
                new TestServerStreamWriter<string>(),
                context,
                (_, _, _) => throw new RpcException(new Status(StatusCode.Internal, "error"))));

        // Assert
        exporter.AssertEventEmitted("grpc.call.failed");
    }

    [Fact]
    public async Task ClientStreamingHandler_EmitsStartedAndCompleted()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateMockContext("/greet.Greeter/CollectHellos");

        // Act
        await interceptor.ClientStreamingServerHandler<string, string>(
            new TestAsyncStreamReader<string>(),
            context,
            (reader, ctx) => Task.FromResult("aggregated"));

        // Assert
        exporter.AssertEventEmitted("grpc.call.started");
        exporter.AssertEventEmitted("grpc.call.completed");
    }

    [Fact]
    public async Task DuplexStreamingHandler_EmitsStartedAndCompleted()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateMockContext("/greet.Greeter/BidiHello");

        // Act
        await interceptor.DuplexStreamingServerHandler<string, string>(
            new TestAsyncStreamReader<string>(),
            new TestServerStreamWriter<string>(),
            context,
            (reader, writer, ctx) => Task.CompletedTask);

        // Assert
        exporter.AssertEventEmitted("grpc.call.started");
        exporter.AssertEventEmitted("grpc.call.completed");
    }

    // ─── Multiple Calls Test ────────────────────────────────────────────

    [Fact]
    public async Task MultipleCalls_EachGetsOwnEvents()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();

        // Act
        await interceptor.UnaryServerHandler<string, string>(
            "req1", CreateMockContext("/svc.A/Method1"),
            (_, _) => Task.FromResult("resp1"));
        await interceptor.UnaryServerHandler<string, string>(
            "req2", CreateMockContext("/svc.B/Method2"),
            (_, _) => Task.FromResult("resp2"));

        // Assert — should have 2 started + 2 completed
        var started = exporter.LogRecords.Where(r => r.EventName == "grpc.call.started").ToList();
        var completed = exporter.LogRecords.Where(r => r.EventName == "grpc.call.completed").ToList();
        Assert.Equal(2, started.Count);
        Assert.Equal(2, completed.Count);
    }
}

// ─── Test Doubles for Streaming ─────────────────────────────────────────

/// <summary>Minimal IServerStreamWriter for testing.</summary>
internal sealed class TestServerStreamWriter<T> : IServerStreamWriter<T>
{
    public WriteOptions? WriteOptions { get; set; }

    public Task WriteAsync(T message)
    {
        return Task.CompletedTask;
    }
}

/// <summary>Minimal IAsyncStreamReader for testing.</summary>
internal sealed class TestAsyncStreamReader<T> : IAsyncStreamReader<T>
{
    public T Current => default!;

    public Task<bool> MoveNext(CancellationToken cancellationToken)
    {
        return Task.FromResult(false);
    }
}
