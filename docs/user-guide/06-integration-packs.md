# Integration Packs

Pre-built middleware and observers that auto-emit schema-defined events for common frameworks.

## OtelEvents.AspNetCore

Auto-instruments HTTP request lifecycle.

### Install

```bash
dotnet add package OtelEvents.AspNetCore
```

### Configure

```csharp
builder.Services.AddOtelEventsAspNetCore(options =>
{
    options.CaptureUserAgent = false;    // Default: false (PII)
    options.CaptureClientIp = false;     // Default: false (PII)
    options.UseRouteTemplate = true;     // Prevents cardinality explosion
    options.ExcludePaths = ["/health", "/ready", "/metrics"];
    options.MaxPathLength = 256;
    options.EnableCausalScope = true;    // Links all request events
});

app.UseOtelEventsAspNetCore();
```

### Events Emitted

| Event | When | Key Fields |
|-------|------|------------|
| `http.request.received` | Request starts | httpMethod, httpPath |
| `http.request.completed` | Request ends | httpMethod, httpPath, httpStatusCode, durationMs |
| `http.request.failed` | Unhandled exception | httpMethod, httpPath, errorType, errorMessage |

### Metrics

- `http.request.duration` — Histogram (ms)
- `http.request.count` — Counter
- `http.request.errors` — Counter
- `http.request.active` — UpDownCounter

---

## OtelEvents.Grpc

Auto-instruments gRPC server and client calls.

### Install

```bash
dotnet add package OtelEvents.Grpc
```

### Configure

```csharp
builder.Services.AddOtelEventsGrpc(options =>
{
    options.EnableCausalScope = true;
    options.ExcludeMethods = ["grpc.health.v1.Health/Check"];
    options.CaptureMetadata = false;
});
```

### Events Emitted

| Event | When | Key Fields |
|-------|------|------------|
| `grpc.call.started` | Call begins | grpcService, grpcMethod, grpcType |
| `grpc.call.completed` | Call succeeds | grpcService, grpcMethod, grpcStatusCode, durationMs |
| `grpc.call.failed` | Call fails | grpcService, grpcMethod, grpcStatusCode, errorType |

---

## OtelEvents.Azure.CosmosDb

Observes Azure CosmosDB SDK operations via DiagnosticListener.

### Install

```bash
dotnet add package OtelEvents.Azure.CosmosDb
```

### Configure

```csharp
builder.Services.AddOtelEventsCosmosDb(options =>
{
    options.CaptureQueryText = false;     // Default: false — query text may contain PII
    options.RuThreshold = 100;            // Only log operations above 100 RU
    options.LatencyThresholdMs = 500;     // Only log operations above 500ms
});
```

### Events Emitted

| Event | Key Fields |
|-------|------------|
| `cosmosdb.query.executed` | database, container, requestCharge, durationMs, cosmosQueryText |
| `cosmosdb.query.failed` | database, container, statusCode, errorType |
| `cosmosdb.point.read` | database, container, partitionKey, requestCharge, durationMs |
| `cosmosdb.point.write` | database, container, partitionKey, requestCharge, durationMs |

**Query sanitization:** When `CaptureQueryText = true`, string literals in SQL are replaced with `?` placeholders to minimize PII exposure.

### Metrics

- `cosmosdb.request.charge` — Histogram (RU)
- `cosmosdb.request.duration` — Histogram (ms)
- `cosmosdb.operation.count` — Counter

---

## OtelEvents.Azure.Storage

Intercepts Azure Blob and Queue Storage operations via HttpPipelinePolicy.

### Install

```bash
dotnet add package OtelEvents.Azure.Storage
```

### Configure

```csharp
builder.Services.AddOtelEventsAzureStorage(options =>
{
    options.EnableBlobEvents = true;
    options.EnableQueueEvents = true;
    options.ExcludeContainers = ["$logs", "diagnostics"];
    options.ExcludeQueues = ["poison"];
});
```

### Events Emitted

| Event | Key Fields |
|-------|------------|
| `storage.blob.uploaded` | accountName, containerName, blobName, contentLength, durationMs |
| `storage.blob.downloaded` | accountName, containerName, blobName, contentLength, durationMs |
| `storage.blob.deleted` | accountName, containerName, blobName, durationMs |
| `storage.queue.sent` | accountName, queueName, durationMs |
| `storage.queue.received` | accountName, queueName, durationMs |

---

## OtelEvents.HealthChecks

Publishes structured events for ASP.NET Core health check executions.

### Install

```bash
dotnet add package OtelEvents.HealthChecks
```

### Configure

```csharp
builder.Services.AddOtelEventsHealthChecks(options =>
{
    options.EmitExecutedEvents = true;     // Emit per-check execution events
    options.EmitStateChangedEvents = true; // Emit only on status transitions
    options.EmitDurationMetrics = true;    // Record check duration histogram
    options.MaxTrackedComponents = 1000;   // Bounded state tracking
});
```

### Events Emitted

| Event | When | Key Fields |
|-------|------|------------|
| `health.check.executed` | Each check runs | component, status, durationMs |
| `health.state.changed` | Status transitions | component, previousStatus, currentStatus, reason |

State changes are tracked in a bounded `ConcurrentDictionary` (max 1,000 entries) — only fires when a component's status actually changes.
