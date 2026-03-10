using Microsoft.Extensions.Logging;

namespace OtelEvents.Exporter.Json.Tests;

/// <summary>
/// Tests for reserved <c>all.*</c> prefix stripping from non-ALL attributes.
/// Per §16.4: any attribute with key starting with <c>all.</c> that was NOT set by
/// OtelEventsCausalityProcessor or OtelEventsJsonExporter is stripped from the exported envelope.
/// </summary>
public sealed class ReservedPrefixTests
{
    [Fact]
    public void Export_ReservedAllPrefixAttribute_IsStripped()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("otel_events.custom_field", "should be stripped"),
                new("method", "GET"),
            ]);

        var doc = harness.ExportSingle(lr);

        var attr = doc.RootElement.GetProperty("attr");
        Assert.False(attr.TryGetProperty("otel_events.custom_field", out _));
        Assert.Equal("GET", attr.GetProperty("method").GetString());
    }

    [Fact]
    public void Export_AllEventId_IsNotStripped()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("otel_events.event_id", "evt_abc123"),
                new("method", "GET"),
            ]);

        var doc = harness.ExportSingle(lr);

        // otel_events.event_id is an allowed prefix — extracted to top-level eventId
        Assert.Equal("evt_abc123", doc.RootElement.GetProperty("eventId").GetString());
    }

    [Fact]
    public void Export_AllParentEventId_IsNotStripped()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("otel_events.parent_event_id", "evt_parent123"),
                new("method", "GET"),
            ]);

        var doc = harness.ExportSingle(lr);

        Assert.Equal("evt_parent123", doc.RootElement.GetProperty("parentEventId").GetString());
    }

    [Fact]
    public void Export_AllTags_IsNotStripped()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("otel_events.tags", new[] { "api" }),
                new("method", "GET"),
            ]);

        var doc = harness.ExportSingle(lr);

        Assert.True(doc.RootElement.TryGetProperty("tags", out _));
    }

    [Fact]
    public void Export_MultipleReservedPrefixes_AllStripped()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("otel_events.spoofed_version", "1.0.0"),
                new("otel_events.spoofed_host", "evil-host"),
                new("otel_events.spoofed_seq", "999"),
                new("method", "GET"),
            ]);

        var doc = harness.ExportSingle(lr);

        var attr = doc.RootElement.GetProperty("attr");
        Assert.False(attr.TryGetProperty("otel_events.spoofed_version", out _));
        Assert.False(attr.TryGetProperty("otel_events.spoofed_host", out _));
        Assert.False(attr.TryGetProperty("otel_events.spoofed_seq", out _));
        Assert.Equal("GET", attr.GetProperty("method").GetString());
    }

    [Fact]
    public void Export_NonAllPrefixAttribute_IsNotStripped()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("application.version", "2.0.0"),
                new("custom.field", "value"),
            ]);

        var doc = harness.ExportSingle(lr);

        var attr = doc.RootElement.GetProperty("attr");
        Assert.Equal("2.0.0", attr.GetProperty("application.version").GetString());
        Assert.Equal("value", attr.GetProperty("custom.field").GetString());
    }
}
