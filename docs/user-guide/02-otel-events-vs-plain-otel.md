# Chapter 2 — otel-events vs Plain OTEL: Side-by-Side Comparison

This chapter shows the same scenario implemented two ways: the "before" with plain OpenTelemetry .NET, and the "after" with ALL. The scenario is an e-commerce order placement endpoint.

---

## The Scenario

An HTTP POST endpoint receives an order request, persists it, and responds with the created order. We want to:

1. Log the event with typed fields
2. Record a histogram for request duration
3. Record a counter for total orders
4. Include causal linking between related events

---

## Before: Plain OTEL .NET

```csharp
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

public partial class OrderController : ControllerBase
{
    private readonly ILogger<OrderController> _logger;
    private readonly IOrderService _orderService;

    // ─── Manual metric setup ───────────────────────────────────────────
    private static readonly Meter s_meter = new("MyApp.Orders");
    private static readonly Counter<long> s_orderCount =
        s_meter.CreateCounter<long>("orders.created", "orders", "Total orders created");
    private static readonly Histogram<double> s_orderDuration =
        s_meter.CreateHistogram<double>("orders.duration", "ms", "Order creation duration");

    // ─── Manual [LoggerMessage] definition ─────────────────────────────
    [LoggerMessage(
        EventId = 1001,
        EventName = "order.placed",
        Level = LogLevel.Information,
        Message = "Order {OrderId} placed by {CustomerId} for {Amount}")]
    private static partial void LogOrderPlaced(
        ILogger logger, string orderId, string customerId, decimal amount);

    public OrderController(ILogger<OrderController> logger, IOrderService orderService)
    {
        _logger = logger;
        _orderService = orderService;
    }

    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder(CreateOrderRequest request)
    {
        var sw = Stopwatch.StartNew();

        var order = await _orderService.CreateAsync(request);

        // ─── Must remember to emit BOTH log AND metrics ────────────────
        LogOrderPlaced(_logger, order.Id, request.CustomerId, request.Amount);

        s_orderCount.Add(1, new TagList
        {
            { "customer_id", request.CustomerId },
        });
        s_orderDuration.Record(sw.Elapsed.TotalMilliseconds, new TagList
        {
            { "customer_id", request.CustomerId },
        });

        return Created($"/orders/{order.Id}", order);
    }
}
```

### Problems with this approach

| Problem | Details |
|---------|---------|
| **Untyped fields** | Nothing prevents `OrderId` from being passed as `int` in one place and `string` in another |
| **No schema enforcement** | Developer B on the same team might write `"New order: {Id} by {User}"` for the same event |
| **Metrics manually maintained** | Forget to call `s_orderCount.Add(1)` and your counter drifts from reality |
| **Log + metrics not atomic** | The log and metric calls are separate — easy to update one and forget the other |
| **No causal linking** | If this order triggers downstream events (payment, inventory), there's no link between them |
| **Boilerplate per event** | Every new event needs a `[LoggerMessage]` partial method + metric instruments + `TagList` wiring |

### Plain OTEL JSON output (Console Exporter)

```json
{
  "Timestamp": "2025-01-15T14:30:00.1234560Z",
  "EventId": 1001,
  "EventName": "order.placed",
  "LogLevel": "Information",
  "Category": "MyApp.OrderController",
  "Message": "Order ORD-789 placed by CUST-001 for 99.99",
  "State": {
    "OrderId": "ORD-789",
    "CustomerId": "CUST-001",
    "Amount": "99.99",
    "{OriginalFormat}": "Order {OrderId} placed by {CustomerId} for {Amount}"
  },
  "Scopes": [],
  "TraceId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "SpanId": "00f067aa0ba902b7"
}
```

Verbose, includes the raw format string, no causal linking, no sequence number, no service name in the envelope.

---

## After: ALL

### Step 1 — Define the event in YAML

```yaml
# schemas/orders.all.yaml
schema:
  name: "OrderEvents"
  version: "1.0.0"
  namespace: "MyApp.Events"
  meterName: "MyApp.Events.Orders"

events:
  order.placed:
    id: 1001
    severity: INFO
    description: "An order was placed by a customer"
    message: "Order {orderId} placed by {customerId} for {amount}"
    fields:
      orderId:
        type: string
        required: true
        index: true
      customerId:
        type: string
        required: true
        index: true
      amount:
        type: double
        required: true
      durationMs:
        type: double
        required: true
        description: "Time to process the order in milliseconds"
    metrics:
      order.placed.count:
        type: counter
        unit: "orders"
        description: "Total orders placed"
      order.placed.amount:
        type: histogram
        unit: "USD"
        description: "Order amount distribution"
      order.placed.duration:
        type: histogram
        unit: "ms"
        description: "Order processing duration"
    tags:
      - commerce
      - orders
```

### Step 2 — Use the generated extension method

```csharp
using MyApp.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

public class OrderController : ControllerBase
{
    private readonly ILogger<OrderController> _logger;
    private readonly IOrderService _orderService;

    public OrderController(
        ILogger<OrderController> logger,
        IOrderService orderService)
    {
        _logger = logger;
        _orderService = orderService;
    }

    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder(CreateOrderRequest request)
    {
        // Causal scope — auto-generates eventId, tracks elapsed time.
        // All events emitted inside (including from _orderService)
        // automatically get parentEventId via AsyncLocal ambient context.
        using var scope = OtelEventsCausalScope.Begin();

        var order = await _orderService.CreateAsync(request);

        // ─── One call: log + metrics + type safety + IntelliSense ──────
        _logger.EmitOrderPlaced(
            orderId: order.Id,
            customerId: request.CustomerId,
            amount: (double)request.Amount,
            durationMs: scope.ElapsedMilliseconds);  // Duration from scope — no Stopwatch needed

        return Created($"/orders/{order.Id}", order);
    }
}
```

That's it. The generated `EmitOrderPlaced` extension method:

- Calls the `[LoggerMessage]` partial method → creates an OTEL `LogRecord`
- Records the `order.placed.count` counter
- Records the `order.placed.amount` histogram with the dollar amount
- Records the `order.placed.duration` histogram with the elapsed time
- Provides full IntelliSense with parameter names and XML doc comments
- Enforces types at compile time (`string orderId`, not `object`)

### otel-events JSON output (OtelEventsJsonExporter)

```json
{"timestamp":"2025-01-15T14:30:00.123456Z","event":"order.placed","severity":"INFO","severityNumber":9,"message":"Order ORD-789 placed by CUST-001 for 99.99","service":"order-service","environment":"production","traceId":"4bf92f3577b34da6a3ce929d0e0e4736","spanId":"00f067aa0ba902b7","eventId":"evt_019470a0-b1c2-7d3e-8f4a-5b6c7d8e9f0a","parentEventId":"evt_019470a0-a1b2-7c3d-8e4f-5a6b7c8d9e0f","attr":{"orderId":"ORD-789","customerId":"CUST-001","amount":99.99,"durationMs":42.7},"tags":["commerce","orders"],"all.v":"1.0.0","all.seq":42}
```

Single line. No nulls. UTC microsecond timestamps. Causal event ID. Service name from OTEL resource. Schema version stamp. Monotonic sequence number.

---

## Comparison Table

| Aspect | Plain OTEL | With ALL |
|--------|-----------|----------|
| **Event definition** | Hand-written `[LoggerMessage]` per event | YAML schema → generated code |
| **Metrics** | Manual `Meter` / `Counter` / `Histogram` setup | Auto-generated from schema `metrics:` block |
| **Log + metrics atomicity** | Separate calls — easy to forget one | Single extension method emits both |
| **Message consistency** | Varies by developer | Schema-enforced templates |
| **IntelliSense** | None for valid field names | Full IntelliSense on generated methods |
| **Compile-time validation** | None | Roslyn analyzers catch `Console.Write`, untyped `ILogger` |
| **Causal linking** | Not available | `OtelEventsCausalityProcessor` adds `eventId` / `parentEventId` |
| **JSON output** | OTEL Console exporter (verbose, multi-line) | `OtelEventsJsonExporter` (compact, single-line JSONL) |
| **Type safety** | Easy to pass wrong type | Typed parameters enforced at compile time |
| **Boilerplate per event** | ~20 lines (method + meter + counter + histogram + TagList) | ~10 lines of YAML |
| **Schema governance** | None — each developer invents their own | Central YAML schema shared across services |
| **Event catalog** | None | `dotnet all docs` generates Markdown from schema |

---

## What Changes in Your OTEL Setup?

Nothing breaks. ALL extends your existing `AddOpenTelemetry()` call:

```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("order-service"))
    .WithLogging(logging =>
    {
        // ─── otel-events extensions (add these) ────────────────────────────────
        logging.AddProcessor<OtelEventsCausalityProcessor>();  // eventId/parentEventId
        logging.AddOtelEventsJsonExporter(options =>            // AI-optimized JSONL
        {
            options.Output = OtelEventsJsonOutput.Stdout;
            options.SchemaVersion = "1.0.0";
        });

        // ─── Standard OTEL (keep these) ────────────────────────────────
        logging.AddOtlpExporter();
    })
    .WithMetrics(metrics =>
    {
        // Pick up ALL-generated meters
        metrics.AddMeter("MyApp.Events.*");
        metrics.AddOtlpExporter();
    });
```

Your existing OTEL exporters, traces, and resource configuration stay exactly the same. The `LogRecord`s generated by otel-events are standard OTEL `LogRecord`s — every exporter in your pipeline sees them as native records.

---

## Next Steps

- [Chapter 3 — Core Concepts](03-core-concepts.md) — understand Events, Schemas, Causality, and Sensitivity
- [Chapter 4 — Getting Started](04-getting-started.md) — emit your first event in 10 minutes
