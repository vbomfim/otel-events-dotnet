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
