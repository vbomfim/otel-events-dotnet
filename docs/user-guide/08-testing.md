# Chapter 8 — Testing with otel-events

otel-events ships a dedicated testing package — `OtelEvents.Testing` — that plugs into the OTEL pipeline and captures log records for assertion. This chapter shows how to write unit tests for services that emit otel-events events.

---

## OtelEvents.Testing Package Overview

Install the package in your test project:

```bash
dotnet add package OtelEvents.Testing
```

The package provides four core types:

| Type | Purpose |
|---|---|
| `OtelEventsTestHost` | Factory that creates a pre-configured `ILoggerFactory` + `InMemoryLogExporter` pair |
| `InMemoryLogExporter` | OTEL `BaseExporter<LogRecord>` that captures records in memory |
| `ExportedLogRecord` | Immutable snapshot of a `LogRecord` safe for assertions |
| `LogAssertions` | Extension methods for fluent event assertions |

### Why a Dedicated Package?

OTEL's `LogRecord` is **mutable and pooled** — the SDK recycles instances after export. If your test captures a reference to a `LogRecord`, the data may change or be zeroed out by the time you assert on it. `OtelEvents.Testing` solves this by snapshotting each record into an immutable `ExportedLogRecord` at export time.

---

## InMemoryLogExporter

`InMemoryLogExporter` is a thread-safe OTEL exporter that stores snapshots of every `LogRecord` it receives:

```csharp
public sealed class InMemoryLogExporter : BaseExporter<LogRecord>
{
    public IReadOnlyList<ExportedLogRecord> LogRecords { get; }
    public override ExportResult Export(in Batch<LogRecord> batch);
    public void Clear();
}
```

### Key Behaviors

- **Thread-safe** — uses `ConcurrentQueue<ExportedLogRecord>` internally
- **Snapshot pattern** — converts each recyclable `LogRecord` to an immutable `ExportedLogRecord`
- **Point-in-time reads** — `LogRecords` returns a snapshot of all captured records
- **Resettable** — `Clear()` removes all captured records for test reuse

---

## OtelEventsTestHost.Create()

`OtelEventsTestHost.Create()` is the fastest way to set up a test logging pipeline:

```csharp
public static class OtelEventsTestHost
{
    public static (ILoggerFactory Factory, InMemoryLogExporter Exporter) Create();
}
```

### What Create() Configures

1. **Minimum level:** `LogLevel.Trace` — captures all severity levels
2. **IncludeFormattedMessage:** `true` — preserves formatted message strings
3. **ParseStateValues:** `true` — captures structured logging parameters as attributes
4. **Processor:** `SimpleLogRecordExportProcessor` — synchronous export (records are captured immediately, before test assertions run)

### Basic Usage

```csharp
var (factory, exporter) = OtelEventsTestHost.Create();
var logger = factory.CreateLogger("TestCategory");

logger.LogInformation("Hello {Name}", "World");

Assert.Single(exporter.LogRecords);
Assert.Equal("World", exporter.LogRecords[0].Attributes["Name"]);

// Always dispose the factory to flush the pipeline
factory.Dispose();
```

### With Causality

`OtelEventsTestHost.Create()` builds a minimal pipeline. To test causal linking (`otel_events.event_id`, `otel_events.parent_event_id`), manually add the `OtelEventsCausalityProcessor` before the exporter:

```csharp
using OtelEvents.Causality;

var exporter = new InMemoryLogExporter();

var factory = LoggerFactory.Create(builder =>
{
    builder.SetMinimumLevel(LogLevel.Trace);
    builder.AddOpenTelemetry(options =>
    {
        options.IncludeFormattedMessage = true;
        options.ParseStateValues = true;

        // Add causality processor BEFORE the exporter
        options.AddProcessor(new OtelEventsCausalityProcessor());
        options.AddProcessor(new SimpleLogRecordExportProcessor(exporter));
    });
});

var logger = factory.CreateLogger("TestCategory");

using var scope = OtelEventsCausalScope.Begin();
logger.LogInformation(new EventId(1, "order.placed"), "Order placed");

var record = exporter.LogRecords[0];
var eventId = record.Attributes["otel_events.event_id"] as string;
Assert.NotNull(eventId);
Assert.StartsWith("evt_", eventId);

factory.Dispose();
```

> **Key point:** Processor order matters. `OtelEventsCausalityProcessor` must be added **before** the `SimpleLogRecordExportProcessor` so the causal attributes are present when the exporter snapshots the record.

---

## ExportedLogRecord

`ExportedLogRecord` is an immutable snapshot of an OTEL `LogRecord`, safe for assertions after the original record is recycled:

```csharp
public sealed class ExportedLogRecord
{
    public string? EventName { get; }
    public EventId EventId { get; }
    public LogLevel LogLevel { get; }
    public string? FormattedMessage { get; }
    public Exception? Exception { get; }
    public IReadOnlyDictionary<string, object?> Attributes { get; }
    public ActivityTraceId TraceId { get; }
    public ActivitySpanId SpanId { get; }
    public DateTime Timestamp { get; }

    public static ExportedLogRecord From(LogRecord record);
}
```

### Captured Properties

| Property | Source | Notes |
|---|---|---|
| `EventName` | `LogRecord.EventId.Name` | The schema-defined event name (e.g., `"order.placed"`) |
| `EventId` | `LogRecord.EventId` | Full `EventId` (numeric ID + name) |
| `LogLevel` | `LogRecord.LogLevel` | Severity: `Trace` through `Critical` |
| `FormattedMessage` | `LogRecord.FormattedMessage` | Interpolated message string |
| `Exception` | `LogRecord.Exception` | Associated exception, if any |
| `Attributes` | `LogRecord.Attributes` | Immutable copy of all key-value pairs |
| `TraceId` | `LogRecord.TraceId` | W3C trace ID from `Activity.Current` |
| `SpanId` | `LogRecord.SpanId` | W3C span ID from `Activity.Current` |
| `Timestamp` | `LogRecord.Timestamp` | UTC timestamp |

---

## LogAssertions

`LogAssertions` provides fluent extension methods for common assertion patterns:

### AssertEventEmitted

Verifies that **at least one** log record with the specified event name was emitted:

```csharp
exporter.AssertEventEmitted("order.placed");
```

If no match is found, throws with a message listing all emitted events:

```
Expected event 'order.placed' to be emitted, but it was not found.
Emitted events: 'order.created', 'order.shipped'
```

### AssertSingle

Verifies that **exactly one** record with the specified event name exists, and returns it for further assertions:

```csharp
ExportedLogRecord record = exporter.AssertSingle("order.placed");
Assert.Equal(LogLevel.Information, record.LogLevel);
```

Throws if zero or more than one matching records are found.

### AssertAttribute

Verifies a specific attribute key and value on an `ExportedLogRecord`:

```csharp
var record = exporter.AssertSingle("http.request.completed");
record.AssertAttribute("StatusCode", 200);
record.AssertAttribute("Path", "/api/orders");
```

If the attribute is missing, the failure message lists all available attributes:

```
Expected attribute 'StatusCode' not found. Available attributes: 'Path', 'Method', 'DurationMs'
```

If the value doesn't match, the failure message shows both expected and actual values with types:

```
Attribute 'StatusCode' expected value '200' (type: Int32) but found '404' (type: Int32).
```

### AssertNoErrors

Verifies that **no** `Error` or `Critical` level records were emitted:

```csharp
exporter.AssertNoErrors();
```

If errors are found, the failure message lists each one:

```
Expected no errors, but found 2 error-level record(s):
  [Error] db.connection.failed: Connection timeout (IOException: ...)
  [Critical] app.crash: Unhandled exception
```

---

## Example: Testing a Service That Emits Events

This example demonstrates a full test for a service that uses otel-events generated events.

### The Service Under Test

```csharp
public class OrderService
{
    private readonly ILogger<OrderEventsEventSource> _logger;

    public OrderService(ILogger<OrderEventsEventSource> logger)
    {
        _logger = logger;
    }

    public Order PlaceOrder(string customerId, decimal amount)
    {
        var order = new Order
        {
            Id = Guid.NewGuid().ToString(),
            CustomerId = customerId,
            Amount = amount,
        };

        // otel-events generated event method
        _logger.OrderPlaced(
            orderId: order.Id,
            customerId: customerId,
            amount: amount);

        return order;
    }
}
```

### The Test

```csharp
public class OrderServiceTests : IDisposable
{
    private readonly ILoggerFactory _factory;
    private readonly InMemoryLogExporter _exporter;
    private readonly OrderService _service;

    public OrderServiceTests()
    {
        // Set up the test pipeline
        (_factory, _exporter) = OtelEventsTestHost.Create();

        var logger = _factory.CreateLogger<OrderEventsEventSource>();
        _service = new OrderService(logger);
    }

    [Fact]
    public void PlaceOrder_EmitsOrderPlacedEvent()
    {
        // Act
        var order = _service.PlaceOrder("CUST-001", 99.99m);

        // Assert — event was emitted
        var record = _exporter.AssertSingle("order.placed");
        Assert.Equal(LogLevel.Information, record.LogLevel);
    }

    [Fact]
    public void PlaceOrder_IncludesCorrectAttributes()
    {
        // Act
        var order = _service.PlaceOrder("CUST-001", 99.99m);

        // Assert — attributes match
        var record = _exporter.AssertSingle("order.placed");
        record.AssertAttribute("CustomerId", "CUST-001");
        record.AssertAttribute("Amount", 99.99m);
    }

    [Fact]
    public void PlaceOrder_DoesNotEmitErrors()
    {
        // Act
        _service.PlaceOrder("CUST-001", 99.99m);

        // Assert — no error-level events
        _exporter.AssertNoErrors();
    }

    [Fact]
    public void PlaceOrder_IncludesFormattedMessage()
    {
        // Act
        var order = _service.PlaceOrder("CUST-001", 42.00m);

        // Assert — message is interpolated
        var record = _exporter.AssertSingle("order.placed");
        Assert.Contains("CUST-001", record.FormattedMessage);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }
}
```

### Testing Multiple Events

```csharp
[Fact]
public void ProcessOrder_EmitsMultipleEvents()
{
    var (factory, exporter) = OtelEventsTestHost.Create();
    var logger = factory.CreateLogger<OrderEventsEventSource>();

    // Emit multiple events
    logger.OrderPlaced(orderId: "ORD-1", customerId: "C-1", amount: 100m);
    logger.OrderShipped(orderId: "ORD-1", carrier: "FedEx");

    // Assert — both events present
    exporter.AssertEventEmitted("order.placed");
    exporter.AssertEventEmitted("order.shipped");

    // Assert — counts
    Assert.Equal(2, exporter.LogRecords.Count);

    factory.Dispose();
}
```

### Testing Error Events

```csharp
[Fact]
public void FailedOperation_EmitsErrorEventWithException()
{
    var (factory, exporter) = OtelEventsTestHost.Create();
    var logger = factory.CreateLogger<OrderEventsEventSource>();

    var exception = new InvalidOperationException("Out of stock");
    logger.OrderFailed(orderId: "ORD-1", reason: "Out of stock", exception: exception);

    var record = exporter.AssertSingle("order.failed");
    Assert.Equal(LogLevel.Error, record.LogLevel);
    Assert.NotNull(record.Exception);
    Assert.IsType<InvalidOperationException>(record.Exception);

    factory.Dispose();
}
```

### Clearing Between Tests

If you reuse an exporter across tests in a test class, call `Clear()` to reset:

```csharp
[Fact]
public void Test1()
{
    _exporter.Clear(); // Start fresh
    _service.PlaceOrder("CUST-001", 50m);
    Assert.Single(_exporter.LogRecords);
}

[Fact]
public void Test2()
{
    _exporter.Clear(); // Start fresh
    _service.PlaceOrder("CUST-002", 75m);
    Assert.Single(_exporter.LogRecords);
}
```

---

## Tips

| Tip | Why |
|---|---|
| Always dispose `ILoggerFactory` in test cleanup | Ensures the OTEL pipeline flushes all pending records |
| Use `SimpleLogRecordExportProcessor` (default in `OtelEventsTestHost`) | Synchronous export — records captured before assertions run |
| Test event names, not formatted messages | Event names are stable; message templates may change |
| Use `AssertSingle` to get a record for attribute assertions | Returns the record directly for chaining |
| Test at the service boundary, not the logger | Ensures the service calls the right event method with correct data |

---

## Next Steps

- [Chapter 9 — CLI Tool](09-cli-tool.md) — validate schemas and generate code from the command line
- [Chapter 11 — Advanced Topics](11-advanced-topics.md) — rate limiting, sampling, and more
