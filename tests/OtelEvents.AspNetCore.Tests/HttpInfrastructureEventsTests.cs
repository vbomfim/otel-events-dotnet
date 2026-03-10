using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OtelEvents.AspNetCore;
using OtelEvents.AspNetCore.Events;
using OtelEvents.Causality;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace OtelEvents.AspNetCore.Tests;

/// <summary>
/// TDD tests for HTTP infrastructure events (IDs 10004–10006).
/// Validates http.connection.failed, http.auth.failed, and http.throttled events
/// emitted as SUPPLEMENTAL events alongside the existing operation events.
/// </summary>
public class HttpInfrastructureEventsTests : IAsyncDisposable
{
    // ─── Test Infrastructure ────────────────────────────────────────────

    /// <summary>All OtelEvents event names including infrastructure events.</summary>
    private static readonly HashSet<string> AllOtelEventNames =
    [
        "http.request.received",
        "http.request.completed",
        "http.request.failed",
        "http.connection.failed",
        "http.auth.failed",
        "http.throttled"
    ];

    /// <summary>Infrastructure event names only.</summary>
    private static readonly HashSet<string> InfraEventNames =
    [
        "http.connection.failed",
        "http.auth.failed",
        "http.throttled"
    ];

    private static List<TestLogRecord> GetOtelEvents(TestLogExporter exporter) =>
        exporter.LogRecords.Where(r => r.EventName is not null && AllOtelEventNames.Contains(r.EventName)).ToList();

    private static List<TestLogRecord> GetInfraEvents(TestLogExporter exporter) =>
        exporter.LogRecords.Where(r => r.EventName is not null && InfraEventNames.Contains(r.EventName)).ToList();

    /// <summary>
    /// Creates a TestServer with configurable endpoints and infrastructure event support.
    /// </summary>
    private static async Task<(IHost Host, TestLogExporter Exporter)> CreateTestHost(
        Action<OtelEventsAspNetCoreOptions>? configureOptions = null,
        Action<IEndpointRouteBuilder>? configureEndpoints = null)
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
                    services.AddOtelEventsAspNetCore(configureOptions ?? (_ => { }));
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(configureEndpoints ?? (endpoints =>
                    {
                        endpoints.MapGet("/api/ok", async context =>
                        {
                            await context.Response.WriteAsync("OK");
                        });

                        // 401 Unauthorized
                        endpoints.MapGet("/api/unauthorized", context =>
                        {
                            context.Response.StatusCode = 401;
                            context.Response.Headers["WWW-Authenticate"] = "Bearer";
                            return context.Response.WriteAsync("Unauthorized");
                        });

                        // 403 Forbidden
                        endpoints.MapGet("/api/forbidden", context =>
                        {
                            context.Response.StatusCode = 403;
                            context.Response.Headers["WWW-Authenticate"] = "Bearer realm=\"api\"";
                            return context.Response.WriteAsync("Forbidden");
                        });

                        // 429 Too Many Requests
                        endpoints.MapGet("/api/throttled", context =>
                        {
                            context.Response.StatusCode = 429;
                            context.Response.Headers["Retry-After"] = "30";
                            return context.Response.WriteAsync("Too Many Requests");
                        });

                        // 429 with absolute date Retry-After
                        endpoints.MapGet("/api/throttled-date", context =>
                        {
                            context.Response.StatusCode = 429;
                            var retryDate = DateTimeOffset.UtcNow.AddSeconds(60);
                            context.Response.Headers["Retry-After"] = retryDate.ToString("R");
                            return context.Response.WriteAsync("Too Many Requests");
                        });

                        // 429 without Retry-After header
                        endpoints.MapGet("/api/throttled-no-retry", context =>
                        {
                            context.Response.StatusCode = 429;
                            return context.Response.WriteAsync("Too Many Requests");
                        });

                        // 429 with X-RateLimit-Limit header
                        endpoints.MapGet("/api/throttled-with-limit", context =>
                        {
                            context.Response.StatusCode = 429;
                            context.Response.Headers["Retry-After"] = "10";
                            context.Response.Headers["X-RateLimit-Limit"] = "100";
                            return context.Response.WriteAsync("Too Many Requests");
                        });

                        // Endpoint that throws HttpRequestException (connection failed)
                        endpoints.MapGet("/api/connection-fail", _ =>
                        {
                            throw new HttpRequestException(
                                "Connection refused",
                                new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionRefused),
                                System.Net.HttpStatusCode.ServiceUnavailable);
                        });

                        // Endpoint that throws generic exception (not HttpRequestException)
                        endpoints.MapGet("/api/generic-throw", _ =>
                        {
                            throw new InvalidOperationException("Something broke");
                        });

                        // 401 with SAS token auth
                        endpoints.MapGet("/api/unauthorized-sas", context =>
                        {
                            context.Response.StatusCode = 401;
                            return context.Response.WriteAsync("Unauthorized");
                        });

                        // 401 with SharedKey auth
                        endpoints.MapGet("/api/unauthorized-sharedkey", context =>
                        {
                            context.Response.StatusCode = 401;
                            context.Response.Headers["WWW-Authenticate"] = "SharedKey";
                            return context.Response.WriteAsync("Unauthorized");
                        });

                        // 429 with extremely large retry-after (should be clamped)
                        endpoints.MapGet("/api/throttled-huge-retry", context =>
                        {
                            context.Response.StatusCode = 429;
                            context.Response.Headers["Retry-After"] = "999999";
                            return context.Response.WriteAsync("Too Many Requests");
                        });
                    }));
                });
            })
            .StartAsync();

        return (host, exporter);
    }

    private readonly List<IHost> _hosts = [];

    private async Task<(HttpClient Client, TestLogExporter Exporter)> CreateClient(
        Action<OtelEventsAspNetCoreOptions>? configureOptions = null,
        Action<IEndpointRouteBuilder>? configureEndpoints = null)
    {
        var (host, exporter) = await CreateTestHost(configureOptions, configureEndpoints);
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

    // ═══════════════════════════════════════════════════════════════════
    // §1 — EmitInfrastructureEvents Option
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void EmitInfrastructureEvents_DefaultsToTrue()
    {
        var options = new OtelEventsAspNetCoreOptions();
        Assert.True(options.EmitInfrastructureEvents);
    }

    [Fact]
    public async Task InfraEvents_Disabled_NoInfraEventsEmitted()
    {
        // Arrange — disable infra events
        var (client, exporter) = await CreateClient(options =>
        {
            options.EmitInfrastructureEvents = false;
        });

        // Act — 401 response
        await client.GetAsync("/api/unauthorized");

        // Assert — operation event fires, but NO infra event
        exporter.AssertEventEmitted("http.request.completed");
        Assert.Empty(GetInfraEvents(exporter));
    }

    // ═══════════════════════════════════════════════════════════════════
    // §2 — http.auth.failed (10005) — 401/403 Response
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AuthFailed_401_EmitsAuthFailedEvent()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await client.GetAsync("/api/unauthorized");

        // Assert — SUPPLEMENTAL: both completed + auth.failed
        exporter.AssertEventEmitted("http.request.completed");
        exporter.AssertEventEmitted("http.auth.failed");
    }

    [Fact]
    public async Task AuthFailed_403_EmitsAuthFailedEvent()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await client.GetAsync("/api/forbidden");

        // Assert
        exporter.AssertEventEmitted("http.request.completed");
        exporter.AssertEventEmitted("http.auth.failed");
    }

    [Fact]
    public async Task AuthFailed_HasCorrectEventId()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await client.GetAsync("/api/unauthorized");

        // Assert
        var record = exporter.AssertSingle("http.auth.failed");
        Assert.Equal(10005, record.EventId.Id);
        Assert.Equal("http.auth.failed", record.EventId.Name);
    }

    [Fact]
    public async Task AuthFailed_HasErrorLogLevel()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await client.GetAsync("/api/unauthorized");

        // Assert
        var record = exporter.AssertSingle("http.auth.failed");
        Assert.Equal(LogLevel.Error, record.LogLevel);
    }

    [Fact]
    public async Task AuthFailed_CapturesStatusCode()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await client.GetAsync("/api/unauthorized");

        // Assert
        var record = exporter.AssertSingle("http.auth.failed");
        record.AssertAttribute("HttpStatusCode", 401);
    }

    [Fact]
    public async Task AuthFailed_CapturesAuthScheme_Bearer()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await client.GetAsync("/api/unauthorized");

        // Assert — WWW-Authenticate: Bearer → authScheme = "Bearer"
        var record = exporter.AssertSingle("http.auth.failed");
        record.AssertAttribute("AuthScheme", "Bearer");
    }

    [Fact]
    public async Task AuthFailed_CapturesAuthScheme_Unknown_WhenNoHeader()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act — SAS endpoint returns 401 without WWW-Authenticate
        await client.GetAsync("/api/unauthorized-sas");

        // Assert
        var record = exporter.AssertSingle("http.auth.failed");
        record.AssertAttribute("AuthScheme", "Unknown");
    }

    [Fact]
    public async Task AuthFailed_CapturesIdentityHint_HashNotRaw()
    {
        // Arrange
        var (client, exporter) = await CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "my-secret-token-12345");

        // Act
        await client.GetAsync("/api/unauthorized");

        // Assert — identityHint should be first 8 chars of SHA-256 hash, NEVER raw credential
        var record = exporter.AssertSingle("http.auth.failed");
        Assert.True(record.Attributes.ContainsKey("identityHint"),
            $"Should capture identityHint. Available: {string.Join(", ", record.Attributes.Keys)}");
        var hint = record.Attributes["identityHint"] as string;
        Assert.NotNull(hint);
        Assert.Equal(8, hint.Length);
        // MUST NOT contain the raw token
        Assert.DoesNotContain("my-secret-token", hint);
    }

    [Fact]
    public async Task AuthFailed_200Response_NoAuthFailedEvent()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act — 200 OK, should NOT emit auth.failed
        await client.GetAsync("/api/ok");

        // Assert
        Assert.DoesNotContain(exporter.LogRecords, r => r.EventName == "http.auth.failed");
    }

    // ═══════════════════════════════════════════════════════════════════
    // §3 — http.throttled (10006) — 429 Response
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Throttled_429_EmitsThrottledEvent()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await client.GetAsync("/api/throttled");

        // Assert — SUPPLEMENTAL: both completed + throttled
        exporter.AssertEventEmitted("http.request.completed");
        exporter.AssertEventEmitted("http.throttled");
    }

    [Fact]
    public async Task Throttled_HasCorrectEventId()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await client.GetAsync("/api/throttled");

        // Assert
        var record = exporter.AssertSingle("http.throttled");
        Assert.Equal(10006, record.EventId.Id);
        Assert.Equal("http.throttled", record.EventId.Name);
    }

    [Fact]
    public async Task Throttled_HasWarningLogLevel()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await client.GetAsync("/api/throttled");

        // Assert
        var record = exporter.AssertSingle("http.throttled");
        Assert.Equal(LogLevel.Warning, record.LogLevel);
    }

    [Fact]
    public async Task Throttled_CapturesStatusCode()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await client.GetAsync("/api/throttled");

        // Assert
        var record = exporter.AssertSingle("http.throttled");
        record.AssertAttribute("HttpStatusCode", 429);
    }

    [Fact]
    public async Task Throttled_CapturesRetryAfterMs_FromSeconds()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act — Retry-After: 30 → 30000ms
        await client.GetAsync("/api/throttled");

        // Assert
        var record = exporter.AssertSingle("http.throttled");
        record.AssertAttribute("RetryAfterMs", 30000L);
    }

    [Fact]
    public async Task Throttled_RetryAfterMs_Null_WhenNoHeader()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act — no Retry-After header
        await client.GetAsync("/api/throttled-no-retry");

        // Assert
        var record = exporter.AssertSingle("http.throttled");
        Assert.True(record.Attributes.ContainsKey("RetryAfterMs"));
        Assert.Null(record.Attributes["RetryAfterMs"]);
    }

    [Fact]
    public async Task Throttled_RetryAfterMs_ClampedToMax()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act — Retry-After: 999999 → clamped to 3600000 (1 hour)
        await client.GetAsync("/api/throttled-huge-retry");

        // Assert
        var record = exporter.AssertSingle("http.throttled");
        record.AssertAttribute("RetryAfterMs", 3600000L);
    }

    [Fact]
    public async Task Throttled_CapturesCurrentLimit()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act — X-RateLimit-Limit: 100
        await client.GetAsync("/api/throttled-with-limit");

        // Assert
        var record = exporter.AssertSingle("http.throttled");
        record.AssertAttribute("currentLimit", "100");
    }

    // ═══════════════════════════════════════════════════════════════════
    // §4 — http.connection.failed (10004) — HttpRequestException
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ConnectionFailed_EmitsConnectionFailedEvent()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act — HttpRequestException
        await Assert.ThrowsAsync<HttpRequestException>(
            async () => await client.GetAsync("/api/connection-fail"));

        // Assert — SUPPLEMENTAL: both request.failed + connection.failed
        exporter.AssertEventEmitted("http.request.failed");
        exporter.AssertEventEmitted("http.connection.failed");
    }

    [Fact]
    public async Task ConnectionFailed_HasCorrectEventId()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await Assert.ThrowsAsync<HttpRequestException>(
            async () => await client.GetAsync("/api/connection-fail"));

        // Assert
        var record = exporter.AssertSingle("http.connection.failed");
        Assert.Equal(10004, record.EventId.Id);
        Assert.Equal("http.connection.failed", record.EventId.Name);
    }

    [Fact]
    public async Task ConnectionFailed_HasErrorLogLevel()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await Assert.ThrowsAsync<HttpRequestException>(
            async () => await client.GetAsync("/api/connection-fail"));

        // Assert
        var record = exporter.AssertSingle("http.connection.failed");
        Assert.Equal(LogLevel.Error, record.LogLevel);
    }

    [Fact]
    public async Task ConnectionFailed_CapturesEndpoint()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await Assert.ThrowsAsync<HttpRequestException>(
            async () => await client.GetAsync("/api/connection-fail"));

        // Assert
        var record = exporter.AssertSingle("http.connection.failed");
        Assert.True(record.Attributes.ContainsKey("Endpoint"),
            "Should capture endpoint");
        var endpoint = record.Attributes["Endpoint"] as string;
        Assert.NotNull(endpoint);
        Assert.Contains("/api/connection-fail", endpoint);
    }

    [Fact]
    public async Task ConnectionFailed_CapturesDurationMs()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await Assert.ThrowsAsync<HttpRequestException>(
            async () => await client.GetAsync("/api/connection-fail"));

        // Assert
        var record = exporter.AssertSingle("http.connection.failed");
        Assert.True(record.Attributes.ContainsKey("DurationMs"));
        var durationMs = (double)record.Attributes["DurationMs"]!;
        Assert.True(durationMs >= 0);
    }

    [Fact]
    public async Task ConnectionFailed_CapturesFailureReason_ConnectionRefused()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act
        await Assert.ThrowsAsync<HttpRequestException>(
            async () => await client.GetAsync("/api/connection-fail"));

        // Assert
        var record = exporter.AssertSingle("http.connection.failed");
        record.AssertAttribute("FailureReason", "ConnectionRefused");
    }

    [Fact]
    public async Task ConnectionFailed_NonHttpRequestException_NoConnectionFailedEvent()
    {
        // Arrange
        var (client, exporter) = await CreateClient();

        // Act — generic exception, not HttpRequestException
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await client.GetAsync("/api/generic-throw"));

        // Assert — http.request.failed YES, http.connection.failed NO
        exporter.AssertEventEmitted("http.request.failed");
        Assert.DoesNotContain(exporter.LogRecords, r => r.EventName == "http.connection.failed");
    }

    // ═══════════════════════════════════════════════════════════════════
    // §5 — Defensive Behavior
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task InfraEvents_NeverThrow_OnEventEmission()
    {
        // Arrange — this test verifies the middleware doesn't break even when
        // infrastructure event emission encounters edge cases.
        // A normal request should complete successfully regardless.
        var (client, exporter) = await CreateClient();

        // Act — 200 OK
        var response = await client.GetAsync("/api/ok");

        // Assert — request succeeds, no infra events for 200
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(GetInfraEvents(exporter));
    }

    // ═══════════════════════════════════════════════════════════════════
    // §6 — Schema Verification
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void EmbeddedSchema_ContainsInfrastructureEvents()
    {
        // Arrange
        var assembly = typeof(OtelEventsAspNetCoreOptions).Assembly;
        using var stream = assembly.GetManifestResourceStream("OtelEvents.AspNetCore.aspnetcore.all.yaml")!;
        using var reader = new StreamReader(stream);

        // Act
        var content = reader.ReadToEnd();

        // Assert — infrastructure events in schema
        Assert.Contains("http.connection.failed", content);
        Assert.Contains("http.auth.failed", content);
        Assert.Contains("http.throttled", content);
        Assert.Contains("id: 10004", content);
        Assert.Contains("id: 10005", content);
        Assert.Contains("id: 10006", content);
    }

    [Fact]
    public void EmbeddedSchema_ContainsInfrastructureFields()
    {
        // Arrange
        var assembly = typeof(OtelEventsAspNetCoreOptions).Assembly;
        using var stream = assembly.GetManifestResourceStream("OtelEvents.AspNetCore.aspnetcore.all.yaml")!;
        using var reader = new StreamReader(stream);

        // Act
        var content = reader.ReadToEnd();

        // Assert — infrastructure-specific field definitions
        Assert.Contains("failureReason", content);
        Assert.Contains("authScheme", content);
        Assert.Contains("identityHint", content);
        Assert.Contains("retryAfterMs", content);
        Assert.Contains("currentLimit", content);
    }
}
