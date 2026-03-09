using Microsoft.Extensions.Logging;

namespace All.Exporter.Json.Tests;

/// <summary>
/// Tests for attribute value truncation at <see cref="AllJsonExporterOptions.MaxAttributeValueLength"/>.
/// Values exceeding the limit are truncated with "…[truncated]" suffix.
/// </summary>
public sealed class TruncationTests
{
    [Fact]
    public void Export_ShortValue_NotTruncated()
    {
        using var harness = new TestExporterHarness(new AllJsonExporterOptions { MaxAttributeValueLength = 100 });
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes: [new("msg", "short value")]);

        var doc = harness.ExportSingle(lr);

        Assert.Equal("short value", doc.RootElement.GetProperty("attr").GetProperty("msg").GetString());
    }

    [Fact]
    public void Export_ExactLengthValue_NotTruncated()
    {
        var value = new string('x', 100);
        using var harness = new TestExporterHarness(new AllJsonExporterOptions { MaxAttributeValueLength = 100 });
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes: [new("msg", value)]);

        var doc = harness.ExportSingle(lr);

        Assert.Equal(value, doc.RootElement.GetProperty("attr").GetProperty("msg").GetString());
    }

    [Fact]
    public void Export_LongValue_TruncatedWithSuffix()
    {
        var value = new string('x', 150);
        using var harness = new TestExporterHarness(new AllJsonExporterOptions { MaxAttributeValueLength = 100 });
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes: [new("msg", value)]);

        var doc = harness.ExportSingle(lr);

        var result = doc.RootElement.GetProperty("attr").GetProperty("msg").GetString()!;
        Assert.EndsWith("…[truncated]", result);
        Assert.StartsWith(new string('x', 100), result);
    }

    [Fact]
    public void Export_DefaultMaxLength_Is4096()
    {
        var options = new AllJsonExporterOptions();
        Assert.Equal(4096, options.MaxAttributeValueLength);
    }

    [Fact]
    public void Export_ValueAtDefaultLimit_NotTruncated()
    {
        var value = new string('a', 4096);
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes: [new("msg", value)]);

        var doc = harness.ExportSingle(lr);

        Assert.Equal(value, doc.RootElement.GetProperty("attr").GetProperty("msg").GetString());
    }

    [Fact]
    public void Export_ValueExceedingDefaultLimit_IsTruncated()
    {
        var value = new string('a', 4097);
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes: [new("msg", value)]);

        var doc = harness.ExportSingle(lr);

        var result = doc.RootElement.GetProperty("attr").GetProperty("msg").GetString()!;
        Assert.EndsWith("…[truncated]", result);
    }
}
