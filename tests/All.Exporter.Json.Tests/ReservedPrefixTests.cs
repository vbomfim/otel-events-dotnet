using Microsoft.Extensions.Logging;

namespace All.Exporter.Json.Tests;

/// <summary>
/// Tests for reserved <c>all.*</c> prefix stripping from non-ALL attributes.
/// Per §16.4: any attribute with key starting with <c>all.</c> that was NOT set by
/// AllCausalityProcessor or AllJsonExporter is stripped from the exported envelope.
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
                new("all.custom_field", "should be stripped"),
                new("method", "GET"),
            ]);

        var doc = harness.ExportSingle(lr);

        var attr = doc.RootElement.GetProperty("attr");
        Assert.False(attr.TryGetProperty("all.custom_field", out _));
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
                new("all.event_id", "evt_abc123"),
                new("method", "GET"),
            ]);

        var doc = harness.ExportSingle(lr);

        // all.event_id is an allowed prefix — extracted to top-level eventId
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
                new("all.parent_event_id", "evt_parent123"),
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
                new("all.tags", new[] { "api" }),
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
                new("all.spoofed_version", "1.0.0"),
                new("all.spoofed_host", "evil-host"),
                new("all.spoofed_seq", "999"),
                new("method", "GET"),
            ]);

        var doc = harness.ExportSingle(lr);

        var attr = doc.RootElement.GetProperty("attr");
        Assert.False(attr.TryGetProperty("all.spoofed_version", out _));
        Assert.False(attr.TryGetProperty("all.spoofed_host", out _));
        Assert.False(attr.TryGetProperty("all.spoofed_seq", out _));
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
