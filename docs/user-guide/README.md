# otel-events User Guide

An extension to the OpenTelemetry .NET SDK for schema-driven events, AI-optimized JSON export, and causal event linking.

---

## Chapters

| # | Chapter | Description |
|---|---------|-------------|
| 1 | [Introduction — What is otel-events?](01-introduction.md) | The problem with freestyle logging, what otel-events solves, and how it extends OTEL |
| 2 | [otel-events vs Plain OTEL](02-otel-events-vs-plain-otel.md) | Side-by-side code comparison showing the before and after |
| 3 | [Core Concepts](03-core-concepts.md) | Events, Schemas, JSON Envelope, Causality, Sensitivity, Environment Profiles |
| 4 | [Getting Started](04-getting-started.md) | 10-minute tutorial: install → schema → generate → emit → see output |
| 5 | [Schema Reference](05-schema-reference.md) | Complete YAML grammar: fields, types, severity, metrics, enums, sensitivity |
| 6 | [Integration Packs](06-integration-packs.md) | Pre-built packs: AspNetCore, gRPC, CosmosDB, Azure Storage |
| 7 | [Configuration](07-configuration.md) | appsettings.json, environment variables, exporter options, filtering |
| 8 | [Testing](08-testing.md) | `OtelEvents.Testing` package, `OtelEventsTestHost`, assertions, test patterns |
| 9 | [CLI Tool](09-cli-tool.md) | `dotnet otel-events validate`, `generate`, `diff`, `docs` commands |
| 10 | [Security & Privacy](10-security-privacy.md) | Sensitivity classification, redaction, environment profiles, OWASP mapping |
| 11 | [Advanced Topics](11-advanced-topics.md) | Rate limiting, sampling, schema versioning, sharing, signing, analyzers |
| 12 | [Migration Guide](12-migration-guide.md) | Step-by-step migration from plain `ILogger` to otel-events |
| 13 | [FAQ](13-faq.md) | Common questions about otel-events adoption, debugging, and performance |
| 14 | [OtelEvents.Health](14-health.md) | Event-driven health intelligence — state machine, YAML components, K8s probes |

---

## Quick Links

- [SPECIFICATION.md](../../SPECIFICATION.md) — Full project specification
- [README.md](../../README.md) — Project overview and quick start
- [Security Documentation](../security/) — Threat model, PII classification, OWASP mapping
- [Deployment Guide](../deployment/) — Container & Kubernetes deployment

---

## Who is this guide for?

| Persona | Start here |
|---------|-----------|
| **Application Developer** — wants to emit events fast | [Chapter 4 — Getting Started](04-getting-started.md) |
| **Tech Lead / Architect** — evaluating otel-events for the team | [Chapter 1 — Introduction](01-introduction.md) → [Chapter 2 — otel-events vs Plain OTEL](02-otel-events-vs-plain-otel.md) |
| **Platform / SRE Engineer** — cares about JSON output and deployment | [Chapter 3 — Core Concepts](03-core-concepts.md) → [Chapter 7 — Configuration](07-configuration.md) |
| **Migrating from plain ILogger** — already logging, want schema enforcement | [Chapter 12 — Migration Guide](12-migration-guide.md) |
| **Health/SRE** — wants event-driven health probes from real traffic | [Chapter 14 — OtelEvents.Health](14-health.md) |
| **Newcomer to OTEL** — hasn't used OpenTelemetry before | [Chapter 1 — Introduction](01-introduction.md) (start from the beginning) |
