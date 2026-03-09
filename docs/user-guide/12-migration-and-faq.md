# Migration Guide

How to adopt ALL in existing projects.

## From Plain ILogger

ALL is designed for **gradual adoption** — you don't need to migrate everything at once.

### Step 1: Install and Configure

```bash
dotnet add package All.Exporter.Json
```

```csharp
// Program.cs — ALL exporter alongside existing logging
builder.Services.AddOpenTelemetry()
    .WithLogging(logging =>
    {
        logging.AddAllJsonExporter(); // Captures ALL + non-ALL ILogger calls
    });
```

**Non-ALL ILogger calls pass through automatically** — they appear in JSONL output with `"event": "dotnet.ilogger"`. No migration needed for existing code to benefit from structured JSON output.

### Step 2: Define Schemas for Key Events

Start with your most important events:

```yaml
# events.all.yaml
events:
  order.placed:
    id: 1001
    severity: INFO
    message: "Order {orderId} placed"
    fields:
      orderId: { type: string, required: true }
```

### Step 3: Generate and Replace

```bash
dotnet all generate events.all.yaml -o Generated/
```

Replace:
```csharp
// Before
logger.LogInformation("Order {OrderId} placed", orderId);

// After
logger.EmitOrderPlaced(orderId);
```

### Step 4: Add Analyzers

```bash
dotnet add package All.Analyzers
```

ALL001–ALL003 will flag remaining `Console.Write*`, direct `ILogger.Log*`, and string interpolation calls.

### Step 5: Add Integration Packs

Replace manual HTTP logging with `OtelEvents.AspNetCore`, manual health check logging with `OtelEvents.HealthChecks`, etc.

## Migration Checklist

- [ ] Install `All.Exporter.Json` — immediate JSONL output for all logs
- [ ] Define `.all.yaml` for your top 5 events
- [ ] Generate C# and replace direct `ILogger` calls
- [ ] Install `All.Analyzers` to find remaining freestyle logging
- [ ] Add integration packs for ASP.NET Core, gRPC, Azure
- [ ] Configure `EnvironmentProfile` for PII redaction
- [ ] Set up severity filtering and/or rate limiting
- [ ] Add `All.Testing` utilities to your test projects

## Coexistence Strategy

ALL coexists with any logging framework:

- **Serilog/NLog**: Keep as sinks — ALL writes to OTEL pipeline, Serilog/NLog writes to their sinks. Both can run simultaneously.
- **Non-ALL ILogger calls**: Automatically captured by `AllJsonExporter` as `"dotnet.ilogger"` events. Use `AttributeAllowlist`/`AttributeDenylist` to filter sensitive attributes from third-party libraries.
- **Gradual rollout**: Migrate one service at a time. ALL-instrumented services produce schema-enforced JSONL; others produce freestyle logs. Both flow through the same OTEL Collector.

---

# FAQ & Troubleshooting

## Does ALL replace OpenTelemetry?

**No.** ALL is an extension of the OTEL .NET SDK. It uses OTEL's `BaseExporter<LogRecord>`, `BaseProcessor<LogRecord>`, `ILogger`, `Meter`, and `Activity` APIs. ALL adds schema-driven code generation and an AI-optimized JSON format on top.

## Can I use ALL with Serilog or NLog?

**Yes.** ALL operates at the OTEL pipeline level. Serilog/NLog run as separate logging providers. You can have both active simultaneously. ALL captures events through `ILogger` → OTEL SDK → `AllJsonExporter`.

## What happens if my YAML schema has errors?

Schema errors are reported at **build time** with specific error codes:

```
error ALL_SCHEMA_003: Message placeholder '{orderId}' does not match any field name
error ALL_SCHEMA_006: Event name 'OrderPlaced' must be lowercase dot-namespaced
error ALL_SCHEMA_014: Sensitivity 'secret' is not valid (use: public, internal, pii, credential)
```

The build fails — you can't ship code with an invalid schema.

## How do I debug missing events?

1. **Check severity filter**: Events below `MinSeverity` are dropped. Default is `Information`.
2. **Check DI registration**: Ensure `AddAllJsonExporter()` is called in `WithLogging()`.
3. **Check excluded paths**: `ExcludePaths` in integration packs may filter your endpoint.
4. **Check rate limiting**: Events exceeding the rate limit are silently dropped — check `all.processor.rate_limit.events_dropped` counter.
5. **Enable Development profile**: `EnvironmentProfile = Development` shows all fields including PII.

## What's the performance impact?

- **< 500ns per event** (log + metrics combined)
- **< 256 bytes allocation per event** at steady state
- **Zero Gen2 GC collections** under normal load (target: < 3/min at 100K events/sec)
- `Utf8JsonWriter` with `ArrayPool<byte>.Shared` for buffer pooling
- `[LoggerMessage]` source generation eliminates boxing and string allocation

## Can I use ALL without the JSON exporter?

**Yes.** The generated `[LoggerMessage]` methods and `Meter` instruments work with any OTEL exporter (OTLP, Console, etc.). `AllJsonExporter` is optional — it adds the AI-optimized JSONL format. You can use `AddOtlpExporter()` instead or alongside.
