# Advanced Topics

## Rate Limiting

Prevent log flooding with `AllRateLimitProcessor`:

```csharp
logging.AddAllRateLimiter(options =>
{
    options.DefaultMaxEventsPerWindow = 1000;  // Global limit per window
    options.Window = TimeSpan.FromSeconds(1);  // Sliding window duration
    options.EventLimits = new Dictionary<string, int>
    {
        ["cosmosdb.point.read"] = 100,    // Max 100/sec for noisy operations
        ["health.check.*"] = 10,          // Wildcard matching
    };
}, innerProcessor);
```

Events exceeding the limit are silently dropped. Self-telemetry counters track `events_dropped` and `events_passed`.

## Event Sampling

Probabilistic sampling with head and tail strategies:

```csharp
logging.AddAllSampler(options =>
{
    options.Strategy = AllSamplingStrategy.Tail;
    options.DefaultSamplingRate = 0.1;        // Sample 10% of events
    options.AlwaysSampleErrors = true;        // Always keep errors
    options.ErrorMinLevel = LogLevel.Error;
    options.EventRates = new Dictionary<string, double>
    {
        ["http.request.completed"] = 0.01,   // 1% of HTTP success events
        ["order.*"] = 1.0,                    // Keep all order events
    };
}, innerProcessor);
```

- **Head sampling**: Pure probability — decided before processing
- **Tail sampling**: Error-aware — always samples errors, probability for non-errors

## Schema Versioning

Version compatibility is enforced during schema merging:

```bash
# Compare schema versions
dotnet all diff v1/events.all.yaml v2/events.all.yaml

# Output:
# Breaking changes detected (exit code 2):
#   - REMOVED event: order.cancelled
#   - CHANGED field type: order.placed.amount (int → double)
# Non-breaking changes:
#   + ADDED event: order.refunded
#   + ADDED field: order.placed.currency
```

All schemas in a merged set must share the same major version.

## Schema Sharing via NuGet

Share event contracts across services:

**Publisher** — Package schemas in NuGet:
```xml
<!-- The All.Schema.targets auto-packages .all.yaml files -->
<ItemGroup>
  <Content Include="schemas/**/*.all.yaml" Pack="true"
           PackagePath="contentFiles/any/any/schemas/" />
</ItemGroup>
```

**Consumer** — Import from packages:
```yaml
imports:
  - "package:SharedEvents/events.all.yaml"
```

## Schema Signing

HMAC-SHA256 integrity verification:

```bash
# Sign a schema
dotnet all sign events.all.yaml --key-env ALL_SCHEMA_SIGNING_KEY
# Produces events.all.yaml.sig

# Verify
dotnet all verify events.all.yaml --key-env ALL_SCHEMA_SIGNING_KEY
```

Keys are sourced from environment variables (base64-encoded) or files — never hardcoded.

## IMeterFactory DI Mode

For DI-friendly, disposable meters:

```yaml
schema:
  name: MyApp
  meterLifecycle: di   # Instead of "static" (default)
```

Generates:
```csharp
public sealed class MyAppMetrics : IDisposable
{
    public MyAppMetrics(IMeterFactory meterFactory) { ... }
    public Counter<long> OrderPlacedCount { get; }
}

// Register: services.AddMyAppMetrics();
```

## Roslyn Analyzers

Install `All.Analyzers` for build-time checks:

| Rule | Severity | Description |
|------|----------|-------------|
| ALL001 | Warning | `Console.Write*` detected — use structured events |
| ALL002 | Warning | Direct `ILogger.Log*` bypasses schema |
| ALL003 | Error | String interpolation in logger calls |
| ALL006 | Warning | Swallowed exception in catch block |
| ALL007 | Warning | `Debug.Write*`/`Trace.Write*` detected |
| ALL008 | Error | Reserved `all.` prefix in field names |
| ALL009 | Warning | PII field without redaction policy |

## Dashboard Generation

Generate Grafana dashboards from schema metrics:

```bash
dotnet all docs events.all.yaml -o docs/events.md
```

The `SchemaMetricsDashboardGenerator` produces Grafana JSON with panels for counters (rate graphs) and histograms (percentile charts). Pre-built templates available in `docs/dashboards/`.
