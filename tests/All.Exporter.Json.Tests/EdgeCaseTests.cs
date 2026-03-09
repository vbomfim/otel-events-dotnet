using Microsoft.Extensions.Logging;
using OpenTelemetry;

namespace All.Exporter.Json.Tests;

/// <summary>
/// Tests for edge cases: empty batches, null attribute values, timestamp precision,
/// and regex timeout fail-closed behavior.
/// </summary>
public sealed class EdgeCaseTests
{
    [Fact]
    public void Export_EmptyBatch_ProducesNoOutput()
    {
        using var harness = new TestExporterHarness();

        var result = harness.ExportRaw([]);

        Assert.Equal(ExportResult.Success, result);
        Assert.Equal(string.Empty, harness.GetRawOutput());
    }

    [Fact]
    public void Export_NullAttributeValue_OmitsKey()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("presentKey", "value"),
                new("nullKey", null),
            ]);

        var doc = harness.ExportSingle(lr);

        var attr = doc.RootElement.GetProperty("attr");
        Assert.Equal("value", attr.GetProperty("presentKey").GetString());
        Assert.False(attr.TryGetProperty("nullKey", out _));
    }

    [Fact]
    public void Export_TimestampFormat_IsMicrosecondPrecisionUtc()
    {
        using var harness = new TestExporterHarness();
        // Create a timestamp with known microsecond precision
        var timestamp = new DateTime(2025, 6, 15, 10, 30, 45, 123, DateTimeKind.Utc).AddTicks(4560);

        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            timestamp: timestamp);

        var doc = harness.ExportSingle(lr);

        var ts = doc.RootElement.GetProperty("timestamp").GetString();
        Assert.NotNull(ts);
        Assert.Equal("2025-06-15T10:30:45.123456Z", ts);
        Assert.EndsWith("Z", ts); // UTC indicator
        Assert.Matches(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{6}Z", ts);
    }

    [Fact]
    public void Export_RegexTimeout_FailsClosed_RedactsValue()
    {
        // Configure a user pattern that will cause catastrophic backtracking
        // The ReDoS pattern (a+)+ against a long 'a' string will timeout at 50ms
        using var harness = new TestExporterHarness(new AllJsonExporterOptions
        {
            RedactPatterns = [@"^(a+)+$"],
        });

        // Input designed to cause catastrophic backtracking: many 'a's followed by '!'
        var evilInput = new string('a', 50) + "!";
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes: [new("payload", evilInput)]);

        var doc = harness.ExportSingle(lr);

        // The value must be redacted (fail-closed), not passed through
        var attrValue = doc.RootElement.GetProperty("attr").GetProperty("payload").GetString();
        Assert.Equal("[REDACTED:timeout]", attrValue);
    }

    [Fact]
    public void Export_BooleanAttribute_NotSubjectToRedaction()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes: [new("isEnabled", (object)true)]);

        var doc = harness.ExportSingle(lr);

        // Boolean should be written as JSON boolean, not stringified then processed
        var attr = doc.RootElement.GetProperty("attr").GetProperty("isEnabled");
        Assert.Equal(System.Text.Json.JsonValueKind.True, attr.ValueKind);
    }

    [Fact]
    public void Export_NumericAttribute_NotSubjectToRedaction()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes: [new("count", (object)42)]);

        var doc = harness.ExportSingle(lr);

        // Integer should be written as JSON number, not stringified then processed
        var attr = doc.RootElement.GetProperty("attr").GetProperty("count");
        Assert.Equal(System.Text.Json.JsonValueKind.Number, attr.ValueKind);
        Assert.Equal(42, attr.GetInt32());
    }

    [Fact]
    public void AddAllJsonExporter_NullBuilder_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AllJsonExporterExtensions.AddAllJsonExporter(null!, (Action<AllJsonExporterOptions>?)null));
    }

    [Fact]
    public void Export_StringArrayTags_RenderedCorrectly()
    {
        // Ensure string[] works via IEnumerable<string> after dead branch removal
        using var harness = new TestExporterHarness();
        var tags = new string[] { "tag1", "tag2", "tag3" };
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes: [new("all.tags", tags)]);

        var doc = harness.ExportSingle(lr);

        var tagsArray = doc.RootElement.GetProperty("tags");
        Assert.Equal(System.Text.Json.JsonValueKind.Array, tagsArray.ValueKind);
        Assert.Equal(3, tagsArray.GetArrayLength());
        Assert.Equal("tag1", tagsArray[0].GetString());
        Assert.Equal("tag2", tagsArray[1].GetString());
        Assert.Equal("tag3", tagsArray[2].GetString());
    }

    [Fact]
    public void Export_ListTags_RenderedCorrectly()
    {
        // Ensure List<string> (IEnumerable<string>) still works
        using var harness = new TestExporterHarness();
        var tags = new List<string> { "alpha", "beta" };
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes: [new("all.tags", tags)]);

        var doc = harness.ExportSingle(lr);

        var tagsArray = doc.RootElement.GetProperty("tags");
        Assert.Equal(2, tagsArray.GetArrayLength());
        Assert.Equal("alpha", tagsArray[0].GetString());
        Assert.Equal("beta", tagsArray[1].GetString());
    }
}
