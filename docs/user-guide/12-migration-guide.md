# Chapter 12 — Migration Guide

Step-by-step guide for migrating from plain `ILogger` usage to otel-events generated schema-defined events.

---

## Migration Philosophy

otel-events is designed for **gradual, non-breaking adoption**. You don't need to rewrite your application in one go:

1. otel-events generated events and hand-written `ILogger` calls **coexist** in the same pipeline
2. The `OtelEventsJsonExporter` exports **all** `LogRecord`s — both otel-events generated and plain `ILogger` calls
3. Third-party library logs pass through unchanged
4. You migrate one event category at a time

> **Nothing breaks.** Adding otel-events to an existing OTEL setup is additive. Your existing `ILogger` calls, severity filters, and OTLP exporters continue working exactly as before.

---

## Before You Start

### Prerequisites

- [ ] Your project already uses OpenTelemetry .NET SDK (or is willing to add it)
- [ ] Your project uses `ILogger<T>` for logging (standard .NET pattern)
- [ ] You have a list of events your service emits (or can audit them)

### Scope assessment

| Team Size | Recommended Approach |
|-----------|---------------------|
| 1–3 developers | Migrate all events in one sprint |
| 4–10 developers | Migrate one event category per sprint (e.g., HTTP first, then DB) |
| 10+ developers | Start with new features; migrate existing code category-by-category |

---

## Step 1 — Install Packages

Add otel-events packages alongside your existing OTEL setup:

```bash
# Core: schema parser + code generator
dotnet add package OtelEvents.Schema

# AI-optimized JSON exporter
dotnet add package OtelEvents.Exporter.Json

# Causal event linking (optional)
dotnet add package OtelEvents.Causality

# Compile-time analyzers (optional — add after initial migration)
dotnet add package OtelEvents.Analyzers
```

> **Tip:** Hold off on `OtelEvents.Analyzers` until you've migrated a few event categories. Adding it immediately will flag every existing `ILogger` call as `ALL002`, which creates noise during migration.

---

## Step 2 — Register otel-events in Program.cs

Add otel-events components to your existing OTEL setup. **Don't remove anything** — otel-events extends, it doesn't replace:

```csharp
// BEFORE: Existing OTEL setup
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("my-service"))
    .WithLogging(logging =>
    {
        logging.AddOtlpExporter();   // Keep existing exporter
    })
    .WithMetrics(metrics =>
    {
        metrics.AddOtlpExporter();   // Keep existing metrics
    });

// AFTER: Add otel-events alongside existing setup
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("my-service"))
    .WithLogging(logging =>
    {
        // NEW: otel-events causality processor
        logging.AddProcessor<OtelEventsCausalityProcessor>();

        // NEW: otel-events JSON exporter (stdout)
        logging.AddOtelEventsJsonExporter(options =>
        {
            options.Output = OtelEventsJsonOutput.Stdout;
            options.EnvironmentProfile = OtelEventsEnvironmentProfile.Development;
        });

        logging.AddOtlpExporter();   // Keep existing exporter
    })
    .WithMetrics(metrics =>
    {
        // NEW: Pick up otel-events generated meters
        metrics.AddMeter("MyCompany.MyService.Events.*");

        metrics.AddOtlpExporter();   // Keep existing metrics
    });
```

At this point, your existing logs flow through both the OTLP exporter AND the otel-events JSON exporter. Non-otel-events `LogRecord`s appear in JSONL with `"event": "dotnet.ilogger"`.

---

## Step 3 — Audit Existing Events

Identify your service's events. Look for patterns like:

```csharp
// Pattern 1: Hand-written [LoggerMessage]
[LoggerMessage(EventId = 100, Level = LogLevel.Information,
    Message = "Order {OrderId} placed by {UserId}")]
public static partial void OrderPlaced(this ILogger logger, string orderId, string userId);

// Pattern 2: Direct ILogger calls
_logger.LogInformation("HTTP {Method} {Path} completed with {StatusCode} in {Duration}ms",
    method, path, statusCode, duration);

// Pattern 3: String interpolation (bad practice)
_logger.LogInformation($"Order {orderId} created");

// Pattern 4: Console.Write (worst practice)
Console.WriteLine($"Processing order {orderId}");
```

Create a migration table:

| Current Code | Event Name | Priority | Status |
|-------------|------------|----------|--------|
| `OrderPlaced(orderId, userId)` | `order.placed` | High | ⬜ |
| `_logger.LogInformation("HTTP {Method}...")` | `http.request.completed` | High | ⬜ |
| `_logger.LogError(ex, "DB query failed")` | `db.query.failed` | Medium | ⬜ |
| `Console.WriteLine(...)` | Remove | Low | ⬜ |

---

## Step 4 — Create Your Schema

Translate your event audit into a YAML schema:

```yaml
schema:
  name: "MyService"
  version: "1.0.0"
  namespace: "MyCompany.MyService.Events"
  meterName: "MyCompany.MyService.Events"

fields:
  orderId:
    type: string
    description: "Unique order identifier"
    index: true

  userId:
    type: string
    description: "User who placed the order"
    sensitivity: pii
    index: true

  durationMs:
    type: double
    description: "Duration in milliseconds"
    unit: "ms"

events:
  order.placed:
    id: 1001
    severity: INFO
    description: "A new order was placed"
    message: "Order {orderId} placed by {userId}"
    fields:
      orderId:
        ref: orderId
        required: true
      userId:
        ref: userId
        required: true
      amount:
        type: double
        required: true
    metrics:
      order.placed.count:
        type: counter
        unit: "orders"
    tags:
      - commerce

  http.request.completed:
    id: 1002
    severity: INFO
    message: "HTTP {method} {path} completed with {statusCode} in {durationMs}ms"
    fields:
      method:
        type: string
        required: true
      path:
        type: string
        required: true
      statusCode:
        type: int
        required: true
      durationMs:
        ref: durationMs
        required: true
    metrics:
      http.request.duration:
        type: histogram
        unit: "ms"
        buckets: [5, 10, 25, 50, 100, 250, 500, 1000]
    tags:
      - api
```

---

## Step 5 — Generate and Replace

Generate the code:

```bash
dotnet all generate
```

Then replace your existing logging calls one at a time:

### Before (hand-written)

```csharp
public class OrderService
{
    private readonly ILogger<OrderService> _logger;

    public async Task PlaceOrder(OrderRequest request)
    {
        var order = await CreateOrder(request);

        _logger.LogInformation("Order {OrderId} placed by {UserId} for ${Amount}",
            order.Id, request.UserId, request.Amount);
    }
}
```

### After (otel-events generated)

```csharp
using MyCompany.MyService.Events;

public class OrderService
{
    private readonly ILogger<OrderEventSource> _logger;  // Changed: category type

    public async Task PlaceOrder(OrderRequest request)
    {
        var order = await CreateOrder(request);

        _logger.OrderPlaced(                             // Changed: generated method
            orderId: order.Id,
            userId: request.UserId,
            amount: request.Amount);
    }
}
```

### What changes

| Aspect | Before | After |
|--------|--------|-------|
| Logger category | `ILogger<OrderService>` | `ILogger<OrderEventSource>` |
| Method call | `_logger.LogInformation("Order {OrderId}...")` | `_logger.OrderPlaced(orderId, userId, amount)` |
| Metrics | Manually created Counter/Histogram | Automatically recorded by generated code |
| Type safety | String template — typos compile fine | Typed parameters — typos won't compile |
| Sensitivity | Not classified | `userId` has `sensitivity: pii` — auto-redacted in production |

---

## Step 6 — Non-otel-events Passthrough

During migration, your codebase will have a mix of otel-events generated events and plain `ILogger` calls. This is expected and fully supported.

### How non-otel-events LogRecords are handled

The `OtelEventsJsonExporter` exports **all** `LogRecord`s:

| Source | `event` field | `attr` field |
|--------|--------------|-------------|
| otel-events generated | `"http.request.completed"` | Schema-defined typed fields |
| Hand-written `[LoggerMessage]` | `EventId.Name` if set | State key-value pairs |
| Direct `_logger.LogInformation(...)` | `"dotnet.ilogger"` (fallback) | State key-value pairs |
| Third-party library (EF Core, ASP.NET) | `EventId.Name` if set | Library-specific attributes |

### Filtering non-otel-events attributes

Non-otel-events `LogRecord`s may contain sensitive data. Configure filtering:

```csharp
logging.AddOtelEventsJsonExporter(options =>
{
    // Only pass through these attributes from non-otel-events LogRecords
    options.AttributeAllowlist = ["RequestPath", "StatusCode", "ElapsedMs"];

    // Never emit these attributes (takes precedence)
    options.AttributeDenylist = ["ConnectionString", "Password", "Token"];

    // Redact matching values
    options.RedactPatterns =
    [
        @"(?i)(password|pwd|secret)\s*[=:]\s*\S+",
        @"Bearer\s+[A-Za-z0-9\-._~+/]+=*",
    ];
});
```

---

## Step 7 — Validate and Enforce

Once you've migrated a critical mass of events:

### Add schema validation to CI

```bash
# In your CI pipeline
dotnet all validate events.all.yaml
```

### Enable analyzers

```bash
dotnet add package OtelEvents.Analyzers
```

Start with warnings, then promote to errors as migration completes:

```editorconfig
# .editorconfig — gradual enforcement

# Phase 1: Warn about Console.Write (easy wins)
[src/**/*.cs]
dotnet_diagnostic.ALL001.severity = warning

# Phase 2: Warn about untyped ILogger (after most events migrated)
dotnet_diagnostic.ALL002.severity = warning

# Phase 3: Error on string interpolation (always bad)
dotnet_diagnostic.ALL003.severity = error
```

### Suppress for unmigrated code

```csharp
// Temporary suppression — remove after migrating this event
#pragma warning disable ALL002 // Will be replaced with otel-events generated event in sprint 14
_logger.LogWarning("Legacy event: {Detail}", detail);
#pragma warning restore ALL002
```

---

## Migration Checklist

Use this checklist to track your migration progress:

### Phase 1 — Foundation (Sprint 1)

- [ ] Install `OtelEvents.Schema`, `OtelEvents.Exporter.Json`, `OtelEvents.Causality`
- [ ] Register otel-events components in `Program.cs` alongside existing OTEL
- [ ] Create initial `events.all.yaml` with 3–5 high-priority events
- [ ] Run `dotnet all generate` and verify generated code
- [ ] Replace first event category (e.g., HTTP request events)
- [ ] Verify JSONL output on stdout
- [ ] Add `dotnet all validate` to CI

### Phase 2 — Expansion (Sprint 2–3)

- [ ] Migrate remaining event categories one at a time
- [ ] Add sensitivity annotations to PII fields
- [ ] Configure `AttributeAllowlist` / `AttributeDenylist` for non-otel-events logs
- [ ] Add integration packs for common frameworks (AspNetCore, CosmosDB, etc.)
- [ ] Install `OtelEvents.Analyzers` with warning-level severity

### Phase 3 — Enforcement (Sprint 4+)

- [ ] Promote `ALL001` (Console.Write) to error
- [ ] Promote `ALL002` (untyped ILogger) to error for migrated namespaces
- [ ] Remove remaining `#pragma warning disable` suppressions
- [ ] Configure `EnvironmentProfile = Production` for production deployments
- [ ] Set up rate limiting / sampling for high-volume events
- [ ] Add `dotnet all diff` to PR checks for schema compatibility

---

## What Stays the Same

| Aspect | Before otel-events | After otel-events |
|--------|-----------|----------|
| OTEL `AddOtlpExporter()` | ✅ Works | ✅ Still works — unchanged |
| `builder.Logging.AddFilter(...)` | ✅ Works | ✅ Still works — unchanged |
| Third-party library `ILogger` output | ✅ Captured by OTEL | ✅ Still captured — also in JSONL |
| `Activity` / trace correlation | ✅ Works | ✅ Still works — unchanged |
| Severity filtering | ✅ Works | ✅ Still works — unchanged |
| `LogRecord`s in OTEL pipeline | Standard `LogRecord` | **Identical** `LogRecord` — OTEL pipeline sees no difference |

---

## Next Steps

- [Chapter 13 — FAQ](13-faq.md) — common questions about otel-events adoption
- [Chapter 7 — Configuration](07-configuration.md) — configure for production
- [Chapter 11 — Advanced Topics](11-advanced-topics.md) — rate limiting, sampling, schema versioning
