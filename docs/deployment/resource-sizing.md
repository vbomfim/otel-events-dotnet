# Resource Sizing Recommendations

Resource sizing guide for .NET services using ALL (Another Logging Library).
Use these recommendations as a starting point and adjust based on load testing.

> **Source:** [SPECIFICATION.md §17.5](../../SPECIFICATION.md)

## Sizing Table

| Throughput (events/sec) | CPU Request | CPU Limit | Memory Request | Memory Limit | Notes |
|------------------------|-------------|-----------|----------------|--------------|-------|
| < 1,000 | 50m | 200m | 64 Mi | 256 Mi | Small service, low event volume |
| 1,000–10,000 | 100m | 500m | 128 Mi | 512 Mi | Typical microservice |
| 10,000–50,000 | 250m | 1000m | 256 Mi | 1 Gi | High-throughput service |
| 50,000–100,000 | 500m | 2000m | 512 Mi | 2 Gi | Event-heavy service; monitor GC pressure |
| > 100,000 | 1000m+ | 4000m+ | 1 Gi+ | 4 Gi+ | Benchmark-specific; consider event sampling |

## Performance Characteristics

- **ALL overhead:** ~500ns per event (log + metrics). At 100K events/s, ALL consumes
  ~50ms of CPU per second.
- **Memory overhead:** Dominated by OTEL SDK batching buffers, not ALL components.
- **Allocation rate at 100K events/s:** ~24.4 MB/s (256 bytes/event). Monitor Gen2 GC
  collections — target < 3/min.
- **Buffer pooling:** The `Utf8JsonWriter` uses `ArrayPool<byte>.Shared` for buffer
  pooling. Pool size scales with throughput.

## Monitoring Recommendations

When running at high throughput (> 10K events/s), monitor these metrics:

| Metric | Target | Action if Exceeded |
|--------|--------|--------------------|
| Gen2 GC collections | < 3/min | Increase memory limits, review allocation patterns |
| CPU utilization | < 70% average | Scale horizontally (HPA) or increase CPU limits |
| Memory utilization | < 80% average | Increase memory limits |
| OTEL export failures | 0 | Check Collector connectivity, review network policies |
| Event drop rate | 0 | Increase batch size or export frequency |

## Applying to Kubernetes Manifests

Update the `resources` section in [k8s/deployment.yaml](k8s/deployment.yaml) to match
your throughput tier:

```yaml
resources:
  requests:
    cpu: 100m      # Adjust per sizing table
    memory: 128Mi  # Adjust per sizing table
  limits:
    cpu: 500m      # Adjust per sizing table
    memory: 512Mi  # Adjust per sizing table
```

Also adjust the HPA thresholds in [k8s/hpa.yaml](k8s/hpa.yaml) based on your
target utilization.
