using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace OtelEvents.Exporter.Json.Tests;

/// <summary>
/// Tests for the metadata fields: all.v, all.seq, all.host, all.pid.
/// </summary>
public sealed class MetadataTests
{
    [Fact]
    public void Export_ContainsSchemaVersion()
    {
        using var harness = new TestExporterHarness(new OtelEventsJsonExporterOptions { SchemaVersion = "2.0.0" });
        var lr = TestExporterHarness.CreateLogRecord(eventName: "test.event");

        var doc = harness.ExportSingle(lr);

        Assert.Equal("2.0.0", doc.RootElement.GetProperty("all.v").GetString());
    }

    [Fact]
    public void Export_DefaultSchemaVersion_Is100()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(eventName: "test.event");

        var doc = harness.ExportSingle(lr);

        Assert.Equal("1.0.0", doc.RootElement.GetProperty("all.v").GetString());
    }

    [Fact]
    public void Export_SequenceNumberStartsAt1()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(eventName: "test.event");

        var doc = harness.ExportSingle(lr);

        Assert.Equal(1, doc.RootElement.GetProperty("all.seq").GetInt64());
    }

    [Fact]
    public void Export_SequenceNumberIncrementsPerRecord()
    {
        using var harness = new TestExporterHarness();
        var lr1 = TestExporterHarness.CreateLogRecord(eventName: "test.first");
        var lr2 = TestExporterHarness.CreateLogRecord(eventName: "test.second");
        var lr3 = TestExporterHarness.CreateLogRecord(eventName: "test.third");

        var docs = harness.ExportBatch([lr1, lr2, lr3]);

        Assert.Equal(1, docs[0].RootElement.GetProperty("all.seq").GetInt64());
        Assert.Equal(2, docs[1].RootElement.GetProperty("all.seq").GetInt64());
        Assert.Equal(3, docs[2].RootElement.GetProperty("all.seq").GetInt64());
    }

    [Fact]
    public void Export_EmitHostInfoFalse_OmitsHostAndPid()
    {
        using var harness = new TestExporterHarness(new OtelEventsJsonExporterOptions { EmitHostInfo = false });
        var lr = TestExporterHarness.CreateLogRecord(eventName: "test.event");

        var doc = harness.ExportSingle(lr);

        Assert.False(doc.RootElement.TryGetProperty("all.host", out _));
        Assert.False(doc.RootElement.TryGetProperty("all.pid", out _));
    }

    [Fact]
    public void Export_EmitHostInfoTrue_IncludesHostAndPid()
    {
        using var harness = new TestExporterHarness(new OtelEventsJsonExporterOptions { EmitHostInfo = true });
        var lr = TestExporterHarness.CreateLogRecord(eventName: "test.event");

        var doc = harness.ExportSingle(lr);

        Assert.True(doc.RootElement.TryGetProperty("all.host", out var host));
        Assert.False(string.IsNullOrEmpty(host.GetString()));

        Assert.True(doc.RootElement.TryGetProperty("all.pid", out var pid));
        Assert.True(pid.GetInt32() > 0);
    }
}
