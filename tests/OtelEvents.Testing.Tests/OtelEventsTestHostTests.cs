using Microsoft.Extensions.Logging;

namespace OtelEvents.Testing.Tests;

/// <summary>
/// Tests for <see cref="OtelEventsTestHost"/> — factory for creating test-configured
/// OTEL pipelines with in-memory log collection.
/// </summary>
public sealed class OtelEventsTestHostTests : IDisposable
{
    private ILoggerFactory? _loggerFactory;

    public void Dispose()
    {
        _loggerFactory?.Dispose();
    }

    [Fact]
    public void Create_ReturnsLoggerFactory()
    {
        var (factory, _) = OtelEventsTestHost.Create();
        _loggerFactory = factory;

        Assert.NotNull(factory);
    }

    [Fact]
    public void Create_ReturnsExporter()
    {
        var (factory, exporter) = OtelEventsTestHost.Create();
        _loggerFactory = factory;

        Assert.NotNull(exporter);
    }

    [Fact]
    public void Create_LoggerCapturesEvents()
    {
        var (factory, exporter) = OtelEventsTestHost.Create();
        _loggerFactory = factory;
        var logger = factory.CreateLogger("TestCategory");

        logger.LogInformation("Test message");

        Assert.Single(exporter.LogRecords);
    }

    [Fact]
    public void Create_CapturesEventNameFromEventId()
    {
        var (factory, exporter) = OtelEventsTestHost.Create();
        _loggerFactory = factory;
        var logger = factory.CreateLogger("TestCategory");

        logger.LogInformation(new EventId(1, "order.placed"), "Order placed");

        var record = exporter.LogRecords[0];
        Assert.Equal("order.placed", record.EventName);
    }

    [Fact]
    public void Create_CapturesStructuredAttributes()
    {
        var (factory, exporter) = OtelEventsTestHost.Create();
        _loggerFactory = factory;
        var logger = factory.CreateLogger("TestCategory");

        logger.LogInformation("Order {OrderId} total {Amount}", "ORD-123", 42.50);

        var record = exporter.LogRecords[0];
        Assert.Equal("ORD-123", record.Attributes["OrderId"]);
        Assert.Equal(42.50, record.Attributes["Amount"]);
    }

    [Fact]
    public void Create_CapturesAllLogLevels()
    {
        var (factory, exporter) = OtelEventsTestHost.Create();
        _loggerFactory = factory;
        var logger = factory.CreateLogger("TestCategory");

        logger.LogTrace("Trace");
        logger.LogDebug("Debug");
        logger.LogInformation("Info");
        logger.LogWarning("Warning");
        logger.LogError("Error");
        logger.LogCritical("Critical");

        Assert.Equal(6, exporter.LogRecords.Count);
    }

    [Fact]
    public void Create_CapturesExceptions()
    {
        var (factory, exporter) = OtelEventsTestHost.Create();
        _loggerFactory = factory;
        var logger = factory.CreateLogger("TestCategory");
        var exception = new InvalidOperationException("test failure");

        logger.LogError(exception, "Something failed");

        Assert.Same(exception, exporter.LogRecords[0].Exception);
    }

    [Fact]
    public void Create_MultipleLoggersSameExporter()
    {
        var (factory, exporter) = OtelEventsTestHost.Create();
        _loggerFactory = factory;
        var logger1 = factory.CreateLogger("Category1");
        var logger2 = factory.CreateLogger("Category2");

        logger1.LogInformation("From logger 1");
        logger2.LogInformation("From logger 2");

        Assert.Equal(2, exporter.LogRecords.Count);
    }

    [Fact]
    public void Create_WithDefaultConfig_NoFiltering()
    {
        var (factory, exporter) = OtelEventsTestHost.Create();
        _loggerFactory = factory;
        var logger = factory.CreateLogger("TestCategory");

        // Trace is the lowest level — should still be captured
        logger.LogTrace("Trace level message");

        Assert.Single(exporter.LogRecords);
        Assert.Equal(LogLevel.Trace, exporter.LogRecords[0].LogLevel);
    }
}
