using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace All.Exporter.Json.Tests;

/// <summary>
/// Tests for attribute extraction: eventId, parentEventId, tags, and typed attr object.
/// Also tests <c>{OriginalFormat}</c> exclusion and null-value omission.
/// </summary>
public sealed class AttributeTests
{
    [Fact]
    public void Export_WithEventId_ExtractsToTopLevel()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("all.event_id", "evt_7f8a9b2c-3d4e-5f6a-7b8c-9d0e1f2a3b4c"),
                new("method", "GET"),
            ]);

        var doc = harness.ExportSingle(lr);

        Assert.Equal("evt_7f8a9b2c-3d4e-5f6a-7b8c-9d0e1f2a3b4c", doc.RootElement.GetProperty("eventId").GetString());
        // Should NOT appear in attr
        Assert.False(doc.RootElement.GetProperty("attr").TryGetProperty("all.event_id", out _));
    }

    [Fact]
    public void Export_WithParentEventId_ExtractsToTopLevel()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("all.parent_event_id", "evt_1a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d"),
                new("method", "POST"),
            ]);

        var doc = harness.ExportSingle(lr);

        Assert.Equal("evt_1a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d", doc.RootElement.GetProperty("parentEventId").GetString());
    }

    [Fact]
    public void Export_WithTags_ExtractsToTopLevelArray()
    {
        using var harness = new TestExporterHarness();
        var tags = new[] { "api", "http" };
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("all.tags", tags),
                new("method", "GET"),
            ]);

        var doc = harness.ExportSingle(lr);

        var tagsArray = doc.RootElement.GetProperty("tags");
        Assert.Equal(JsonValueKind.Array, tagsArray.ValueKind);
        Assert.Equal(2, tagsArray.GetArrayLength());
        Assert.Equal("api", tagsArray[0].GetString());
        Assert.Equal("http", tagsArray[1].GetString());
    }

    [Fact]
    public void Export_WithAttributes_WritesAttrObject()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("method", "GET"),
                new("statusCode", 200),
                new("durationMs", 45.2),
            ]);

        var doc = harness.ExportSingle(lr);

        var attr = doc.RootElement.GetProperty("attr");
        Assert.Equal("GET", attr.GetProperty("method").GetString());
        Assert.Equal(200, attr.GetProperty("statusCode").GetInt32());
        Assert.Equal(45.2, attr.GetProperty("durationMs").GetDouble());
    }

    [Fact]
    public void Export_NoAttributes_OmitsAttrObject()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(eventName: "test.event");

        var doc = harness.ExportSingle(lr);

        Assert.False(doc.RootElement.TryGetProperty("attr", out _));
    }

    [Fact]
    public void Export_OriginalFormatAttribute_IsExcluded()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("{OriginalFormat}", "Request {method} completed"),
                new("method", "GET"),
            ]);

        var doc = harness.ExportSingle(lr);

        var attr = doc.RootElement.GetProperty("attr");
        Assert.False(attr.TryGetProperty("{OriginalFormat}", out _));
        Assert.Equal("GET", attr.GetProperty("method").GetString());
    }

    [Fact]
    public void Export_BooleanAttribute_WritesAsBoolean()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes: [new("isActive", (object)true)]);

        var doc = harness.ExportSingle(lr);

        Assert.True(doc.RootElement.GetProperty("attr").GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public void Export_IntegerAttribute_WritesAsNumber()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes: [new("count", (object)42)]);

        var doc = harness.ExportSingle(lr);

        Assert.Equal(42, doc.RootElement.GetProperty("attr").GetProperty("count").GetInt32());
    }

    [Fact]
    public void Export_LongAttribute_WritesAsNumber()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes: [new("bigNumber", (object)9876543210L)]);

        var doc = harness.ExportSingle(lr);

        Assert.Equal(9876543210L, doc.RootElement.GetProperty("attr").GetProperty("bigNumber").GetInt64());
    }

    [Fact]
    public void Export_WithoutEventIdOrParentEventId_OmitsFields()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes: [new("key", "value")]);

        var doc = harness.ExportSingle(lr);

        Assert.False(doc.RootElement.TryGetProperty("eventId", out _));
        Assert.False(doc.RootElement.TryGetProperty("parentEventId", out _));
    }

    [Fact]
    public void Export_WithoutTags_OmitsTagsField()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes: [new("method", "GET")]);

        var doc = harness.ExportSingle(lr);

        Assert.False(doc.RootElement.TryGetProperty("tags", out _));
    }
}
