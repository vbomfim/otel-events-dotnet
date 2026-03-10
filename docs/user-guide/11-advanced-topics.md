# Chapter 11 — Advanced Topics

This chapter covers advanced otel-events features for production-scale deployments: rate limiting, event sampling, schema versioning, schema sharing, schema signing, DI-based metrics, Roslyn analyzers, and dashboard generation.

---

## Rate Limiting

At high throughput, some event categories can flood the pipeline. The `OtelEventsRateLimitProcessor` is a `BaseProcessor<LogRecord>` that caps event emission per category using a sliding time window.

### Configuration

```csharp
builder.Services.AddOpenTelemetry()
    .WithLogging(logging =>
    {
        logging.AddOtelEventsRateLimiter(options =>
        {
            // Global default: max 1000 events per window across all categories
            options.DefaultMaxEventsPerWindow = 1000;

            // Sliding window duration
            options.Window = TimeSpan.FromSeconds(1);

            // Per-event overrides (wildcard matching supported)
            options.EventLimits = new Dictionary<string, int>
            {
                ["cosmosdb.point.read"] = 100,    // Max 100 point reads/sec
                ["cosmosdb.point.write"] = 100,   // Max 100 point writes/sec
                ["health.check.*"] = 10,          // Max 10 health events/sec (wildcard)
                ["http.request.completed"] = 500, // Max 500 HTTP completions/sec
            };
        });
    });
```

### Behavior

| Aspect | Details |
|--------|---------|
| **Drop policy** | Events exceeding the limit are silently dropped (not queued) |
| **Self-telemetry** | `otel_events.ratelimiter.events_dropped` (Counter) and `otel_events.ratelimiter.events_passed` (Counter) track rate limiter activity |
| **Wildcard matching** | `"health.check.*"` matches `health.check.executed`, `health.state.changed`, etc. |
| **No per-event state** | Rate limiting uses event name as the grouping key — all events with the same name share a counter |
| **Severity bypass** | ERROR and FATAL events are never rate-limited (configurable via `options.MinBypassSeverity`) |

> **When to use:** Rate limiting is appropriate when specific event categories can burst (e.g., point reads in a hot loop). For overall volume reduction, use event sampling instead.

---

## Event Sampling

Probabilistic sampling reduces event volume while preserving statistical properties. otel-events provides two strategies via `OtelEventsSamplingProcessor`, a `BaseProcessor<LogRecord>`:

### Head Sampling

Decides **before processing** based on pure probability:

```csharp
logging.AddOtelEventsSampler(options =>
{
    options.Strategy = OtelEventsSamplingStrategy.Head;
    options.DefaultSamplingRate = 0.1;   // Sample 10% of all events

    // Per-event overrides
    options.EventRates = new Dictionary<string, double>
    {
        ["http.request.completed"] = 0.01,  // 1% of HTTP success events
        ["order.*"] = 1.0,                   // Keep all order events (100%)
        ["health.check.executed"] = 0.0,     // Drop all health check executed events
    };
});
```

### Tail Sampling

Error-aware sampling — **always keeps errors**, applies probability to non-errors:

```csharp
logging.AddOtelEventsSampler(options =>
{
    options.Strategy = OtelEventsSamplingStrategy.Tail;
    options.DefaultSamplingRate = 0.1;        // Sample 10% of non-error events
    options.AlwaysSampleErrors = true;        // Always keep error+ events
    options.ErrorMinLevel = LogLevel.Error;   // What counts as "error"

    options.EventRates = new Dictionary<string, double>
    {
        ["http.request.completed"] = 0.01,   // 1% of HTTP success events
        ["order.*"] = 1.0,                    // Keep all order events
    };
});
```

### Strategy comparison

| Aspect | Head Sampling | Tail Sampling |
|--------|--------------|---------------|
| **Decision point** | Before processing | After processing (inspects severity) |
| **Error handling** | Errors may be dropped | Errors are always kept |
| **Use case** | Uniform volume reduction | Production debugging (never lose errors) |
| **Overhead** | Minimal | Slightly higher (severity check) |

> **Tip:** In production, use tail sampling with `AlwaysSampleErrors = true`. You'll reduce volume by 90%+ while keeping every error for investigation.

---

## Schema Versioning

Schemas use [semver](https://semver.org/) versioning. The schema `version` field is stamped into every JSON envelope as `otel_events.v`.

### Compatibility rules

| Change Type | Semver | Example | Breaking? |
|-------------|--------|---------|-----------|
| Add new event | Minor | `order.refunded` added | No |
| Add optional field | Minor | `order.placed.currency` added | No |
| Remove event | Major | `order.cancelled` removed | **Yes** |
| Change field type | Major | `amount` changed from `int` to `double` | **Yes** |
| Add required field | Major | New required field on existing event | **Yes** |
| Change severity | Minor | `DEBUG` → `INFO` | No |
| Rename event | Major | `order.placed` → `order.created` | **Yes** |

### Detecting breaking changes

```bash
# Compare two schema versions
dotnet otel-events diff v1/events.otel.yaml v2/events.otel.yaml

# Output:
# Breaking changes detected (exit code 2):
#   - REMOVED event: order.cancelled
#   - CHANGED field type: order.placed.amount (int → double)
# Non-breaking changes:
#   + ADDED event: order.refunded
#   + ADDED field: order.placed.currency
```

| Exit Code | Meaning |
|-----------|---------|
| 0 | No changes |
| 1 | Non-breaking changes only |
| 2 | Breaking changes detected |

### Version constraints

All schemas in a merged set must share the **same major version**. Mixing `1.x` and `2.x` schemas in the same project produces a build error.

---

## Schema Sharing via NuGet

Share event contracts across services by packaging schemas in NuGet packages.

### Publisher — Package schemas

```xml
<!-- In your shared schema project's .csproj -->
<PropertyGroup>
  <PackageId>MyCompany.Events.Shared</PackageId>
  <Version>1.0.0</Version>
</PropertyGroup>

<ItemGroup>
  <!-- OtelEvents.Schema.targets auto-packages .otel.yaml files -->
  <Content Include="schemas/**/*.otel.yaml" Pack="true"
           PackagePath="contentFiles/any/any/schemas/" />
</ItemGroup>
```

### Consumer — Import from packages

```yaml
# In your service's events.otel.yaml
imports:
  - "package:MyCompany.Events.Shared/events.otel.yaml"

events:
  order.placed:
    id: 1001
    severity: INFO
    message: "Order {orderId} placed"
    fields:
      orderId:
        ref: orderId          # Defined in shared schema
        required: true
```

### Workflow

1. Define shared fields and enums in a dedicated schema package
2. Publish to your internal NuGet feed
3. Consumer services reference the package and import the schema
4. `dotnet otel-events validate` in CI verifies cross-service schema compatibility
5. Schema version bumps trigger downstream rebuilds via NuGet dependency updates

---

## Schema Signing

Schema signing provides HMAC-SHA256 integrity verification for multi-team environments where schema tampering could introduce security risks.

### Sign a schema

```bash
# Sign using an environment variable (base64-encoded key)
dotnet otel-events sign events.otel.yaml --key-env OTEL_SCHEMA_SIGNING_KEY

# Produces: events.otel.yaml.sig
```

### Verify a signature

```bash
# Verify in CI pipeline
dotnet otel-events verify events.otel.yaml --key-env OTEL_SCHEMA_SIGNING_KEY

# Exit code 0 = valid, 1 = invalid or missing signature
```

### Key management

| Source | Flag | Example |
|--------|------|---------|
| Environment variable | `--key-env` | `--key-env OTEL_SCHEMA_SIGNING_KEY` |
| File | `--key-file` | `--key-file /secrets/schema-key.bin` |

> **Never hardcode signing keys.** Use environment variables sourced from a secret manager (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault).

### CI integration

```yaml
# GitHub Actions — verify schemas before build
- name: Verify schema signatures
  run: dotnet otel-events verify schemas/*.otel.yaml --key-env OTEL_SCHEMA_SIGNING_KEY
  env:
    OTEL_SCHEMA_SIGNING_KEY: ${{ secrets.SCHEMA_SIGNING_KEY }}
```

---

## IMeterFactory DI Mode

By default, otel-events generates static `Meter` instances. For DI-friendly, disposable meters (useful in testing and multi-tenant scenarios), enable `IMeterFactory` mode:

### Schema configuration

```yaml
schema:
  name: "MyApp"
  version: "1.0.0"
  namespace: "MyApp.Events"
  meterLifecycle: di           # "static" (default) or "di"
```

### Generated code (DI mode)

```csharp
/// <summary>DI-managed metrics for MyApp events.</summary>
public sealed class MyAppMetrics : IDisposable
{
    private readonly Meter _meter;

    public MyAppMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create("MyApp.Events", "1.0.0");
        OrderPlacedCount = _meter.CreateCounter<long>("order.placed.count", "orders");
        OrderDuration = _meter.CreateHistogram<double>("order.duration", "ms");
    }

    public Counter<long> OrderPlacedCount { get; }
    public Histogram<double> OrderDuration { get; }

    public void Dispose() => _meter.Dispose();
}
```

### Registration

```csharp
// Register DI-managed metrics
builder.Services.AddMyAppMetrics();

// The Meter is created via IMeterFactory and disposed with the DI container
```

### When to use

| Scenario | Recommended Mode |
|----------|-----------------|
| Long-lived services (typical) | `static` (default) — simpler, lower overhead |
| Unit tests with isolated metrics | `di` — each test gets its own Meter instance |
| Multi-tenant services | `di` — separate Meter per tenant context |
| Short-lived processes (Functions, CLI tools) | `di` — Meter disposed with the host |

> **Trade-off:** Static Meters are never disposed — acceptable for long-lived services. DI-managed Meters provide proper lifecycle management but add DI complexity.

---

## Roslyn Analyzers

The `OtelEvents.Analyzers` package provides compile-time checks that enforce schema usage and logging hygiene.

### Install

```bash
dotnet add package OtelEvents.Analyzers
```

Analyzers activate automatically when the package is referenced.

### Analyzer rules

| Rule | Severity | Title | Description |
|------|----------|-------|-------------|
| **OTEL001** | Warning | Console output detected | `Console.Write`, `Console.WriteLine`, `Console.Error.Write` detected. Use otel-events generated events instead. |
| **OTEL002** | Warning | Untyped ILogger usage | Direct `ILogger.LogInformation`, `ILogger.LogError`, etc. without using an otel-events-generated extension method. |
| **OTEL003** | Error | String interpolation in event field | `$"..."` string interpolation passed to an otel-events-generated method parameter. Pass raw values — otel-events handles message interpolation. |
| **OTEL004** | Warning | Undefined event name | String literal that looks like an event name doesn't match any schema-defined event. |
| **OTEL005** | Info | Unused event definition | Schema defines an event that is never called in the codebase. |
| **OTEL006** | Warning | Exception not captured | `catch` block doesn't emit an otel-events event with the caught exception. |
| **OTEL007** | Warning | Debug.Write detected | `Debug.Write*`, `Trace.Write*` detected. Use otel-events generated events instead. |
| **OTEL008** | Error | Reserved prefix usage | Code uses `otel_events.` prefix in field names — reserved for library metadata. |
| **OTEL009** | Warning | PII field without redaction | Schema field with `sensitivity: pii` or `sensitivity: credential` used but no redaction policy configured. |

### Severity overrides

Override analyzer severity in `.editorconfig`:

```editorconfig
# Promote Console.Write to error in production code
[src/**/*.cs]
dotnet_diagnostic.OTEL001.severity = error

# Allow direct ILogger in specific adapter files
[src/**/Adapters/**/*.cs]
dotnet_diagnostic.OTEL002.severity = none

# Keep string interpolation as error everywhere
dotnet_diagnostic.OTEL003.severity = error
```

### Suppression

```csharp
// Explicit suppression when needed (e.g., test code, infrastructure)
#pragma warning disable OTEL001 // Console output in test assertion
Console.WriteLine(capturedOutput);
#pragma warning restore OTEL001
```

---

## Dashboard Generation

Generate event catalog documentation and Grafana dashboard templates from your schema metrics.

### Event documentation

```bash
# Generate markdown documentation from schema
dotnet otel-events docs events.otel.yaml -o docs/events.md
```

This produces a markdown file with:
- All events listed with descriptions, fields, and severity
- Metric instrument inventory (counters, histograms, gauges)
- Sensitivity classification matrix
- Field type reference

### Grafana dashboard templates

The `SchemaMetricsDashboardGenerator` produces Grafana JSON dashboard definitions:

- **Counters** → rate graphs (events/sec)
- **Histograms** → percentile charts (p50, p90, p99)
- **Gauges** → current value panels

Pre-built Grafana templates are available in the repository's `docs/dashboards/` directory.

---

## Summary

| Feature | Phase | Use Case |
|---------|-------|----------|
| Rate Limiting | 2.8 | Cap noisy event categories (e.g., point reads) |
| Event Sampling | 3.3 | Reduce overall volume while keeping errors |
| Schema Versioning | 2.3 | Detect breaking changes before deployment |
| Schema Sharing | 3.2 | Shared event contracts across microservices |
| Schema Signing | 3.10 | Integrity verification in multi-team environments |
| IMeterFactory DI Mode | 2.9 | Testable, disposable meters for DI scenarios |
| Roslyn Analyzers | 2.1 | Compile-time logging hygiene enforcement |
| Dashboard Generation | 3.5 | Auto-generated Grafana dashboards from schema metrics |

---

## Next Steps

- [Chapter 12 — Migration Guide](12-migration-guide.md) — step-by-step migration from plain `ILogger` to otel-events
- [Chapter 13 — FAQ](13-faq.md) — frequently asked questions
