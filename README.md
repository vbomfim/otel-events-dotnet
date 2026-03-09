# ALL — Another Logging Library

[![CI](https://github.com/all4dotnet/otel-events-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/all4dotnet/otel-events-dotnet/actions/workflows/ci.yml)

**An extension to the OpenTelemetry .NET SDK** — schema-driven code generation, AI-optimized JSON export, causal event linking, and compile-time consistency enforcement.

## Overview

ALL extends the standard OpenTelemetry pipeline with:

- **Schema-driven events** — Define events in YAML, get type-safe C# methods via code generation
- **AI-optimized JSON export** — Compact, single-line JSONL output optimized for machine investigation
- **Causal event linking** — Track cause-and-effect relationships between events via `eventId`/`parentEventId`
- **Compile-time enforcement** — Roslyn analyzers catch `Console.Write`, untyped `ILogger` usage, and schema violations

Projects already using OpenTelemetry .NET can adopt ALL incrementally — add a package, point it at a YAML schema, and get type-safe, schema-enforced events flowing through the existing OTEL pipeline.

## Packages

| Package | Description |
|---------|-------------|
| `All.Schema` | YAML parser, schema model, validation, and code generator |
| `All.Exporter.Json` | Custom OTEL `BaseExporter<LogRecord>` for AI-optimized JSONL |
| `All.Causality` | Custom OTEL `BaseProcessor<LogRecord>` for causal event linking |
| `All.Analyzers` | Roslyn analyzers for logging hygiene enforcement |
| `All.Testing` | In-memory `LogRecord` collector and assertion extensions |

## Quick Start

```bash
# Clone and build
git clone https://github.com/all4dotnet/otel-events-dotnet.git
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
│   ├── All.Schema/              # YAML schema parser & code generator
│   ├── All.Exporter.Json/       # JSON log exporter for OTEL pipeline
│   ├── All.Causality/           # Causal event linking processor
│   ├── All.Analyzers/           # Roslyn analyzers
│   └── All.Testing/             # Test utilities
├── tests/
│   ├── All.Schema.Tests/
│   ├── All.Exporter.Json.Tests/
│   └── All.Causality.Tests/
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

See the [User Guide](docs/user-guide/README.md) for comprehensive documentation:

- [Introduction — What is ALL?](docs/user-guide/01-introduction.md)
- [ALL vs Plain OTEL](docs/user-guide/02-all-vs-plain-otel.md) — side-by-side comparison
- [Core Concepts](docs/user-guide/03-core-concepts.md) — Events, Schemas, Causality, Sensitivity
- [Getting Started](docs/user-guide/04-getting-started.md) — 10-minute tutorial
- [Schema Reference](docs/user-guide/05-schema-reference.md) — complete YAML grammar
- [Integration Packs](docs/user-guide/06-integration-packs.md) — AspNetCore, gRPC, CosmosDB, Storage, HealthChecks
- [Configuration](docs/user-guide/07-configuration.md), [Testing](docs/user-guide/08-testing.md), [CLI Tool](docs/user-guide/09-cli-tool.md), [Security](docs/user-guide/10-security-privacy.md)

## Specification

See [SPECIFICATION.md](SPECIFICATION.md) for the full project specification, including architecture, YAML schema format, JSON envelope format, and design decisions.

## Deployment

See the [Container & Kubernetes Deployment Guide](docs/deployment/README.md) for:

- OTEL Collector configuration for ALL envelope parsing
- Sample Dockerfile (distroless, non-root, SBOM)
- Kubernetes manifests (Deployment, PDB, HPA, NetworkPolicy)
- Resource sizing recommendations
- TLS/mTLS configuration for OTLP endpoints

## Contributing

Contributions are welcome! Please open an issue or pull request.

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
