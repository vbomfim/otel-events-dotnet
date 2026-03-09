# TLS Configuration for OTLP Endpoints

Guide for configuring TLS and mTLS between .NET services using ALL and the
OTEL Collector.

> **Source:** [SPECIFICATION.md §17.2](../../SPECIFICATION.md)

## Overview

When using `AddOtlpExporter()` for direct OTLP export (without filelog), TLS
secures the connection between your application and the OTEL Collector. In
Kubernetes, TLS certificates are typically managed via Secrets and mounted
into pods.

## Application Configuration

```csharp
// Program.cs — OTLP with TLS
builder.Services.AddOpenTelemetry()
    .WithLogging(logging =>
    {
        logging.AddOtlpExporter(otlp =>
        {
            otlp.Endpoint = new Uri("https://otel-collector.monitoring.svc.cluster.local:4317");
            otlp.Protocol = OtlpExportProtocol.Grpc;
            // TLS is configured via environment variables (preferred in K8s):
            // OTEL_EXPORTER_OTLP_CERTIFICATE=/etc/otel/certs/ca.crt
            // OTEL_EXPORTER_OTLP_CLIENT_CERTIFICATE=/etc/otel/certs/tls.crt
            // OTEL_EXPORTER_OTLP_CLIENT_KEY=/etc/otel/certs/tls.key
        });
    });
```

## Environment Variables

Configure TLS via environment variables in your Kubernetes Deployment manifest.
These are set in [k8s/deployment.yaml](k8s/deployment.yaml) and read by the
OpenTelemetry .NET SDK automatically.

| Variable | Description |
|----------|-------------|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Collector endpoint (e.g., `https://collector:4317`) |
| `OTEL_EXPORTER_OTLP_CERTIFICATE` | Path to CA certificate for verifying Collector |
| `OTEL_EXPORTER_OTLP_CLIENT_CERTIFICATE` | Path to client certificate for mTLS |
| `OTEL_EXPORTER_OTLP_CLIENT_KEY` | Path to client private key for mTLS |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | `grpc` or `http/protobuf` |

## Kubernetes Secret Setup

### 1. Create TLS Secret

```bash
# From certificate files
kubectl create secret tls otel-tls-certs \
    --cert=certs/tls.crt \
    --key=certs/tls.key \
    -n production
```

### 2. Create CA Certificate ConfigMap (for mTLS)

```bash
# CA certificate for verifying the Collector's identity
kubectl create configmap otel-ca-cert \
    --from-file=ca.crt=certs/ca.crt \
    -n production
```

### 3. Mount in Deployment

The [k8s/deployment.yaml](k8s/deployment.yaml) already includes the volume mount:

```yaml
volumeMounts:
  - name: otel-certs
    mountPath: /etc/otel/certs
    readOnly: true
volumes:
  - name: otel-certs
    secret:
      secretName: otel-tls-certs
```

## OTEL Collector TLS Configuration

The Collector side TLS is configured in [otel-collector-config.yaml](otel-collector-config.yaml):

```yaml
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317
        tls:
          cert_file: /etc/otel/certs/tls.crt
          key_file: /etc/otel/certs/tls.key
```

## Certificate Rotation

For production environments, use a certificate manager (e.g., cert-manager)
to automate certificate rotation:

```bash
# Install cert-manager (if not already installed)
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/latest/download/cert-manager.yaml
```

Configure an Issuer and Certificate resource to automatically rotate TLS
certificates mounted by your pods.

## Verification

```bash
# Verify the secret exists and contains expected keys
kubectl get secret otel-tls-certs -n production -o jsonpath='{.data}' | jq 'keys'

# Verify the certificate is valid
kubectl exec -n production deploy/my-service -- \
    openssl x509 -in /etc/otel/certs/tls.crt -text -noout 2>/dev/null \
    | grep -E "(Subject|Issuer|Not After)"

# Test gRPC connectivity to the Collector
kubectl exec -n production deploy/my-service -- \
    wget --spider --timeout=5 https://otel-collector.monitoring.svc.cluster.local:4317 2>&1 \
    || echo "Connection test complete (failure expected for gRPC health check via wget)"
```
