using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace All.Exporter.Json.Tests;

/// <summary>
/// Tests for the JSON envelope structure produced by <see cref="AllJsonExporter"/>.
/// Verifies mandatory fields, field ordering, and JSONL format.
/// </summary>
public sealed class JsonEnvelopeTests
{
    [Fact]
    public void Export_BasicLogRecord_ContainsTimestampField()
    {
        using var harness = new TestExporterHarness();
        var timestamp = new DateTime(2025, 1, 15, 14, 30, 0, 123, DateTimeKind.Utc).AddTicks(4560); // microsecond precision
        var lr = TestExporterHarness.CreateLogRecord(
            logLevel: LogLevel.Information,
            eventName: "test.event",
            message: "Test message",
            timestamp: timestamp);

        var doc = harness.ExportSingle(lr);

        Assert.Equal("2025-01-15T14:30:00.123456Z", doc.RootElement.GetProperty("timestamp").GetString());
    }

    [Fact]
    public void Export_BasicLogRecord_ContainsEventField()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(eventName: "http.request.completed");

        var doc = harness.ExportSingle(lr);

        Assert.Equal("http.request.completed", doc.RootElement.GetProperty("event").GetString());
    }

    [Fact]
    public void Export_NoEventName_UsesFallback()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(eventName: null);

        var doc = harness.ExportSingle(lr);

        Assert.Equal("dotnet.ilogger", doc.RootElement.GetProperty("event").GetString());
    }

    [Fact]
    public void Export_EmptyEventName_UsesFallback()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(eventName: "");

        var doc = harness.ExportSingle(lr);

        Assert.Equal("dotnet.ilogger", doc.RootElement.GetProperty("event").GetString());
    }

    [Theory]
    [InlineData(LogLevel.Trace, "TRACE", 1)]
    [InlineData(LogLevel.Debug, "DEBUG", 5)]
    [InlineData(LogLevel.Information, "INFO", 9)]
    [InlineData(LogLevel.Warning, "WARN", 13)]
    [InlineData(LogLevel.Error, "ERROR", 17)]
    [InlineData(LogLevel.Critical, "FATAL", 21)]
    public void Export_SeverityMapping_MatchesSpecification(LogLevel logLevel, string expectedSeverity, int expectedNumber)
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(logLevel: logLevel, eventName: "test.event");

        var doc = harness.ExportSingle(lr);

        Assert.Equal(expectedSeverity, doc.RootElement.GetProperty("severity").GetString());
        Assert.Equal(expectedNumber, doc.RootElement.GetProperty("severityNumber").GetInt32());
    }

    [Fact]
    public void Export_WithMessage_ContainsMessageField()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(message: "HTTP GET /api/orders completed with 200 in 45.2ms");

        var doc = harness.ExportSingle(lr);

        Assert.Equal("HTTP GET /api/orders completed with 200 in 45.2ms", doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public void Export_NullMessage_OmitsMessageField()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(message: null);

        var doc = harness.ExportSingle(lr);

        Assert.False(doc.RootElement.TryGetProperty("message", out _));
    }

    [Fact]
    public void Export_WithTraceContext_ContainsTraceAndSpanIds()
    {
        using var harness = new TestExporterHarness();
        var traceId = System.Diagnostics.ActivityTraceId.CreateFromString("4bf92f3577b34da6a3ce929d0e0e4736");
        var spanId = System.Diagnostics.ActivitySpanId.CreateFromString("00f067aa0ba902b7");
        var lr = TestExporterHarness.CreateLogRecord(traceId: traceId, spanId: spanId);

        var doc = harness.ExportSingle(lr);

        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", doc.RootElement.GetProperty("traceId").GetString());
        Assert.Equal("00f067aa0ba902b7", doc.RootElement.GetProperty("spanId").GetString());
    }

    [Fact]
    public void Export_WithoutTraceContext_OmitsTraceAndSpanIds()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord();

        var doc = harness.ExportSingle(lr);

        Assert.False(doc.RootElement.TryGetProperty("traceId", out _));
        Assert.False(doc.RootElement.TryGetProperty("spanId", out _));
    }

    [Fact]
    public void Export_OutputIsSingleLineJsonl()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(eventName: "test.event", message: "msg");

        harness.ExportSingle(lr);
        var output = harness.GetRawOutput();

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        Assert.DoesNotContain("\r", lines[0]); // No carriage return
        Assert.True(output.EndsWith('\n')); // Terminated by newline
    }

    [Fact]
    public void Export_MultipleBatches_ProducesSeparateLines()
    {
        using var harness = new TestExporterHarness();
        var lr1 = TestExporterHarness.CreateLogRecord(eventName: "event.one", message: "first");
        var lr2 = TestExporterHarness.CreateLogRecord(eventName: "event.two", message: "second");

        var docs = harness.ExportBatch([lr1, lr2]);

        Assert.Equal(2, docs.Count);
        Assert.Equal("event.one", docs[0].RootElement.GetProperty("event").GetString());
        Assert.Equal("event.two", docs[1].RootElement.GetProperty("event").GetString());
    }

    [Fact]
    public void Export_OutputIsValidUtf8Json()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "unicode.test",
            message: "Unicode: 日本語 🚀 résumé");

        var doc = harness.ExportSingle(lr);

        Assert.Equal("Unicode: 日本語 🚀 résumé", doc.RootElement.GetProperty("message").GetString());
    }
}
