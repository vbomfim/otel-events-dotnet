using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace OtelEvents.HttpClient.Tests;

/// <summary>
/// Unit tests for OtelEventsOutboundTrackingHandler.
/// Validates all three outbound HTTP lifecycle events, configuration options,
/// failure classification, URL redaction, and duration measurement.
/// </summary>
public sealed class OtelEventsOutboundTrackingHandlerTests : IDisposable
{
    private ServiceProvider? _serviceProvider;

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }

    // ─── Test Infrastructure ────────────────────────────────────────────

    /// <summary>
    /// Creates an HttpClient wired with the OtelEvents tracking handler and an in-memory log exporter.
    /// The inner handler is a <see cref="FakeHttpMessageHandler"/> for controlling responses.
    /// </summary>
    private (System.Net.Http.HttpClient Client, TestLogExporter Exporter) CreateTestClient(
        Action<OtelEventsOutboundTrackingOptions>? configureOptions = null,
        FakeHttpMessageHandler? innerHandler = null,
        string httpClientName = "TestClient")
    {
        var exporter = new TestLogExporter();

        var services = new ServiceCollection();

        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Trace);
            logging.AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.ParseStateValues = true;
                options.AddProcessor(new SimpleLogRecordExportProcessor(exporter));
            });
        });

        services.AddHttpClient(httpClientName)
            .AddOtelEventsOutboundTracking(configureOptions)
            .ConfigurePrimaryHttpMessageHandler(() => innerHandler ?? new FakeHttpMessageHandler());

        _serviceProvider = services.BuildServiceProvider();

        var factory = _serviceProvider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient(httpClientName);

        return (client, exporter);
    }

    // ─── Successful Request Tests ───────────────────────────────────────

    [Fact]
    public async Task Successful_request_emits_completed_event_with_correct_fields()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK);
        var (client, exporter) = CreateTestClient(innerHandler: handler);

        // Act
        await client.GetAsync("https://api.example.com/orders");

        // Assert
        var record = exporter.AssertSingle("http.outbound.completed");
        Assert.Equal(LogLevel.Debug, record.LogLevel);
        record.AssertAttribute("httpMethod", "GET");
        record.AssertAttribute("httpUrl", "https://api.example.com/orders");
        record.AssertAttribute("httpStatusCode", 200);
        record.AssertAttribute("httpClientName", "TestClient");
        Assert.True(record.Attributes.ContainsKey("durationMs"));
    }

    [Fact]
    public async Task Successful_request_emits_started_event_by_default()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK);
        var (client, exporter) = CreateTestClient(innerHandler: handler);

        // Act
        await client.GetAsync("https://api.example.com/orders");

        // Assert
        exporter.AssertEventEmitted("http.outbound.started");
        var started = exporter.AssertSingle("http.outbound.started");
        started.AssertAttribute("httpMethod", "GET");
        started.AssertAttribute("httpUrl", "https://api.example.com/orders");
        started.AssertAttribute("httpClientName", "TestClient");
    }

    // ─── Exception Tests ────────────────────────────────────────────────

    [Fact]
    public async Task Exception_emits_failed_event_with_exception()
    {
        // Arrange
        var expectedException = new HttpRequestException("Connection refused");
        var handler = new FakeHttpMessageHandler(expectedException);
        var (client, exporter) = CreateTestClient(innerHandler: handler);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetAsync("https://api.example.com/orders"));

        var record = exporter.AssertSingle("http.outbound.failed");
        Assert.Equal(LogLevel.Error, record.LogLevel);
        record.AssertAttribute("httpMethod", "GET");
        record.AssertAttribute("httpUrl", "https://api.example.com/orders");
        record.AssertAttribute("errorType", "HttpRequestException");
        Assert.True(record.Attributes.ContainsKey("durationMs"));
        Assert.NotNull(record.Exception);
    }

    [Fact]
    public async Task Cancellation_emits_failed_event()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var handler = new FakeHttpMessageHandler(new TaskCanceledException("Request timed out"));
        var (client, exporter) = CreateTestClient(innerHandler: handler);

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            client.GetAsync("https://api.example.com/orders", cts.Token));

        var record = exporter.AssertSingle("http.outbound.failed");
        record.AssertAttribute("errorType", "TaskCanceledException");
        record.AssertAttribute("httpClientName", "TestClient");
    }

    // ─── Custom IsFailure Classification Tests ──────────────────────────

    [Fact]
    public async Task Custom_IsFailure_classifies_429_as_failure()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(HttpStatusCode.TooManyRequests);
        var (client, exporter) = CreateTestClient(
            configureOptions: opts =>
            {
                opts.IsFailure = response => (int)response.StatusCode == 429;
            },
            innerHandler: handler);

        // Act
        await client.GetAsync("https://api.example.com/orders");

        // Assert
        var record = exporter.AssertSingle("http.outbound.failed");
        record.AssertAttribute("errorType", "HTTP 429");
        record.AssertAttribute("httpClientName", "TestClient");
    }

    [Fact]
    public async Task Default_failure_classification_treats_500_as_failure()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError);
        var (client, exporter) = CreateTestClient(innerHandler: handler);

        // Act
        await client.GetAsync("https://api.example.com/orders");

        // Assert
        exporter.AssertSingle("http.outbound.failed");
        exporter.AssertEventNotEmitted("http.outbound.completed");
    }

    [Fact]
    public async Task Default_failure_classification_treats_404_as_success()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(HttpStatusCode.NotFound);
        var (client, exporter) = CreateTestClient(innerHandler: handler);

        // Act
        await client.GetAsync("https://api.example.com/orders");

        // Assert
        exporter.AssertSingle("http.outbound.completed");
        exporter.AssertEventNotEmitted("http.outbound.failed");
    }

    // ─── URL Redaction Tests ────────────────────────────────────────────

    [Fact]
    public async Task UrlRedactor_strips_query_params()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK);
        var (client, exporter) = CreateTestClient(
            configureOptions: opts =>
            {
                opts.UrlRedactor = uri => $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";
            },
            innerHandler: handler);

        // Act
        await client.GetAsync("https://api.example.com/orders?secret=123&token=abc");

        // Assert
        var record = exporter.AssertSingle("http.outbound.completed");
        record.AssertAttribute("httpUrl", "https://api.example.com/orders");
    }

    [Fact]
    public async Task UrlRedactor_applied_to_started_event()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK);
        var (client, exporter) = CreateTestClient(
            configureOptions: opts =>
            {
                opts.UrlRedactor = uri => $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";
            },
            innerHandler: handler);

        // Act
        await client.GetAsync("https://api.example.com/orders?secret=123");

        // Assert
        var started = exporter.AssertSingle("http.outbound.started");
        started.AssertAttribute("httpUrl", "https://api.example.com/orders");
    }

    // ─── EmitStartedEvent Tests ─────────────────────────────────────────

    [Fact]
    public async Task EmitStartedEvent_false_suppresses_started_event()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK);
        var (client, exporter) = CreateTestClient(
            configureOptions: opts => opts.EmitStartedEvent = false,
            innerHandler: handler);

        // Act
        await client.GetAsync("https://api.example.com/orders");

        // Assert
        exporter.AssertEventNotEmitted("http.outbound.started");
        exporter.AssertEventEmitted("http.outbound.completed");
    }

    [Fact]
    public async Task EmitStartedEvent_true_emits_started_event()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK);
        var (client, exporter) = CreateTestClient(
            configureOptions: opts => opts.EmitStartedEvent = true,
            innerHandler: handler);

        // Act
        await client.GetAsync("https://api.example.com/orders");

        // Assert
        exporter.AssertEventEmitted("http.outbound.started");
    }

    // ─── Client Name Tests ──────────────────────────────────────────────

    [Fact]
    public async Task Client_name_propagated_in_events()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK);
        var (client, exporter) = CreateTestClient(
            innerHandler: handler,
            httpClientName: "PaymentGateway");

        // Act
        await client.GetAsync("https://api.example.com/pay");

        // Assert
        var started = exporter.AssertSingle("http.outbound.started");
        started.AssertAttribute("httpClientName", "PaymentGateway");

        var completed = exporter.AssertSingle("http.outbound.completed");
        completed.AssertAttribute("httpClientName", "PaymentGateway");
    }

    // ─── Duration Measurement Tests ─────────────────────────────────────

    [Fact]
    public async Task Duration_measured_on_completed_event()
    {
        // Arrange — add 50ms simulated delay
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, delay: TimeSpan.FromMilliseconds(50));
        var (client, exporter) = CreateTestClient(innerHandler: handler);

        // Act
        await client.GetAsync("https://api.example.com/orders");

        // Assert
        var record = exporter.AssertSingle("http.outbound.completed");
        var durationMs = (double)record.Attributes["durationMs"]!;
        Assert.True(durationMs >= 40, $"Expected durationMs >= 40 but got {durationMs}");
    }

    [Fact]
    public async Task Duration_measured_on_failed_exception_event()
    {
        // Arrange — add 50ms simulated delay before throwing
        var handler = new FakeHttpMessageHandler(
            new HttpRequestException("Timeout"),
            delay: TimeSpan.FromMilliseconds(50));
        var (client, exporter) = CreateTestClient(innerHandler: handler);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetAsync("https://api.example.com/orders"));

        var record = exporter.AssertSingle("http.outbound.failed");
        var durationMs = (double)record.Attributes["durationMs"]!;
        Assert.True(durationMs >= 40, $"Expected durationMs >= 40 but got {durationMs}");
    }
}

/// <summary>
/// Fake HTTP message handler for testing. Returns a configurable response
/// or throws a configurable exception.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode? _statusCode;
    private readonly Exception? _exception;
    private readonly TimeSpan _delay;

    public FakeHttpMessageHandler(HttpStatusCode statusCode = HttpStatusCode.OK, TimeSpan delay = default)
    {
        _statusCode = statusCode;
        _delay = delay;
    }

    public FakeHttpMessageHandler(Exception exception, TimeSpan delay = default)
    {
        _exception = exception;
        _delay = delay;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_delay > TimeSpan.Zero)
        {
            await Task.Delay(_delay, cancellationToken);
        }

        if (_exception is not null)
        {
            throw _exception;
        }

        return new HttpResponseMessage(_statusCode ?? HttpStatusCode.OK);
    }
}
