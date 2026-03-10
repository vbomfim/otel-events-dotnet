using OtelEvents.Causality;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace OtelEvents.Causality.Tests;

/// <summary>
/// Tests for OtelEventsCausalityProcessor — BaseProcessor that enriches LogRecords
/// with otel_events.event_id and otel_events.parent_event_id attributes.
/// </summary>
public class OtelEventsCausalityProcessorTests : IDisposable
{
    private readonly TestLogExporter _exporter;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;

    public OtelEventsCausalityProcessorTests()
    {
        _exporter = new TestLogExporter();
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.AddProcessor(new OtelEventsCausalityProcessor());
                options.AddProcessor(new SimpleLogRecordExportProcessor(_exporter));
            });
            builder.SetMinimumLevel(LogLevel.Trace);
        });
        _logger = _loggerFactory.CreateLogger("TestLogger");
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
    }

    [Fact]
    public void OnEnd_AddsEventIdAttribute()
    {
        // Act
        _logger.LogInformation("Test message");

        // Assert
        var records = _exporter.GetRecords();
        Assert.Single(records);
        Assert.True(
            records[0].Attributes.ContainsKey("otel_events.event_id"),
            "LogRecord should have 'otel_events.event_id' attribute");
    }

    [Fact]
    public void OnEnd_EventIdHasEvtPrefix()
    {
        // Act
        _logger.LogInformation("Test message");

        // Assert
        var eventId = _exporter.GetRecords()[0].Attributes["otel_events.event_id"] as string;
        Assert.NotNull(eventId);
        Assert.StartsWith("evt_", eventId);
    }

    [Fact]
    public void OnEnd_EventIdIsValidUuid7Format()
    {
        // Act
        _logger.LogInformation("Test message");

        // Assert
        var eventId = _exporter.GetRecords()[0].Attributes["otel_events.event_id"] as string;
        Assert.NotNull(eventId);
        Assert.Matches(
            @"^evt_[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$",
            eventId);
    }

    [Fact]
    public void OnEnd_GeneratesUniqueEventIds()
    {
        // Act
        for (int i = 0; i < 100; i++)
        {
            _logger.LogInformation("Message {Index}", i);
        }

        // Assert
        var eventIds = _exporter.GetRecords()
            .Select(r => r.Attributes["otel_events.event_id"] as string)
            .ToHashSet();

        Assert.Equal(100, eventIds.Count);
    }

    [Fact]
    public void OnEnd_NoParentEventId_WhenNoScopeActive()
    {
        // Act
        _logger.LogInformation("Test message");

        // Assert
        var record = _exporter.GetRecords()[0];
        Assert.False(
            record.Attributes.ContainsKey("otel_events.parent_event_id"),
            "Should NOT have parent_event_id when no scope is active");
    }

    [Fact]
    public void OnEnd_SetsParentEventId_WhenScopeActive()
    {
        // Arrange
        var parentId = "evt_test-parent-id";

        // Act
        using (OtelEventsCausalityContext.SetParent(parentId))
        {
            _logger.LogInformation("Child event");
        }

        // Assert
        var record = _exporter.GetRecords()[0];
        Assert.Equal(parentId, record.Attributes["otel_events.parent_event_id"]);
    }

    [Fact]
    public void OnEnd_ParentEventId_FollowsScope()
    {
        // Act — emit outside scope, inside scope, and after scope
        _logger.LogInformation("Before scope");

        using (OtelEventsCausalityContext.SetParent("evt_parent-1"))
        {
            _logger.LogInformation("Inside scope");
        }

        _logger.LogInformation("After scope");

        // Assert
        var records = _exporter.GetRecords();
        Assert.Equal(3, records.Count);

        // Before scope — no parent
        Assert.False(records[0].Attributes.ContainsKey("otel_events.parent_event_id"));

        // Inside scope — has parent
        Assert.Equal("evt_parent-1", records[1].Attributes["otel_events.parent_event_id"]);

        // After scope — no parent
        Assert.False(records[2].Attributes.ContainsKey("otel_events.parent_event_id"));
    }

    [Fact]
    public void OnEnd_NestedScopes_UseInnerParent()
    {
        // Act
        using (OtelEventsCausalityContext.SetParent("evt_outer"))
        {
            _logger.LogInformation("In outer scope");

            using (OtelEventsCausalityContext.SetParent("evt_inner"))
            {
                _logger.LogInformation("In inner scope");
            }

            _logger.LogInformation("Back in outer scope");
        }

        // Assert
        var records = _exporter.GetRecords();
        Assert.Equal(3, records.Count);

        Assert.Equal("evt_outer", records[0].Attributes["otel_events.parent_event_id"]);
        Assert.Equal("evt_inner", records[1].Attributes["otel_events.parent_event_id"]);
        Assert.Equal("evt_outer", records[2].Attributes["otel_events.parent_event_id"]);
    }

    [Fact]
    public void OnEnd_PreservesExistingAttributes()
    {
        // Act — emit a structured log with parameters
        _logger.LogInformation("Order {OrderId} processed for {Amount}", "ORD-123", 42.50);

        // Assert — the otel_events.event_id is added alongside existing structured attributes
        var record = _exporter.GetRecords()[0];
        Assert.True(record.Attributes.ContainsKey("otel_events.event_id"));
        // Existing structured params should still be there
        Assert.True(
            record.Attributes.ContainsKey("OrderId") || record.Attributes.ContainsKey("{OriginalFormat}"),
            "Existing attributes should be preserved");
    }

    [Fact]
    public void OnEnd_WorksWithAllLogLevels()
    {
        // Act
        _logger.LogTrace("Trace");
        _logger.LogDebug("Debug");
        _logger.LogInformation("Info");
        _logger.LogWarning("Warning");
        _logger.LogError("Error");
        _logger.LogCritical("Critical");

        // Assert — all records get event IDs
        var records = _exporter.GetRecords();
        Assert.Equal(6, records.Count);

        foreach (var record in records)
        {
            var eventId = record.Attributes["otel_events.event_id"] as string;
            Assert.NotNull(eventId);
            Assert.StartsWith("evt_", eventId);
        }
    }

    [Fact]
    public async Task OnEnd_ConcurrentRequests_MaintainSeparateCausalChains()
    {
        // Arrange
        var barrier = new Barrier(2);
        const int eventsPerTask = 10;

        // Act — two concurrent "requests" with different parents
        var task1 = Task.Run(() =>
        {
            using var scope = OtelEventsCausalityContext.SetParent("evt_request-1");
            barrier.SignalAndWait();
            for (int i = 0; i < eventsPerTask; i++)
            {
                _logger.LogInformation("Request 1 event {Index}", i);
            }
        });

        var task2 = Task.Run(() =>
        {
            using var scope = OtelEventsCausalityContext.SetParent("evt_request-2");
            barrier.SignalAndWait();
            for (int i = 0; i < eventsPerTask; i++)
            {
                _logger.LogInformation("Request 2 event {Index}", i);
            }
        });

        await Task.WhenAll(task1, task2);

        // Assert — each event should have either request-1 or request-2 parent, never mixed
        var records = _exporter.GetRecords();
        Assert.Equal(eventsPerTask * 2, records.Count);

        foreach (var record in records)
        {
            var parentId = record.Attributes["otel_events.parent_event_id"] as string;
            Assert.NotNull(parentId);
            Assert.True(
                parentId == "evt_request-1" || parentId == "evt_request-2",
                $"Unexpected parent ID: {parentId}");
        }
    }
}
