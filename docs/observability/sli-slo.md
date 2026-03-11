# SLI/SLO Recommendations for otel-events

Practical guidance on defining **Service Level Indicators** (SLIs) and
**Service Level Objectives** (SLOs) for services instrumented with
**otel-events** integration packs.

> **Audience:** SRE teams, platform engineers, and service owners who operate
> .NET services that use `OtelEvents.AspNetCore`, `OtelEvents.Grpc`,
> `OtelEvents.Azure.CosmosDb`, `OtelEvents.Azure.Storage`, or
> `OtelEvents.HealthChecks`.

---

## Table of Contents

1. [Introduction](#introduction)
2. [Recommended SLIs](#recommended-slis)
3. [Suggested SLO Targets](#suggested-slo-targets)
4. [Burn-Rate Alerting](#burn-rate-alerting)
5. [Grafana Alert Examples](#grafana-alert-examples)
6. [Implementation Checklist](#implementation-checklist)

---

## Introduction

### What Are SLIs and SLOs?

| Term | Definition |
|------|-----------|
| **SLI** (Service Level Indicator) | A quantitative measure of a service's behaviour — availability, latency, error rate, throughput. |
| **SLO** (Service Level Objective) | A target value (or range) for an SLI over a rolling time window — e.g. "99.9 % of requests succeed within 500 ms over 30 days." |
| **Error Budget** | The tolerated amount of SLO violation: `1 − SLO target`. A 99.9 % SLO gives a 0.1 % error budget (≈ 43 min/month). |

### Why They Matter for otel-events Services

otel-events integration packs emit **standardised, schema-driven metrics**
(counters, histograms, gauges) for every HTTP request, gRPC call, CosmosDB
operation, storage action, and health check. These metrics map directly to the
SLIs that matter most:

- **Availability** — `otel.http.request.received.count` vs
  `otel.http.request.error.count`
- **Latency** — `otel.http.request.duration` histogram with p95 / p99
  quantiles
- **Dependency health** — `otel.cosmosdb.query.ru`,
  `otel.cosmosdb.throttled.count`, `otel.storage.throttled.count`
- **Infrastructure signals** — `otel.health.check.status` gauge,
  `otel.health.state.change.count`

Because the metric names and label dimensions are consistent across all
services that use the same integration pack, you can define **organisation-wide
SLIs once** and reuse them everywhere.

### Metric Name Convention

OTEL / Prometheus converts dotted metric names to underscored and appends type
suffixes automatically:

| Schema Metric Name | Prometheus Name |
|-------------------|-----------------|
| `otel.http.request.received.count` | `otel_http_request_received_count_total` |
| `otel.http.request.duration` | `otel_http_request_duration_bucket` / `_sum` / `_count` |
| `otel.cosmosdb.query.ru` | `otel_cosmosdb_query_ru_bucket` / `_sum` / `_count` |

See [dashboards/README.md](../dashboards/README.md#metric-name-mapping) for
the full mapping table.

---

## Recommended SLIs

### HTTP / API SLIs (OtelEvents.AspNetCore)

| SLI | Type | Metric Source | PromQL |
|-----|------|--------------|--------|
| **Availability** | Ratio | `otel.http.request.received.count`, `otel.http.request.error.count` | `1 - (rate(otel_http_request_error_count_total[5m]) / rate(otel_http_request_received_count_total[5m]))` |
| **Latency p95** | Histogram | `otel.http.request.duration` | `histogram_quantile(0.95, sum(rate(otel_http_request_duration_bucket[5m])) by (le))` |
| **Latency p99** | Histogram | `otel.http.request.duration` | `histogram_quantile(0.99, sum(rate(otel_http_request_duration_bucket[5m])) by (le))` |
| **Error Rate** | Ratio | `otel.http.request.error.count`, `otel.http.request.received.count` | `rate(otel_http_request_error_count_total[5m]) / rate(otel_http_request_received_count_total[5m])` |
| **Throttle Rate** | Ratio | `otel.http.throttled.count`, `otel.http.request.received.count` | `rate(otel_http_throttled_count_total[5m]) / rate(otel_http_request_received_count_total[5m])` |

### gRPC SLIs (OtelEvents.Grpc)

| SLI | Type | Metric Source | PromQL |
|-----|------|--------------|--------|
| **Availability** | Ratio | `otel.grpc.call.completed.count`, `otel.grpc.call.error.count` | `1 - (rate(otel_grpc_call_error_count_total[5m]) / rate(otel_grpc_call_completed_count_total[5m]))` |
| **Latency p95** | Histogram | `otel.grpc.call.duration` | `histogram_quantile(0.95, sum(rate(otel_grpc_call_duration_bucket[5m])) by (le))` |
| **Latency p99** | Histogram | `otel.grpc.call.duration` | `histogram_quantile(0.99, sum(rate(otel_grpc_call_duration_bucket[5m])) by (le))` |
| **Throttle Rate** | Ratio | `otel.grpc.throttled.count`, `otel.grpc.call.started.count` | `rate(otel_grpc_throttled_count_total[5m]) / rate(otel_grpc_call_started_count_total[5m])` |

### CosmosDB SLIs (OtelEvents.Azure.CosmosDb)

| SLI | Type | Metric Source | PromQL |
|-----|------|--------------|--------|
| **RU Consumption p95** | Histogram | `otel.cosmosdb.query.ru` | `histogram_quantile(0.95, sum(rate(otel_cosmosdb_query_ru_bucket[5m])) by (le))` |
| **RU Consumption p99** | Histogram | `otel.cosmosdb.point.ru` | `histogram_quantile(0.99, sum(rate(otel_cosmosdb_point_ru_bucket[5m])) by (le))` |
| **Query Latency p95** | Histogram | `otel.cosmosdb.query.duration` | `histogram_quantile(0.95, sum(rate(otel_cosmosdb_query_duration_bucket[5m])) by (le))` |
| **Point Op Latency p95** | Histogram | `otel.cosmosdb.point.duration` | `histogram_quantile(0.95, sum(rate(otel_cosmosdb_point_duration_bucket[5m])) by (le))` |
| **Throttle Rate** | Ratio | `otel.cosmosdb.throttled.count`, `otel.cosmosdb.operation.count` | `rate(otel_cosmosdb_throttled_count_total[5m]) / rate(otel_cosmosdb_operation_count_total[5m])` |
| **Error Rate** | Ratio | `otel.cosmosdb.error.count`, `otel.cosmosdb.operation.count` | `rate(otel_cosmosdb_error_count_total[5m]) / rate(otel_cosmosdb_operation_count_total[5m])` |

### Azure Storage SLIs (OtelEvents.Azure.Storage)

| SLI | Type | Metric Source | PromQL |
|-----|------|--------------|--------|
| **Blob Upload Latency p95** | Histogram | `otel.storage.blob.upload.duration` | `histogram_quantile(0.95, sum(rate(otel_storage_blob_upload_duration_bucket[5m])) by (le))` |
| **Queue Send Latency p95** | Histogram | `otel.storage.queue.send.duration` | `histogram_quantile(0.95, sum(rate(otel_storage_queue_send_duration_bucket[5m])) by (le))` |
| **Throttle Rate** | Ratio | `otel.storage.throttled.count`, total ops | `rate(otel_storage_throttled_count_total[5m]) / (rate(otel_storage_blob_upload_count_total[5m]) + rate(otel_storage_queue_send_count_total[5m]))` |
| **Error Rate** | Ratio | `otel.storage.blob.error.count` + `otel.storage.queue.error.count` | `(rate(otel_storage_blob_error_count_total[5m]) + rate(otel_storage_queue_error_count_total[5m])) / (rate(otel_storage_blob_upload_count_total[5m]) + rate(otel_storage_queue_send_count_total[5m]))` |

### Health Check SLIs (OtelEvents.HealthChecks)

| SLI | Type | Metric Source | PromQL |
|-----|------|--------------|--------|
| **Health Status** | Gauge | `otel.health.check.status` | `otel_health_check_status` (0 = Healthy, 1 = Degraded, 2 = Unhealthy) |
| **State Transitions** | Counter | `otel.health.state.change.count` | `increase(otel_health_state_change_count_total[1h])` |
| **Check Latency p95** | Histogram | `otel.health.check.duration` | `histogram_quantile(0.95, sum(rate(otel_health_check_duration_bucket[5m])) by (le))` |

### Application Lifecycle SLIs

| SLI | Type | Metric Source | PromQL |
|-----|------|--------------|--------|
| **Startup Duration p95** | Histogram | `app.startup.durationMs` | `histogram_quantile(0.95, sum(rate(app_startup_durationMs_bucket[5m])) by (le))` |

---

## Suggested SLO Targets

Assign every service to a **tier** based on user impact and business
criticality. Each tier maps to an SLO target and an implied error budget.

### Tier Definitions

| Tier | Description | Example Services |
|------|------------|------------------|
| **Tier 1 — Critical** | Revenue-impacting, user-facing, no fallback | Payment API, Auth service, Primary API gateway |
| **Tier 2 — Important** | User-visible but with graceful degradation | Search API, Recommendations, Notification service |
| **Tier 3 — Standard** | Internal tools, batch processing, async workers | Admin portal, Report generator, Data pipeline |

### SLO Targets by Tier

| SLI | Tier 1 (99.99 %) | Tier 2 (99.9 %) | Tier 3 (99 %) |
|-----|-------------------|-----------------|---------------|
| **Availability** | ≥ 99.99 % | ≥ 99.9 % | ≥ 99 % |
| **HTTP Latency p95** | ≤ 100 ms | ≤ 250 ms | ≤ 1 000 ms |
| **HTTP Latency p99** | ≤ 250 ms | ≤ 500 ms | ≤ 2 000 ms |
| **Error Rate** | ≤ 0.01 % | ≤ 0.1 % | ≤ 1 % |
| **Throttle Rate** | ≤ 0.01 % | ≤ 0.1 % | ≤ 1 % |
| **CosmosDB Throttle Rate** | ≤ 1 % | ≤ 5 % | ≤ 10 % |
| **CosmosDB Query Latency p95** | ≤ 50 ms | ≤ 200 ms | ≤ 1 000 ms |
| **CosmosDB RU p95 (query)** | ≤ 50 RU | ≤ 200 RU | ≤ 500 RU |
| **Startup Duration p95** | ≤ 5 s | ≤ 15 s | ≤ 60 s |
| **Health Check Latency p95** | ≤ 500 ms | ≤ 2 000 ms | ≤ 5 000 ms |

### Error Budget Reference

| SLO Target | Monthly Budget | Daily Budget | Meaning |
|-----------|---------------|-------------|---------|
| 99.99 % | 4 min 22 s | 8.6 s | "Four nines" — near-zero downtime |
| 99.9 % | 43 min 12 s | 1 min 26 s | "Three nines" — brief incidents only |
| 99 % | 7 h 12 min | 14 min 24 s | "Two nines" — some degradation acceptable |

> **Tip:** Start with conservative targets (Tier 2) and tighten after you have
> 30 days of baseline data. Overly ambitious SLOs waste on-call energy on
> noise.

---

## Burn-Rate Alerting

Traditional threshold-based alerts ("error rate > 1 %") produce noisy pages
and miss slow-burning regressions. **Multi-window burn-rate alerting**
(from the [Google SRE Workbook](https://sre.google/workbook/alerting-on-slos/))
fixes both problems by measuring how quickly the error budget is being
consumed.

### How It Works

The **burn rate** is the rate at which the error budget is consumed relative to
a steady-state pace:

```
burn_rate = actual_error_rate / (1 − SLO_target)
```

A burn rate of **1×** means the budget will be exactly exhausted at the end of
the SLO window (e.g. 30 days). A burn rate of **14.4×** means the full
monthly budget will be consumed in ~2 hours.

### Recommended Alert Windows

| Alert Class | Burn Rate | Short Window | Long Window | Action | Budget Consumed |
|------------|-----------|-------------|-------------|--------|----------------|
| **Fast burn** | 14.4× | 5 min | 1 h | Page immediately | 2 % in 1 h |
| **Slow burn** | 6× | 30 min | 6 h | Page within shift | 5 % in 6 h |
| **Chronic burn** | 3× | 1 h | 1 d | Ticket (next business day) | 10 % in 1 d |
| **Budget exhaustion** | 1× | 6 h | 3 d | Ticket / review | 10 % in 3 d |

### Multi-Window Validation

Each alert level uses **two windows** to reduce noise:

- **Long window** catches sustained degradation.
- **Short window** (≈ 1/12 of the long window) confirms the issue is still
  happening _right now_, preventing pages for problems that have already
  recovered.

An alert fires only when **both windows** exceed the burn-rate threshold
simultaneously.

### Burn-Rate Formula

For a 30-day SLO window with an availability SLO:

```
# Error ratio over a given range
http_error_ratio_<window> =
  rate(otel_http_request_error_count_total[<window>])
  /
  rate(otel_http_request_received_count_total[<window>])

# Burn rate = error_ratio / (1 - SLO)
# For 99.9% SLO: budget = 0.001
# Fast burn alert fires when:
#   http_error_ratio_1h  > 14.4 * 0.001   (= 0.0144)
#   AND
#   http_error_ratio_5m  > 14.4 * 0.001   (= 0.0144)
```

---

## Grafana Alert Examples

The following alert rules use PromQL compatible with **Grafana Alerting**
(Grafana 9+) backed by a Prometheus data source. Adjust the `slo_target`
value and `for` duration to match your tier.

### 1. HTTP Availability — Fast Burn (Page)

```yaml
# Grafana Alert Rule — HTTP Availability Fast Burn
# Fires when 2% of monthly error budget is consumed in 1 hour.
# Action: Page on-call immediately.

apiVersion: 1
groups:
  - orgId: 1
    name: otel-events-slo-availability
    folder: SLO Alerts
    interval: 1m
    rules:
      - uid: slo-http-avail-fast-burn
        title: "SLO: HTTP Availability — Fast Burn (Page)"
        condition: fast_burn
        data:
          # Long window (1h)
          - refId: error_ratio_1h
            relativeTimeRange:
              from: 3600
              to: 0
            datasourceUid: prometheus
            model:
              expr: >-
                rate(otel_http_request_error_count_total[1h])
                /
                rate(otel_http_request_received_count_total[1h])
              instant: true
          # Short window (5m)
          - refId: error_ratio_5m
            relativeTimeRange:
              from: 300
              to: 0
            datasourceUid: prometheus
            model:
              expr: >-
                rate(otel_http_request_error_count_total[5m])
                /
                rate(otel_http_request_received_count_total[5m])
              instant: true
          # Condition: both windows exceed 14.4× burn rate
          # For 99.9% SLO → budget = 0.001 → threshold = 14.4 * 0.001 = 0.0144
          - refId: fast_burn
            datasourceUid: __expr__
            model:
              type: math
              expression: "$error_ratio_1h > 0.0144 && $error_ratio_5m > 0.0144"
        for: 2m
        labels:
          severity: critical
          slo: availability
          tier: "2"
        annotations:
          summary: >-
            HTTP availability SLO burn rate is 14.4× — budget will exhaust in ~2 hours.
          description: >-
            Error ratio (1h): {{ $values.error_ratio_1h }},
            Error ratio (5m): {{ $values.error_ratio_5m }}.
            At this rate, the monthly error budget will be fully consumed within ~2 hours.
          runbook_url: "https://wiki.example.com/runbooks/slo-http-availability"
```

### 2. HTTP Availability — Slow Burn (Page Within Shift)

```yaml
      - uid: slo-http-avail-slow-burn
        title: "SLO: HTTP Availability — Slow Burn (Page Within Shift)"
        condition: slow_burn
        data:
          - refId: error_ratio_6h
            relativeTimeRange:
              from: 21600
              to: 0
            datasourceUid: prometheus
            model:
              expr: >-
                rate(otel_http_request_error_count_total[6h])
                /
                rate(otel_http_request_received_count_total[6h])
              instant: true
          - refId: error_ratio_30m
            relativeTimeRange:
              from: 1800
              to: 0
            datasourceUid: prometheus
            model:
              expr: >-
                rate(otel_http_request_error_count_total[30m])
                /
                rate(otel_http_request_received_count_total[30m])
              instant: true
          # 6× burn rate for 99.9% SLO → threshold = 6 * 0.001 = 0.006
          - refId: slow_burn
            datasourceUid: __expr__
            model:
              type: math
              expression: "$error_ratio_6h > 0.006 && $error_ratio_30m > 0.006"
        for: 5m
        labels:
          severity: warning
          slo: availability
          tier: "2"
        annotations:
          summary: >-
            HTTP availability SLO burn rate is 6× — budget will exhaust in ~5 days.
          description: >-
            Error ratio (6h): {{ $values.error_ratio_6h }},
            Error ratio (30m): {{ $values.error_ratio_30m }}.
          runbook_url: "https://wiki.example.com/runbooks/slo-http-availability"
```

### 3. HTTP Latency p99 — Fast Burn (Page)

```yaml
      - uid: slo-http-latency-fast-burn
        title: "SLO: HTTP Latency p99 — Fast Burn (Page)"
        condition: fast_burn
        data:
          - refId: latency_p99_1h
            relativeTimeRange:
              from: 3600
              to: 0
            datasourceUid: prometheus
            model:
              expr: >-
                histogram_quantile(0.99,
                  sum(rate(otel_http_request_duration_bucket[1h])) by (le)
                )
              instant: true
          - refId: latency_p99_5m
            relativeTimeRange:
              from: 300
              to: 0
            datasourceUid: prometheus
            model:
              expr: >-
                histogram_quantile(0.99,
                  sum(rate(otel_http_request_duration_bucket[5m])) by (le)
                )
              instant: true
          # Tier 2 target: p99 ≤ 500ms
          - refId: fast_burn
            datasourceUid: __expr__
            model:
              type: math
              expression: "$latency_p99_1h > 500 && $latency_p99_5m > 500"
        for: 2m
        labels:
          severity: critical
          slo: latency
          tier: "2"
        annotations:
          summary: >-
            HTTP p99 latency exceeds 500ms SLO target — sustained over 1h and 5m windows.
          description: >-
            p99 latency (1h): {{ $values.latency_p99_1h }}ms,
            p99 latency (5m): {{ $values.latency_p99_5m }}ms.
```

### 4. CosmosDB Throttle Rate — Slow Burn (Ticket)

```yaml
      - uid: slo-cosmosdb-throttle-slow-burn
        title: "SLO: CosmosDB Throttle Rate — Slow Burn (Ticket)"
        condition: slow_burn
        data:
          - refId: throttle_ratio_6h
            relativeTimeRange:
              from: 21600
              to: 0
            datasourceUid: prometheus
            model:
              expr: >-
                rate(otel_cosmosdb_throttled_count_total[6h])
                /
                rate(otel_cosmosdb_operation_count_total[6h])
              instant: true
          - refId: throttle_ratio_30m
            relativeTimeRange:
              from: 1800
              to: 0
            datasourceUid: prometheus
            model:
              expr: >-
                rate(otel_cosmosdb_throttled_count_total[30m])
                /
                rate(otel_cosmosdb_operation_count_total[30m])
              instant: true
          # Tier 2 target: throttle ≤ 5% → budget = 0.05 → 6× = 0.30
          - refId: slow_burn
            datasourceUid: __expr__
            model:
              type: math
              expression: "$throttle_ratio_6h > 0.30 && $throttle_ratio_30m > 0.30"
        for: 5m
        labels:
          severity: warning
          slo: cosmosdb-throttle
          tier: "2"
        annotations:
          summary: >-
            CosmosDB throttle rate exceeds 6× burn rate — consider scaling RU provisioning.
          description: >-
            Throttle ratio (6h): {{ $values.throttle_ratio_6h }},
            Throttle ratio (30m): {{ $values.throttle_ratio_30m }}.
            Review partition key distribution and RU allocation.
```

### 5. CosmosDB RU Consumption — Breach Alert

```yaml
      - uid: slo-cosmosdb-ru-breach
        title: "SLO: CosmosDB RU Consumption p95 Exceeds Target"
        condition: breach
        data:
          - refId: ru_p95
            relativeTimeRange:
              from: 3600
              to: 0
            datasourceUid: prometheus
            model:
              expr: >-
                histogram_quantile(0.95,
                  sum(rate(otel_cosmosdb_query_ru_bucket[1h])) by (le)
                )
              instant: true
          # Tier 2 target: p95 ≤ 200 RU
          - refId: breach
            datasourceUid: __expr__
            model:
              type: math
              expression: "$ru_p95 > 200"
        for: 15m
        labels:
          severity: warning
          slo: cosmosdb-ru
          tier: "2"
        annotations:
          summary: >-
            CosmosDB query RU consumption p95 exceeds 200 RU for 15+ minutes.
          description: >-
            p95 RU: {{ $values.ru_p95 }}.
            Review query patterns and indexing strategy.
```

### 6. Health Check Degradation

```yaml
      - uid: slo-health-degraded
        title: "SLO: Health Check Degraded or Unhealthy"
        condition: unhealthy
        data:
          - refId: health_status
            relativeTimeRange:
              from: 300
              to: 0
            datasourceUid: prometheus
            model:
              expr: "max(otel_health_check_status) by (component)"
              instant: true
          # status > 0 means Degraded (1) or Unhealthy (2)
          - refId: unhealthy
            datasourceUid: __expr__
            model:
              type: math
              expression: "$health_status > 0"
        for: 5m
        labels:
          severity: warning
          slo: health
        annotations:
          summary: >-
            One or more health checks report Degraded or Unhealthy for 5+ minutes.
          description: >-
            Health status: {{ $values.health_status }}
            (0=Healthy, 1=Degraded, 2=Unhealthy).
```

### 7. Startup Duration — Breach Alert

```yaml
      - uid: slo-startup-duration
        title: "SLO: Startup Duration p95 Exceeds Target"
        condition: breach
        data:
          - refId: startup_p95
            relativeTimeRange:
              from: 3600
              to: 0
            datasourceUid: prometheus
            model:
              expr: >-
                histogram_quantile(0.95,
                  sum(rate(app_startup_durationMs_bucket[1h])) by (le)
                )
              instant: true
          # Tier 2 target: ≤ 15s (15000ms)
          - refId: breach
            datasourceUid: __expr__
            model:
              type: math
              expression: "$startup_p95 > 15000"
        for: 5m
        labels:
          severity: warning
          slo: startup
          tier: "2"
        annotations:
          summary: >-
            Application startup duration p95 exceeds 15s target.
          description: >-
            Startup p95: {{ $values.startup_p95 }}ms.
            Check container image size, dependency injection graph, and health check probes.
```

---

## Implementation Checklist

Use this checklist when adopting SLIs/SLOs for an otel-events service:

- [ ] **Assign a tier** (1 / 2 / 3) based on user impact and business criticality
- [ ] **Enable integration packs** — install the relevant `OtelEvents.*` packages
- [ ] **Register meters** — call `AddMeter("OtelEvents.AspNetCore")` (and others) in `Program.cs`
- [ ] **Deploy the OTEL Collector** — see [dashboards/otel-collector-config.yaml](../dashboards/otel-collector-config.yaml)
- [ ] **Import the Grafana dashboard** — see [dashboards/grafana-template.json](../dashboards/grafana-template.json)
- [ ] **Create Grafana alert rules** — adapt the examples above with your tier thresholds
- [ ] **Configure notification channels** — PagerDuty / Slack / email for each severity
- [ ] **Baseline for 30 days** — collect data before committing to SLO targets
- [ ] **Review monthly** — track error budget burn and adjust targets or invest in reliability

### Adapting Thresholds for Your Tier

To adapt the Grafana alert examples to a different tier, change the threshold
values:

```
threshold = burn_rate × (1 − SLO_target)
```

| Tier | SLO Target | Budget | 14.4× Threshold | 6× Threshold | 3× Threshold |
|------|-----------|--------|-----------------|--------------|--------------|
| Tier 1 | 99.99 % | 0.0001 | 0.00144 | 0.0006 | 0.0003 |
| Tier 2 | 99.9 % | 0.001 | 0.0144 | 0.006 | 0.003 |
| Tier 3 | 99 % | 0.01 | 0.144 | 0.06 | 0.03 |

---

## Further Reading

- [Google SRE Book — Service Level Objectives](https://sre.google/sre-book/service-level-objectives/)
- [Google SRE Workbook — Alerting on SLOs](https://sre.google/workbook/alerting-on-slos/)
- [Grafana SLO documentation](https://grafana.com/docs/grafana-cloud/alerting-and-irm/slo/)
- [otel-events Performance Dashboard](../dashboards/README.md)
- [otel-events Deployment Guide](../deployment/README.md)
