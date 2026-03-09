using OtelEvents.Causality;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OtelEvents.Grpc;
using OtelEvents.Grpc.Events;

namespace OtelEvents.Grpc.Tests;

/// <summary>
/// Unit tests for OtelEventsGrpcClientInterceptor.
/// Creates mock method descriptors and continuation delegates to verify
/// all three gRPC lifecycle events are emitted correctly for client calls.
/// </summary>
public class OtelEventsGrpcClientInterceptorTests
{
    // ─── Test Infrastructure ────────────────────────────────────────────

    private static (OtelEventsGrpcClientInterceptor Interceptor, TestLogExporter Exporter)
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
        var interceptor = provider.GetRequiredService<OtelEventsGrpcClientInterceptor>();

        return (interceptor, exporter);
    }

    /// <summary>Creates a test method descriptor for unary calls.</summary>
    private static Method<string, string> CreateMethod(
        string serviceName = "greet.Greeter",
        string methodName = "SayHello")
    {
        return new Method<string, string>(
            type: MethodType.Unary,
            serviceName: serviceName,
            name: methodName,
            requestMarshaller: Marshallers.StringMarshaller,
            responseMarshaller: Marshallers.StringMarshaller);
    }

    /// <summary>Creates a client interceptor context for testing.</summary>
    private static ClientInterceptorContext<string, string> CreateClientContext(
        Method<string, string>? method = null)
    {
        method ??= CreateMethod();
        return new ClientInterceptorContext<string, string>(
            method,
            host: "localhost",
            options: new CallOptions());
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
    public async Task AsyncUnaryCall_EmitsStartedEvent()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateClientContext();

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request",
            context,
            (req, ctx) => CreateSuccessfulCall("response"));

        await call.ResponseAsync;

        // Assert
        exporter.AssertEventEmitted("grpc.call.started");
        var record = exporter.AssertSingle("grpc.call.started");
        Assert.Equal(LogLevel.Information, record.LogLevel);
    }

    [Fact]
    public async Task AsyncUnaryCall_EmitsCompletedEvent()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateClientContext();

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request",
            context,
            (req, ctx) => CreateSuccessfulCall("response"));

        await call.ResponseAsync;

        // Assert
        exporter.AssertEventEmitted("grpc.call.completed");
        var record = exporter.AssertSingle("grpc.call.completed");
        Assert.Equal(LogLevel.Information, record.LogLevel);
    }

    [Fact]
    public async Task AsyncUnaryCall_EmitsFailedEvent_OnRpcException()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateClientContext();

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request",
            context,
            (req, ctx) => CreateFailedCall(StatusCode.Unavailable, "Connection refused"));

        await Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);

        // Assert
        exporter.AssertEventEmitted("grpc.call.failed");
        var record = exporter.AssertSingle("grpc.call.failed");
        Assert.Equal(LogLevel.Error, record.LogLevel);
    }

    [Fact]
    public async Task AsyncUnaryCall_RethrowsException()
    {
        // Arrange
        var (interceptor, _) = CreateInterceptor();
        var context = CreateClientContext();

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request",
            context,
            (req, ctx) => CreateFailedCall(StatusCode.Internal, "Server error"));

        var ex = await Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);
        Assert.Equal(StatusCode.Internal, ex.StatusCode);
    }

    // ─── Event Field Tests ──────────────────────────────────────────────

    [Fact]
    public async Task StartedEvent_CapturesServiceName()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var method = CreateMethod("greet.Greeter", "SayHello");
        var context = CreateClientContext(method);

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context, (_, _) => CreateSuccessfulCall("response"));
        await call.ResponseAsync;

        // Assert
        var record = exporter.AssertSingle("grpc.call.started");
        record.AssertAttribute("GrpcService", "greet.Greeter");
    }

    [Fact]
    public async Task StartedEvent_CapturesMethodName()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var method = CreateMethod("greet.Greeter", "SayHello");
        var context = CreateClientContext(method);

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context, (_, _) => CreateSuccessfulCall("response"));
        await call.ResponseAsync;

        // Assert
        var record = exporter.AssertSingle("grpc.call.started");
        record.AssertAttribute("GrpcMethod", "SayHello");
    }

    [Fact]
    public async Task StartedEvent_CapturesClientSide()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateClientContext();

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context, (_, _) => CreateSuccessfulCall("response"));
        await call.ResponseAsync;

        // Assert
        var record = exporter.AssertSingle("grpc.call.started");
        record.AssertAttribute("GrpcSide", "Client");
    }

    [Fact]
    public async Task CompletedEvent_CapturesDuration()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateClientContext();

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context, (_, _) => CreateSuccessfulCall("response"));
        await call.ResponseAsync;

        // Assert
        var record = exporter.AssertSingle("grpc.call.completed");
        Assert.True(record.Attributes.ContainsKey("DurationMs"),
            "Should capture call duration");
        var durationMs = (double)record.Attributes["DurationMs"]!;
        Assert.True(durationMs >= 0, $"Duration should be non-negative, was {durationMs}");
    }

    [Fact]
    public async Task CompletedEvent_CapturesOkStatusCode()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateClientContext();

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context, (_, _) => CreateSuccessfulCall("response"));
        await call.ResponseAsync;

        // Assert
        var record = exporter.AssertSingle("grpc.call.completed");
        record.AssertAttribute("GrpcStatusCode", (int)StatusCode.OK);
    }

    [Fact]
    public async Task FailedEvent_CapturesRpcStatusCode()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateClientContext();

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context,
            (_, _) => CreateFailedCall(StatusCode.Unavailable, "Connection refused"));
        await Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);

        // Assert
        var record = exporter.AssertSingle("grpc.call.failed");
        record.AssertAttribute("grpcStatusCode", (int)StatusCode.Unavailable);
    }

    [Fact]
    public async Task FailedEvent_CapturesErrorType()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateClientContext();

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context,
            (_, _) => CreateFailedCall(StatusCode.Unavailable, "Connection refused"));
        await Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);

        // Assert
        var record = exporter.AssertSingle("grpc.call.failed");
        record.AssertAttribute("ErrorType", "RpcException");
    }

    [Fact]
    public async Task FailedEvent_CapturesException()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateClientContext();

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context,
            (_, _) => CreateFailedCall(StatusCode.Internal, "Server error"));
        await Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);

        // Assert
        var record = exporter.AssertSingle("grpc.call.failed");
        Assert.NotNull(record.Exception);
        Assert.IsType<RpcException>(record.Exception);
    }

    // ─── Event ID Tests ─────────────────────────────────────────────────

    [Fact]
    public async Task Events_HaveCorrectEventIds()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var context = CreateClientContext();

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context, (_, _) => CreateSuccessfulCall("response"));
        await call.ResponseAsync;

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
        var context = CreateClientContext();

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context,
            (_, _) => CreateFailedCall(StatusCode.Internal, "error"));
        await Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);

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
        var method = CreateMethod("grpc.health.v1.Health", "Check");
        var context = CreateClientContext(method);
        var called = false;

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context,
            (req, ctx) =>
            {
                called = true;
                return CreateSuccessfulCall("response");
            });
        await call.ResponseAsync;

        // Assert — no events for excluded service, but call still proceeds
        Assert.True(called, "Continuation should still be called for excluded services");
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
        var context = CreateClientContext();

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context, (_, _) => CreateSuccessfulCall("response"));
        await call.ResponseAsync;

        // Assert
        var completed = exporter.AssertSingle("grpc.call.completed");
        Assert.True(completed.Attributes.ContainsKey("all.parent_event_id"),
            "Completed event should have all.parent_event_id when causal scope is enabled");
    }

    [Fact]
    public async Task CausalScope_WhenDisabled_CompletedEventHasNoParentEventId()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor(options =>
        {
            options.EnableCausalScope = false;
        });
        var context = CreateClientContext();

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context, (_, _) => CreateSuccessfulCall("response"));
        await call.ResponseAsync;

        // Assert
        var completed = exporter.AssertSingle("grpc.call.completed");
        Assert.False(completed.Attributes.ContainsKey("all.parent_event_id"),
            "Completed event should not have all.parent_event_id when causal scope is disabled");
    }

    // ─── Formatted Message Tests ────────────────────────────────────────

    [Fact]
    public async Task StartedEvent_HasFormattedMessage()
    {
        // Arrange
        var (interceptor, exporter) = CreateInterceptor();
        var method = CreateMethod("greet.Greeter", "SayHello");
        var context = CreateClientContext(method);

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context, (_, _) => CreateSuccessfulCall("response"));
        await call.ResponseAsync;

        // Assert
        var record = exporter.AssertSingle("grpc.call.started");
        Assert.NotNull(record.FormattedMessage);
        Assert.Contains("Client", record.FormattedMessage);
        Assert.Contains("greet.Greeter", record.FormattedMessage);
        Assert.Contains("SayHello", record.FormattedMessage);
    }

    // ─── Helpers ────────────────────────────────────────────────────────

    private static AsyncUnaryCall<string> CreateSuccessfulCall(string response)
    {
        return new AsyncUnaryCall<string>(
            responseAsync: Task.FromResult(response),
            responseHeadersAsync: Task.FromResult(new Metadata()),
            getStatusFunc: () => new Status(StatusCode.OK, string.Empty),
            getTrailersFunc: () => new Metadata(),
            disposeAction: () => { });
    }

    private static AsyncUnaryCall<string> CreateFailedCall(StatusCode statusCode, string detail)
    {
        return new AsyncUnaryCall<string>(
            responseAsync: Task.FromException<string>(
                new RpcException(new Status(statusCode, detail))),
            responseHeadersAsync: Task.FromResult(new Metadata()),
            getStatusFunc: () => new Status(statusCode, detail),
            getTrailersFunc: () => new Metadata(),
            disposeAction: () => { });
    }
}
