# Chapter 14 — OtelEvents.Health

OtelEvents.Health is an event-driven health intelligence layer that turns your existing otel-events signals into a real-time health state machine. Instead of writing custom health checks, you declare components in YAML and let the framework derive health from actual traffic.

---

## What It Does

Traditional health checks poll endpoints on a timer — they tell you nothing about real user traffic. OtelEvents.Health takes a different approach:

1. **Listens to real events** — HTTP requests, gRPC calls, outbound calls — the events your integration packs already emit
2. **Buffers signals in a sliding window** — each component tracks success/failure signals over a configurable time window
3. **Evaluates a three-state machine** — `Healthy → Degraded → CircuitOpen` based on success-rate thresholds
4. **Exposes K8s probe endpoints** — `/healthz/live`, `/healthz/ready`, `/healthz/startup` that Kubernetes consumes
5. **Supports quorum evaluation** — for load-balanced backends, evaluate whether enough instances are healthy

```
Real Traffic Events          Health State Machine          K8s Probes
─────────────────           ─────────────────────         ──────────
http.request.completed  ──►  orders-db: Healthy     ──►  /healthz/ready → 200
http.request.failed     ──►  payment-api: Degraded  ──►  /healthz/live  → 200
http.outbound.failed    ──►  cache: CircuitOpen     ──►  /healthz/ready → 503
```

### Three Packages

| Package | Purpose | When to use |
|---------|---------|-------------|
| **OtelEvents.Health** | Core state machine, signal buffers, policy evaluator | Always — this is the engine |
| **OtelEvents.Health.AspNetCore** | K8s probe endpoints (`/healthz/*`) | ASP.NET Core apps that need HTTP health endpoints |
| **OtelEvents.Health.Grpc** | gRPC subchannel health adapter for quorum evaluation | Services with gRPC backend pools |

---

## Quick Start

### 1. Install

```bash
dotnet add package OtelEvents.Health
dotnet add package OtelEvents.Health.AspNetCore
```

### 2. Define components in YAML

Create a `schemas/health.otel.yaml` file:

```yaml
schema:
  name: MyServiceHealth
  version: "1.0.0"
  namespace: MyApp.Health
  prefix: HEALTH

components:
  orders-db:
    window: 300s
    healthyAbove: 0.95
    degradedAbove: 0.7
    minimumSignals: 10
    signals:
      - event: "http.request.completed"
        match: { httpRoute: "/orders/*" }
      - event: "http.request.failed"
        match: { httpRoute: "/orders/*" }

  payment-api:
    window: 600s
    healthyAbove: 0.8
    degradedAbove: 0.5
    signals:
      - event: "http.outbound.completed"
        match: { httpClientName: "PaymentApi" }
      - event: "http.outbound.failed"
        match: { httpClientName: "PaymentApi" }
```

### 3. Register in Program.cs (3 lines)

```csharp
builder.Services.AddOtelEventsAspNetCore();                        // HTTP events
builder.Services.AddOtelEventsHealth("schemas/health.otel.yaml");  // Health state machine
app.MapHealthEndpoints();                                          // K8s probes
```

That's it. The health system automatically subscribes to the events your integration packs emit, feeds them into the state machine, and exposes probe endpoints.

---

## YAML `components:` Reference

The `components:` block defines the dependencies your service monitors. Each component has its own sliding window, thresholds, and signal mappings.

### Full Field Reference

```yaml
components:
  <component-name>:             # Unique name (e.g., "orders-db", "payment-api")
    window: <duration>          # Sliding window for signal evaluation (e.g., "300s", "600s")
    healthyAbove: <float>       # Success rate threshold for Healthy state (0.0–1.0)
    degradedAbove: <float>      # Success rate threshold for Degraded state (0.0–1.0)
    minimumSignals: <int>       # Minimum signals before evaluation starts (default: 5)
    cooldown: <duration>        # Optional cooldown between state transitions (e.g., "30s")
    responseTime:               # Optional latency-based policy
      percentile: <float>       # Percentile to evaluate (e.g., 0.95 for p95)
      degradedAfterMs: <int>    # Latency above which the component is degraded
      unhealthyAfterMs: <int>   # Latency above which the component is unhealthy
    signals:                    # Event-to-signal mappings (at least one required)
      - event: <event-name>     # The event name to subscribe to
        match:                  # Optional attribute filters (AND logic)
          <key>: <pattern>      # Exact match or trailing wildcard (e.g., "/orders/*")
```

### Field Details

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `window` | duration string | No | `300s` (5 min) | Sliding window for signal evaluation. Older signals are discarded. |
| `healthyAbove` | float (0.0–1.0) | No | `0.9` | Success rate above which the component is **Healthy**. Below this → Degraded. |
| `degradedAbove` | float (0.0–1.0) | No | `0.5` | Success rate above which the component stays **Degraded**. Below this → CircuitOpen. |
| `minimumSignals` | int | No | `5` | Minimum signals required before health evaluation starts. Below this count, the component stays Healthy. |
| `cooldown` | duration string | No | `30s` | Minimum time between state transitions, preventing rapid flapping. |
| `responseTime` | object | No | — | Optional latency-based policy. When configured, the worst of success-rate and latency determines the health state. |
| `signals` | list | Yes | — | Event-to-signal mappings. Each entry subscribes to an event name and optionally filters by attributes. |

### Duration Strings

Duration values use a simple format:

| Format | Example | Meaning |
|--------|---------|---------|
| `<number>s` | `300s` | 300 seconds (5 minutes) |
| `<number>s` | `60s` | 60 seconds (1 minute) |

### Match Patterns

Signal match filters support two patterns:

| Pattern | Example | Matches |
|---------|---------|---------|
| Exact match | `"PaymentApi"` | Only the exact string `PaymentApi` |
| Prefix wildcard | `"/orders/*"` | Any string starting with `/orders/` |

All match filters on a signal are combined with **AND** logic — every filter must match for the signal to be recorded.

---

## Signal Mapping Conventions

The health bridge automatically classifies events into health signals based on their name suffix:

| Event suffix | Signal outcome | Example events |
|-------------|---------------|----------------|
| `*.completed` | **Success** | `http.request.completed`, `grpc.call.completed` |
| `*.executed` | **Success** | `cosmosdb.query.executed` |
| `*.failed` | **Failure** | `http.request.failed`, `http.outbound.failed` |
| `*.throttled` | **Failure** | `http.throttled`, `cosmosdb.throttled` |
| `*.started` | **Skipped** | `http.outbound.started` (timing marker, not a health signal) |

This convention means you don't need to tell the system whether an event is good or bad — it infers the outcome from the event name.

### Latency Extraction

If the event contains a `durationMs` attribute, the bridge extracts it as a latency measurement for response-time policies. All otel-events integration packs include `durationMs` in their `*.completed` events.

---

## State Machine Rules

OtelEvents.Health uses a three-state model with clear transition rules:

```
                 success rate drops
    ┌─────────┐  below healthyAbove   ┌──────────┐  success rate drops   ┌──────────────┐
    │ Healthy │ ─────────────────────► │ Degraded │  below degradedAbove  │ CircuitOpen  │
    │         │                        │          │ ────────────────────► │              │
    └─────────┘                        └──────────┘                       └──────────────┘
         ▲                                  │                                    │
         │          success rate rises      │                                    │
         │          above healthyAbove      │                                    │
         │◄─────────────────────────────────┘                                    │
         │                                                                       │
         │              recovery probe succeeds                                  │
         │◄──────────────────────────────────────────────────────────────────────┘
```

### Transitions

| From | To | Condition |
|------|----|-----------|
| Healthy | Degraded | Success rate drops below `healthyAbove` threshold |
| Degraded | CircuitOpen | Success rate drops below `degradedAbove` threshold |
| Degraded | Healthy | Success rate rises above `healthyAbove` threshold |
| CircuitOpen | Healthy | Recovery probe succeeds (success rate back above threshold) |

### Key Behaviors

- **Cooldown period** — After a transition, no further transitions occur for the cooldown duration (default 30s). This prevents rapid flapping during transient failures.
- **Minimum signals** — Health evaluation doesn't start until `minimumSignals` are recorded. Before that, the component stays Healthy to avoid false alarms during startup.
- **Sliding window** — Only signals within the window are evaluated. Old signals expire automatically, so a burst of failures 10 minutes ago doesn't affect current health.
- **Worst-of-both** — When a response-time policy is configured, the final health state is the worst of success-rate evaluation and latency evaluation.

### Recovery

When a component enters `CircuitOpen`:
1. A recovery probe periodically checks if the dependency is available
2. If the probe succeeds and the success rate is back above threshold, the circuit closes directly to `Healthy`
3. There is no intermediate `Degraded` state during recovery — the transition is `CircuitOpen → Healthy`

---

## Probe Endpoint Configuration

The `OtelEvents.Health.AspNetCore` package provides K8s-compatible probe endpoints.

### Default Paths

| Endpoint | Default Path | Purpose |
|----------|-------------|---------|
| Liveness | `/healthz/live` | Is the process alive? Returns 200 as long as the app is running |
| Readiness | `/healthz/ready` | Can the app accept traffic? Returns 503 when dependencies are unhealthy |
| Startup | `/healthz/startup` | Has the app finished initializing? Returns 503 until startup completes |
| Tenant Health | `/healthz/tenants` | Per-tenant health status (disabled by default, for internal diagnostics) |

### Custom Paths

```csharp
app.MapHealthEndpoints(options =>
{
    options.LivenessPath = "/health/live";
    options.ReadinessPath = "/health/ready";
    options.StartupPath = "/health/startup";
    options.TenantHealthPath = "/health/tenants";
});
```

### Detail Levels

Control how much information health endpoints return:

| Level | What's returned | Use case |
|-------|----------------|----------|
| `StatusOnly` (default) | HTTP status code only (200/503), minimal body | Production — K8s probes only need status codes |
| `Summary` | Aggregate health status with component names | Internal dashboards behind auth |
| `Full` | Per-dependency snapshots, success rates, signal counts | Development and debugging |

```csharp
app.MapHealthEndpoints(options =>
{
    options.DefaultDetailLevel = DetailLevel.Full;  // Development only!
});
```

> **Security Note:** In production, always use `StatusOnly` (the default). Higher detail levels expose internal dependency names and health metrics. If you need detailed health data, protect the endpoints with authentication:
> ```csharp
> app.MapHealthEndpoints().RequireAuthorization("HealthOpsPolicy");
> ```

### Kubernetes Configuration Example

```yaml
# deployment.yaml
spec:
  containers:
    - name: my-service
      livenessProbe:
        httpGet:
          path: /healthz/live
          port: 8080
        initialDelaySeconds: 5
        periodSeconds: 10
      readinessProbe:
        httpGet:
          path: /healthz/ready
          port: 8080
        initialDelaySeconds: 10
        periodSeconds: 5
      startupProbe:
        httpGet:
          path: /healthz/startup
          port: 8080
        failureThreshold: 30
        periodSeconds: 2
```

---

## Quorum Evaluation

For services with multiple backend instances (e.g., gRPC backend pools), quorum evaluation determines whether enough instances are healthy to consider the service operational.

### How It Works

1. An `IInstanceHealthProbe` implementation discovers instances and probes their health
2. The `IQuorumEvaluator` counts healthy instances against a `QuorumHealthPolicy`
3. The result is a `QuorumAssessment` with a recommended `HealthState`

### gRPC Subchannel Quorum

The `OtelEvents.Health.Grpc` package provides `GrpcSubchannelHealthAdapter`, which bridges gRPC subchannel counts to the quorum system:

```csharp
// Register the gRPC health adapter
var adapter = new GrpcSubchannelHealthAdapter(grpcSource, "grpc-backend-pool");

// Evaluate quorum
var quorumEvaluator = serviceProvider.GetRequiredService<IQuorumEvaluator>();
var policy = new QuorumHealthPolicy(
    MinimumHealthyInstances: 2,
    TotalExpectedInstances: 5);

var assessment = await quorumEvaluator.EvaluateAsync(adapter, policy);
// assessment.QuorumMet → true if ≥2 of 5 instances are healthy
```

### Quorum Policy

| Field | Type | Description |
|-------|------|-------------|
| `MinimumHealthyInstances` | int | Minimum healthy instances required for quorum (≥ 1) |
| `TotalExpectedInstances` | int | Total expected instances (0 = unknown/dynamic fleet) |
| `ProbeInterval` | TimeSpan | Interval between probe cycles |
| `ProbeTimeout` | TimeSpan | Timeout per probe request |

### Quorum Assessment Result

| Field | Type | Description |
|-------|------|-------------|
| `HealthyInstances` | int | Number of instances that reported healthy |
| `TotalInstances` | int | Total instances probed |
| `MinimumRequired` | int | The quorum threshold from the policy |
| `QuorumMet` | bool | Whether healthy count meets or exceeds minimum |
| `Status` | HealthState | Recommended health state based on quorum |
| `InstanceResults` | list | Per-instance probe results |

> **Security Note:** Instance identifiers are opaque (e.g., "instance-0", "instance-1"). IP addresses, hostnames, and ports are never exposed in probe results.

---

## Example Configurations

### Basic — Single HTTP Dependency

```yaml
schema:
  name: SimpleHealth
  version: "1.0.0"
  namespace: MyApp.Health
  prefix: HEALTH

components:
  api-backend:
    window: 300s
    healthyAbove: 0.95
    degradedAbove: 0.7
    signals:
      - event: "http.outbound.completed"
        match: { httpClientName: "BackendApi" }
      - event: "http.outbound.failed"
        match: { httpClientName: "BackendApi" }
```

### Multi-Dependency — Database + Cache + External API

```yaml
schema:
  name: OrderServiceHealth
  version: "1.0.0"
  namespace: Orders.Health
  prefix: HEALTH

components:
  orders-db:
    window: 300s
    healthyAbove: 0.99
    degradedAbove: 0.9
    minimumSignals: 20
    signals:
      - event: "cosmosdb.query.executed"
      - event: "cosmosdb.query.failed"

  payment-gateway:
    window: 600s
    healthyAbove: 0.8
    degradedAbove: 0.5
    signals:
      - event: "http.outbound.completed"
        match: { httpClientName: "PaymentGateway" }
      - event: "http.outbound.failed"
        match: { httpClientName: "PaymentGateway" }

  notification-service:
    window: 120s
    healthyAbove: 0.7
    degradedAbove: 0.3
    minimumSignals: 5
    signals:
      - event: "grpc.call.completed"
        match: { grpcService: "NotificationService" }
      - event: "grpc.call.failed"
        match: { grpcService: "NotificationService" }
```

### With Response-Time Policy

```yaml
components:
  search-api:
    window: 300s
    healthyAbove: 0.95
    degradedAbove: 0.7
    responseTime:
      percentile: 0.95
      degradedAfterMs: 500
      unhealthyAfterMs: 2000
    signals:
      - event: "http.outbound.completed"
        match: { httpClientName: "SearchApi" }
      - event: "http.outbound.failed"
        match: { httpClientName: "SearchApi" }
```

With this configuration, the search-api component becomes Degraded if **either**:
- Success rate drops below 95%, **or**
- p95 latency exceeds 500ms

And becomes CircuitOpen if **either**:
- Success rate drops below 70%, **or**
- p95 latency exceeds 2000ms

### Programmatic Configuration (Without YAML)

For advanced scenarios, configure components in code:

```csharp
builder.Services.AddOtelEventsHealth(opts =>
{
    opts.AddComponent("orders-db", c => c
        .Window(TimeSpan.FromMinutes(5))
        .HealthyAbove(0.95)
        .DegradedAbove(0.7)
        .MinimumSignals(10));

    opts.AddComponent("payment-api", c => c
        .Window(TimeSpan.FromMinutes(10))
        .HealthyAbove(0.8)
        .DegradedAbove(0.5)
        .WithResponseTime(rt => rt
            .Percentile(0.95)
            .DegradedAfter(TimeSpan.FromMilliseconds(500))
            .UnhealthyAfter(TimeSpan.FromMilliseconds(2000))));
});
```

---

## Next Steps

- [Chapter 6 — Integration Packs](06-integration-packs.md) — events that feed the health system
- [Chapter 7 — Configuration](07-configuration.md) — environment-specific settings
- [Chapter 8 — Testing](08-testing.md) — test your health configuration with `OtelEvents.Testing`
