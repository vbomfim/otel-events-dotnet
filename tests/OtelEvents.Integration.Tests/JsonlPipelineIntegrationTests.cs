using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OtelEvents.Causality;
using OtelEvents.Exporter.Json;
using OtelEvents.Testing;

namespace OtelEvents.Integration.Tests;

/// <summary>
/// End-to-end integration tests for the otel-events pipeline.
/// Validates the full flow: ILogger → Processors → Exporter → JSONL output.
/// Uses real OTEL pipeline components (no mocks) to verify correct wiring
/// and data transformation across the entire event lifecycle.
/// </summary>
public class JsonlPipelineIntegrationTests
{
    // ─── Test Infrastructure ────────────────────────────────────────────

    /// <summary>
    /// Creates a real OTEL pipeline that writes JSONL to a MemoryStream.
    /// Returns the factory and stream for assertions.
    /// </summary>
    private static (ILoggerFactory Factory, MemoryStream Stream) CreateJsonlPipeline(
        Action<OtelEventsJsonExporterOptions>? configureExporter = null,
        Action<OtelEventsSeverityFilterOptions>? configureSeverityFilter = null,
        Action<OtelEventsRateLimitOptions>? configureRateLimit = null,
        bool addCausality = true)
    {
        var stream = new MemoryStream();
        var exporterOptions = new OtelEventsJsonExporterOptions();
        configureExporter?.Invoke(exporterOptions);

        var exporter = new OtelEventsJsonExporter(exporterOptions, stream);
        BaseProcessor<LogRecord> innerProcessor = new SimpleLogRecordExportProcessor(exporter);

        if (configureRateLimit is not null)
        {
            var rateLimitOptions = new OtelEventsRateLimitOptions();
            configureRateLimit(rateLimitOptions);
            innerProcessor = new OtelEventsRateLimitProcessor(rateLimitOptions, innerProcessor);
        }

        if (configureSeverityFilter is not null)
        {
            var filterOptions = new OtelEventsSeverityFilterOptions();
            configureSeverityFilter(filterOptions);
            innerProcessor = new OtelEventsSeverityFilterProcessor(filterOptions, innerProcessor);
        }

        var factory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.ParseStateValues = true;

                if (addCausality)
                {
                    options.AddProcessor(new OtelEventsCausalityProcessor());
                }

                options.AddProcessor(innerProcessor);
            });
        });

        return (factory, stream);
    }

    /// <summary>
    /// Parses JSONL output from a MemoryStream into a list of JsonDocument objects.
    /// </summary>
    private static List<JsonDocument> ParseJsonlOutput(MemoryStream stream)
    {
        stream.Position = 0;
        var content = Encoding.UTF8.GetString(stream.ToArray());

        return content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonDocument.Parse(line))
            .ToList();
    }

    // ─── Test 1: Full Pipeline JSONL Output ─────────────────────────────

    /// <summary>
    /// Verifies the complete pipeline: LoggerFactory → CausalityProcessor
    /// → SeverityFilter → JsonExporter writes valid JSONL with all
    /// required envelope fields.
    /// </summary>
    [Fact]
    public void Full_Pipeline_Produces_Valid_Jsonl_With_All_Envelope_Fields()
    {
        // Arrange: build full pipeline with causality + severity filter
        var (factory, stream) = CreateJsonlPipeline(
            configureExporter: opts =>
            {
                opts.EnvironmentProfile = OtelEventsEnvironmentProfile.Development;
                opts.SchemaVersion = "1.0.0";
            },
            configureSeverityFilter: opts =>
            {
                opts.MinSeverity = LogLevel.Information;
            });

        // Act: emit an event through the pipeline
        var logger = factory.CreateLogger("IntegrationTest");
        logger.Log(
            logLevel: LogLevel.Information,
            eventId: new EventId(1001, "order.placed"),
            state: new[] { new KeyValuePair<string, object?>("orderId", "ORD-42") },
            exception: null,
            formatter: (s, _) => "Order ORD-42 placed");

        // Flush the pipeline by disposing the factory
        factory.Dispose();

        // Assert: parse JSONL and verify all envelope fields
        var docs = ParseJsonlOutput(stream);
        Assert.Single(docs);

        var root = docs[0].RootElement;

        // timestamp — ISO 8601 format with microseconds
        Assert.True(root.TryGetProperty("timestamp", out var timestamp));
        Assert.True(DateTime.TryParse(timestamp.GetString(), out _),
            $"timestamp '{timestamp.GetString()}' is not a valid ISO 8601 datetime");

        // event — the event name
        Assert.Equal("order.placed", root.GetProperty("event").GetString());

        // severity — mapped string (INFO for LogLevel.Information)
        Assert.Equal("INFO", root.GetProperty("severity").GetString());

        // severityNumber — OTEL severity number (9 for Information)
        Assert.Equal(9, root.GetProperty("severityNumber").GetInt32());

        // eventId — from CausalityProcessor (evt_<uuid7>)
        Assert.True(root.TryGetProperty("eventId", out var eventId));
        Assert.StartsWith("evt_", eventId.GetString());
        Assert.Equal(40, eventId.GetString()!.Length); // "evt_" + 36-char UUID

        // attr — contains the orderId attribute
        Assert.True(root.TryGetProperty("attr", out var attr));
        Assert.Equal("ORD-42", attr.GetProperty("orderId").GetString());

        // otel_events.v — schema version
        Assert.Equal("1.0.0", root.GetProperty("otel_events.v").GetString());

        // otel_events.seq — sequence number (first event = 1)
        Assert.Equal(1, root.GetProperty("otel_events.seq").GetInt64());

        // message — formatted message
        Assert.Equal("Order ORD-42 placed", root.GetProperty("message").GetString());

        // Clean up
        foreach (var doc in docs) doc.Dispose();
        stream.Dispose();
    }

    // ─── Test 3: Non-otel-events ILogger Passthrough ────────────────────

    /// <summary>
    /// Verifies that a standard ILogger.LogInformation("plain message") call
    /// (without EventId) produces JSONL with event="dotnet.ilogger" fallback.
    /// </summary>
    [Fact]
    public void Plain_ILogger_Message_Produces_Dotnet_ILogger_Event_Name()
    {
        // Arrange
        var (factory, stream) = CreateJsonlPipeline(
            configureExporter: opts =>
            {
                opts.EnvironmentProfile = OtelEventsEnvironmentProfile.Development;
            },
            addCausality: false);

        // Act: emit a plain log message without EventId
        var logger = factory.CreateLogger("PlainLogger");
        logger.LogInformation("Hello from plain ILogger");

        factory.Dispose();

        // Assert
        var docs = ParseJsonlOutput(stream);
        Assert.Single(docs);

        var root = docs[0].RootElement;
        Assert.Equal("dotnet.ilogger", root.GetProperty("event").GetString());
        Assert.Equal("INFO", root.GetProperty("severity").GetString());
        Assert.Equal("Hello from plain ILogger", root.GetProperty("message").GetString());

        foreach (var doc in docs) doc.Dispose();
        stream.Dispose();
    }

    // ─── Test 4: Sensitivity Redaction End-to-End ────────────────────────

    /// <summary>
    /// Verifies that PII-classified fields are redacted in Production profile.
    /// Uses the built-in sensitivity registry where "clientIp" is classified as PII.
    /// </summary>
    [Fact]
    public void Production_Profile_Redacts_Pii_Fields_In_Jsonl_Output()
    {
        // Arrange: Production profile should redact PII fields
        var (factory, stream) = CreateJsonlPipeline(
            configureExporter: opts =>
            {
                opts.EnvironmentProfile = OtelEventsEnvironmentProfile.Production;
                // Add a custom PII-classified field for the test
                opts.SensitivityMappings = new Dictionary<string, OtelEventsSensitivity>
                {
                    ["emailAddress"] = OtelEventsSensitivity.Pii,
                };
            },
            addCausality: false);

        // Act: emit an event with PII attributes
        var logger = factory.CreateLogger("RedactionTest");
        logger.Log(
            logLevel: LogLevel.Information,
            eventId: new EventId(2001, "user.login"),
            state: new[]
            {
                new KeyValuePair<string, object?>("emailAddress", "user@example.com"),
                new KeyValuePair<string, object?>("clientIp", "192.168.1.1"),
                new KeyValuePair<string, object?>("action", "login"),
            },
            exception: null,
            formatter: (s, _) => "User login");

        factory.Dispose();

        // Assert: PII fields should be redacted, public fields should be visible
        var docs = ParseJsonlOutput(stream);
        Assert.Single(docs);

        var root = docs[0].RootElement;
        var attr = root.GetProperty("attr");

        // PII fields must be redacted with [REDACTED:pii]
        Assert.Equal("[REDACTED:pii]", attr.GetProperty("emailAddress").GetString());
        Assert.Equal("[REDACTED:pii]", attr.GetProperty("clientIp").GetString());

        // Non-sensitive field must be visible
        Assert.Equal("login", attr.GetProperty("action").GetString());

        foreach (var doc in docs) doc.Dispose();
        stream.Dispose();
    }

    /// <summary>
    /// Verifies that PII fields are NOT redacted in Development profile.
    /// </summary>
    [Fact]
    public void Development_Profile_Does_Not_Redact_Pii_Fields()
    {
        // Arrange: Development profile allows PII
        var (factory, stream) = CreateJsonlPipeline(
            configureExporter: opts =>
            {
                opts.EnvironmentProfile = OtelEventsEnvironmentProfile.Development;
            },
            addCausality: false);

        // Act
        var logger = factory.CreateLogger("RedactionTest");
        logger.Log(
            logLevel: LogLevel.Information,
            eventId: new EventId(2002, "user.viewed"),
            state: new[]
            {
                new KeyValuePair<string, object?>("clientIp", "10.0.0.1"),
                new KeyValuePair<string, object?>("action", "view"),
            },
            exception: null,
            formatter: (s, _) => "User viewed page");

        factory.Dispose();

        // Assert: PII fields visible in Development
        var docs = ParseJsonlOutput(stream);
        Assert.Single(docs);

        var attr = docs[0].RootElement.GetProperty("attr");
        Assert.Equal("10.0.0.1", attr.GetProperty("clientIp").GetString());
        Assert.Equal("view", attr.GetProperty("action").GetString());

        foreach (var doc in docs) doc.Dispose();
        stream.Dispose();
    }

    /// <summary>
    /// Verifies that Credential fields are ALWAYS redacted, even in Development.
    /// </summary>
    [Fact]
    public void Credential_Fields_Are_Always_Redacted_Even_In_Development()
    {
        // Arrange
        var (factory, stream) = CreateJsonlPipeline(
            configureExporter: opts =>
            {
                opts.EnvironmentProfile = OtelEventsEnvironmentProfile.Development;
            },
            addCausality: false);

        // Act
        var logger = factory.CreateLogger("CredentialTest");
        logger.Log(
            logLevel: LogLevel.Warning,
            eventId: new EventId(2003, "config.loaded"),
            state: new[]
            {
                new KeyValuePair<string, object?>("apiKey", "sk-12345-secret"),
                new KeyValuePair<string, object?>("region", "us-east-1"),
            },
            exception: null,
            formatter: (s, _) => "Config loaded");

        factory.Dispose();

        // Assert: apiKey must be redacted even in Development
        var docs = ParseJsonlOutput(stream);
        Assert.Single(docs);

        var attr = docs[0].RootElement.GetProperty("attr");
        Assert.Equal("[REDACTED:credential]", attr.GetProperty("apiKey").GetString());
        Assert.Equal("us-east-1", attr.GetProperty("region").GetString());

        foreach (var doc in docs) doc.Dispose();
        stream.Dispose();
    }

    // ─── Test 5: Causal Scope Propagation ───────────────────────────────

    /// <summary>
    /// Verifies that CausalityProcessor generates eventId for each event,
    /// and that events within a causal scope carry the correct parentEventId.
    /// </summary>
    [Fact]
    public void Causal_Scope_Propagates_ParentEventId_To_Child_Events()
    {
        // Arrange
        var (factory, stream) = CreateJsonlPipeline(
            configureExporter: opts =>
            {
                opts.EnvironmentProfile = OtelEventsEnvironmentProfile.Development;
            },
            addCausality: true);

        var logger = factory.CreateLogger("CausalTest");

        // Act: emit parent event (outside scope)
        logger.Log(
            logLevel: LogLevel.Information,
            eventId: new EventId(3001, "request.started"),
            state: new[] { new KeyValuePair<string, object?>("path", "/api/orders") },
            exception: null,
            formatter: (s, _) => "Request started");

        // Parse parent eventId to use as scope parent
        factory.Dispose();

        var docs = ParseJsonlOutput(stream);
        Assert.Single(docs);

        var parentEventId = docs[0].RootElement.GetProperty("eventId").GetString()!;
        Assert.StartsWith("evt_", parentEventId);

        foreach (var doc in docs) doc.Dispose();
        stream.Dispose();
    }

    /// <summary>
    /// Verifies that child events within a causal scope carry parentEventId
    /// matching the scope's parent event ID.
    /// </summary>
    [Fact]
    public void Child_Events_In_Causal_Scope_Have_Matching_ParentEventId()
    {
        // Arrange: pipeline with causality
        var (factory, stream) = CreateJsonlPipeline(
            configureExporter: opts =>
            {
                opts.EnvironmentProfile = OtelEventsEnvironmentProfile.Development;
            },
            addCausality: true);

        var logger = factory.CreateLogger("CausalScopeTest");

        // Act: emit parent event (no scope), then set scope and emit child
        logger.Log(
            logLevel: LogLevel.Information,
            eventId: new EventId(4001, "scope.parent"),
            state: new[] { new KeyValuePair<string, object?>("step", "parent") },
            exception: null,
            formatter: (s, _) => "Parent event");

        // Set a causal scope — all subsequent events should carry this parentEventId
        var scopeParentId = Uuid7.FormatEventId();
        using (OtelEventsCausalityContext.SetParent(scopeParentId))
        {
            logger.Log(
                logLevel: LogLevel.Information,
                eventId: new EventId(4002, "scope.child"),
                state: new[] { new KeyValuePair<string, object?>("step", "child") },
                exception: null,
                formatter: (s, _) => "Child event in scope");
        }

        factory.Dispose();

        // Assert
        var docs = ParseJsonlOutput(stream);
        Assert.Equal(2, docs.Count);

        // Parent event: should have eventId but no parentEventId
        var parentRoot = docs[0].RootElement;
        Assert.Equal("scope.parent", parentRoot.GetProperty("event").GetString());
        Assert.True(parentRoot.TryGetProperty("eventId", out var parentEvtId));
        Assert.StartsWith("evt_", parentEvtId.GetString());
        // Parent should NOT have parentEventId (no scope was active)
        Assert.False(parentRoot.TryGetProperty("parentEventId", out _),
            "Parent event should not have parentEventId");

        // Child event: should have both eventId and parentEventId
        var childRoot = docs[1].RootElement;
        Assert.Equal("scope.child", childRoot.GetProperty("event").GetString());
        Assert.True(childRoot.TryGetProperty("eventId", out var childEvtId));
        Assert.StartsWith("evt_", childEvtId.GetString());
        Assert.True(childRoot.TryGetProperty("parentEventId", out var childParentId));
        Assert.Equal(scopeParentId, childParentId.GetString());

        // eventId should differ between parent and child
        Assert.NotEqual(parentEvtId.GetString(), childEvtId.GetString());

        foreach (var doc in docs) doc.Dispose();
        stream.Dispose();
    }

    // ─── Test 6: Severity Filtering ─────────────────────────────────────

    /// <summary>
    /// Verifies that events below the configured MinSeverity are dropped
    /// and do not appear in the JSONL output.
    /// </summary>
    [Fact]
    public void Severity_Filter_Drops_Events_Below_Minimum_Level()
    {
        // Arrange: only Warning and above should pass
        var (factory, stream) = CreateJsonlPipeline(
            configureExporter: opts =>
            {
                opts.EnvironmentProfile = OtelEventsEnvironmentProfile.Development;
            },
            configureSeverityFilter: opts =>
            {
                opts.MinSeverity = LogLevel.Warning;
            },
            addCausality: false);

        var logger = factory.CreateLogger("SeverityTest");

        // Act: emit events at Debug and Warning levels
        logger.Log(
            logLevel: LogLevel.Debug,
            eventId: new EventId(5001, "debug.event"),
            state: new[] { new KeyValuePair<string, object?>("detail", "debug-info") },
            exception: null,
            formatter: (s, _) => "Debug event");

        logger.Log(
            logLevel: LogLevel.Warning,
            eventId: new EventId(5002, "warning.event"),
            state: new[] { new KeyValuePair<string, object?>("detail", "warning-info") },
            exception: null,
            formatter: (s, _) => "Warning event");

        factory.Dispose();

        // Assert: only Warning event should appear
        var docs = ParseJsonlOutput(stream);
        Assert.Single(docs);

        var root = docs[0].RootElement;
        Assert.Equal("warning.event", root.GetProperty("event").GetString());
        Assert.Equal("WARN", root.GetProperty("severity").GetString());
        Assert.Equal(13, root.GetProperty("severityNumber").GetInt32());

        foreach (var doc in docs) doc.Dispose();
        stream.Dispose();
    }

    /// <summary>
    /// Verifies that events at exactly the MinSeverity level pass through.
    /// </summary>
    [Fact]
    public void Severity_Filter_Passes_Events_At_Exact_Minimum_Level()
    {
        // Arrange
        var (factory, stream) = CreateJsonlPipeline(
            configureSeverityFilter: opts =>
            {
                opts.MinSeverity = LogLevel.Information;
            },
            addCausality: false);

        var logger = factory.CreateLogger("SeverityTest");

        // Act: emit Trace (below), Information (at), Error (above)
        logger.Log(LogLevel.Trace, new EventId(6001, "trace.event"), "Trace event");
        logger.Log(LogLevel.Information, new EventId(6002, "info.event"), "Info event");
        logger.Log(LogLevel.Error, new EventId(6003, "error.event"), "Error event");

        factory.Dispose();

        // Assert: Trace should be dropped, Information and Error should pass
        var docs = ParseJsonlOutput(stream);
        Assert.Equal(2, docs.Count);

        var events = docs.Select(d => d.RootElement.GetProperty("event").GetString()).ToList();
        Assert.Contains("info.event", events);
        Assert.Contains("error.event", events);
        Assert.DoesNotContain("trace.event", events);

        foreach (var doc in docs) doc.Dispose();
        stream.Dispose();
    }

    // ─── Test 7: Rate Limiting ──────────────────────────────────────────

    /// <summary>
    /// Verifies that rate limiting drops events exceeding the configured
    /// maximum events per window.
    /// </summary>
    [Fact]
    public void Rate_Limiter_Drops_Events_Exceeding_Window_Limit()
    {
        // Arrange: limit to 2 events per 1-second window
        var (factory, stream) = CreateJsonlPipeline(
            configureExporter: opts =>
            {
                opts.EnvironmentProfile = OtelEventsEnvironmentProfile.Development;
            },
            configureRateLimit: opts =>
            {
                opts.DefaultMaxEventsPerWindow = 2;
                opts.Window = TimeSpan.FromSeconds(1);
            },
            addCausality: false);

        var logger = factory.CreateLogger("RateLimitTest");

        // Act: emit 5 events rapidly (within the same window)
        for (var i = 1; i <= 5; i++)
        {
            logger.Log(
                logLevel: LogLevel.Information,
                eventId: new EventId(7000 + i, "rate.test"),
                state: new[] { new KeyValuePair<string, object?>("index", i) },
                exception: null,
                formatter: (s, _) => $"Event {i}");
        }

        factory.Dispose();

        // Assert: only 2 events should appear (rate limit = 2/sec)
        var docs = ParseJsonlOutput(stream);
        Assert.Equal(2, docs.Count);

        // Verify both are "rate.test" events
        foreach (var doc in docs)
        {
            Assert.Equal("rate.test", doc.RootElement.GetProperty("event").GetString());
        }

        foreach (var doc in docs) doc.Dispose();
        stream.Dispose();
    }

    // ─── Test: InMemoryLogExporter Passthrough ──────────────────────────

    /// <summary>
    /// Verifies that the InMemoryLogExporter from OtelEvents.Testing captures
    /// events correctly through the real pipeline (used as a complementary
    /// verification mechanism alongside JSONL output).
    /// </summary>
    [Fact]
    public void InMemory_Exporter_Captures_Events_Through_Real_Pipeline()
    {
        // Arrange: use OtelEventsTestHost for in-memory capture
        var (factory, exporter) = OtelEventsTestHost.Create();

        var logger = factory.CreateLogger("InMemoryTest");

        // Act
        logger.Log(
            logLevel: LogLevel.Information,
            eventId: new EventId(8001, "order.created"),
            state: new[] { new KeyValuePair<string, object?>("orderId", "ORD-99") },
            exception: null,
            formatter: (s, _) => "Order created");

        factory.Dispose();

        // Assert: use LogAssertions
        exporter.AssertEventEmitted("order.created");
        exporter.AssertNoErrors();

        var record = exporter.AssertSingle("order.created");
        record.AssertAttribute("orderId", "ORD-99");
        Assert.Equal(LogLevel.Information, record.LogLevel);
        Assert.Equal("Order created", record.FormattedMessage);
    }
}
