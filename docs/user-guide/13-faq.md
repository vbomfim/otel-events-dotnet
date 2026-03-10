# Chapter 13 — FAQ

Frequently asked questions about otel-events.

---

## General

### Does otel-events replace OpenTelemetry?

**No.** otel-events is an **extension** to the OpenTelemetry .NET SDK, not a replacement. otel-events adds four components to your existing OTEL pipeline:

| otel-events Component | OTEL Extension Point |
|---------------|---------------------|
| `OtelEvents.Schema` | N/A (build-time code generator) |
| `OtelEventsJsonExporter` | `BaseExporter<LogRecord>` |
| `OtelEventsCausalityProcessor` | `BaseProcessor<LogRecord>` |
| `OtelEvents.Analyzers` | Roslyn `DiagnosticAnalyzer` |

Your existing OTEL exporters (`AddOtlpExporter()`), trace instrumentation (`AddAspNetCoreInstrumentation()`), and metric pipelines continue working unchanged. otel-events sits alongside them.

### Can I use otel-events without OTEL?

No. otel-events generates code that uses OTEL types — `ILogger<T>`, `Meter`, `Counter<T>`, `Histogram<T>`, `LogRecord`. The OpenTelemetry .NET SDK must be configured in your application. otel-events extends OTEL; it has no standalone runtime.

### Can I use otel-events with Serilog?

**Yes, indirectly.** otel-events generated code uses `ILogger<T>` (Microsoft.Extensions.Logging). Serilog integrates with this via `Serilog.Extensions.Logging`. The flow is:

```
otel-events generated code → ILogger<T> → Serilog sink (via bridge)
                                 → OTEL LoggerProvider → OtelEventsJsonExporter
```

However, `OtelEventsJsonExporter` and Serilog sinks produce different output formats. You can use both, but the JSONL output from `OtelEventsJsonExporter` is specifically optimized for AI investigation and won't go through Serilog.

> **Recommendation:** If you're adopting otel-events, use the OTEL pipeline directly (`AddOpenTelemetry().WithLogging(...)`) rather than routing through Serilog. This avoids an unnecessary abstraction layer.

### Can I use otel-events with NLog or log4net?

The same pattern as Serilog applies — otel-events works through `ILogger<T>`, which NLog and log4net can bridge. But otel-events' JSON exporter runs in the OTEL pipeline, not through those frameworks. For the best experience, use OTEL's native logging pipeline.

### What .NET versions are supported?

| Package | Target Frameworks |
|---------|-------------------|
| `OtelEvents.Schema` | `netstandard2.0` (build-time only) |
| `OtelEvents.Exporter.Json` | `net8.0`, `net9.0` |
| `OtelEvents.Causality` | `net8.0`, `net9.0` |
| `OtelEvents.Analyzers` | `netstandard2.0` (build-time only) |
| `OtelEvents.Testing` | `net8.0`, `net9.0` |

Runtime packages require **.NET 8+** (LTS baseline). Build-time packages target `netstandard2.0` per Roslyn/MSBuild requirements.

### Is otel-events AOT-compatible?

Yes. otel-events is designed for Native AOT:

- `[LoggerMessage]` source generator — zero-alloc, no reflection
- `System.Text.Json` source generators — AOT-friendly serialization
- No `System.Reflection.Emit` usage
- All runtime packages set `IsAotCompatible = true`
- CI includes `PublishAot = true` verification

---

## Schema Questions

### What happens if my schema has errors?

Schema errors are caught at **build time** with clear error codes and messages. The build fails with a descriptive error:

```
error OTEL_SCHEMA_003: Message template placeholder '{userId}' does not match any field name
  in event 'order.placed' at events.otel.yaml:15
```

See [Chapter 5 — Schema Reference](05-schema-reference.md) for the complete list of 18 validation rules.

### Can I have multiple schema files?

Yes. Place multiple `.otel.yaml` files in your project — they are merged at build time. Cross-file `ref` resolution works. Event names and numeric IDs must be unique across all files.

```
src/MyService/
├── http-events.otel.yaml     # HTTP request events (IDs 1001-1099)
├── order-events.otel.yaml    # Order domain events (IDs 2001-2099)
└── shared-fields.otel.yaml   # Reusable field definitions
```

### What's the maximum schema size?

| Limit | Value |
|-------|-------|
| Max file size | 1 MB per file |
| Max events (merged) | 500 events total |
| Max fields per event | 50 fields |
| Max YAML nesting depth | 20 levels |

These limits prevent excessive code generation and YAML parsing attacks.

### Can I share schemas between services?

Yes. Package schemas as NuGet packages and import them:

```yaml
imports:
  - "package:MyCompany.Events.Shared/events.otel.yaml"
```

See [Chapter 11 — Advanced Topics](11-advanced-topics.md) for details on schema sharing via NuGet.

---

## Debugging

### How do I debug missing events?

If events aren't appearing in the JSONL output:

**1. Check severity filtering:**

```csharp
// Are you filtering out the severity level?
builder.Logging.AddFilter<OpenTelemetryLoggerProvider>(
    "MyCompany.MyService.Events", LogLevel.Information);
    // ↑ DEBUG events will be filtered out
```

**2. Verify the exporter is registered:**

```csharp
builder.Services.AddOpenTelemetry()
    .WithLogging(logging =>
    {
        logging.AddOtelEventsJsonExporter();  // Is this present?
    });
```

**3. Check OTEL SDK batching:**

The OTEL SDK batches `LogRecord`s before calling exporters (default: 5-second interval). Events may not appear immediately. For debugging, reduce the batch interval:

```csharp
logging.AddOtelEventsJsonExporter();
// Or configure the OTEL batch processor:
// Default batch export interval is 5 seconds
```

**4. Verify the generated code is being called:**

Set a breakpoint in the generated extension method. If the method isn't being called, check that you're using the correct `ILogger<T>` category type:

```csharp
// Wrong — uses the controller as category, not the generated event source
private readonly ILogger<OrderController> _logger;

// Right — uses the generated event source type
private readonly ILogger<OrderEventSource> _logger;
```

**5. Check for rate limiting or sampling:**

If you've configured `OtelEventsRateLimitProcessor` or `OtelEventsSamplingProcessor`, events may be intentionally dropped. Check the self-telemetry counters:

- `otel_events.ratelimiter.events_dropped`
- `otel_events.sampler.events_dropped`

### How do I see pretty-printed JSON?

otel-events always outputs single-line JSONL. For human-readable output, pipe through `jq`:

```bash
# Pretty-print all events
dotnet run 2>&1 | jq .

# Filter to a specific event type
dotnet run 2>&1 | jq 'select(.event == "http.request.completed")'

# Show only errors
dotnet run 2>&1 | jq 'select(.severity == "ERROR")'

# Show events with their causal parents
dotnet run 2>&1 | jq '{event: .event, eventId: .eventId, parentEventId: .parentEventId}'
```

### Why is `eventId` missing from my events?

`eventId` is added by `OtelEventsCausalityProcessor`. Ensure it's registered in the pipeline:

```csharp
builder.Services.AddOpenTelemetry()
    .WithLogging(logging =>
    {
        logging.AddProcessor<OtelEventsCausalityProcessor>();  // Required for eventId
        logging.AddOtelEventsJsonExporter();
    });
```

The processor must be registered **before** the exporter in the pipeline.

---

## Performance

### What's the performance overhead?

otel-events' overhead is minimal and well-characterized:

| Component | Target | Measurement |
|-----------|--------|-------------|
| otel-events extension method (log + metrics) | < 500ns p95 | BenchmarkDotNet |
| `OtelEventsJsonExporter` per-record serialization | < 1μs p95 | BenchmarkDotNet |
| `OtelEventsCausalityProcessor` per-record | < 200ns p95 | BenchmarkDotNet |
| Memory per event | < 256 bytes | `[MemoryDiagnoser]` |
| Throughput | > 100,000 events/s | Sustained benchmark |

For context: at 100,000 events/s, otel-events consumes ~50ms of CPU per second. The OTEL SDK pipeline itself (batching, export) adds its own overhead on top of this.

### Does otel-events allocate on the hot path?

otel-events minimizes allocations:

| Component | Allocation Strategy |
|-----------|-------------------|
| `[LoggerMessage]` call | Zero-alloc (compiler-generated `LoggerMessage.Define`) |
| Metric recording | `TagList` is a struct with inline storage for ≤ 8 tags |
| JSON writing | `Utf8JsonWriter` writing directly to stream, `ArrayPool<byte>.Shared` pooling |
| Enum serialization | Pre-computed `string` via switch expression (no `Enum.ToString()`) |
| Event ID generation | UUID v7 from `Guid.CreateVersion7()` (.NET 9+) or custom impl (.NET 8) |
| Sequence number | `Interlocked.Increment` (no lock, no allocation) |

### Will otel-events cause GC pressure?

At typical volumes (< 10,000 events/s), no measurable GC impact. At high volumes (> 50,000 events/s), monitor Gen2 GC collections — target < 3/min. The main allocation source is the OTEL SDK's batching buffers, not otel-events components.

---

## Configuration

### How do I configure otel-events for production?

```csharp
logging.AddOtelEventsJsonExporter(options =>
{
    options.Output = OtelEventsJsonOutput.Stdout;
    options.EnvironmentProfile = OtelEventsEnvironmentProfile.Production;
    // Production defaults:
    //   ExceptionDetailLevel = TypeAndMessage (no stack traces)
    //   EmitHostInfo = false
    //   pii fields = REDACTED
    //   internal fields = REDACTED
    //   credential fields = REDACTED
});
```

> Always explicitly set `EnvironmentProfile` rather than relying on defaults. The default is `Production` (most restrictive), but being explicit makes the configuration obvious.

See [Chapter 7 — Configuration](07-configuration.md) for all options.

### Can I use appsettings.json for configuration?

Yes. otel-events binds to the `OtelEvents` configuration section:

```json
{
  "OtelEvents": {
    "EnvironmentProfile": "Production",
    "EmitHostInfo": false,
    "MaxAttributeValueLength": 4096,
    "SchemaVersion": "1.0.0"
  }
}
```

Environment variables override `appsettings.json` using the `OTELEVENTS__` prefix:

```bash
OTELEVENTS__EnvironmentProfile=Production
OTELEVENTS__EmitHostInfo=false
```

### How do I filter out noisy events?

Use standard .NET logging filters:

```csharp
// Filter by category and level
builder.Logging.AddFilter<OpenTelemetryLoggerProvider>(
    "MyCompany.MyService.Events.Db", LogLevel.Information);
    // ↑ Drops DEBUG-level DB events

// Or use otel-events specific rate limiting:
logging.AddAllRateLimiter(options =>
{
    options.EventLimits = new Dictionary<string, int>
    {
        ["cosmosdb.point.read"] = 100,  // Max 100/sec
    };
});
```

---

## Integration

### What happens to third-party library logs?

Third-party libraries using `ILogger` (Entity Framework, ASP.NET Core, HttpClient) produce `LogRecord`s that flow through the OTEL pipeline. `OtelEventsJsonExporter` exports them alongside otel-events events:

- **Event name**: `LogRecord.EventId.Name` if set, otherwise `"dotnet.ilogger"`
- **Attributes**: State key-value pairs from the `LogRecord`
- **Filtering**: Controlled via `AttributeAllowlist`, `AttributeDenylist`, and `RedactPatterns`

No special bridge or configuration needed — it works automatically.

### Can I use otel-events with existing `[LoggerMessage]` definitions?

Yes. Existing hand-written `[LoggerMessage]` methods produce standard `LogRecord`s that flow through the OTEL pipeline. They'll appear in the JSONL output with the `EventId.Name` you specified.

Over time, you can replace them with otel-events generated equivalents. See [Chapter 12 — Migration Guide](12-migration-guide.md) for the step-by-step process.

### Do integration packs conflict with OTEL auto-instrumentation?

No. Integration packs (e.g., `OtelEvents.AspNetCore`) produce **log events** (`LogRecord`s). OTEL auto-instrumentation (`AddAspNetCoreInstrumentation()`) produces **trace spans** (`Activity`). They operate on different signal types and coexist without conflict.

You get complementary data:
- **OTEL traces**: Distributed request flow across services
- **otel-events events**: Structured, schema-defined occurrence records with causal linking

---

## Deployment

### How does otel-events work in containers?

otel-events is designed for containerized environments:

1. `OtelEventsJsonExporter` writes JSONL to **stdout** (container-native log collection)
2. The OTEL Collector's `filelog` receiver reads container stdout from `/var/log/pods/`
3. The Collector parses the otel-events JSON envelope and exports to backends (Loki, Elasticsearch, OTLP)

See [the Deployment Guide](../deployment/README.md) for OTEL Collector configuration, Dockerfiles, and Kubernetes manifests.

### What's the recommended OTEL Collector topology?

**DaemonSet + Gateway:**

- **DaemonSet** (one per node): Runs `filelog` receiver to collect stdout JSONL from all pods
- **Gateway** (centralized): Receives OTLP metrics and traces from services

This provides the best balance of resource efficiency and reliability.

---

## Next Steps

- [Chapter 1 — Introduction](01-introduction.md) — start from the beginning
- [Chapter 4 — Getting Started](04-getting-started.md) — 10-minute hands-on tutorial
- [SPECIFICATION.md](../../SPECIFICATION.md) — full project specification
