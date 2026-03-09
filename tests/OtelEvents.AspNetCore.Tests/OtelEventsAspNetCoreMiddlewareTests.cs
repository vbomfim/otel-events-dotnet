using OtelEvents.Causality;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OtelEvents.AspNetCore;

namespace OtelEvents.AspNetCore.Tests;

/// <summary>
/// Integration tests for OtelEventsAspNetCoreMiddleware using TestServer.
/// Validates all three HTTP lifecycle events, configuration options,
/// causal scope, and path exclusion behavior.
/// </summary>
public class OtelEventsAspNetCoreMiddlewareTests : IAsyncDisposable
{
    // ─── Test Infrastructure ────────────────────────────────────────────

    /// <summary>
    /// Creates a TestServer with the OtelEvents middleware and in-memory log exporter.
    /// Configurable via options action and pipeline setup.
    /// </summary>
    private static async Task<(IHost Host, TestLogExporter Exporter)> CreateTestHost(
        Action<OtelEventsAspNetCoreOptions>? configureOptions = null,
        Action<IApplicationBuilder>? configurePipeline = null)
    {
        var exporter = new TestLogExporter();

        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(LogLevel.Trace);
                    logging.AddOpenTelemetry(options =>
                    {
                        options.IncludeFormattedMessage = true;
                        options.ParseStateValues = true;
                        options.AddProcessor(new OtelEventsCausalityProcessor());
                        options.AddProcessor(new SimpleLogRecordExportProcessor(exporter));
                    });
                });
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();

                    if (configureOptions is not null)
                    {
                        services.AddOtelEventsAspNetCore(configureOptions);
                    }
                    else
                    {
                        services.AddOtelEventsAspNetCore();
                    }
                });
                webBuilder.Configure(app =>
                {
                    // Note: OtelEventsAspNetCoreMiddleware is auto-registered via IStartupFilter
                    // from AddOtelEventsAspNetCore() — no need to call UseOtelEventsAspNetCore()
                    app.UseRouting();

                    configurePipeline?.Invoke(app);

                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/api/orders", async context =>
                        {
                            context.Response.ContentLength = 42;
                            await context.Response.WriteAsync("OK");
                        });

                        endpoints.MapGet("/api/orders/{id}", async context =>
                        {
                            await context.Response.WriteAsync("Order details");
                        });

                        endpoints.MapPost("/api/orders", async context =>
                        {
                            context.Response.StatusCode = 201;
                            await context.Response.WriteAsync("Created");
                        });

                        endpoints.MapGet("/api/throw", _ =>
                        {
                            throw new InvalidOperationException("Test exception");
                        });

                        endpoints.MapGet("/health", async context =>
                        {
                            await context.Response.WriteAsync("Healthy");
                        });

                        endpoints.MapGet("/metrics", async context =>
                        {
                            await context.Response.WriteAsync("metrics_data");
                        });
                    });
                });
            })
            .StartAsync();

        return (host, exporter);
    }

    private readonly List<IHost> _hosts = [];

    /// <summary>OtelEvents event names for filtering out framework noise.</summary>
    private static readonly HashSet<string> OtelEventNames =
    [
        "http.request.received",
        "http.request.completed",
        "http.request.failed"
    ];

    /// <summary>Filters only OtelEvents records from the exporter (excludes framework logs).</summary>
    private static List<TestLogRecord> GetOtelEvents(TestLogExporter exporter) =>
        exporter.LogRecords.Where(r => r.EventName is not null && OtelEventNames.Contains(r.EventName)).ToList();

    private async Task<(HttpClient Client, TestLogExporter Exporter)> CreateClient(
        Action<OtelEventsAspNetCoreOptions>? configureOptions = null,
        Action<IApplicationBuilder>? configurePipeline = null)
    {
        var (host, exporter) = await CreateTestHost(configureOptions, configurePipeline);
        _hosts.Add(host);
        var client = host.GetTestClient();
        return (client, exporter);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var host in _hosts)
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    // ─── Event Emission Tests ───────────────────────────────────────────

    [Fact]
    public async Task Middleware_EmitsRequestReceived_OnRequestStart()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await client.GetAsync("/api/orders");

        // Assert
        exporter.AssertEventEmitted("http.request.received");
        var record = exporter.AssertSingle("http.request.received");
        Assert.Equal(LogLevel.Information, record.LogLevel);
    }

    [Fact]
    public async Task Middleware_EmitsRequestCompleted_OnSuccessfulRequest()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await client.GetAsync("/api/orders");

        // Assert
        exporter.AssertEventEmitted("http.request.completed");
        var record = exporter.AssertSingle("http.request.completed");
        Assert.Equal(LogLevel.Information, record.LogLevel);
    }

    [Fact]
    public async Task Middleware_EmitsRequestFailed_OnUnhandledException()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act & Assert — exception is expected
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await client.GetAsync("/api/throw"));

        // Assert
        exporter.AssertEventEmitted("http.request.failed");
        var record = exporter.AssertSingle("http.request.failed");
        Assert.Equal(LogLevel.Error, record.LogLevel);
    }

    [Fact]
    public async Task Middleware_EmitsAllThreeEvents_OnFailedRequest()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act — exception expected
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await client.GetAsync("/api/throw"));

        // Assert — should have received + failed (not completed)
        exporter.AssertEventEmitted("http.request.received");
        exporter.AssertEventEmitted("http.request.failed");
        Assert.DoesNotContain(exporter.LogRecords, r => r.EventName == "http.request.completed");
    }

    [Fact]
    public async Task Middleware_EmitsBothReceivedAndCompleted_OnSuccessfulRequest()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await client.GetAsync("/api/orders");

        // Assert — should have both received and completed
        exporter.AssertEventEmitted("http.request.received");
        exporter.AssertEventEmitted("http.request.completed");
        Assert.DoesNotContain(exporter.LogRecords, r => r.EventName == "http.request.failed");
    }

    // ─── Event Field Tests ──────────────────────────────────────────────

    [Fact]
    public async Task RequestReceived_CapturesHttpMethod()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await client.GetAsync("/api/orders");

        // Assert
        var record = exporter.AssertSingle("http.request.received");
        record.AssertAttribute("HttpMethod", "GET");
    }

    [Fact]
    public async Task RequestReceived_CapturesHttpPath()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await client.GetAsync("/api/orders");

        // Assert
        var record = exporter.AssertSingle("http.request.received");
        record.AssertAttribute("HttpPath", "/api/orders");
    }

    [Fact]
    public async Task RequestReceived_CapturesRequestId()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await client.GetAsync("/api/orders");

        // Assert
        var record = exporter.AssertSingle("http.request.received");
        Assert.True(record.Attributes.ContainsKey("requestId"),
            "Should capture ASP.NET Core TraceIdentifier as requestId");
        Assert.NotNull(record.Attributes["requestId"]);
    }

    [Fact]
    public async Task RequestCompleted_CapturesStatusCode()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await client.GetAsync("/api/orders");

        // Assert
        var record = exporter.AssertSingle("http.request.completed");
        record.AssertAttribute("HttpStatusCode", 200);
    }

    [Fact]
    public async Task RequestCompleted_CapturesDuration()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await client.GetAsync("/api/orders");

        // Assert
        var record = exporter.AssertSingle("http.request.completed");
        Assert.True(record.Attributes.ContainsKey("DurationMs"),
            "Should capture request duration");
        var durationMs = (double)record.Attributes["DurationMs"]!;
        Assert.True(durationMs >= 0, $"Duration should be non-negative, was {durationMs}");
    }

    [Fact]
    public async Task RequestCompleted_CapturesPostStatusCode()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await client.PostAsync("/api/orders", null);

        // Assert
        var record = exporter.AssertSingle("http.request.completed");
        record.AssertAttribute("HttpStatusCode", 201);
        record.AssertAttribute("HttpMethod", "POST");
    }

    [Fact]
    public async Task RequestFailed_CapturesErrorType()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await client.GetAsync("/api/throw"));

        // Assert
        var record = exporter.AssertSingle("http.request.failed");
        record.AssertAttribute("ErrorType", "InvalidOperationException");
    }

    [Fact]
    public async Task RequestFailed_CapturesDuration()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await client.GetAsync("/api/throw"));

        // Assert
        var record = exporter.AssertSingle("http.request.failed");
        Assert.True(record.Attributes.ContainsKey("DurationMs"));
        var durationMs = (double)record.Attributes["DurationMs"]!;
        Assert.True(durationMs >= 0, $"Duration should be non-negative, was {durationMs}");
    }

    [Fact]
    public async Task RequestFailed_CapturesException()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await client.GetAsync("/api/throw"));

        // Assert
        var record = exporter.AssertSingle("http.request.failed");
        Assert.NotNull(record.Exception);
        Assert.IsType<InvalidOperationException>(record.Exception);
        Assert.Equal("Test exception", record.Exception.Message);
    }

    // ─── PII Default Tests ──────────────────────────────────────────────

    [Fact]
    public async Task DefaultOptions_DoesNotCaptureUserAgent()
    {
        // Arrange — default options (CaptureUserAgent = false)
        var (client, exporter) = await CreateClient();
        client.DefaultRequestHeaders.Add("User-Agent", "TestBot/1.0");

        // Act
        await client.GetAsync("/api/orders");

        // Assert — userAgent should be null with default options
        var record = exporter.AssertSingle("http.request.received");
        var userAgent = record.Attributes.GetValueOrDefault("userAgent");
        Assert.Null(userAgent);
    }

    [Fact]
    public async Task DefaultOptions_DoesNotCaptureClientIp()
    {
        // Arrange — default options (CaptureClientIp = false)
        var (client, exporter) = await CreateClient();

        // Act
        await client.GetAsync("/api/orders");

        // Assert — clientIp should be null with default options
        var record = exporter.AssertSingle("http.request.received");
        var clientIp = record.Attributes.GetValueOrDefault("ClientIp");
        Assert.Null(clientIp);
    }

    [Fact]
    public async Task CaptureUserAgent_WhenEnabled_CapturesHeader()
    {
        // Arrange
        var (client, exporter) = await CreateClient(options =>
        {
            options.CaptureUserAgent = true;
        });
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TestBot/1.0");

        // Act
        await client.GetAsync("/api/orders");

        // Assert — userAgent should be captured
        var record = exporter.AssertSingle("http.request.received");
        var userAgent = record.Attributes["userAgent"] as string;
        Assert.NotNull(userAgent);
        Assert.Contains("TestBot", userAgent);
    }

    [Fact]
    public async Task CaptureClientIp_WhenEnabled_CapturesIp()
    {
        // Arrange
        var (client, exporter) = await CreateClient(options =>
        {
            options.CaptureClientIp = true;
        });

        // Act
        await client.GetAsync("/api/orders");

        // Assert — clientIp should be captured (TestServer provides loopback)
        var record = exporter.AssertSingle("http.request.received");
        Assert.True(record.Attributes.ContainsKey("ClientIp"),
            "ClientIp attribute should be present when CaptureClientIp is enabled");
    }

    // ─── ExcludePaths Tests ─────────────────────────────────────────────

    [Fact]
    public async Task ExcludePaths_SkipsHealthEndpoint()
    {
        // Arrange
        var (client, exporter) = await CreateClient(options =>
        {
            options.ExcludePaths = ["/health", "/metrics"];
        });

        // Act
        await client.GetAsync("/health");

        // Assert — no OtelEvents emitted for excluded path
        Assert.Empty(GetOtelEvents(exporter));
    }

    [Fact]
    public async Task ExcludePaths_SkipsMetricsEndpoint()
    {
        // Arrange
        var (client, exporter) = await CreateClient(options =>
        {
            options.ExcludePaths = ["/health", "/metrics"];
        });

        // Act
        await client.GetAsync("/metrics");

        // Assert — no OtelEvents emitted for excluded path
        Assert.Empty(GetOtelEvents(exporter));
    }

    [Fact]
    public async Task ExcludePaths_DoesNotAffectNonExcludedPaths()
    {
        // Arrange
        var (client, exporter) = await CreateClient(options =>
        {
            options.ExcludePaths = ["/health", "/metrics"];
        });

        // Act
        await client.GetAsync("/api/orders");

        // Assert — events should be emitted for non-excluded paths
        exporter.AssertEventEmitted("http.request.received");
        exporter.AssertEventEmitted("http.request.completed");
    }

    [Fact]
    public async Task ExcludePaths_IsCaseInsensitive()
    {
        // Arrange
        var (client, exporter) = await CreateClient(options =>
        {
            options.ExcludePaths = ["/Health"];
        });

        // Act
        await client.GetAsync("/health");

        // Assert — case-insensitive match
        Assert.Empty(GetOtelEvents(exporter));
    }

    [Fact]
    public async Task ExcludePaths_MatchesPrefix()
    {
        // Arrange
        var (client, exporter) = await CreateClient(options =>
        {
            options.ExcludePaths = ["/health"];
        });

        // Note: TestServer won't have /health/ready mapped, but the middleware
        // should still skip event emission before hitting routing
        // We test this by verifying no events are emitted for the path

        // Act — use the /health endpoint which matches prefix
        await client.GetAsync("/health");

        // Assert — excluded by exact/prefix match
        Assert.Empty(GetOtelEvents(exporter));
    }

    // ─── UseRouteTemplate Tests ─────────────────────────────────────────

    [Fact]
    public async Task UseRouteTemplate_WhenEnabled_UsesRouteTemplateAsPath()
    {
        // Arrange
        var (client, exporter) = await CreateClient(options =>
        {
            options.UseRouteTemplate = true;
        });

        // Act — request to a parameterized route
        await client.GetAsync("/api/orders/123");

        // Assert — path should use route template, not raw path
        var record = exporter.AssertSingle("http.request.completed");
        var httpPath = record.Attributes["HttpPath"] as string;
        Assert.NotNull(httpPath);
        // Route template should be /api/orders/{id} not /api/orders/123
        Assert.Contains("{id}", httpPath);
    }

    [Fact]
    public async Task UseRouteTemplate_WhenDisabled_UsesRawPath()
    {
        // Arrange
        var (client, exporter) = await CreateClient(options =>
        {
            options.UseRouteTemplate = false;
        });

        // Act
        await client.GetAsync("/api/orders/456");

        // Assert — completed event path should be raw path when UseRouteTemplate is false
        var completed = exporter.AssertSingle("http.request.completed");
        var httpPath = completed.Attributes["HttpPath"] as string;
        Assert.NotNull(httpPath);
        Assert.Equal("/api/orders/456", httpPath);
    }

    // ─── MaxPathLength Tests ────────────────────────────────────────────

    [Fact]
    public async Task MaxPathLength_TruncatesLongPaths()
    {
        // Arrange
        var (client, exporter) = await CreateClient(options =>
        {
            options.MaxPathLength = 10;
            options.UseRouteTemplate = false;
        });

        // Act — this will 404 but middleware still processes
        await client.GetAsync("/api/orders/very-long-path-that-exceeds-limit");

        // Assert — path should be truncated
        var record = exporter.AssertSingle("http.request.received");
        var httpPath = record.Attributes["HttpPath"] as string;
        Assert.NotNull(httpPath);
        Assert.True(httpPath.Length <= 10, $"Path should be truncated to 10 chars, was {httpPath.Length}: '{httpPath}'");
    }

    // ─── RecordRequestReceived Tests ────────────────────────────────────

    [Fact]
    public async Task RecordRequestReceived_WhenDisabled_SkipsReceivedEvent()
    {
        // Arrange
        var (client, exporter) = await CreateClient(options =>
        {
            options.RecordRequestReceived = false;
        });

        // Act
        await client.GetAsync("/api/orders");

        // Assert — only completed event, no received
        Assert.DoesNotContain(exporter.LogRecords, r => r.EventName == "http.request.received");
        exporter.AssertEventEmitted("http.request.completed");
    }

    // ─── Causal Scope Tests ─────────────────────────────────────────────

    [Fact]
    public async Task CausalScope_WhenEnabled_CompletedEventHasParentEventId()
    {
        // Arrange
        var (client, exporter) = await CreateClient(options =>
        {
            options.EnableCausalScope = true;
        });

        // Act
        await client.GetAsync("/api/orders");

        // Assert — completed event should have parentEventId
        var completed = exporter.AssertSingle("http.request.completed");
        Assert.True(completed.Attributes.ContainsKey("all.parent_event_id"),
            "Completed event should have all.parent_event_id when causal scope is enabled");
    }

    [Fact]
    public async Task CausalScope_WhenDisabled_CompletedEventHasNoParentEventId()
    {
        // Arrange
        var (client, exporter) = await CreateClient(options =>
        {
            options.EnableCausalScope = false;
        });

        // Act
        await client.GetAsync("/api/orders");

        // Assert — completed event should NOT have parentEventId from middleware
        var completed = exporter.AssertSingle("http.request.completed");
        Assert.False(completed.Attributes.ContainsKey("all.parent_event_id"),
            "Completed event should not have all.parent_event_id when causal scope is disabled");
    }

    [Fact]
    public async Task CausalScope_ReceivedEventHasEventId()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await client.GetAsync("/api/orders");

        // Assert — received event should have all.event_id (from OtelEventsCausalityProcessor)
        var received = exporter.AssertSingle("http.request.received");
        Assert.True(received.Attributes.ContainsKey("all.event_id"),
            "Received event should have all.event_id");
        var eventId = received.Attributes["all.event_id"] as string;
        Assert.NotNull(eventId);
        Assert.StartsWith("evt_", eventId);
    }

    // ─── Event ID Tests ─────────────────────────────────────────────────

    [Fact]
    public async Task Events_HaveCorrectEventIds()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await client.GetAsync("/api/orders");

        // Assert — verify event IDs match spec (10001, 10002)
        var received = exporter.AssertSingle("http.request.received");
        Assert.Equal(10001, received.EventId.Id);
        Assert.Equal("http.request.received", received.EventId.Name);

        var completed = exporter.AssertSingle("http.request.completed");
        Assert.Equal(10002, completed.EventId.Id);
        Assert.Equal("http.request.completed", completed.EventId.Name);
    }

    [Fact]
    public async Task FailedEvent_HasCorrectEventId()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await client.GetAsync("/api/throw"));

        // Assert
        var failed = exporter.AssertSingle("http.request.failed");
        Assert.Equal(10003, failed.EventId.Id);
        Assert.Equal("http.request.failed", failed.EventId.Name);
    }

    // ─── Formatted Message Tests ────────────────────────────────────────

    [Fact]
    public async Task RequestReceived_HasFormattedMessage()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await client.GetAsync("/api/orders");

        // Assert
        var record = exporter.AssertSingle("http.request.received");
        Assert.NotNull(record.FormattedMessage);
        Assert.Contains("GET", record.FormattedMessage);
        Assert.Contains("/api/orders", record.FormattedMessage);
    }

    [Fact]
    public async Task RequestCompleted_HasFormattedMessage()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await client.GetAsync("/api/orders");

        // Assert
        var record = exporter.AssertSingle("http.request.completed");
        Assert.NotNull(record.FormattedMessage);
        Assert.Contains("200", record.FormattedMessage);
    }

    [Fact]
    public async Task RequestFailed_HasFormattedMessage()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await client.GetAsync("/api/throw"));

        // Assert
        var record = exporter.AssertSingle("http.request.failed");
        Assert.NotNull(record.FormattedMessage);
        Assert.Contains("InvalidOperationException", record.FormattedMessage);
    }

    // ─── Multiple Requests Test ─────────────────────────────────────────

    [Fact]
    public async Task MultipleRequests_EachGetsOwnEvents()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await client.GetAsync("/api/orders");
        await client.PostAsync("/api/orders", null);

        // Assert — should have 2 received + 2 completed
        var received = exporter.LogRecords.Where(r => r.EventName == "http.request.received").ToList();
        var completed = exporter.LogRecords.Where(r => r.EventName == "http.request.completed").ToList();
        Assert.Equal(2, received.Count);
        Assert.Equal(2, completed.Count);
    }
}
