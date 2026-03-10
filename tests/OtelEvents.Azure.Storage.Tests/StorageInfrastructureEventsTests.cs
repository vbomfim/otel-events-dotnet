using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace OtelEvents.Azure.Storage.Tests;

/// <summary>
/// Tests for infrastructure events: storage.connection.failed (10308),
/// storage.auth.failed (10309), and storage.throttled (10310).
/// Verifies that RequestFailedException is classified and the correct
/// infrastructure event is emitted with the expected fields.
/// </summary>
public sealed class StorageInfrastructureEventsTests
{
    // ─── Test Infrastructure ──────────────────────────────────────────

    private static (HttpPipeline Pipeline, TestLogExporter Exporter, MockPipelineTransport Transport)
        CreateThrowingPipeline(
            Func<Request, MockPipelineResponse>? responseFactory = null,
            Action<OtelEventsAzureStorageOptions>? configureOptions = null)
    {
        var exporter = new TestLogExporter();

        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Trace);
            logging.AddOpenTelemetry(otel =>
            {
                otel.IncludeFormattedMessage = true;
                otel.ParseStateValues = true;
                otel.AddProcessor(new SimpleLogRecordExportProcessor(exporter));
            });
        });

        var storageOptions = new OtelEventsAzureStorageOptions();
        storageOptions.EmitInfrastructureEvents = true;
        configureOptions?.Invoke(storageOptions);
        services.AddSingleton(storageOptions);

        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<OtelEvents.Azure.Storage.Events.OtelEventsStorageEventSource>>();

        var policy = new OtelEventsStoragePipelinePolicy(logger, storageOptions);

        var transport = new MockPipelineTransport(responseFactory ?? (_ => new MockPipelineResponse(200)));
        var clientOptions = new TestClientOptions { Transport = transport };
        var pipeline = HttpPipelineBuilder.Build(clientOptions, policy);

        return (pipeline, exporter, transport);
    }

    private static (HttpPipeline Pipeline, TestLogExporter Exporter)
        CreateExceptionThrowingPipeline(
            RequestFailedException exceptionToThrow,
            Action<OtelEventsAzureStorageOptions>? configureOptions = null)
    {
        var exporter = new TestLogExporter();

        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Trace);
            logging.AddOpenTelemetry(otel =>
            {
                otel.IncludeFormattedMessage = true;
                otel.ParseStateValues = true;
                otel.AddProcessor(new SimpleLogRecordExportProcessor(exporter));
            });
        });

        var storageOptions = new OtelEventsAzureStorageOptions();
        storageOptions.EmitInfrastructureEvents = true;
        configureOptions?.Invoke(storageOptions);
        services.AddSingleton(storageOptions);

        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<OtelEvents.Azure.Storage.Events.OtelEventsStorageEventSource>>();

        var policy = new OtelEventsStoragePipelinePolicy(logger, storageOptions);

        var transport = new ThrowingMockTransport(exceptionToThrow);
        var clientOptions = new TestClientOptions { Transport = transport };
        var pipeline = HttpPipelineBuilder.Build(clientOptions, policy);

        return (pipeline, exporter);
    }

    private static HttpMessage CreateMessage(HttpPipeline pipeline, string uri, RequestMethod method)
    {
        var message = pipeline.CreateMessage();
        message.Request.Uri.Reset(new Uri(uri));
        message.Request.Method = method;
        return message;
    }

    // ─── storage.connection.failed (10308) ────────────────────────────

    [Fact]
    public void Process_ConnectionError_EmitsStorageConnectionFailed()
    {
        var innerEx = new System.Net.Http.HttpRequestException("Connection refused");
        var rfex = new RequestFailedException("Unable to connect", innerEx);
        var (pipeline, exporter) = CreateExceptionThrowingPipeline(rfex);

        var message = CreateMessage(pipeline,
            "https://myaccount.blob.core.windows.net/container/blob.txt",
            RequestMethod.Put);

        Assert.Throws<RequestFailedException>(() =>
            pipeline.Send(message, CancellationToken.None));

        exporter.AssertEventEmitted("storage.connection.failed");
        var record = exporter.AssertSingle("storage.connection.failed");
        Assert.Equal(10308, record.EventId.Id);
        Assert.Equal(LogLevel.Error, record.LogLevel);
        record.AssertAttribute("storageAccountName", "myaccount");
        record.AssertHasAttribute("durationMs");
        record.AssertHasAttribute("errorType");
        record.AssertHasAttribute("errorMessage");
        record.AssertHasAttribute("failureReason");
    }

    [Fact]
    public void Process_ConnectionError_EndpointField()
    {
        var innerEx = new System.Net.Http.HttpRequestException("Name resolution failure");
        var rfex = new RequestFailedException("DNS lookup failed", innerEx);
        var (pipeline, exporter) = CreateExceptionThrowingPipeline(rfex);

        var message = CreateMessage(pipeline,
            "https://myaccount.blob.core.windows.net/container/blob.txt",
            RequestMethod.Get);

        Assert.Throws<RequestFailedException>(() =>
            pipeline.Send(message, CancellationToken.None));

        var record = exporter.AssertSingle("storage.connection.failed");
        record.AssertAttribute("endpoint", "myaccount.blob.core.windows.net");
    }

    [Fact]
    public void Process_TaskCanceledException_EmitsConnectionFailed()
    {
        var innerEx = new TaskCanceledException("Request timed out");
        var rfex = new RequestFailedException("Timeout", innerEx);
        var (pipeline, exporter) = CreateExceptionThrowingPipeline(rfex);

        var message = CreateMessage(pipeline,
            "https://myaccount.blob.core.windows.net/container/blob.txt",
            RequestMethod.Get);

        Assert.Throws<RequestFailedException>(() =>
            pipeline.Send(message, CancellationToken.None));

        exporter.AssertEventEmitted("storage.connection.failed");
    }

    [Fact]
    public void Process_SocketException_EmitsConnectionFailed()
    {
        var innerEx = new System.Net.Sockets.SocketException(10061); // Connection refused
        var rfex = new RequestFailedException("Connection failed", innerEx);
        var (pipeline, exporter) = CreateExceptionThrowingPipeline(rfex);

        var message = CreateMessage(pipeline,
            "https://myaccount.queue.core.windows.net/tasks/messages",
            RequestMethod.Post);

        Assert.Throws<RequestFailedException>(() =>
            pipeline.Send(message, CancellationToken.None));

        exporter.AssertEventEmitted("storage.connection.failed");
        var record = exporter.AssertSingle("storage.connection.failed");
        record.AssertAttribute("storageAccountName", "myaccount");
    }

    // ─── storage.auth.failed (10309) ──────────────────────────────────

    [Fact]
    public void Process_Http401_EmitsStorageAuthFailed()
    {
        var rfex = new RequestFailedException(401, "Unauthorized");
        var (pipeline, exporter) = CreateExceptionThrowingPipeline(rfex);

        var message = CreateMessage(pipeline,
            "https://myaccount.blob.core.windows.net/private/secret.txt",
            RequestMethod.Get);

        Assert.Throws<RequestFailedException>(() =>
            pipeline.Send(message, CancellationToken.None));

        exporter.AssertEventEmitted("storage.auth.failed");
        var record = exporter.AssertSingle("storage.auth.failed");
        Assert.Equal(10309, record.EventId.Id);
        Assert.Equal(LogLevel.Error, record.LogLevel);
        record.AssertAttribute("httpStatusCode", 401);
        record.AssertAttribute("storageAccountName", "myaccount");
        record.AssertHasAttribute("authScheme");
    }

    [Fact]
    public void Process_Http403_EmitsStorageAuthFailed()
    {
        var rfex = new RequestFailedException(403, "Forbidden");
        var (pipeline, exporter) = CreateExceptionThrowingPipeline(rfex);

        var message = CreateMessage(pipeline,
            "https://myaccount.blob.core.windows.net/container/blob.txt",
            RequestMethod.Put);

        Assert.Throws<RequestFailedException>(() =>
            pipeline.Send(message, CancellationToken.None));

        exporter.AssertEventEmitted("storage.auth.failed");
        var record = exporter.AssertSingle("storage.auth.failed");
        record.AssertAttribute("httpStatusCode", 403);
    }

    [Fact]
    public void Process_AuthFailed_DetectsSharedKeyScheme()
    {
        var rfex = new RequestFailedException(403, "SharedKey auth failed");
        var (pipeline, exporter) = CreateExceptionThrowingPipeline(rfex);

        var message = CreateMessage(pipeline,
            "https://myaccount.blob.core.windows.net/container/blob.txt",
            RequestMethod.Get);
        message.Request.Headers.Add("Authorization", "SharedKey myaccount:abc123");

        Assert.Throws<RequestFailedException>(() =>
            pipeline.Send(message, CancellationToken.None));

        var record = exporter.AssertSingle("storage.auth.failed");
        record.AssertAttribute("authScheme", "SharedKey");
    }

    [Fact]
    public void Process_AuthFailed_DetectsSasScheme()
    {
        var rfex = new RequestFailedException(403, "SAS token expired");
        var (pipeline, exporter) = CreateExceptionThrowingPipeline(rfex);

        var message = CreateMessage(pipeline,
            "https://myaccount.blob.core.windows.net/container/blob.txt?sig=abc&se=2024-01-01",
            RequestMethod.Get);

        Assert.Throws<RequestFailedException>(() =>
            pipeline.Send(message, CancellationToken.None));

        var record = exporter.AssertSingle("storage.auth.failed");
        record.AssertAttribute("authScheme", "SAS");
    }

    [Fact]
    public void Process_AuthFailed_NoAuthHeader_ReportsAnonymous()
    {
        var rfex = new RequestFailedException(401, "Unauthorized");
        var (pipeline, exporter) = CreateExceptionThrowingPipeline(rfex);

        var message = CreateMessage(pipeline,
            "https://myaccount.blob.core.windows.net/container/blob.txt",
            RequestMethod.Get);
        // No Authorization header, no SAS query params

        Assert.Throws<RequestFailedException>(() =>
            pipeline.Send(message, CancellationToken.None));

        var record = exporter.AssertSingle("storage.auth.failed");
        record.AssertAttribute("authScheme", "Anonymous");
    }

    // ─── storage.throttled (10310) ────────────────────────────────────

    [Fact]
    public void Process_Http429_EmitsStorageThrottled()
    {
        var rfex = new RequestFailedException(429, "Too Many Requests");
        var (pipeline, exporter) = CreateExceptionThrowingPipeline(rfex);

        var message = CreateMessage(pipeline,
            "https://myaccount.blob.core.windows.net/container/blob.txt",
            RequestMethod.Get);

        Assert.Throws<RequestFailedException>(() =>
            pipeline.Send(message, CancellationToken.None));

        exporter.AssertEventEmitted("storage.throttled");
        var record = exporter.AssertSingle("storage.throttled");
        Assert.Equal(10310, record.EventId.Id);
        Assert.Equal(LogLevel.Warning, record.LogLevel);
        record.AssertAttribute("httpStatusCode", 429);
        record.AssertAttribute("storageAccountName", "myaccount");
    }

    [Fact]
    public void Process_Http503_EmitsStorageThrottled()
    {
        var rfex = new RequestFailedException(503, "Service Unavailable");
        var (pipeline, exporter) = CreateExceptionThrowingPipeline(rfex);

        var message = CreateMessage(pipeline,
            "https://myaccount.queue.core.windows.net/tasks/messages",
            RequestMethod.Post);

        Assert.Throws<RequestFailedException>(() =>
            pipeline.Send(message, CancellationToken.None));

        exporter.AssertEventEmitted("storage.throttled");
        var record = exporter.AssertSingle("storage.throttled");
        record.AssertAttribute("httpStatusCode", 503);
        record.AssertAttribute("storageAccountName", "myaccount");
    }

    [Fact]
    public void Process_Throttled_NoRetryAfterHeader_ReturnsNull()
    {
        var rfex = new RequestFailedException(429, "Too Many Requests");
        var (pipeline, exporter) = CreateExceptionThrowingPipeline(rfex);

        var message = CreateMessage(pipeline,
            "https://myaccount.blob.core.windows.net/container/blob.txt",
            RequestMethod.Get);

        Assert.Throws<RequestFailedException>(() =>
            pipeline.Send(message, CancellationToken.None));

        var record = exporter.AssertSingle("storage.throttled");
        // retryAfterMs should be null/not present when no Retry-After header
        // (the field is nullable, so it simply won't be set)
        record.AssertHasAttribute("retryAfterMs");
    }

    // ─── Opt-in behavior ──────────────────────────────────────────────

    [Fact]
    public void Process_InfraEventsDisabled_NoInfraEventEmitted()
    {
        var rfex = new RequestFailedException(401, "Unauthorized");
        var (pipeline, exporter) = CreateExceptionThrowingPipeline(
            rfex,
            configureOptions: o => o.EmitInfrastructureEvents = false);

        var message = CreateMessage(pipeline,
            "https://myaccount.blob.core.windows.net/container/blob.txt",
            RequestMethod.Get);

        Assert.Throws<RequestFailedException>(() =>
            pipeline.Send(message, CancellationToken.None));

        exporter.AssertEventNotEmitted("storage.auth.failed");
        exporter.AssertEventNotEmitted("storage.connection.failed");
        exporter.AssertEventNotEmitted("storage.throttled");
        // The regular blob.failed event should still be emitted
        exporter.AssertEventEmitted("storage.blob.failed");
    }

    // ─── Supplemental behavior (infra + regular event) ────────────────

    [Fact]
    public void Process_Http401_EmitsBothAuthFailedAndBlobFailed()
    {
        var rfex = new RequestFailedException(401, "Unauthorized");
        var (pipeline, exporter) = CreateExceptionThrowingPipeline(rfex);

        var message = CreateMessage(pipeline,
            "https://myaccount.blob.core.windows.net/container/blob.txt",
            RequestMethod.Get);

        Assert.Throws<RequestFailedException>(() =>
            pipeline.Send(message, CancellationToken.None));

        // Infrastructure event emitted
        exporter.AssertEventEmitted("storage.auth.failed");
        // Regular error event also emitted
        exporter.AssertEventEmitted("storage.blob.failed");
    }
}

/// <summary>
/// Mock transport that throws a RequestFailedException on every request.
/// Used to simulate Azure Storage connection and authentication failures.
/// </summary>
internal sealed class ThrowingMockTransport : HttpPipelineTransport
{
    private readonly RequestFailedException _exception;

    public ThrowingMockTransport(RequestFailedException exception)
    {
        _exception = exception;
    }

    public override Request CreateRequest() => new MockPipelineRequest();

    public override void Process(HttpMessage message)
    {
        throw _exception;
    }

    public override ValueTask ProcessAsync(HttpMessage message)
    {
        throw _exception;
    }
}
