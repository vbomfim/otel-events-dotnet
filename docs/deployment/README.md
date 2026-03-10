# Container & Kubernetes Deployment Guide

This guide covers deploying .NET services that use [otel-events](../../README.md)
in containerized and Kubernetes environments. It includes OTEL Collector configuration,
sample Kubernetes manifests, resource sizing, TLS setup, and network policies.

> **Note:** otel-events publishes documentation and sample manifests only — not a reference
> Helm chart. Helm charts are highly organization-specific (naming conventions, label
> standards, ingress controllers). The sample manifests here serve as a starting point.
> See [SPECIFICATION.md §17.7, Decision OQ-PG-04](../../SPECIFICATION.md).

## Contents

| Document | Description |
|----------|-------------|
| [otel-collector-config.yaml](otel-collector-config.yaml) | OTEL Collector configuration with `filelog` receiver for otel-events envelope |
| [dockerfile](dockerfile) | Multi-stage Dockerfile (distroless, non-root, SBOM) |
| [k8s/deployment.yaml](k8s/deployment.yaml) | Kubernetes Deployment manifest |
| [k8s/pdb.yaml](k8s/pdb.yaml) | PodDisruptionBudget |
| [k8s/hpa.yaml](k8s/hpa.yaml) | HorizontalPodAutoscaler |
| [k8s/networkpolicy.yaml](k8s/networkpolicy.yaml) | Network policy for OTLP egress |
| [resource-sizing.md](resource-sizing.md) | Resource sizing recommendations by throughput |
| [tls-configuration.md](tls-configuration.md) | TLS and mTLS setup for OTLP endpoints |

## Architecture Overview

The recommended OTEL Collector topology is a **DaemonSet** on each Kubernetes node
for log collection (via `filelog` receiver reading container stdout), combined with
a **Gateway** Collector deployment for OTLP metrics and traces.

```
┌─────────────────────────────────────────────────────────┐
│  Kubernetes Pod                                          │
│                                                          │
│  ┌─────────────────────┐  stdout  ┌──────────────────┐  │
│  │ .NET Application     │─────────▶│ Container Runtime │  │
│  │ (OtelEventsJsonExporter     │  JSONL   │ (writes to        │  │
│  │  → stdout)           │          │  /var/log/pods/)   │  │
│  └─────────────────────┘          └────────┬─────────┘  │
│                                             │            │
└─────────────────────────────────────────────│────────────┘
                                              │
                      ┌───────────────────────▼────────────────┐
                      │  OTEL Collector DaemonSet               │
                      │  (one per node)                         │
                      │                                         │
                      │  filelog receiver                        │
                      │  → reads /var/log/pods/**/*.log          │
                      │  → parses JSONL (otel-events envelope format)   │
                      │                                         │
                      │  Exporters:                              │
                      │  → OTLP (to central Collector/backend)  │
                      │  → Loki (for log storage)               │
                      │  → Elasticsearch (alternative)          │
                      └───────────────────────────────────────┘
```

## Deployment Patterns

| Pattern | Logs | Metrics | Traces | When to Use |
|---------|------|---------|--------|-------------|
| **DaemonSet (filelog)** | ✅ stdout → filelog | ❌ | ❌ | Log collection from all pods on a node |
| **Gateway (OTLP)** | Optional | ✅ | ✅ | Central aggregation of metrics/traces via OTLP |
| **Sidecar** | ✅ | ✅ | ✅ | Per-pod isolation (higher resource cost) |
| **DaemonSet + Gateway** | ✅ (DaemonSet) | ✅ (Gateway) | ✅ (Gateway) | **Recommended** — best balance of resource efficiency and reliability |

## Quick Start

### 1. Build the Container Image

```bash
docker build -f docs/deployment/dockerfile -t my-service:v1.0.0 .
```

### 2. Deploy the OTEL Collector

```bash
# Create the ConfigMap from the provided config
kubectl create configmap otel-collector-config \
    --from-file=config.yaml=docs/deployment/otel-collector-config.yaml \
    -n monitoring

# Deploy the Collector DaemonSet (use your organization's Collector manifests)
```

### 3. Deploy to Kubernetes

```bash
# Create namespace (if needed)
kubectl create namespace production

# Create TLS secrets (see tls-configuration.md)
kubectl create secret tls otel-tls-certs \
    --cert=certs/tls.crt \
    --key=certs/tls.key \
    -n production

# Apply manifests
kubectl apply -f docs/deployment/k8s/
```

### 4. Verify

```bash
# Check pods
kubectl get pods -n production -l app=my-service

# Check logs (should see otel-events JSONL output)
kubectl logs -n production -l app=my-service --tail=10

# Check OTEL Collector is receiving logs
kubectl logs -n monitoring -l app=otel-collector --tail=10
```

## Further Reading

- [SPECIFICATION.md §17](../../SPECIFICATION.md) — Full specification for container and Kubernetes deployment
- [Observability Dashboards](../dashboards/README.md) — Grafana dashboards for otel-events metrics
- [OpenTelemetry Collector Documentation](https://opentelemetry.io/docs/collector/)
