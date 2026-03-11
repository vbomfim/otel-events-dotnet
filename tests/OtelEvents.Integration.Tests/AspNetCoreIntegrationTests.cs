using System.Collections.Concurrent;
using System.Diagnostics;
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
using OtelEvents.Causality;

namespace OtelEvents.Integration.Tests;

/// <summary>
/// Integration tests for the ASP.NET Core middleware with a real OTEL pipeline.
/// Uses TestServer to make HTTP requests and verifies the correct lifecycle
/// events are emitted through the full pipeline.
/// </summary>
public class AspNetCoreIntegrationTests : IAsyncDisposable
{
    private readonly List<IHost> _hosts = [];

    public async ValueTask DisposeAsync()
    {
        foreach (var host in _hosts)
        {
            await host.StopAsync();
            host.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    // ─── Test Infrastructure ────────────────────────────────────────────

    /// <summary>
    /// Creates a TestServer with the OtelEvents middleware and in-memory log exporter.
    /// Follows the same pattern as existing OtelEventsAspNetCoreMiddlewareTests.
    /// </summary>
    private async Task<(IHost Host, IntegrationTestLogExporter Exporter)> CreateTestHost(
        Action<OtelEventsAspNetCoreOptions>? configureOptions = null)
    {
        var exporter = new IntegrationTestLogExporter();

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
                    app.UseRouting();

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

                        endpoints.MapGet("/api/throw", _ =>
                        {
                            throw new InvalidOperationException("Test exception");
                        });

                        endpoints.MapGet("/health", async context =>
                        {
                            await context.Response.WriteAsync("Healthy");
                        });
                    });
                });
            })
            .StartAsync();

        _hosts.Add(host);
        return (host, exporter);
    }

    /// <summary>Event names emitted by the OtelEvents ASP.NET Core middleware.</summary>
    private static readonly HashSet<string> OtelEventNames =
    [
        "http.request.received",
        "http.request.completed",
        "http.request.failed"
    ];

    // ─── Test 2: ASP.NET Core Middleware Integration ─────────────────────

    /// <summary>
    /// Verifies that the middleware emits http.request.received and
    /// http.request.completed events with correct fields when processing
    /// a successful HTTP request through TestServer.
    /// </summary>
    [Fact]
    public async Task Middleware_Emits_Received_And_Completed_Events_For_Successful_Request()
    {
        // Arrange
        var (host, exporter) = await CreateTestHost();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/api/orders");

        // Assert: response should be successful
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        // Filter to only OtelEvents events (exclude framework noise)
        var otelRecords = exporter.LogRecords
            .Where(r => OtelEventNames.Contains(r.EventName ?? ""))
            .ToList();

        // Should have http.request.received and http.request.completed
        Assert.Contains(otelRecords, r => r.EventName == "http.request.received");
        Assert.Contains(otelRecords, r => r.EventName == "http.request.completed");

        // Verify http.request.received fields
        var received = otelRecords.First(r => r.EventName == "http.request.received");
        Assert.Equal(10001, received.EventId.Id);
        Assert.True(received.Attributes.ContainsKey("HttpMethod") || received.Attributes.ContainsKey("httpMethod"),
            $"Expected HttpMethod attribute. Available: {string.Join(", ", received.Attributes.Keys)}");

        // Verify http.request.completed fields
        var completed = otelRecords.First(r => r.EventName == "http.request.completed");
        Assert.Equal(10002, completed.EventId.Id);
        Assert.Equal(LogLevel.Information, completed.LogLevel);

        // Verify completed event has HTTP method and status code attributes
        var completedAttrs = completed.Attributes;
        var hasMethod = completedAttrs.ContainsKey("HttpMethod") || completedAttrs.ContainsKey("httpMethod");
        Assert.True(hasMethod,
            $"Expected HttpMethod attribute. Available: {string.Join(", ", completedAttrs.Keys)}");

        var hasStatusCode = completedAttrs.ContainsKey("HttpStatusCode") || completedAttrs.ContainsKey("httpStatusCode");
        Assert.True(hasStatusCode,
            $"Expected HttpStatusCode attribute. Available: {string.Join(", ", completedAttrs.Keys)}");

        // Verify status code value is 200
        var statusCodeKey = completedAttrs.ContainsKey("HttpStatusCode") ? "HttpStatusCode" : "httpStatusCode";
        Assert.Equal(200, completedAttrs[statusCodeKey]);
    }

    /// <summary>
    /// Verifies that the middleware emits http.request.failed event when
    /// an unhandled exception occurs during request processing.
    /// </summary>
    [Fact]
    public async Task Middleware_Emits_Failed_Event_For_Unhandled_Exception()
    {
        // Arrange
        var (host, exporter) = await CreateTestHost();
        var client = host.GetTestClient();

        // Act: request endpoint that throws
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await client.GetAsync("/api/throw"));

        // Assert
        var otelRecords = exporter.LogRecords
            .Where(r => OtelEventNames.Contains(r.EventName ?? ""))
            .ToList();

        Assert.Contains(otelRecords, r => r.EventName == "http.request.failed");

        var failed = otelRecords.First(r => r.EventName == "http.request.failed");
        Assert.Equal(10003, failed.EventId.Id);
        Assert.Equal(LogLevel.Error, failed.LogLevel);

        // Verify error type attribute
        var attrs = failed.Attributes;
        var hasErrorType = attrs.ContainsKey("ErrorType") || attrs.ContainsKey("errorType");
        Assert.True(hasErrorType,
            $"Expected ErrorType attribute. Available: {string.Join(", ", attrs.Keys)}");

        var errorTypeKey = attrs.ContainsKey("ErrorType") ? "ErrorType" : "errorType";
        Assert.Equal("InvalidOperationException", attrs[errorTypeKey]);
    }

    /// <summary>
    /// Verifies that the middleware generates causal event IDs for each
    /// HTTP lifecycle event when CausalityProcessor is in the pipeline.
    /// </summary>
    [Fact]
    public async Task Middleware_Events_Have_Causal_EventIds()
    {
        // Arrange
        var (host, exporter) = await CreateTestHost();
        var client = host.GetTestClient();

        // Act
        await client.GetAsync("/api/orders");

        // Assert: all events should have otel_events.event_id
        var otelRecords = exporter.LogRecords
            .Where(r => OtelEventNames.Contains(r.EventName ?? ""))
            .ToList();

        foreach (var record in otelRecords)
        {
            Assert.True(record.Attributes.ContainsKey("otel_events.event_id"),
                $"Event '{record.EventName}' missing otel_events.event_id. " +
                $"Available: {string.Join(", ", record.Attributes.Keys)}");

            var eventId = record.Attributes["otel_events.event_id"]?.ToString();
            Assert.NotNull(eventId);
            Assert.StartsWith("evt_", eventId);
        }
    }

    /// <summary>
    /// Verifies that excluded paths do not emit any events.
    /// </summary>
    [Fact]
    public async Task Excluded_Paths_Do_Not_Emit_Events()
    {
        // Arrange: exclude /health
        var (host, exporter) = await CreateTestHost(opts =>
        {
            opts.ExcludePaths = ["/health"];
        });
        var client = host.GetTestClient();

        // Act: request the excluded path
        var response = await client.GetAsync("/health");

        // Assert: response should succeed but no otel events emitted
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var otelRecords = exporter.LogRecords
            .Where(r => OtelEventNames.Contains(r.EventName ?? ""))
            .ToList();

        Assert.Empty(otelRecords);
    }
}

// ─── Test Infrastructure: In-Memory Log Exporter ────────────────────────

/// <summary>
/// In-memory OTEL LogRecord collector for integration tests.
/// Captures immutable snapshots of LogRecords for safe assertions.
/// </summary>
internal sealed class IntegrationTestLogExporter : BaseExporter<LogRecord>
{
    private readonly ConcurrentQueue<IntegrationTestLogRecord> _records = new();

    /// <summary>Gets all captured log records as a list in chronological order.</summary>
    public IReadOnlyList<IntegrationTestLogRecord> LogRecords => _records.ToArray().ToList();

    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        foreach (var record in batch)
        {
            _records.Enqueue(IntegrationTestLogRecord.From(record));
        }

        return ExportResult.Success;
    }
}

/// <summary>
/// Immutable snapshot of a LogRecord for integration test assertions.
/// </summary>
internal sealed class IntegrationTestLogRecord
{
    public string? EventName { get; init; }
    public EventId EventId { get; init; }
    public LogLevel LogLevel { get; init; }
    public string? FormattedMessage { get; init; }
    public Exception? Exception { get; init; }
    public IReadOnlyDictionary<string, object?> Attributes { get; init; } = new Dictionary<string, object?>();

    public static IntegrationTestLogRecord From(LogRecord record)
    {
        var attrs = new Dictionary<string, object?>();
        if (record.Attributes is not null)
        {
            foreach (var kvp in record.Attributes)
            {
                attrs[kvp.Key] = kvp.Value;
            }
        }

        return new IntegrationTestLogRecord
        {
            EventName = record.EventId.Name,
            EventId = record.EventId,
            LogLevel = record.LogLevel,
            FormattedMessage = record.FormattedMessage,
            Exception = record.Exception,
            Attributes = attrs,
        };
    }
}
