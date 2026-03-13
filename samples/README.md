# OtelEvents Sample — Web API

A minimal ASP.NET Core Web API that demonstrates all otel-events features working together:

- **Schema-defined events** — typed, structured order lifecycle events
- **JSONL exporter** — AI-optimized structured output to stdout
- **Causal linking** — automatic `eventId` / `parentEventId` via UUID v7
- **ASP.NET Core integration** — zero-code HTTP request lifecycle events
- **Subscriptions** — in-process event reactions via lambda handlers

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later

## Run the sample

From the repository root:

```bash
dotnet run --project samples/OtelEvents.Sample.WebApi
```

The API starts on `http://localhost:5000` (HTTP) by default.

## Test the endpoints

### Place an order

```bash
curl -s -X POST http://localhost:5000/orders \
  -H "Content-Type: application/json" \
  -d '{"customerId": "cust-42", "amount": 99.95}' | jq .
```

**Expected response:**
```json
{
  "orderId": "a1b2c3d4",
  "customerId": "cust-42",
  "amount": 99.95,
  "status": "Placed"
}
```

### Complete an order

```bash
curl -s -X POST http://localhost:5000/orders/a1b2c3d4/complete | jq .
```

### Simulate a failure

```bash
curl -s -X POST http://localhost:5000/orders/a1b2c3d4/fail | jq .
```

## What to look for in the output

When you run the sample, each event produces a structured JSONL line on stdout. Here's what the key fields mean:

### Event identity and causality

```json
{
  "timestamp": "2025-01-15T10:30:00.000Z",
  "eventName": "order.placed",
  "eventId": 20001,
  "severity": "Information",
  "body": "Order a1b2c3d4 placed by customer cust-42 for $99.95",
  "attributes": {
    "OrderId": "a1b2c3d4",
    "CustomerId": "cust-42",
    "Amount": 99.95,
    "otel_events.event_id": "0196ff1a-7c00-7abc-...",
    "otel_events.parent_event_id": null
  }
}
```

- **`otel_events.event_id`** — UUID v7 (time-sortable) unique to this event
- **`otel_events.parent_event_id`** — links to the causal scope's parent event
- **`otel_events.elapsed_ms`** — time since the causal scope was opened

### HTTP lifecycle events (automatic)

The ASP.NET Core integration pack emits events automatically for every HTTP request:

```
http.request.received  → HTTP POST /orders received
http.request.completed → HTTP POST /orders completed with 201 in 12.5ms
```

### Subscription console output

You'll also see subscription handler output in the console:

```
📦 Subscription: Order a1b2c3d4 placed for $99.95
📊 Subscription: Event 'order.placed' observed at 10:30:00.123
⚠️ Subscription: Order a1b2c3d4 failed — Payment processing failed
```

### Metrics

The sample registers metrics that can be collected by an OTEL Collector:

| Metric | Type | Description |
|--------|------|-------------|
| `sample.order.placed.count` | Counter | Total orders placed (by customerId) |
| `sample.order.placed.amount` | Histogram | Order amount distribution |
| `sample.order.failed.count` | Counter | Total order failures |

## Project structure

```
samples/OtelEvents.Sample.WebApi/
├── Program.cs                      # DI setup + API endpoints
├── OtelEvents.Sample.WebApi.csproj # Project references (not NuGet)
├── Events/
│   ├── OrderEventSource.cs         # Logger category marker
│   └── OrderEvents.g.cs            # Pre-generated event code
└── schemas/
    └── orders.otel.yaml            # Event schema definition
```

## Modify the schema and regenerate code

1. Edit `schemas/orders.otel.yaml` — add fields, events, or metrics
2. Regenerate the C# code:
   ```bash
   dotnet run --project tools/OtelEvents.Cli -- generate \
     samples/OtelEvents.Sample.WebApi/schemas/orders.otel.yaml \
     --output samples/OtelEvents.Sample.WebApi/Events/
   ```
3. The generated `OrderEvents.g.cs` will be updated with your changes
4. Build and run to see the new events in action

## Architecture overview

```
HTTP Request
    │
    ▼
┌─────────────────────────────┐
│  ASP.NET Core Middleware    │  ← OtelEvents.AspNetCore (auto HTTP events)
│  (http.request.received)    │
└────────────┬────────────────┘
             │
             ▼
┌─────────────────────────────┐
│  Minimal API Endpoint       │
│  (order.placed / completed  │  ← Your code: ILogger<OrderEventSource>
│   / failed)                 │
└────────────┬────────────────┘
             │
             ▼
┌─────────────────────────────┐
│  OTEL Logging Pipeline      │
│  ┌───────────────────────┐  │
│  │ CausalityProcessor    │──┼── Adds eventId + parentEventId (UUID v7)
│  ├───────────────────────┤  │
│  │ SubscriptionProcessor │──┼── Dispatches to lambda handlers
│  ├───────────────────────┤  │
│  │ JsonExporter          │──┼── Writes JSONL to stdout
│  └───────────────────────┘  │
└─────────────────────────────┘
```
