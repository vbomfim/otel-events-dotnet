# ALL User Guide

**Another Logging Library** — an extension to the OpenTelemetry .NET SDK for schema-driven events, AI-optimized JSON export, and causal event linking.

---

## Chapters

| # | Chapter | Description |
|---|---------|-------------|
| 1 | [Introduction — What is ALL?](01-introduction.md) | The problem with freestyle logging, what ALL solves, and how it extends OTEL |
| 2 | [ALL vs Plain OTEL](02-all-vs-plain-otel.md) | Side-by-side code comparison showing the before and after |
| 3 | [Core Concepts](03-core-concepts.md) | Events, Schemas, JSON Envelope, Causality, Sensitivity, Environment Profiles |
| 4 | [Getting Started](04-getting-started.md) | 10-minute tutorial: install → schema → generate → emit → see output |
| 5 | [Schema Reference](05-schema-reference.md) | Complete YAML grammar: fields, types, severity, metrics, enums, sensitivity |
| 6 | [Integration Packs](06-integration-packs.md) | Pre-built packs: AspNetCore, gRPC, CosmosDB, Azure Storage, HealthChecks |
| 7 | [Configuration](07-configuration.md) | appsettings.json, environment variables, exporter options, filtering |
| 8 | [Testing](08-testing.md) | `All.Testing` package, `AllTestHost`, assertions, test patterns |
| 9 | [CLI Tool](09-cli-tool.md) | `dotnet all validate`, `generate`, `diff`, `docs` commands |
| 10 | [Security & Privacy](10-security-privacy.md) | Sensitivity classification, redaction, environment profiles, OWASP mapping |
| 11 | [Advanced Topics](11-advanced-topics.md) | Rate limiting, sampling, schema versioning |
| 12 | [Migration & FAQ](12-migration-and-faq.md) | Adopting ALL in existing projects, frequently asked questions |

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
| **Tech Lead / Architect** — evaluating ALL for the team | [Chapter 1 — Introduction](01-introduction.md) → [Chapter 2 — ALL vs Plain OTEL](02-all-vs-plain-otel.md) |
| **Platform / SRE Engineer** — cares about JSON output and deployment | [Chapter 3 — Core Concepts](03-core-concepts.md) → [Chapter 7 — Configuration](07-configuration.md) |
| **Newcomer to OTEL** — hasn't used OpenTelemetry before | [Chapter 1 — Introduction](01-introduction.md) (start from the beginning) |
