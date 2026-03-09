# Chapter 6 — Integration Packs

Integration packs are pre-built packages that automatically emit structured events for common .NET technologies. Each pack ships with a bundled YAML schema, pre-compiled event code, runtime glue (middleware/interceptor/observer/publisher), and a simple `Add*()` registration method.

**You don't need to write schemas or generate code** — integration packs handle everything.

---

## Overview

| Package | Technology | Events | Registration |
|---------|-----------|--------|--------------|
| `OtelEvents.AspNetCore` | ASP.NET Core HTTP | `http.request.received`, `http.request.completed`, `http.request.failed` | `AddOtelEventsAspNetCore()` |
| `OtelEvents.Grpc` | gRPC (server + client) | `grpc.call.started`, `grpc.call.completed`, `grpc.call.failed` | `AddOtelEventsGrpc()` |
| `OtelEvents.Azure.CosmosDb` | Azure Cosmos DB | `cosmosdb.query.executed`, `cosmosdb.query.failed`, `cosmosdb.point.read`, `cosmosdb.point.write` | `AddOtelEventsCosmosDb()` |
| `OtelEvents.Azure.Storage` | Azure Blob + Queue Storage | `storage.blob.uploaded`, `storage.blob.downloaded`, `storage.blob.deleted`, `storage.blob.failed`, `storage.queue.sent`, `storage.queue.received`, `storage.queue.failed` | `AddOtelEventsAzureStorage()` |
| `OtelEvents.HealthChecks` | .NET Health Checks | `health.check.executed`, `health.state.changed`, `health.report.completed` | `AddOtelEventsHealthChecks()` |

### Event ID Ranges

Integration packs use event IDs starting at 10000 to avoid collisions with your application events (1–9999):

| Pack | Event ID Range |
|------|---------------|
| AspNetCore | 10001–10003 |
| Grpc | 10101–10103 |
| CosmosDb | 10201–10204 |
| Azure.Storage | 10301–10307 |
| HealthChecks | 10401–10403 |

### Architecture Pattern

All integration packs follow the same pattern:

1. **Bundled YAML schema** (embedded resource) — defines the events
2. **Pre-compiled event code** — `[LoggerMessage]` methods + metrics, already generated
3. **Runtime glue** — middleware, interceptor, observer, or publisher
4. **`Add*()` extension method** — single-line DI registration
5. **Options class** — configure behavior, exclusions, and PII capture

---

## OtelEvents.AspNetCore

Automatically emits HTTP request lifecycle events via ASP.NET Core middleware.

### Install

```bash
dotnet add package OtelEvents.AspNetCore
```

### Register

```csharp
// Program.cs
builder.Services.AddOtelEventsAspNetCore();
```

The middleware registers automatically via `IStartupFilter` at the outermost position in the pipeline. For explicit pipeline ordering, use manual registration:

```csharp
// Manual middleware registration (instead of IStartupFilter)
app.UseOtelEventsAspNetCore();
```

### Configure

```csharp
builder.Services.AddOtelEventsAspNetCore(options =>
{
    // Causal scope: all events during a request share a parentEventId
    options.EnableCausalScope = true;        // default: true

    // Emit http.request.received at the start of each request
    options.RecordRequestReceived = true;    // default: true

    // PII fields — opt-in only (GDPR/CCPA safe by default)
    options.CaptureUserAgent = false;        // default: false
    options.CaptureClientIp = false;         // default: false

    // Use route template (/api/orders/{id}) instead of raw path
    // Prevents cardinality explosion in metrics
    options.UseRouteTemplate = true;         // default: true

    // Exclude paths from event emission
    options.ExcludePaths = ["/health", "/metrics", "/ready"];

    // Truncate long paths
    options.MaxPathLength = 256;             // default: 256
});
```

### Events Emitted

| Event | ID | Severity | When |
|-------|-----|----------|------|
| `http.request.received` | 10001 | INFO | Request starts |
| `http.request.completed` | 10002 | INFO | Request completes |
| `http.request.failed` | 10003 | ERROR | Unhandled exception |

### Metrics

| Metric | Type | Unit |
|--------|------|------|
| `otel.http.request.received.count` | Counter | requests |
| `otel.http.request.duration` | Histogram | ms |
| `otel.http.response.count` | Counter | responses |
| `otel.http.request.error.count` | Counter | errors |

### `OtelEventsAspNetCoreOptions` Reference

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableCausalScope` | `bool` | `true` | Link all request events with a shared parent |
| `RecordRequestReceived` | `bool` | `true` | Emit start-of-request event |
| `CaptureUserAgent` | `bool` | `false` | Capture User-Agent header (PII) |
| `CaptureClientIp` | `bool` | `false` | Capture client IP address (PII) |
| `UseRouteTemplate` | `bool` | `true` | Use route template instead of raw path |
| `ExcludePaths` | `IList<string>` | `[]` | Paths to exclude (prefix match) |
| `MaxPathLength` | `int` | `256` | Truncate paths beyond this length |

---

## OtelEvents.Grpc

Automatically emits gRPC call lifecycle events via server and client interceptors. Handles all four gRPC call types: Unary, ServerStreaming, ClientStreaming, and DuplexStreaming.

### Install

```bash
dotnet add package OtelEvents.Grpc
```

### Register

```csharp
// Program.cs
builder.Services.AddOtelEventsGrpc();
```

Then add the interceptors to your gRPC server and/or client:

```csharp
// Server-side: add the interceptor to the gRPC service
builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<OtelEventsGrpcServerInterceptor>();
});

// Client-side: add the interceptor to a gRPC channel
var channel = GrpcChannel.ForAddress("https://localhost:5001", new GrpcChannelOptions
{
    ServiceProvider = serviceProvider,
});
```

### Configure

```csharp
builder.Services.AddOtelEventsGrpc(options =>
{
    // Causal scope per gRPC call
    options.EnableCausalScope = true;          // default: true

    // Server and client interceptors
    options.EnableServerInterceptor = true;    // default: true
    options.EnableClientInterceptor = true;    // default: true

    // Capture serialized message size
    options.CaptureMessageSize = true;         // default: true

    // Exclude services from event emission
    options.ExcludeServices = ["grpc.health.v1.Health"];

    // Exclude specific methods
    options.ExcludeMethods = ["/mypackage.MyService/NoisyMethod"];

    // Capture gRPC metadata (headers) — opt-in
    options.CaptureMetadata = false;           // default: false
});
```

### Events Emitted

| Event | ID | Severity | When |
|-------|-----|----------|------|
| `grpc.call.started` | 10101 | INFO | Call begins |
| `grpc.call.completed` | 10102 | INFO | Call completes |
| `grpc.call.failed` | 10103 | ERROR | Call fails |

### Metrics

| Metric | Type | Unit |
|--------|------|------|
| `otel.grpc.call.started.count` | Counter | calls |
| `otel.grpc.call.duration` | Histogram | ms |
| `otel.grpc.call.completed.count` | Counter | calls |
| `otel.grpc.call.error.count` | Counter | errors |

### `OtelEventsGrpcOptions` Reference

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableCausalScope` | `bool` | `true` | Link all call events with a shared parent |
| `EnableServerInterceptor` | `bool` | `true` | Enable server-side interceptor |
| `EnableClientInterceptor` | `bool` | `true` | Enable client-side interceptor |
| `CaptureMessageSize` | `bool` | `true` | Include serialized message size |
| `ExcludeServices` | `IList<string>` | `[]` | Service names to exclude |
| `ExcludeMethods` | `IList<string>` | `[]` | Method paths to exclude |
| `CaptureMetadata` | `bool` | `false` | Capture gRPC metadata (headers) |

---

## OtelEvents.Azure.CosmosDb

Automatically emits events for Azure Cosmos DB operations by subscribing to the Azure SDK's `DiagnosticListener`. No direct SDK dependency — works through reflection on diagnostic events.

### Install

```bash
dotnet add package OtelEvents.Azure.CosmosDb
```

### Register

```csharp
// Program.cs
builder.Services.AddOtelEventsCosmosDb();
```

The observer automatically subscribes to `"Azure.Cosmos.Operation"` diagnostic events.

### Configure

```csharp
builder.Services.AddOtelEventsCosmosDb(options =>
{
    // Capture query text (sanitized: string literals → ?)
    options.CaptureQueryText = false;          // default: false (sensitivity: internal)

    // Causal scope per operation
    options.EnableCausalScope = true;          // default: true

    // Capture the CosmosDB region that served the request
    options.CaptureRegion = true;              // default: true

    // Only emit events for expensive operations (by RU cost)
    options.RuThreshold = 10;                  // default: 0 (all operations)

    // Only emit events for slow operations (by latency)
    options.LatencyThresholdMs = 100;          // default: 0 (all operations)
});
```

### Events Emitted

| Event | ID | Severity | When |
|-------|-----|----------|------|
| `cosmosdb.query.executed` | 10201 | DEBUG | Query completes |
| `cosmosdb.query.failed` | 10202 | ERROR | Query fails |
| `cosmosdb.point.read` | 10203 | DEBUG | Point read completes |
| `cosmosdb.point.write` | 10204 | DEBUG | Point write completes |

### Metrics

| Metric | Type | Unit |
|--------|------|------|
| `otel.cosmosdb.query.duration` | Histogram | ms |
| `otel.cosmosdb.query.ru` | Histogram | RU |
| `otel.cosmosdb.query.item.count` | Histogram | items |
| `otel.cosmosdb.point.duration` | Histogram | ms |
| `otel.cosmosdb.point.ru` | Histogram | RU |
| `otel.cosmosdb.error.count` | Counter | errors |
| `otel.cosmosdb.operation.count` | Counter | operations |

### Query Text Sanitization

When `CaptureQueryText = true`, the observer sanitizes query text before emission:

```sql
-- Original query
SELECT * FROM c WHERE c.userId = 'usr_abc123' AND c.status = 'Active'

-- Sanitized (emitted)
SELECT * FROM c WHERE c.userId = ? AND c.status = ?
```

String literals are replaced with `?` placeholders to prevent PII leakage.

### `OtelEventsCosmosDbOptions` Reference

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `CaptureQueryText` | `bool` | `false` | Capture sanitized query text |
| `EnableCausalScope` | `bool` | `true` | Link operation events with causal parent |
| `CaptureRegion` | `bool` | `true` | Capture the serving CosmosDB region |
| `RuThreshold` | `double` | `0` | Min RU cost to emit event (0 = all) |
| `LatencyThresholdMs` | `double` | `0` | Min latency to emit event (0 = all) |

---

## OtelEvents.Azure.Storage

Automatically emits events for Azure Blob Storage and Queue Storage operations via an Azure SDK HTTP pipeline policy.

### Install

```bash
dotnet add package OtelEvents.Azure.Storage
```

### Register

```csharp
// Program.cs
builder.Services.AddOtelEventsAzureStorage();
```

The policy intercepts Azure SDK HTTP requests and classifies operations by URI and HTTP method.

### Configure

```csharp
builder.Services.AddOtelEventsAzureStorage(options =>
{
    // Enable/disable event types
    options.EnableBlobEvents = true;            // default: true
    options.EnableQueueEvents = true;           // default: true

    // Causal scope per operation
    options.EnableCausalScope = true;           // default: true

    // Exclude specific containers or queues
    options.ExcludeContainers = ["$logs", "diagnostics"];
    options.ExcludeQueues = ["poison-queue"];
});
```

### Events Emitted — Blob Storage

| Event | ID | Severity | When |
|-------|-----|----------|------|
| `storage.blob.uploaded` | 10301 | INFO | Blob upload completes |
| `storage.blob.downloaded` | 10302 | INFO | Blob download completes |
| `storage.blob.deleted` | 10303 | INFO | Blob deleted |
| `storage.blob.failed` | 10304 | ERROR | Blob operation fails |

### Events Emitted — Queue Storage

| Event | ID | Severity | When |
|-------|-----|----------|------|
| `storage.queue.sent` | 10305 | INFO | Message sent to queue |
| `storage.queue.received` | 10306 | INFO | Message received from queue |
| `storage.queue.failed` | 10307 | ERROR | Queue operation fails |

### Metrics

| Metric | Type | Unit |
|--------|------|------|
| Blob upload/download/delete counters | Counter | operations |
| Blob duration histograms | Histogram | ms |
| Blob size histograms | Histogram | bytes |
| Queue send/receive counters | Counter | messages |
| Queue message count histograms | Histogram | messages |
| Error counters | Counter | errors |

### `OtelEventsAzureStorageOptions` Reference

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableBlobEvents` | `bool` | `true` | Emit blob storage events |
| `EnableQueueEvents` | `bool` | `true` | Emit queue storage events |
| `EnableCausalScope` | `bool` | `true` | Causal linking per operation |
| `ExcludeContainers` | `IList<string>` | `[]` | Blob containers to exclude (case-insensitive) |
| `ExcludeQueues` | `IList<string>` | `[]` | Queues to exclude (case-insensitive) |

---

## OtelEvents.HealthChecks

Emits structured events for .NET Health Check poll cycles by implementing `IHealthCheckPublisher`. Tracks state changes across poll cycles to emit `health.state.changed` events.

### Install

```bash
dotnet add package OtelEvents.HealthChecks
```

### Register

```csharp
// Program.cs — requires health checks to be already configured
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck<CacheHealthCheck>("cache");

builder.Services.AddOtelEventsHealthChecks();
```

### Configure

```csharp
builder.Services.AddOtelEventsHealthChecks(options =>
{
    // Per-check execution events
    options.EmitExecutedEvents = true;              // default: true

    // State transition events (Healthy → Degraded → Unhealthy)
    options.EmitStateChangedEvents = true;           // default: true

    // Summary event after each poll cycle
    options.EmitReportCompletedEvents = true;        // default: true

    // Suppress healthy check events (reduce noise)
    options.SuppressHealthyExecutedEvents = false;   // default: false

    // Causal linking
    options.EnableCausalScope = true;                // default: true
});
```

### Events Emitted

| Event | ID | Severity | When |
|-------|-----|----------|------|
| `health.check.executed` | 10401 | INFO/WARN/ERROR | After each health check runs |
| `health.state.changed` | 10402 | WARN | Health check status transitions |
| `health.report.completed` | 10403 | INFO | After each poll cycle completes |

### State Change Detection

The publisher tracks each health check component's state in memory. A `health.state.changed` event is emitted only when a component's status transitions (e.g., `Healthy` → `Degraded`). Stable states produce no change events.

### `OtelEventsHealthCheckOptions` Reference

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EmitExecutedEvents` | `bool` | `true` | Emit per-check execution events |
| `EmitStateChangedEvents` | `bool` | `true` | Emit state transition events |
| `EmitReportCompletedEvents` | `bool` | `true` | Emit poll-cycle summary events |
| `SuppressHealthyExecutedEvents` | `bool` | `false` | Skip events for healthy checks |
| `EnableCausalScope` | `bool` | `true` | Causal linking within poll cycle |

---

## Using Multiple Packs Together

Integration packs compose naturally. Register each one independently:

```csharp
var builder = WebApplication.CreateBuilder(args);

// ─── OTEL setup ────────────────────────────────────────────────────
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("my-service"))
    .WithLogging(logging =>
    {
        logging.AddProcessor<OtelEventsCausalityProcessor>();
        logging.AddOtelEventsJsonExporter(builder.Configuration);
    })
    .WithMetrics(metrics =>
    {
        // Pick up ALL integration pack meters
        metrics.AddMeter("OtelEvents.AspNetCore");
        metrics.AddMeter("OtelEvents.Grpc");
        metrics.AddMeter("OtelEvents.Azure.CosmosDb");
        metrics.AddMeter("OtelEvents.Azure.Storage");
    });

// ─── Integration packs ────────────────────────────────────────────
builder.Services.AddOtelEventsAspNetCore(opts =>
{
    opts.ExcludePaths = ["/health", "/metrics"];
});

builder.Services.AddOtelEventsGrpc(opts =>
{
    opts.ExcludeServices = ["grpc.health.v1.Health"];
});

builder.Services.AddOtelEventsCosmosDb(opts =>
{
    opts.RuThreshold = 10;
});

builder.Services.AddOtelEventsAzureStorage(opts =>
{
    opts.ExcludeContainers = ["$logs"];
});

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddOtelEventsHealthChecks();
```

---

## Next Steps

- [Chapter 7 — Configuration](07-configuration.md) — configure the exporter, filtering, and rate limiting
- [Chapter 8 — Testing](08-testing.md) — test your events with `OtelEvents.Testing`
