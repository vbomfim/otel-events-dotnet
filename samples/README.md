# OtelEvents Sample — Web API

A minimal ASP.NET Core Web API that demonstrates all otel-events features working together:

- **Schema-defined events** — typed, structured order lifecycle events
- **JSONL exporter** — AI-optimized structured output to stdout
- **Causal linking** — automatic `eventId` / `parentEventId` via UUID v7
- **ASP.NET Core integration** — zero-code HTTP request lifecycle events
- **OtelEvents.Health** — event-driven health intelligence with K8s probe endpoints

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
  "status": "Shipped"
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

### Check health status

```bash
# Liveness probe — is the process alive?
curl -s http://localhost:5000/healthz/live

# Readiness probe — are dependencies healthy?
curl -s http://localhost:5000/healthz/ready

# Startup probe — has the app finished initializing?
curl -s http://localhost:5000/healthz/startup
```

### Simulate and inspect health state transitions

```bash
# View current health state of all components
curl -s -X POST http://localhost:5000/simulate | jq .
```

Place several orders to generate `http.request.completed` events, which feed the `orders-db` component's health signals. The health state machine evaluates these signals against the thresholds defined in `schemas/health.otel.yaml`.

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

### Health state machine

The health system automatically subscribes to `http.request.completed` and `http.request.failed` events matching the configured routes. Each event feeds a signal to the `orders-db` or `payment-api` component:

- Events matching `httpRoute: "/orders/*"` → `orders-db` component
- Events matching `httpClientName: "PaymentApi"` → `payment-api` component

The `/simulate` endpoint shows the current health state of all components.

### Metrics

The sample registers metrics that can be collected by an OTEL Collector:

| Metric | Type | Description |
|--------|------|-------------|
| `sample.order.placed.count` | Counter | Total orders placed (by customerId) |
| `sample.order.placed.amount` | Histogram | Order amount distribution |
| `sample.order.failed.count` | Counter | Total order failures |

## OtelEvents.Health configuration

The health system is configured via `schemas/health.otel.yaml`:

```yaml
components:
  orders-db:
    window: 300s            # 5-minute sliding window
    healthyAbove: 0.95      # Healthy when ≥95% success rate
    degradedAbove: 0.7      # Degraded when 70–95%, CircuitOpen below 70%
    minimumSignals: 10      # Don't evaluate until 10 signals recorded
    signals:
      - event: "http.request.completed"
        match: { httpRoute: "/orders/*" }
      - event: "http.request.failed"
        match: { httpRoute: "/orders/*" }

  payment-api:
    window: 600s            # 10-minute sliding window
    healthyAbove: 0.8       # More lenient — external dependency
    degradedAbove: 0.5
    signals:
      - event: "http.outbound.completed"
        match: { httpClientName: "PaymentApi" }
      - event: "http.outbound.failed"
        match: { httpClientName: "PaymentApi" }
```

The three-line integration in `Program.cs`:

```csharp
builder.Services.AddOtelEventsAspNetCore();                        // HTTP events
builder.Services.AddOtelEventsHealth("schemas/health.otel.yaml");  // Health state machine
app.MapHealthEndpoints();                                          // K8s probes
```

## Project structure

```
samples/OtelEvents.Sample.WebApi/
├── Program.cs                      # DI setup + API endpoints + health registration
├── OtelEvents.Sample.WebApi.csproj # Project references (not NuGet)
├── Events/
│   ├── OrderEventSource.cs         # Logger category marker
│   └── OrderEvents.g.cs            # Pre-generated event code
└── schemas/
    ├── orders.otel.yaml            # Event schema definition
    └── health.otel.yaml            # Health component configuration
```

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
│  │ SubscriptionProcessor │──┼── Dispatches to health signal bridge
│  ├───────────────────────┤  │
│  │ JsonExporter          │──┼── Writes JSONL to stdout
│  └───────────────────────┘  │
└────────────┬────────────────┘
             │
             ▼
┌─────────────────────────────┐
│  OtelEvents.Health          │
│  ┌───────────────────────┐  │
│  │ HealthSignalBridge    │──┼── Routes events → health signals
│  ├───────────────────────┤  │
│  │ SignalBuffer          │──┼── Sliding-window signal storage
│  ├───────────────────────┤  │
│  │ PolicyEvaluator       │──┼── Success rate → HealthState
│  ├───────────────────────┤  │
│  │ HealthOrchestrator    │──┼── Aggregate state + K8s probes
│  └───────────────────────┘  │
└─────────────────────────────┘
```
