using OtelEvents.Causality;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Core.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OtelEvents.Grpc;
using OtelEvents.Grpc.Events;

namespace OtelEvents.Grpc.Tests;

/// <summary>
/// Unit tests for gRPC infrastructure events (10104–10106).
/// Tests the supplemental events: connection.failed, auth.failed, throttled.
/// Covers both client and server interceptors.
/// </summary>
public class GrpcInfrastructureEventsTests
{
    // ─── Test Infrastructure ────────────────────────────────────────────

    private static (OtelEventsGrpcClientInterceptor Interceptor, TestLogExporter Exporter)
        CreateClientInterceptor(Action<OtelEventsGrpcOptions>? configure = null)
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

        services.AddOtelEventsGrpc(configure ?? (_ => { }));

        var provider = services.BuildServiceProvider();
        var interceptor = provider.GetRequiredService<OtelEventsGrpcClientInterceptor>();

        return (interceptor, exporter);
    }

    private static (OtelEventsGrpcServerInterceptor Interceptor, TestLogExporter Exporter)
        CreateServerInterceptor(Action<OtelEventsGrpcOptions>? configure = null)
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

        services.AddOtelEventsGrpc(configure ?? (_ => { }));

        var provider = services.BuildServiceProvider();
        var interceptor = provider.GetRequiredService<OtelEventsGrpcServerInterceptor>();

        return (interceptor, exporter);
    }

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

    private static ClientInterceptorContext<string, string> CreateClientContext(
        Method<string, string>? method = null,
        Metadata? headers = null)
    {
        method ??= CreateMethod();
        var callOptions = headers is not null
            ? new CallOptions(headers: headers)
            : new CallOptions();
        return new ClientInterceptorContext<string, string>(
            method,
            host: "localhost:5001",
            options: callOptions);
    }

    private static ServerCallContext CreateMockServerContext(string method = "/greet.Greeter/SayHello")
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

    private static AsyncUnaryCall<string> CreateFailedCall(
        StatusCode statusCode,
        string detail,
        Metadata? trailers = null)
    {
        var exception = trailers is not null
            ? new RpcException(new Status(statusCode, detail), trailers)
            : new RpcException(new Status(statusCode, detail));

        return new AsyncUnaryCall<string>(
            responseAsync: Task.FromException<string>(exception),
            responseHeadersAsync: Task.FromResult(new Metadata()),
            getStatusFunc: () => new Status(statusCode, detail),
            getTrailersFunc: () => trailers ?? new Metadata(),
            disposeAction: () => { });
    }

    /// <summary>Infrastructure event names for filtering.</summary>
    private static readonly HashSet<string> InfraEventNames =
    [
        "grpc.connection.failed",
        "grpc.auth.failed",
        "grpc.throttled"
    ];

    private static List<TestLogRecord> GetInfraEvents(TestLogExporter exporter) =>
        exporter.LogRecords
            .Where(r => r.EventName is not null && InfraEventNames.Contains(r.EventName))
            .ToList();

    // ─── grpc.connection.failed (10104) ─────────────────────────────────

    [Fact]
    public async Task Client_Unavailable_EmitsConnectionFailedEvent()
    {
        // Arrange
        var (interceptor, exporter) = CreateClientInterceptor();
        var context = CreateClientContext();

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context,
            (_, _) => CreateFailedCall(StatusCode.Unavailable, "Connection refused"));
        await Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);

        // Assert
        exporter.AssertEventEmitted("grpc.connection.failed");
    }

    [Fact]
    public async Task ConnectionFailed_HasCorrectEventIdAndLevel()
    {
        // Arrange
        var (interceptor, exporter) = CreateClientInterceptor();
        var context = CreateClientContext();

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context,
            (_, _) => CreateFailedCall(StatusCode.Unavailable, "Connection refused"));
        await Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);

        // Assert
        var record = exporter.AssertSingle("grpc.connection.failed");
        Assert.Equal(10104, record.EventId.Id);
        Assert.Equal("grpc.connection.failed", record.EventId.Name);
        Assert.Equal(LogLevel.Error, record.LogLevel);
    }

    [Fact]
    public async Task ConnectionFailed_CapturesServiceAndMethod()
    {
        // Arrange
        var (interceptor, exporter) = CreateClientInterceptor();
        var method = CreateMethod("orders.OrderService", "CreateOrder");
        var context = CreateClientContext(method);

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context,
            (_, _) => CreateFailedCall(StatusCode.Unavailable, "Connection refused"));
        await Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);

        // Assert
        var record = exporter.AssertSingle("grpc.connection.failed");
        record.AssertAttribute("GrpcService", "orders.OrderService");
        record.AssertAttribute("GrpcMethod", "CreateOrder");
    }

    [Fact]
    public async Task ConnectionFailed_CapturesFailureReason()
    {
        // Arrange
        var (interceptor, exporter) = CreateClientInterceptor();
        var context = CreateClientContext();

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context,
            (_, _) => CreateFailedCall(StatusCode.Unavailable, "DNS resolution failed"));
        await Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);

        // Assert
        var record = exporter.AssertSingle("grpc.connection.failed");
        record.AssertAttribute("FailureReason", "DNS resolution failed");
    }

    [Fact]
    public async Task ConnectionFailed_CapturesEndpoint()
    {
        // Arrange
        var (interceptor, exporter) = CreateClientInterceptor();
        var context = CreateClientContext(); // host: "localhost:5001"

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context,
            (_, _) => CreateFailedCall(StatusCode.Unavailable, "Connection refused"));
        await Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);

        // Assert
        var record = exporter.AssertSingle("grpc.connection.failed");
        record.AssertAttribute("endpoint", "localhost:5001");
    }

    [Fact]
    public async Task ConnectionFailed_CapturesDurationMs()
    {
        // Arrange
        var (interceptor, exporter) = CreateClientInterceptor();
        var context = CreateClientContext();

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context,
            (_, _) => CreateFailedCall(StatusCode.Unavailable, "Connection refused"));
        await Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);

        // Assert
        var record = exporter.AssertSingle("grpc.connection.failed");
        Assert.True(record.Attributes.ContainsKey("durationMs"), "Should capture durationMs");
        var durationMs = (double)record.Attributes["durationMs"]!;
        Assert.True(durationMs >= 0, $"Duration should be non-negative, was {durationMs}");
    }

    // ─── grpc.auth.failed (10105) ───────────────────────────────────────

    [Fact]
    public async Task Client_Unauthenticated_EmitsAuthFailedEvent()
    {
        // Arrange
        var (interceptor, exporter) = CreateClientInterceptor();
        var context = CreateClientContext();

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context,
            (_, _) => CreateFailedCall(StatusCode.Unauthenticated, "Invalid token"));
        await Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);

        // Assert
        exporter.AssertEventEmitted("grpc.auth.failed");
    }

    [Fact]
    public async Task Client_PermissionDenied_EmitsAuthFailedEvent()
    {
        // Arrange
        var (interceptor, exporter) = CreateClientInterceptor();
        var context = CreateClientContext();

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context,
            (_, _) => CreateFailedCall(StatusCode.PermissionDenied, "Insufficient permissions"));
        await Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);

        // Assert
        exporter.AssertEventEmitted("grpc.auth.failed");
    }

    [Fact]
    public async Task AuthFailed_HasCorrectEventId()
    {
        // Arrange
        var (interceptor, exporter) = CreateClientInterceptor();
        var context = CreateClientContext();

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context,
            (_, _) => CreateFailedCall(StatusCode.Unauthenticated, "Invalid token"));
        await Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);

        // Assert
        var record = exporter.AssertSingle("grpc.auth.failed");
        Assert.Equal(10105, record.EventId.Id);
        Assert.Equal("grpc.auth.failed", record.EventId.Name);
        Assert.Equal(LogLevel.Error, record.LogLevel);
    }

    [Fact]
    public async Task AuthFailed_MapsUnauthenticatedToHttp401()
    {
        // Arrange
        var (interceptor, exporter) = CreateClientInterceptor();
        var context = CreateClientContext();

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context,
            (_, _) => CreateFailedCall(StatusCode.Unauthenticated, "Invalid token"));
        await Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);

        // Assert
        var record = exporter.AssertSingle("grpc.auth.failed");
        record.AssertAttribute("HttpStatusCode", 401);
    }

    [Fact]
    public async Task AuthFailed_MapsPermissionDeniedToHttp403()
    {
        // Arrange
        var (interceptor, exporter) = CreateClientInterceptor();
        var context = CreateClientContext();

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context,
            (_, _) => CreateFailedCall(StatusCode.PermissionDenied, "Insufficient permissions"));
        await Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);

        // Assert
        var record = exporter.AssertSingle("grpc.auth.failed");
        record.AssertAttribute("HttpStatusCode", 403);
    }

    [Fact]
    public async Task AuthFailed_HashesIdentityHint_NeverRaw()
    {
        // Arrange — provide a Bearer token in request headers
        var rawToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.test-payload";
        var headers = new Metadata { { "authorization", $"Bearer {rawToken}" } };
        var (interceptor, exporter) = CreateClientInterceptor();
        var context = CreateClientContext(headers: headers);

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context,
            (_, _) => CreateFailedCall(StatusCode.Unauthenticated, "Token expired"));
        await Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);

        // Assert
        var record = exporter.AssertSingle("grpc.auth.failed");
        record.AssertAttribute("authScheme", "Bearer");

        // Identity hint must be a SHA-256 hash, never the raw token
        Assert.True(record.Attributes.ContainsKey("identityHint"),
            "Should capture identityHint");
        var identityHint = (string)record.Attributes["identityHint"]!;
        Assert.DoesNotContain(rawToken, identityHint);
        Assert.Matches("^[0-9a-f]{16}$", identityHint); // 16 hex chars (first 8 bytes of SHA-256)
    }

    // ─── grpc.throttled (10106) ─────────────────────────────────────────

    [Fact]
    public async Task Client_ResourceExhausted_EmitsThrottledEvent()
    {
        // Arrange
        var (interceptor, exporter) = CreateClientInterceptor();
        var context = CreateClientContext();

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context,
            (_, _) => CreateFailedCall(StatusCode.ResourceExhausted, "Rate limited"));
        await Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);

        // Assert
        exporter.AssertEventEmitted("grpc.throttled");
    }

    [Fact]
    public async Task Throttled_HasCorrectEventIdAndLevel()
    {
        // Arrange
        var (interceptor, exporter) = CreateClientInterceptor();
        var context = CreateClientContext();

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context,
            (_, _) => CreateFailedCall(StatusCode.ResourceExhausted, "Rate limited"));
        await Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);

        // Assert
        var record = exporter.AssertSingle("grpc.throttled");
        Assert.Equal(10106, record.EventId.Id);
        Assert.Equal("grpc.throttled", record.EventId.Name);
        Assert.Equal(LogLevel.Warning, record.LogLevel); // Warning, not Error
    }

    [Fact]
    public async Task Throttled_ExtractsRetryAfterMs()
    {
        // Arrange — server includes retry-after-ms in trailing metadata
        var trailers = new Metadata { { "retry-after-ms", "5000" } };
        var (interceptor, exporter) = CreateClientInterceptor();
        var context = CreateClientContext();

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context,
            (_, _) => CreateFailedCall(StatusCode.ResourceExhausted, "Rate limited", trailers));
        await Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);

        // Assert
        var record = exporter.AssertSingle("grpc.throttled");
        record.AssertAttribute("retryAfterMs", 5000L);
    }

    // ─── Feature Toggle & Supplemental Behavior ─────────────────────────

    [Fact]
    public async Task InfraEvents_NotEmitted_WhenDisabled()
    {
        // Arrange — disable infrastructure events
        var (interceptor, exporter) = CreateClientInterceptor(options =>
        {
            options.EmitInfrastructureEvents = false;
        });
        var context = CreateClientContext();

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context,
            (_, _) => CreateFailedCall(StatusCode.Unavailable, "Connection refused"));
        await Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);

        // Assert — base event emitted, but no infrastructure event
        exporter.AssertEventEmitted("grpc.call.failed");
        Assert.Empty(GetInfraEvents(exporter));
    }

    [Fact]
    public async Task InfraEvent_SupplementalToCallFailed()
    {
        // Arrange
        var (interceptor, exporter) = CreateClientInterceptor();
        var context = CreateClientContext();

        // Act
        using var call = interceptor.AsyncUnaryCall(
            "request", context,
            (_, _) => CreateFailedCall(StatusCode.Unavailable, "Connection refused"));
        await Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);

        // Assert — BOTH grpc.call.failed AND grpc.connection.failed are emitted
        exporter.AssertEventEmitted("grpc.call.failed");
        exporter.AssertEventEmitted("grpc.connection.failed");
    }

    // ─── Server-Side Interceptor ────────────────────────────────────────

    [Fact]
    public async Task Server_Unavailable_EmitsConnectionFailedEvent()
    {
        // Arrange
        var (interceptor, exporter) = CreateServerInterceptor();
        var context = CreateMockServerContext();

        // Act & Assert — server handler throws Unavailable (e.g., downstream dependency down)
        await Assert.ThrowsAsync<RpcException>(async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "request", context,
                (_, _) => throw new RpcException(
                    new Status(StatusCode.Unavailable, "Downstream service unavailable"))));

        // Assert
        exporter.AssertEventEmitted("grpc.connection.failed");
        var record = exporter.AssertSingle("grpc.connection.failed");
        Assert.Equal(10104, record.EventId.Id);
    }

    [Fact]
    public async Task Server_Unauthenticated_EmitsAuthFailedEvent()
    {
        // Arrange
        var (interceptor, exporter) = CreateServerInterceptor();
        var context = CreateMockServerContext();

        // Act
        await Assert.ThrowsAsync<RpcException>(async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "request", context,
                (_, _) => throw new RpcException(
                    new Status(StatusCode.Unauthenticated, "Missing credentials"))));

        // Assert
        exporter.AssertEventEmitted("grpc.auth.failed");
        var record = exporter.AssertSingle("grpc.auth.failed");
        record.AssertAttribute("HttpStatusCode", 401);
    }

    [Fact]
    public async Task Server_ResourceExhausted_EmitsThrottledEvent()
    {
        // Arrange
        var (interceptor, exporter) = CreateServerInterceptor();
        var context = CreateMockServerContext();

        // Act
        await Assert.ThrowsAsync<RpcException>(async () =>
            await interceptor.UnaryServerHandler<string, string>(
                "request", context,
                (_, _) => throw new RpcException(
                    new Status(StatusCode.ResourceExhausted, "Too many requests"))));

        // Assert
        exporter.AssertEventEmitted("grpc.throttled");
        var record = exporter.AssertSingle("grpc.throttled");
        Assert.Equal(LogLevel.Warning, record.LogLevel);
    }
}
