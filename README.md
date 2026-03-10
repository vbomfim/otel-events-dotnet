<p align="center">
  <img src="assets/logo.svg" alt="otel-events" width="720"/>
</p>

<p align="center">
  <a href="https://github.com/vbomfim/otel-events-dotnet/actions/workflows/ci.yml"><img src="https://github.com/vbomfim/otel-events-dotnet/actions/workflows/ci.yml/badge.svg" alt="CI"/></a>
</p>

**Schema-driven event logging for OpenTelemetry .NET** — define events in YAML, get type-safe C# code with metrics, causal linking, and AI-optimized JSON export.

## Overview

otel-events extends the standard OpenTelemetry pipeline with:

- **Schema-driven events** — Define events in YAML, get type-safe C# methods via code generation
- **AI-optimized JSON export** — Compact, single-line JSONL output optimized for machine investigation
- **Causal event linking** — Track cause-and-effect relationships between events via `eventId`/`parentEventId`
- **Compile-time enforcement** — Roslyn analyzers catch `Console.Write`, untyped `ILogger` usage, and schema violations

Projects already using OpenTelemetry .NET can adopt otel-events incrementally — add a package, point it at a YAML schema, and get type-safe, schema-enforced events flowing through the existing OTEL pipeline.

## Packages

| Package | Description |
|---------|-------------|
| `OtelEvents.Schema` | YAML parser, schema model, validation, and code generator |
| `OtelEvents.Exporter.Json` | Custom OTEL `BaseExporter<LogRecord>` for AI-optimized JSONL |
| `OtelEvents.Causality` | Custom OTEL `BaseProcessor<LogRecord>` for causal event linking |
| `OtelEvents.Analyzers` | Roslyn analyzers for logging hygiene enforcement |
| `OtelEvents.Testing` | In-memory `LogRecord` collector and assertion extensions |

## Quick Start

```bash
# Clone and build
git clone https://github.com/vbomfim/otel-events-dotnet.git
cd otel-events-dotnet
dotnet build
dotnet test
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later

## Building

```bash
dotnet restore
dotnet build -c Release
```

## Running Tests

```bash
dotnet test -c Release --verbosity minimal
```

## Project Structure

```
otel-events-dotnet/
├── src/
│   ├── OtelEvents.Schema/              # YAML schema parser & code generator
│   ├── OtelEvents.Exporter.Json/       # JSON log exporter for OTEL pipeline
│   ├── OtelEvents.Causality/           # Causal event linking processor
│   ├── OtelEvents.Analyzers/           # Roslyn analyzers
│   └── OtelEvents.Testing/             # Test utilities
├── tests/
│   ├── OtelEvents.Schema.Tests/
│   ├── OtelEvents.Exporter.Json.Tests/
│   └── OtelEvents.Causality.Tests/
├── Directory.Build.props        # Shared MSBuild properties
├── Directory.Packages.props     # Central package management
└── OtelEvents.slnx              # Solution file
```

## Security & Privacy

See [docs/security/](docs/security/) for standalone security documentation, including:

- [Threat Model](docs/security/threat-model.md) — Trust boundaries, threat vectors, and mitigations
- [PII Classification](docs/security/pii-classification.md) — Sensitivity levels and redaction matrix
- [Environment Profiles](docs/security/environment-profiles.md) — Development, Staging, and Production defaults
- [OWASP Mapping](docs/security/owasp-mapping.md) — OWASP Top 10 (2021) reference mapping

For vulnerability reporting, see [SECURITY.md](SECURITY.md).

## User Guide

See the [otel-events User Guide](docs/user-guide/README.md) for comprehensive documentation:

- [Getting Started](docs/user-guide/04-getting-started.md) — 10-minute tutorial
- [Schema Reference](docs/user-guide/05-schema-reference.md) — Complete YAML grammar
- [Integration Packs](docs/user-guide/06-integration-packs.md) — ASP.NET Core, gRPC, CosmosDB, Azure Storage, HealthChecks
- [Configuration](docs/user-guide/07-configuration.md) — All configuration options
- [Migration Guide](docs/user-guide/12-migration-guide.md) — Migrate from plain `ILogger` to otel-events
- [FAQ](docs/user-guide/13-faq.md) — Common questions

## Specification

See [SPECIFICATION.md](SPECIFICATION.md) for the full project specification, including architecture, YAML schema format, JSON envelope format, and design decisions.

## Deployment

See the [Container & Kubernetes Deployment Guide](docs/deployment/README.md) for:

- OTEL Collector configuration for otel-events envelope parsing
- Sample Dockerfile (distroless, non-root, SBOM)
- Kubernetes manifests (Deployment, PDB, HPA, NetworkPolicy)
- Resource sizing recommendations
- TLS/mTLS configuration for OTLP endpoints

## Contributing

Contributions are welcome! Please open an issue or pull request.

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
