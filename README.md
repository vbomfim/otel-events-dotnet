![otel-events](https://raw.githubusercontent.com/vbomfim/otel-events-dotnet/main/assets/logo.svg)

[![CI](https://github.com/vbomfim/otel-events-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/vbomfim/otel-events-dotnet/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/OtelEvents.Schema?label=NuGet&color=blue)](https://www.nuget.org/packages?q=OtelEvents)

**Schema-driven event logging for OpenTelemetry .NET** — define events in YAML, get type-safe C# code with metrics, causal linking, and AI-optimized JSON export.

## Overview

otel-events extends the standard OpenTelemetry pipeline with:

- **Schema-driven events** — Define events in YAML, get type-safe C# methods via code generation
- **AI-optimized JSON export** — Compact, single-line JSONL output optimized for machine investigation
- **Causal event linking** — Track cause-and-effect relationships between events via `eventId`/`parentEventId`
- **Compile-time enforcement** — Roslyn analyzers catch `Console.Write`, untyped `ILogger` usage, and schema violations
- **Integration packs** — Zero-code instrumentation for ASP.NET Core, gRPC, Azure CosmosDB, Azure Storage, and Health Checks
- **Event subscriptions** — In-process event bus for reactive handlers (circuit breakers, token refresh, alerting)

## Installation

Install from [NuGet](https://www.nuget.org/packages?q=OtelEvents):

```bash
# Core — schema-driven custom events
dotnet add package OtelEvents.Schema           # YAML parser + C# code generator
dotnet add package OtelEvents.Exporter.Json    # AI-optimized JSONL exporter
dotnet add package OtelEvents.Causality        # Causal event linking (eventId/parentEventId)

# Integration packs — zero-code instrumentation (pick what you use)
dotnet add package OtelEvents.AspNetCore       # HTTP request/auth/throttle events
dotnet add package OtelEvents.Grpc             # gRPC call/auth/throttle events
dotnet add package OtelEvents.Azure.CosmosDb   # CosmosDB query/auth/throttle events
dotnet add package OtelEvents.Azure.Storage    # Blob/Queue operation events
dotnet add package OtelEvents.HealthChecks     # Health check execution + state change events

# Optional
dotnet add package OtelEvents.Analyzers        # Roslyn analyzers for logging hygiene
dotnet add package OtelEvents.Testing          # In-memory exporter + assertions for tests
dotnet add package OtelEvents.Subscriptions    # In-process event bus for reactive handlers
```

## Packages

### Core Packages

| Package | What it does | When to use |
|---------|-------------|-------------|
| **OtelEvents.Schema** | Parses `.otel.yaml` schema files and generates type-safe C# code (`[LoggerMessage]` methods + `Meter`/`Counter`/`Histogram` instruments) | You want to define custom business events (e.g., `OrderPlaced`, `PaymentProcessed`) |
| **OtelEvents.Exporter.Json** | OTEL `BaseExporter<LogRecord>` that writes compact, single-line JSONL to stdout — optimized for AI/ML log analysis | You want structured JSON output instead of plain-text logs |
| **OtelEvents.Causality** | OTEL `BaseProcessor<LogRecord>` that auto-generates UUID v7 `eventId` and links events via `parentEventId` using `AsyncLocal` scopes | You want to trace cause-and-effect between events (e.g., all events from one HTTP request) |

### Integration Packs — Zero-Code Instrumentation

These packages auto-emit structured events by hooking into framework pipelines. **No YAML schemas needed** — just install, register in DI, and events flow automatically:

| Package | Events emitted | What it instruments |
|---------|---------------|-------------------|
| **OtelEvents.AspNetCore** | `http.request.received/completed/failed` + `http.connection.failed` + `http.auth.failed` + `http.throttled` | ASP.NET Core middleware — every HTTP request |
| **OtelEvents.Grpc** | `grpc.call.started/completed/failed` + connection/auth/throttle events | gRPC server + client interceptors |
| **OtelEvents.Azure.CosmosDb** | `cosmosdb.query.executed/failed` + `cosmosdb.point.read/write` + connection/auth/throttle events | Azure CosmosDB SDK via DiagnosticListener |
| **OtelEvents.Azure.Storage** | `storage.blob.uploaded/downloaded/deleted` + `storage.queue.sent/received` + connection/auth/throttle events | Azure Storage SDK via HttpPipelinePolicy |
| **OtelEvents.HealthChecks** | `health.check.executed` + `health.state.changed` | ASP.NET Core `IHealthCheckPublisher` |

### Developer Tools

| Package | What it does |
|---------|-------------|
| **OtelEvents.Analyzers** | Roslyn analyzers: warns on `Console.Write`, direct `ILogger.Log*`, string interpolation, reserved prefix usage, PII without redaction |
| **OtelEvents.Testing** | `InMemoryLogExporter`, `OtelEventsTestHost.Create()`, `LogAssertions` — test your events without infrastructure |
| **OtelEvents.Subscriptions** | In-process event bus — subscribe to events with lambda or DI handlers (e.g., trip circuit breaker on `cosmosdb.throttled`) |
| **OtelEvents.Cli** | CLI tool (`dotnet otel-events validate/generate/diff/docs`) for schema validation, code generation, version comparison |

## Quick Start

### Option 1: Integration Packs Only (zero YAML)

```csharp
// Program.cs — get structured events for free
builder.Services.AddOpenTelemetry()
    .WithLogging(logging =>
    {
        logging.AddOtelEventsJsonExporter();                    // JSONL to stdout
        logging.AddProcessor<OtelEventsCausalityProcessor>();   // Causal linking
    });

builder.Services.AddOtelEventsAspNetCore();   // Auto-emit HTTP events
builder.Services.AddOtelEventsCosmosDb();     // Auto-emit CosmosDB events
builder.Services.AddOtelEventsHealthChecks(); // Auto-emit health check events
```

Every HTTP request, CosmosDB query, and health check now emits structured events — including connection failures, auth failures, and throttling. No custom code needed.

### Option 2: Custom Events via YAML Schema

1. Define your events in `orders.otel.yaml`:

```yaml
schema:
  name: Orders
  version: "1.0.0"
  namespace: MyApp.Events
  meterName: myapp.events
  prefix: ORDER              # Event codes become ORDER-1000, ORDER-2000, etc.

events:
  OrderPlaced:
    id: 1000
    type: start              # Creates a transaction scope
    severity: INFO
    message: "Order {orderId} placed by {customerId} for ${amount}"
    fields:
      - orderId
      - customerId: { sensitivity: pii }
      - amount
    metrics:
      order.placed.count:
        type: counter
        labels: [customerId]

  OrderFailed:
    id: 2000
    type: failure             # Closes parent scope as failed
    parent: OrderPlaced
    severity: ERROR
    message: "Order {orderId} failed: {reason}"
    exception: true
    fields:
      - orderId
      - reason
```

2. Generate code and use it:

```bash
dotnet otel-events generate orders.otel.yaml -o Generated/
```

```csharp
// Type-safe, schema-enforced — log + metrics in one call
using var scope = logger.BeginOrderPlaced(orderId, customerId, amount);
// ... do work ...
scope.TryComplete(logger, orderId, carrier, trackingNumber);
// or on failure:
scope.TryFail(logger, orderId, reason, exception);
```

📖 See the full [User Guide](https://github.com/vbomfim/otel-events-dotnet/blob/main/docs/user-guide/README.md) for detailed tutorials.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download) (builds both net8.0 and net10.0 targets)

### Supported Target Frameworks

| Package | Targets |
|---------|---------|
| Library packages (`OtelEvents.*`) | `net8.0` |
| `OtelEvents.Analyzers` | `netstandard2.0` (Roslyn requirement) |
| `OtelEvents.Cli` | `net8.0` |
| Test projects | `net8.0` |

## Project Structure

```
otel-events-dotnet/
├── src/
│   ├── OtelEvents.Schema/              # YAML schema parser & code generator
│   ├── OtelEvents.Exporter.Json/       # JSON log exporter + severity filter + rate limiter + sampler
│   ├── OtelEvents.Causality/           # Causal event linking processor
│   ├── OtelEvents.Analyzers/           # Roslyn analyzers
│   ├── OtelEvents.Testing/             # Test utilities
│   ├── OtelEvents.Subscriptions/       # In-process event bus
│   ├── OtelEvents.AspNetCore/          # ASP.NET Core integration pack
│   ├── OtelEvents.Grpc/                # gRPC integration pack
│   ├── OtelEvents.Azure.CosmosDb/      # Azure CosmosDB integration pack
│   ├── OtelEvents.Azure.Storage/       # Azure Storage integration pack
│   └── OtelEvents.HealthChecks/        # Health checks integration pack
├── tools/
│   └── OtelEvents.Cli/                 # CLI tool (validate, generate, diff, docs)
├── docs/
│   ├── user-guide/                     # 12-chapter user guide
│   ├── security/                       # Threat model, PII classification, OWASP mapping
│   ├── observability/                  # SLI/SLO recommendations, alerting guides
│   └── deployment/                     # K8s manifests, OTEL Collector config, Dockerfile
├── Directory.Build.props               # Shared MSBuild properties
├── Directory.Packages.props            # Central package management
└── OtelEvents.slnx                     # Solution file
```

## Security & Privacy

See [docs/security/](https://github.com/vbomfim/otel-events-dotnet/tree/main/docs/security) for standalone security documentation, including:

- [Threat Model](https://github.com/vbomfim/otel-events-dotnet/blob/main/docs/security/threat-model.md) — Trust boundaries, threat vectors, and mitigations
- [PII Classification](https://github.com/vbomfim/otel-events-dotnet/blob/main/docs/security/pii-classification.md) — Sensitivity levels and redaction matrix
- [Environment Profiles](https://github.com/vbomfim/otel-events-dotnet/blob/main/docs/security/environment-profiles.md) — Development, Staging, and Production defaults
- [OWASP Mapping](https://github.com/vbomfim/otel-events-dotnet/blob/main/docs/security/owasp-mapping.md) — OWASP Top 10 (2021) reference mapping

For vulnerability reporting, see [SECURITY.md](https://github.com/vbomfim/otel-events-dotnet/blob/main/SECURITY.md).

## Observability

See [docs/observability/](https://github.com/vbomfim/otel-events-dotnet/tree/main/docs/observability) for operational guidance:

- [SLI/SLO Recommendations](https://github.com/vbomfim/otel-events-dotnet/blob/main/docs/observability/sli-slo.md) — Service Level Indicators mapped to otel-events metrics, SLO targets by service tier, multi-window burn-rate alerting, and Grafana alert examples
- [Performance Dashboards](https://github.com/vbomfim/otel-events-dotnet/blob/main/docs/dashboards/README.md) — Pre-built Grafana dashboard and OTEL Collector configuration

## User Guide

See the [otel-events User Guide](https://github.com/vbomfim/otel-events-dotnet/blob/main/docs/user-guide/README.md) for comprehensive documentation:

- [Getting Started](https://github.com/vbomfim/otel-events-dotnet/blob/main/docs/user-guide/04-getting-started.md) — 10-minute tutorial
- [Schema Reference](https://github.com/vbomfim/otel-events-dotnet/blob/main/docs/user-guide/05-schema-reference.md) — Complete YAML grammar
- [Integration Packs](https://github.com/vbomfim/otel-events-dotnet/blob/main/docs/user-guide/06-integration-packs.md) — ASP.NET Core, gRPC, CosmosDB, Azure Storage, HealthChecks
- [Configuration](https://github.com/vbomfim/otel-events-dotnet/blob/main/docs/user-guide/07-configuration.md) — All configuration options
- [Migration Guide](https://github.com/vbomfim/otel-events-dotnet/blob/main/docs/user-guide/12-migration-and-faq.md) — Migrate from plain `ILogger` to otel-events
- [FAQ](https://github.com/vbomfim/otel-events-dotnet/blob/main/docs/user-guide/12-migration-and-faq.md) — Common questions

## Specification

See [SPECIFICATION.md](https://github.com/vbomfim/otel-events-dotnet/blob/main/SPECIFICATION.md) for the full project specification, including architecture, YAML schema format, JSON envelope format, and design decisions.

## Deployment

See the [Container & Kubernetes Deployment Guide](https://github.com/vbomfim/otel-events-dotnet/blob/main/docs/deployment/README.md) for:

- OTEL Collector configuration for otel-events envelope parsing
- Sample Dockerfile (distroless, non-root, SBOM)
- Kubernetes manifests (Deployment, PDB, HPA, NetworkPolicy)
- Resource sizing recommendations
- TLS/mTLS configuration for OTLP endpoints

## Contributing

Contributions are welcome! Please open an issue or pull request.

## License

This project is licensed under the MIT License — see the [LICENSE](https://github.com/vbomfim/otel-events-dotnet/blob/main/LICENSE) file for details.
