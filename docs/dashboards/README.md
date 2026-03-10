# Performance Dashboard Setup

Pre-built Grafana dashboard and OTEL Collector configuration for monitoring
applications instrumented with **OtelEvents** schemas.

## What's Included

| File | Description |
|------|-------------|
| `grafana-template.json` | Grafana dashboard with HTTP rate, error rate, duration percentiles, health status, and lifecycle panels |
| `otel-collector-config.yaml` | OTEL Collector config: OTLP receiver → Prometheus exporter |

## Architecture

```
┌──────────────┐     OTLP/gRPC      ┌──────────────────┐    scrape     ┌────────────┐
│  .NET App    │ ──────────────────► │  OTEL Collector   │ ◄──────────── │ Prometheus │
│  (OtelEvents)│     :4317           │  :4317 / :4318    │    :8889      │            │
└──────────────┘                     └──────────────────┘               └─────┬──────┘
                                                                              │
                                                                              ▼
                                                                       ┌────────────┐
                                                                       │  Grafana    │
                                                                       │  :3000      │
                                                                       └────────────┘
```

## Quick Start with Docker Compose

### 1. Create `docker-compose.observability.yaml`

```yaml
services:
  otel-collector:
    image: otel/opentelemetry-collector-contrib:latest
    command: ["--config=/etc/otel-collector-config.yaml"]
    volumes:
      - ./docs/dashboards/otel-collector-config.yaml:/etc/otel-collector-config.yaml
    ports:
      - "4317:4317"   # OTLP gRPC
      - "4318:4318"   # OTLP HTTP
      - "8889:8889"   # Prometheus metrics

  prometheus:
    image: prom/prometheus:latest
    volumes:
      - ./prometheus.yaml:/etc/prometheus/prometheus.yml
    ports:
      - "9090:9090"

  grafana:
    image: grafana/grafana:latest
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
    volumes:
      - ./docs/dashboards/grafana-template.json:/var/lib/grafana/dashboards/otel-events.json
      - ./grafana-dashboards.yaml:/etc/grafana/provisioning/dashboards/default.yaml
      - ./grafana-datasources.yaml:/etc/grafana/provisioning/datasources/default.yaml
    ports:
      - "3000:3000"
```

### 2. Create `prometheus.yaml`

```yaml
global:
  scrape_interval: 10s

scrape_configs:
  - job_name: "otel-collector"
    static_configs:
      - targets: ["otel-collector:8889"]
```

### 3. Create `grafana-datasources.yaml`

```yaml
apiVersion: 1
datasources:
  - name: Prometheus
    type: prometheus
    access: proxy
    url: http://prometheus:9090
    isDefault: true
```

### 4. Create `grafana-dashboards.yaml`

```yaml
apiVersion: 1
providers:
  - name: "default"
    folder: "OtelEvents"
    type: file
    options:
      path: /var/lib/grafana/dashboards
```

### 5. Start the stack

```bash
docker compose -f docker-compose.observability.yaml up -d
```

### 6. Configure your .NET app

```csharp
// In Program.cs
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("OtelEvents.AspNetCore");  // ASP.NET Core schema meter
        metrics.AddMeter("otel_events.lifecycle");           // Lifecycle schema meter
        metrics.AddOtlpExporter(o =>
        {
            o.Endpoint = new Uri("http://localhost:4317");
        });
    });
```

### 7. Open Grafana

Navigate to [http://localhost:3000](http://localhost:3000) (admin/admin) and find the
**OtelEvents — Performance Dashboard** in the dashboards folder.

## Dashboard Panels

| Panel | Metric Source | Type | PromQL Pattern |
|-------|--------------|------|----------------|
| HTTP Request Rate | `otel.http.request.received.count` | Counter → rate | `rate(otel_http_request_received_count_total[$__rate_interval])` |
| HTTP Error Rate | `otel.http.request.error.count` | Counter → rate | `rate(otel_http_request_error_count_total[$__rate_interval])` |
| Duration Percentiles | `otel.http.request.duration` | Histogram → quantile | `histogram_quantile(0.95, sum(rate(otel_http_request_duration_bucket[$__rate_interval])) by (le))` |
| Response Rate by Status | `otel.http.response.count` | Counter → rate | `rate(otel_http_response_count_total[$__rate_interval])` |
| Response Size | `otel.http.response.size` | Histogram → quantile | `histogram_quantile(0.95, ...)` |
| Health Changes | `app.health.changes` | Counter → stat | `increase(app_health_changes_total[$__range])` |
| Lifecycle Transitions | `app.lifecycle.transitions` | Counter → stat | `increase(app_lifecycle_transitions_total[$__range])` |
| Startup Duration | `app.startup.durationMs` | Histogram → quantile | `histogram_quantile(0.95, ...)` |

## Custom Dashboards from Your Schema

Use `SchemaMetricsDashboardGenerator` to generate dashboard JSON from any `.all.yaml` schema:

```csharp
using OtelEvents.Schema.CodeGen;
using OtelEvents.Schema.Parsing;

// Parse your schema
var parser = new SchemaParser();
var doc = parser.Parse(File.ReadAllText("my-service.all.yaml"));

// Generate dashboard files
var generator = new SchemaMetricsDashboardGenerator();
var files = generator.GenerateFiles(doc);

foreach (var file in files)
{
    File.WriteAllText($"output/{file.FileName}", file.Content);
    Console.WriteLine($"Generated: {file.FileName}");
}
// Output:
//   Generated: my-service-dashboard.json
//   Generated: my-service-otel-collector.yaml
```

The generator creates:
- **Counter metrics** → `rate()` timeseries panels with label grouping
- **Histogram metrics** → `histogram_quantile()` panels with p50/p95/p99 lines
- **Gauge metrics** → `stat` panels showing current value

## Metric Name Mapping

OTEL/Prometheus converts dotted metric names to underscored:

| Schema Metric | Prometheus Name |
|--------------|-----------------|
| `otel.http.request.duration` | `otel_http_request_duration` |
| `app.health.changes` | `app_health_changes` |
| `app.lifecycle.transitions` | `app_lifecycle_transitions` |

Counters get a `_total` suffix automatically by the OTEL SDK.
Histograms get `_bucket`, `_sum`, and `_count` suffixes.

## Troubleshooting

### No data in Grafana

1. **Check OTEL Collector is receiving data:**
   ```bash
   curl http://localhost:8889/metrics | grep otel_http
   ```

2. **Check Prometheus is scraping:**
   Open [http://localhost:9090/targets](http://localhost:9090/targets) — the `otel-collector` target should be `UP`.

3. **Check meter names match:**
   The meter name in your app (`AddMeter("...")`) must match the `meterName` in your schema YAML.

### Metrics exist but panels are empty

- Verify the Prometheus datasource variable is selected in the dashboard dropdown.
- Check the time range — default is `now-1h`. Widen it if your app just started.

### Custom schemas not showing metrics

Run `SchemaMetricsDashboardGenerator` to generate a dashboard that matches your schema's exact metric names. The provided template only covers the built-in ASP.NET Core and Lifecycle schemas.
