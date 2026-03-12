# Chapter 3 — Core Concepts

This chapter explains the foundational ideas behind otel-events. Understanding these concepts will help you write better schemas and get the most out of the library.

---

## Events

An otel-events event is not just a log line. It is a **structured, named occurrence** with typed fields, associated metrics, and causal links.

> **Event** — A discrete, typed, schema-defined occurrence in a system, emitted as an OTEL `LogRecord` with associated metrics.

Every event has:

| Property | Description | Example |
|----------|-------------|---------|
| **Name** | PascalCase identifier | `OrderPlaced`, `HttpRequestCompleted` |
| **ID** | Numeric ID combined with schema `prefix` to form event codes | `ORDER-1001`, `HTTP-2001` |
| **Severity** | Log level | `INFO`, `ERROR`, `WARN` |
| **Message** | Template with `{field}` placeholders | `"Order {orderId} placed by {customerId}"` |
| **Fields** | String attributes (required by default) | `- orderId`, `- amount` |
| **Metrics** | Counters and histograms | `order.placed.count: counter` |
| **Tags** | Static labels for categorization | `["commerce", "orders"]` |

### How an Event Maps to OTEL

When you call an otel-events-generated extension method like `_logger.OrderPlaced(...)`, two things happen simultaneously:

1. **LogRecord** — The `[LoggerMessage]`-generated partial method creates a native OTEL `LogRecord` with all fields as structured attributes. This record flows through the standard OTEL log pipeline (processors → exporters).

2. **Metric recording** — The generated code records values to `Counter<T>` and `Histogram<T>` instruments using OTEL's `System.Diagnostics.Metrics` API. These metrics flow through the standard OTEL metrics pipeline.

Both happen in a single method call. You cannot emit the log without the metrics, or vice versa.

---

## Schemas (`.otel.yaml`)

A schema is a YAML file that defines your events. It is the **single source of truth** for what events your service emits, what fields they carry, and what metrics they record.

### Minimal Schema Example

```yaml
schema:
  name: "OrderEvents"
  version: "1.0.0"
  namespace: "MyApp.Events"
  prefix: ORDER

events:
  OrderPlaced:
    id: 1001
    type: start
    severity: INFO
    message: "Order {orderId} placed for {amount}"
    fields:
      - orderId
      - amount

  OrderShipped:
    id: 1002
    type: success
    parent: OrderPlaced
    severity: INFO
    message: "Order {orderId} shipped via {carrier}"
    fields:
      - orderId
      - carrier

  OrderFailed:
    id: 1003
    type: failure
    parent: OrderPlaced
    severity: ERROR
    message: "Order {orderId} failed: {reason}"
    exception: true
    fields:
      - orderId
      - reason
```

### Schema File Structure

| Section | Purpose |
|---------|---------|
| `schema:` | Header — name, version, namespace, optional meter name |
| `fields:` | Reusable field definitions referenced by `ref:` in events |
| `enums:` | Enum type definitions used across events |
| `events:` | Event definitions with fields, metrics, and tags |

### Reusable Fields with `ref:`

Define common fields once and reference them in multiple events:

```yaml
fields:
  orderId:
    type: string
    description: "Unique order identifier"
    index: true

  durationMs:
    type: double
    description: "Duration in milliseconds"
    unit: "ms"

events:
  order.placed:
    id: 1001
    severity: INFO
    message: "Order {orderId} placed"
    fields:
      orderId:
        ref: orderId          # References the reusable definition above
        required: true

  order.completed:
    id: 1002
    severity: INFO
    message: "Order {orderId} completed in {durationMs}ms"
    fields:
      orderId:
        ref: orderId          # Same field definition reused
        required: true
      durationMs:
        ref: durationMs       # Reuse duration field
        required: true
```

### Supported Field Types

| YAML Type | C# Type | JSON Type | Notes |
|-----------|---------|-----------|-------|
| `string` | `string` | `string` | Max length governed by `maxLength` or global `MaxAttributeValueLength` |
| `int` | `int` | `number` | 32-bit signed integer |
| `long` | `long` | `number` | 64-bit signed integer |
| `double` | `double` | `number` | IEEE 754 double-precision |
| `bool` | `bool` | `boolean` | |
| `datetime` | `DateTimeOffset` | `string` | ISO 8601 UTC |
| `duration` | `TimeSpan` | `string` | ISO 8601 duration |
| `guid` | `Guid` | `string` | Standard GUID format |
| `enum` | Generated C# enum | `string` | Serialized as string name |
| `string[]` | `string[]` | `array` | Array of strings |
| `int[]` | `int[]` | `array` | Array of integers |
| `map` | `Dictionary<string, string>` | `object` | String key-value pairs |

For the complete YAML grammar, see [Chapter 5 — Schema Reference](05-schema-reference.md).

---

## JSON Envelope

Every `LogRecord` exported by `OtelEventsJsonExporter` produces a single JSON line with a predictable structure — the **otel-events envelope**:

```json
{
  "timestamp": "2025-01-15T14:30:00.123456Z",
  "event": "OrderPlaced",
  "severity": "INFO",
  "severityNumber": 9,
  "message": "Order ORD-789 placed for 99.99",
  "service": "order-service",
  "environment": "production",
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "spanId": "00f067aa0ba902b7",
  "eventId": "evt_019470a0-b1c2-7d3e-8f4a-5b6c7d8e9f0a",
  "parentEventId": "evt_019470a0-a1b2-7c3d-4e5f-6a7b8c9d0e1f",
  "attr": {
    "orderId": "ORD-789",
    "amount": 99.99
  },
  "tags": ["commerce", "orders"],
  "otel_events.v": "1.0.0",
  "otel_events.seq": 42
}
```

### Envelope Fields

| Field | Source | Description |
|-------|--------|-------------|
| `timestamp` | `LogRecord.Timestamp` | ISO 8601 UTC with microsecond precision |
| `event` | `LogRecord.EventId.Name` | Schema-defined event name |
| `severity` | `LogRecord.LogLevel` | `TRACE`, `DEBUG`, `INFO`, `WARN`, `ERROR`, `FATAL` |
| `severityNumber` | `LogRecord.LogLevel` | OTEL standard 1–24 |
| `message` | `LogRecord.FormattedMessage` | Interpolated message string |
| `service` | OTEL Resource `service.name` | Service name from OTEL configuration |
| `environment` | OTEL Resource `deployment.environment` | Deployment environment |
| `traceId` | `Activity.Current.TraceId` | W3C trace ID (from OTEL) |
| `spanId` | `Activity.Current.SpanId` | W3C span ID (from OTEL) |
| `eventId` | `otel_events.event_id` attribute | UUID v7 with `evt_` prefix (from `OtelEventsCausalityProcessor`) |
| `parentEventId` | `otel_events.parent_event_id` attribute | Parent event's ID (from `OtelEventsCausalityContext`) |
| `attr` | `LogRecord.Attributes` | Typed key-value payload from event fields |
| `tags` | `otel_events.tags` attribute | Schema-defined tags |
| `otel_events.v` | Exporter config | Schema version stamp |
| `otel_events.seq` | Exporter counter | Monotonic per-process sequence number |

### Envelope Rules

- **No null fields** — If a value is absent, the key is **omitted entirely**. No `"field": null`.
- **Single line** — Every event is exactly one JSON line terminated by `\n`. No pretty-printing.
- **UTC timestamps** — ISO 8601 UTC: `yyyy-MM-ddTHH:mm:ss.ffffffZ`
- **UTF-8 encoding** throughout
- **Reserved prefix** — All keys starting with `otel_events.` are reserved for library metadata

### Severity Mapping

| YAML | severityNumber | .NET LogLevel |
|------|---------------|---------------|
| `TRACE` | 1 | `Trace` |
| `DEBUG` | 5 | `Debug` |
| `INFO` | 9 | `Information` |
| `WARN` | 13 | `Warning` |
| `ERROR` | 17 | `Error` |
| `FATAL` | 21 | `Critical` |

---

## Causality

OTEL provides distributed trace correlation via `Activity` (`traceId`, `spanId`). But within a single trace/span, individual log events have no causal relationship. otel-events' `OtelEventsCausalityProcessor` adds **causal event trees** — linking child events to their parent.

### Causal Tree Example

```
evt_001: order.processing.started (orderId: "ORD-123")
├── evt_002: payment.processed     (orderId: "ORD-123", parentEventId: evt_001)
├── evt_003: inventory.reserved    (orderId: "ORD-123", parentEventId: evt_001)
└── evt_004: order.completed       (orderId: "ORD-123", parentEventId: evt_001)
```

### How it Works

1. **`OtelEventsCausalityProcessor`** — An OTEL `BaseProcessor<LogRecord>` that generates a unique `eventId` (UUID v7 with `evt_` prefix) for every `LogRecord` and reads the `parentEventId` from ambient context.

2. **`OtelEventsCausalityContext`** — Uses `AsyncLocal<string?>` to store the current parent event ID, flowing naturally across `async`/`await` boundaries.

3. **`OtelEventsCausalityContext.SetParent()`** — Sets the parent event ID for the duration of a scope:

```csharp
public async Task ProcessOrder(OrderRequest request)
{
    // Emit parent event
    _logger.OrderProcessingStarted(request.OrderId);

    // Set causal parent for subsequent events
    using (OtelEventsCausalityContext.SetParent(lastEmittedEventId))
    {
        _logger.PaymentProcessed(request.OrderId, request.Amount);
        _logger.InventoryReserved(request.OrderId, request.Items.Count);
    }
    // parentEventId reverts to previous value after scope
}
```

### Event ID Properties

| Property | Specification |
|----------|--------------|
| **Format** | `evt_` prefix + UUID v7 (RFC 9562) |
| **Uniqueness** | Globally unique — no collisions across processes |
| **Time-sortable** | UUID v7 encodes timestamp — events sort chronologically by ID |
| **Performance** | Generated in < 100ns |

---

## Sensitivity Levels

Every field in a schema supports an optional `sensitivity` attribute that classifies data sensitivity. The `OtelEventsJsonExporter` uses this to apply redaction based on the current `EnvironmentProfile`.

### The Four Levels

| Level | Description | Examples |
|-------|-------------|---------|
| `public` | Safe in all environments. **Default** if not specified | Event names, status codes, durations, HTTP methods |
| `internal` | Internal infrastructure details | Hostnames, process IDs, internal paths, container names |
| `pii` | Personally Identifiable Information | User IDs, email addresses, IP addresses, user agents |
| `credential` | Secrets, tokens, and keys | API keys, passwords, connection strings, bearer tokens |

### Schema Example

```yaml
fields:
  httpMethod:
    type: string
    sensitivity: public        # Default — safe everywhere

  hostName:
    type: string
    sensitivity: internal      # Redacted in Production

  userId:
    type: string
    sensitivity: pii           # Redacted in Staging + Production

  apiKey:
    type: string
    sensitivity: credential    # ALWAYS redacted
```

### Redaction Behavior

When redacted, the **value** is replaced with `"[REDACTED:{sensitivity}]"`. The field key remains present:

```json
{
  "attr": {
    "httpMethod": "POST",
    "hostName": "[REDACTED:internal]",
    "userId": "[REDACTED:pii]",
    "apiKey": "[REDACTED:credential]"
  }
}
```

For the complete redaction matrix by environment, see [Environment Profiles](#environment-profiles) below.

---

## Environment Profiles

`OtelEventsEnvironmentProfile` adjusts multiple security-sensitive defaults at once. Think of it as a "security preset" for your environment.

### The Three Profiles

| Profile | Description |
|---------|-------------|
| `Development` | Most permissive — full exception details, all sensitivity levels visible (except `credential`) |
| `Staging` | Moderate — `TypeAndMessage` exceptions, PII fields redacted |
| `Production` | Most restrictive (**default**) — `TypeAndMessage` exceptions, PII and internal redacted |

### Redaction Matrix

| Sensitivity | Development | Staging | Production |
|-------------|-------------|---------|------------|
| `public` | ✅ Visible | ✅ Visible | ✅ Visible |
| `internal` | ✅ Visible | ✅ Visible | 🔒 Redacted |
| `pii` | ✅ Visible | 🔒 Redacted | 🔒 Redacted |
| `credential` | 🔒 **Always Redacted** | 🔒 **Always Redacted** | 🔒 **Always Redacted** |

### Profile Defaults Summary

| Setting | Development | Staging | Production |
|---------|-------------|---------|------------|
| `ExceptionDetailLevel` | `Full` | `TypeAndMessage` | `TypeAndMessage` |
| Stack trace file paths | Included | Omitted | Omitted |
| `EmitHostInfo` (`otel_events.host`, `otel_events.pid`) | `true` | `false` | `false` |

### Auto-Detection

The `EnvironmentProfileDetector` reads the environment automatically:

1. Checks `ASPNETCORE_ENVIRONMENT` first
2. Falls back to `DOTNET_ENVIRONMENT`
3. Matches case-insensitively: `"Development"`, `"Staging"`, `"Production"`
4. **Defaults to `Production`** if neither is set (fail-closed design)

### Key Design Decision

> `credential` fields are **always redacted**, even in `Development`. If you need a credential value in local debugging, inspect it in the debugger, not the log output.

> `Production` is the **default** profile. If environment variables are unset, otel-events defaults to `Production` — the most restrictive mode.

---

## Glossary

| Term | Definition |
|------|-----------|
| **Event** | A discrete, typed, schema-defined occurrence — emitted as an OTEL `LogRecord` with associated metrics |
| **Schema** | YAML file (`.otel.yaml`) defining events, fields, metrics, and metadata |
| **Envelope** | The fixed JSON structure that `OtelEventsJsonExporter` writes for every `LogRecord` |
| **Exporter** | An OTEL `BaseExporter<T>` that sends telemetry to a destination |
| **Processor** | An OTEL `BaseProcessor<T>` that enriches or transforms telemetry in-flight |
| **Codegen** | Source generator that creates C# from YAML schemas |
| **LogRecord** | OTEL's native log data type (`OpenTelemetry.Logs.LogRecord`) |
| **JSONL** | JSON Lines — one JSON object per line, newline-delimited |
| **Causal tree** | Directed graph of events linked by `parentEventId` → `eventId` |
| **`[LoggerMessage]`** | .NET source generator attribute that creates zero-alloc `ILogger` extension methods |

---

## Next Steps

- [Chapter 4 — Getting Started](04-getting-started.md) — 10-minute hands-on tutorial
- [Chapter 5 — Schema Reference](05-schema-reference.md) — complete YAML grammar
