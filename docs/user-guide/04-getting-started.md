# Chapter 4 — Getting Started

This tutorial takes you from zero to your first schema-defined event in 10 minutes. By the end, you'll have a running ASP.NET Core API that emits otel-events events as structured JSONL on stdout.

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (or .NET 8/9)
- A terminal
- An editor with C# support (VS Code, Visual Studio, Rider)

---

## Step 1: Create a Project

```bash
# Create a new minimal API project
dotnet new webapi -n MyOrderService
cd MyOrderService
```

---

## Step 2: Install otel-events Packages

```bash
# Core: schema parser + code generator
dotnet add package OtelEvents.Schema

# JSON exporter: AI-optimized JSONL on stdout
dotnet add package OtelEvents.Exporter.Json

# Causality: eventId/parentEventId causal linking
dotnet add package OtelEvents.Causality

# Analyzers: compile-time logging hygiene
dotnet add package OtelEvents.Analyzers
```

```

---

## Step 3: Create Your Schema

Create a `schemas/` directory and add your first schema file:

```bash
mkdir schemas
```

Create `schemas/orders.otel.yaml`:

```yaml
schema:
  name: "OrderEvents"
  version: "3.0.0"
  namespace: "MyOrderService.Events"
  meterName: "MyOrderService.Events.Orders"
  prefix: ORDER

enums:
  OrderStatus:
    description: "Current state of an order"
    values:
      - Pending
      - Confirmed
      - Shipped
      - Delivered
      - Cancelled

events:
  OrderPlaced:
    id: 1000
    type: start
    severity: INFO
    description: "An order was placed by a customer"
    message: "Order {orderId} placed by {customerId} for {amount}"
    fields:
      - orderId
      - customerId
      - amount
    metrics:
      order.placed.count:
        type: counter
        unit: "orders"
        description: "Total orders placed"
      order.placed.amount:
        type: histogram
        unit: "USD"
        description: "Order amount distribution"
    tags:
      - commerce
      - orders

  OrderStatusChanged:
    id: 1002
    severity: INFO
    description: "An order's status changed"
    message: "Order {orderId} changed from {previousStatus} to {newStatus}"
    fields:
      - orderId
      - previousStatus
      - newStatus
    tags:
      - commerce
      - orders

  OrderFailed:
    id: 2000
    type: failure
    parent: OrderPlaced
    severity: ERROR
    description: "Order processing failed"
    message: "Order {orderId} failed: {reason}"
    exception: true
    fields:
      - orderId
      - reason
    metrics:
      order.failure.count:
        type: counter
        description: "Total order failures"
    tags:
      - commerce
      - orders
```

---

## Step 4: Generate Code

Run the code generator:

```bash
dotnet otel-events generate schemas/orders.otel.yaml -o Generated/
```

This creates:

| File | Contents |
|------|----------|
| `Generated/OrderEventsEventSource.g.cs` | `[LoggerMessage]` partial methods + `ILogger<T>` extension methods |
| `Generated/OrderStatus.g.cs` | Generated enum type with `ToStringFast()` |
| `Generated/OrderEventsMetrics.g.cs` | Static `Meter`, `Counter`, `Histogram` instances |

You now have IntelliSense-enabled, type-safe event methods.

---

## Step 5: Configure OTEL + otel-events

Update `Program.cs`:

```csharp
using OtelEvents.Causality;
using OtelEvents.Exporter.Json;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

// ─── Standard OTEL setup — otel-events extends it ──────────────────────────
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("order-service"))
    .WithLogging(logging =>
    {
        // otel-events: causal event linking
        logging.AddProcessor<OtelEventsCausalityProcessor>();

        // otel-events: AI-optimized JSON exporter
        logging.AddOtelEventsJsonExporter(options =>
        {
            options.Output = OtelEventsJsonOutput.Stdout;
            options.SchemaVersion = "1.0.0";
        });
    })
    .WithMetrics(metrics =>
    {
        // Pick up otel-events generated meters
        metrics.AddMeter("MyOrderService.Events.*");
    });

var app = builder.Build();
```

---

## Step 6: Emit Events

Add an endpoint that uses the generated event methods:

```csharp
using MyOrderService.Events;
using OtelEvents.Causality;

// ... after building the app ...

app.MapPost("/orders", (CreateOrderRequest request, ILogger<OrderEventsEventSource> logger) =>
{
    var orderId = Guid.NewGuid().ToString("N")[..8];

    // ─── Typed transaction — BeginOrderPlaced() emits the start event ──
    using var scope = logger.BeginOrderPlaced(
        orderId: orderId,
        customerId: request.CustomerId,
        amount: request.Amount);

    return Results.Created($"/orders/{orderId}", new { id = orderId });
});

app.MapPost("/orders/{id}/ship", (string id, ILogger<OrderEventsEventSource> logger) =>
{
    logger.OrderStatusChanged(
        orderId: id,
        previousStatus: OrderStatus.Confirmed,
        newStatus: OrderStatus.Shipped);

    return Results.Ok();
});

app.Run();

record CreateOrderRequest(string CustomerId, double Amount);
```

---

## Step 7: Run and See Output

```bash
dotnet run
```

In another terminal, send a request:

```bash
curl -X POST http://localhost:5000/orders \
  -H "Content-Type: application/json" \
  -d '{"customerId": "CUST-001", "amount": 99.99}'
```

You'll see a single JSONL line on stdout:

```json
{"timestamp":"2025-01-15T14:30:00.123456Z","event":"OrderPlaced","eventCode":"ORDER-1000","severity":"INFO","severityNumber":9,"message":"Order a1b2c3d4 placed by CUST-001 for 99.99","service":"order-service","traceId":"4bf92f3577b34da6a3ce929d0e0e4736","spanId":"00f067aa0ba902b7","eventId":"evt_019470a0-b1c2-7d3e-8f4a-5b6c7d8e9f0a","attr":{"orderId":"a1b2c3d4","customerId":"CUST-001","amount":99.99},"tags":["commerce","orders"],"otel_events.v":"3.0.0","otel_events.seq":1,"otel_events.elapsed_ms":0.42}
```

Use `jq` to pretty-print for readability:

```bash
dotnet run 2>/dev/null | jq .
```

---

## Step 8: Verify with Tests

Add `OtelEvents.Testing` to your test project:

```bash
dotnet add tests/MyOrderService.Tests package OtelEvents.Testing
```

Write a test using `OtelEventsTestHost`:

```csharp
using OtelEvents.Testing;
using MyOrderService.Events;
using OtelEvents.Causality;

public class OrderEventTests : IDisposable
{
    private readonly ILoggerFactory _factory;
    private readonly InMemoryLogExporter _exporter;

    public OrderEventTests()
    {
        (_factory, _exporter) = OtelEventsTestHost.Create();
    }

    [Fact]
    public void OrderPlaced_EmitsCorrectEvent()
    {
        var logger = _factory.CreateLogger<OrderEventsEventSource>();

        // Typed transaction — BeginOrderPlaced() emits the start event
        using var scope = logger.BeginOrderPlaced(
            orderId: "ORD-001",
            customerId: "CUST-001",
            amount: "42.00");

        var record = _exporter.AssertSingle("OrderPlaced");
        record.AssertAttribute("orderId", "ORD-001");
        record.AssertAttribute("customerId", "CUST-001");
        record.AssertAttribute("amount", "42.00");
    }

    [Fact]
    public void OrderFailed_EmitsErrorWithException()
    {
        var logger = _factory.CreateLogger<OrderEventsEventSource>();
        var exception = new InvalidOperationException("Out of stock");

        // Start a transaction, then fail it
        using var scope = logger.BeginOrderPlaced(
            orderId: "ORD-002",
            customerId: "CUST-001",
            amount: "50.00");

        scope.TryFail(
            reason: "Out of stock",
            exception: exception);

        var record = _exporter.AssertSingle("OrderFailed");
        Assert.Equal(LogLevel.Error, record.LogLevel);
        Assert.NotNull(record.Exception);
    }

    public void Dispose() => _factory.Dispose();
}
```

---

## What You've Built

In 10 minutes, you now have:

| Feature | How |
|---------|-----|
| ✅ Type-safe events | YAML schema → generated `ILogger<T>` extension methods |
| ✅ Auto-generated metrics | Counters and histograms from schema `metrics:` block |
| ✅ AI-optimized JSON | Single-line JSONL on stdout via `OtelEventsJsonExporter` |
| ✅ Causal linking | `eventId`/`parentEventId` via `OtelEventsCausalityProcessor` |
| ✅ Compile-time checks | `OtelEvents.Analyzers` catches `Console.Write` and untyped `ILogger` |
| ✅ IntelliSense | Full autocompletion with parameter names and docs |
| ✅ Unit tests | `OtelEventsTestHost` + `InMemoryLogExporter` for assertions |

---

## What's Next?

| Want to... | Go to... |
|-----------|----------|
| Learn the full YAML grammar | [Chapter 5 — Schema Reference](05-schema-reference.md) |
| Add HTTP/gRPC/CosmosDB/Storage events automatically | [Chapter 6 — Integration Packs](06-integration-packs.md) |
| Configure for production | [Chapter 7 — Configuration](07-configuration.md) |
| Write more tests | [Chapter 8 — Testing](08-testing.md) |
| Validate schemas in CI | [Chapter 9 — CLI Tool](09-cli-tool.md) |
| Understand security & PII | [Chapter 10 — Security & Privacy](10-security-privacy.md) |
