using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace All.Testing.Tests;

/// <summary>
/// Tests for <see cref="LogAssertions"/> — fluent assertion extensions
/// for common test scenarios with <see cref="InMemoryLogExporter"/>.
/// </summary>
public sealed class LogAssertionsTests
{
    [Fact]
    public void AssertEventEmitted_WhenEventExists_DoesNotThrow()
    {
        var exporter = CreateExporterWithRecord(eventName: "order.placed");

        var exception = Record.Exception(() => exporter.AssertEventEmitted("order.placed"));

        Assert.Null(exception);
    }

    [Fact]
    public void AssertEventEmitted_WhenEventMissing_Throws()
    {
        var exporter = CreateExporterWithRecord(eventName: "order.placed");

        Assert.Throws<Xunit.Sdk.XunitException>(
            () => exporter.AssertEventEmitted("order.shipped"));
    }

    [Fact]
    public void AssertEventEmitted_EmptyExporter_Throws()
    {
        var exporter = new InMemoryLogExporter();

        Assert.Throws<Xunit.Sdk.XunitException>(
            () => exporter.AssertEventEmitted("any.event"));
    }

    [Fact]
    public void AssertNoErrors_WhenNoErrors_DoesNotThrow()
    {
        var exporter = new InMemoryLogExporter();
        ExportRecords(exporter,
            CreateLogRecord(logLevel: LogLevel.Information, eventName: "info.event"),
            CreateLogRecord(logLevel: LogLevel.Debug, eventName: "debug.event"));

        var exception = Record.Exception(() => exporter.AssertNoErrors());

        Assert.Null(exception);
    }

    [Fact]
    public void AssertNoErrors_EmptyExporter_DoesNotThrow()
    {
        var exporter = new InMemoryLogExporter();

        var exception = Record.Exception(() => exporter.AssertNoErrors());

        Assert.Null(exception);
    }

    [Fact]
    public void AssertNoErrors_WhenErrorExists_Throws()
    {
        var exporter = new InMemoryLogExporter();
        ExportRecords(exporter,
            CreateLogRecord(logLevel: LogLevel.Error, eventName: "error.event"));

        Assert.Throws<Xunit.Sdk.XunitException>(
            () => exporter.AssertNoErrors());
    }

    [Fact]
    public void AssertNoErrors_WhenCriticalExists_Throws()
    {
        var exporter = new InMemoryLogExporter();
        ExportRecords(exporter,
            CreateLogRecord(logLevel: LogLevel.Critical, eventName: "fatal.event"));

        Assert.Throws<Xunit.Sdk.XunitException>(
            () => exporter.AssertNoErrors());
    }

    [Fact]
    public void AssertNoErrors_WarningDoesNotCount()
    {
        var exporter = new InMemoryLogExporter();
        ExportRecords(exporter,
            CreateLogRecord(logLevel: LogLevel.Warning, eventName: "warn.event"));

        var exception = Record.Exception(() => exporter.AssertNoErrors());

        Assert.Null(exception);
    }

    [Fact]
    public void AssertSingle_WhenExactlyOneMatch_ReturnsRecord()
    {
        var exporter = new InMemoryLogExporter();
        ExportRecords(exporter,
            CreateLogRecord(eventName: "order.placed", message: "Order placed"),
            CreateLogRecord(eventName: "order.shipped", message: "Order shipped"));

        var record = exporter.AssertSingle("order.placed");

        Assert.NotNull(record);
        Assert.Equal("order.placed", record.EventName);
    }

    [Fact]
    public void AssertSingle_WhenNoMatch_Throws()
    {
        var exporter = CreateExporterWithRecord(eventName: "order.placed");

        Assert.Throws<Xunit.Sdk.XunitException>(
            () => exporter.AssertSingle("order.missing"));
    }

    [Fact]
    public void AssertSingle_WhenMultipleMatches_Throws()
    {
        var exporter = new InMemoryLogExporter();
        ExportRecords(exporter,
            CreateLogRecord(eventName: "order.placed"),
            CreateLogRecord(eventName: "order.placed"));

        Assert.Throws<Xunit.Sdk.XunitException>(
            () => exporter.AssertSingle("order.placed"));
    }

    [Fact]
    public void AssertAttribute_WhenAttributeMatches_DoesNotThrow()
    {
        var exporter = new InMemoryLogExporter();
        ExportRecords(exporter,
            CreateLogRecord(
                eventName: "order.placed",
                attributes: [new("OrderId", "ORD-123")]));

        var record = exporter.LogRecords[0];

        var exception = Record.Exception(() => record.AssertAttribute("OrderId", "ORD-123"));

        Assert.Null(exception);
    }

    [Fact]
    public void AssertAttribute_WhenAttributeMissing_Throws()
    {
        var exporter = new InMemoryLogExporter();
        ExportRecords(exporter,
            CreateLogRecord(eventName: "order.placed"));

        var record = exporter.LogRecords[0];

        Assert.Throws<Xunit.Sdk.XunitException>(
            () => record.AssertAttribute("MissingKey", "value"));
    }

    [Fact]
    public void AssertAttribute_WhenValueMismatch_Throws()
    {
        var exporter = new InMemoryLogExporter();
        ExportRecords(exporter,
            CreateLogRecord(
                eventName: "order.placed",
                attributes: [new("StatusCode", 200)]));

        var record = exporter.LogRecords[0];

        Assert.Throws<Xunit.Sdk.XunitException>(
            () => record.AssertAttribute("StatusCode", 404));
    }

    [Fact]
    public void AssertAttribute_WithIntegerValue_MatchesCorrectly()
    {
        var exporter = new InMemoryLogExporter();
        ExportRecords(exporter,
            CreateLogRecord(
                eventName: "http.response",
                attributes: [new("StatusCode", 200)]));

        var record = exporter.LogRecords[0];

        var exception = Record.Exception(() => record.AssertAttribute("StatusCode", 200));

        Assert.Null(exception);
    }

    [Fact]
    public void AssertAttribute_WithNullValue_MatchesCorrectly()
    {
        var exporter = new InMemoryLogExporter();
        ExportRecords(exporter,
            CreateLogRecord(
                eventName: "test.event",
                attributes: [new("NullableField", (object?)null)]));

        var record = exporter.LogRecords[0];

        var exception = Record.Exception(() => record.AssertAttribute("NullableField", null!));

        Assert.Null(exception);
    }

    // --- Helpers ---

    private static InMemoryLogExporter CreateExporterWithRecord(string eventName)
    {
        var exporter = new InMemoryLogExporter();
        ExportRecords(exporter, CreateLogRecord(eventName: eventName));
        return exporter;
    }

    private static void ExportRecords(InMemoryLogExporter exporter, params LogRecord[] records)
    {
        var batch = new Batch<LogRecord>(records, records.Length);
        exporter.Export(batch);
    }

    private static LogRecord CreateLogRecord(
        LogLevel logLevel = LogLevel.Information,
        string? eventName = null,
        string? message = null,
        List<KeyValuePair<string, object?>>? attributes = null,
        Exception? exception = null)
    {
        var lr = (LogRecord)Activator.CreateInstance(typeof(LogRecord), nonPublic: true)!;
        lr.Timestamp = DateTime.UtcNow;
        lr.LogLevel = logLevel;
        lr.EventId = new EventId(1, eventName);
        lr.FormattedMessage = message;
        lr.Attributes = attributes;
        lr.Exception = exception;
        return lr;
    }
}
