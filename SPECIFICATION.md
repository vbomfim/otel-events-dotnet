# otel-events — Another Logging Library

## Project Specification v2.0

### OTEL Extension Architecture

---

## Table of Contents

1. [Project Overview & Vision](#1-project-overview--vision)
2. [Problem Statement & Target Audience](#2-problem-statement--target-audience)
3. [Core Features by Priority](#3-core-features-by-priority)
4. [Technical Architecture](#4-technical-architecture)
5. [NuGet Package Structure](#5-nuget-package-structure)
6. [YAML Schema Specification](#6-yaml-schema-specification)
7. [Generated Code Examples](#7-generated-code-examples)
8. [JSON Envelope Specification](#8-json-envelope-specification)
9. [Causality Processor](#9-causality-processor)
10. [Roslyn Analyzer Rules](#10-roslyn-analyzer-rules)
11. [Testing Strategy](#11-testing-strategy)
12. [Non-Functional Requirements](#12-non-functional-requirements)
13. [Success Metrics](#13-success-metrics)
14. [Out of Scope & Explicit Non-Goals](#14-out-of-scope--explicit-non-goals)
15. [Integration Packs](#15-integration-packs)
16. [Security & Privacy Requirements](#16-security--privacy-requirements)
17. [Container & Kubernetes Deployment Guide](#17-container--kubernetes-deployment-guide)

---

## 1. Project Overview & Vision

### What is otel-events?

**otel-events (Another Logging Library)** is an **extension to the OpenTelemetry .NET SDK**. It is not a standalone observability library, not a replacement for OTEL, and not a wrapper around it. It is a set of packages that **extend** the standard OpenTelemetry pipeline with schema-driven code generation, AI-optimized JSON export, causal event linking, and compile-time consistency enforcement.

Projects already using the OpenTelemetry .NET SDK can **adopt otel-events incrementally** — add a package, point it at a YAML schema, and get type-safe, schema-enforced events flowing through their existing OTEL pipeline. No migration. No replacement. No parallel infrastructure.

### Core Thesis

> Every observable thing that happens in a system is an **event**. Events are **defined in YAML schemas**, not improvised as string templates. A code generator turns schemas into **type-safe C# methods** that emit native OpenTelemetry `LogRecord`s and `Meter` recordings. The output is **structured, predictable, and machine-readable** — optimized for both human debugging and AI-powered investigation.

### What Changed (v1 → v2)

| v1 (Standalone) | v2 (OTEL Extension) |
|-----------------|---------------------|
| Custom `EventData` struct | OTEL `LogRecord` (via `[LoggerMessage]` source generator) |
| Custom `IOtelEventsSink` pipeline | OTEL's native processor/exporter pipeline |
| Custom `IOtelEvents` interface | Extension methods on `ILogger<T>` |
| JSON sink (parallel to OTEL) | JSON exporter (plugs INTO OTEL pipeline) |
| Custom OTEL mapping sink | No mapping needed — events ARE OTEL-native |
| Custom ILogger bridge | Not needed — OTEL already bridges `ILogger` → `LogRecord` |
| `builder.Services.AddAll()` | Extends `builder.Services.AddOpenTelemetry()` |

### Design Philosophy

| Principle | Implementation |
|-----------|---------------|
| **OTEL-native** | Generated code creates `LogRecord`s and `Meter` recordings directly — no intermediate types |
| **Schema-first** | Events defined in YAML → C# generated → compile-time enforced |
| **Type-safe** | No `string.Format`, no anonymous objects — typed methods with IntelliSense |
| **Incremental adoption** | Teams already on OTEL add otel-events packages and get value immediately |
| **Consistent** | Every JSON output line is the same AI-optimized envelope — no formatting surprises |
| **AI-optimized** | Predictable structure, causal trees via `eventId`/`parentEventId`, structured exceptions |
| **Zero-friction** | Works with `dotnet add package`, extends existing OTEL setup, uses YAML files |
| **AOT-ready** | `[LoggerMessage]` source generator, System.Text.Json source generators, no reflection, trimmer-safe |

---

## 2. Problem Statement & Target Audience

### Problems otel-events Solves

1. **Inconsistent logging** — Different developers write different `[LoggerMessage]` definitions for the same events. Messages drift between services. There is no central schema governing what events exist and what fields they carry.

2. **Boilerplate proliferation** — Using `[LoggerMessage]` correctly requires writing partial methods, creating `Meter`/`Counter`/`Histogram` instances, wiring up `TagList`s, and keeping metrics and logs in sync. This is repetitive, error-prone, and tedious.

3. **Missing correlation** — OTEL's `Activity` handles distributed traces, but causal relationships between events within a service (this error happened BECAUSE of that timeout) are invisible. No standard mechanism links a `LogRecord` to its causal parent.

4. **No AI-optimized output** — OTEL's default exporters produce output for backends (OTLP, Jaeger, etc.), but local container output (stdout) is either unstructured text or verbose JSON. There is no exporter that produces compact, single-line, AI-investigation-friendly JSONL.

5. **Logging hygiene decay** — Teams start with good practices, then drift. Someone adds `Console.WriteLine` for debugging and it stays. Third-party libraries use `ILogger` with uncontrolled formats. No compile-time enforcement exists.

### Target Audience

| Persona | Description | What They Need |
|---------|-------------|----------------|
| **Application Developer** | .NET developer building services, already using OTEL | Type-safe API, IntelliSense, zero boilerplate, drop-in to existing setup |
| **Platform/SRE Engineer** | Operates services in production | Predictable JSON output, correlation IDs, consistent metrics across services |
| **Tech Lead / Architect** | Sets standards across teams | Schema governance, analyzer enforcement, consistency guarantees |
| **AI/ML Engineer** | Builds automated log investigation | Predictable structure, causal trees, bounded cardinality, no free-text |

### Scale

- Target: .NET teams with 2–200 developers, already using or willing to adopt OpenTelemetry
- Services: 1 to hundreds of microservices
- Events per service: 10–500 defined events
- Throughput target: 100,000+ events/second per process (limited by OTEL SDK pipeline, not by otel-events)

---

## 3. Core Features by Priority

### Phase 1 — MVP (Foundation)

The minimum viable product that proves the concept and is usable in a real project that already has OTEL configured.

| # | Feature | Description |
|---|---------|-------------|
| 1.1 | **Event schema parser** | Parse YAML schema files into an in-memory model (`EventDefinition`, `FieldDefinition`, etc.) with safe loading: max file size 1 MB, max 500 events, max 50 fields per event |
| 1.2 | **C# code generator** | MSBuild-integrated source generator that produces `[LoggerMessage]` partial methods + `Meter`/`Counter`/`Histogram` instances + typed extension methods from YAML schemas |
| 1.3 | **JSON log exporter** | Custom OTEL `BaseExporter<LogRecord>` that formats `LogRecord`s as AI-optimized single-line JSONL (the otel-events envelope) to stdout or file. Includes environment-aware defaults (see §16). |
| 1.4 | **Causality processor** | Custom OTEL `BaseProcessor<LogRecord>` that adds `otel_events.event_id` and `otel_events.parent_event_id` attributes to `LogRecord`s for causal tree construction |
| 1.5 | **Exception serialization** | Structured exception → `exception` object in JSON exporter with configurable `ExceptionDetailLevel` (`Full`, `TypeAndMessage`, `TypeOnly`), depth cap at 5 |
| 1.6 | **DI integration** | Extension methods on `OpenTelemetryBuilder` to register otel-events components: `.AddOtelEventsJsonExporter()`, `.AddProcessor<OtelEventsCausalityProcessor>()`, `.AddMeter("MyApp.Events.*")` |
| 1.7 | **Basic schema validation** | Validate YAML schemas at build time — duplicate IDs, missing fields, type mismatches, PII sensitivity annotations |
| 1.8 | **Basic severity-based filtering** | Configurable per-severity log filtering as an OTEL processor, allowing events below a threshold to be dropped before export (moved from Phase 3 per Platform Guardian review) |


### Phase 2 — Production Readiness

Features required for production use and team adoption.

| # | Feature | Description |
|---|---------|-------------|
| 2.1 | **Roslyn analyzers** | Detect `Console.Write*`, direct untyped `ILogger` usage, string interpolation in event messages, PII sensitivity warnings (OTEL009) |
| 2.2 | **Configuration** | `appsettings.json` / env-var configuration for JSON exporter output target, severity filtering, metric batching, `EnvironmentProfile` |
| 2.3 | **Schema versioning** | `otel_events.v` field, compatibility validation between schema versions |
| 2.4 | **Health/readiness events** | Built-in schema for application lifecycle events: startup, ready, degraded, shutdown |
| 2.5 | **Testing utilities** | `OtelEvents.Testing` package with in-memory `LogRecord` collector and assertion extensions |
| 2.6 | **OtelEvents.AspNetCore integration pack** | Pre-built ASP.NET Core middleware that auto-emits schema-defined `http.request.received`, `http.request.completed`, `http.request.failed` events with causal scope per request |
| 2.7 | **OtelEvents.HealthChecks integration pack** | Pre-built `IHealthCheckPublisher` that emits `health.check.executed` and `health.state.changed` events; complements 2.4 lifecycle events |
| 2.8 | **Per-event-category rate limiting** | OTEL processor that rate-limits events by category (e.g., max 100 `db.query.executed` events per second), configurable per event name |
| 2.9 | **Optional `IMeterFactory`-based Meter creation** | Alternative to static `Meter` instances for DI-friendly, disposable meter lifecycle (see ADR in Appendix B, DR-023) |


### Phase 3 — Ecosystem & Scale

Advanced features for large-scale adoption.

| # | Feature | Description |
|---|---------|-------------|
| 3.1 | **Schema registry CLI** | `dotnet otel-events validate`, `dotnet otel-events generate`, `dotnet otel-events diff` commands |
| 3.2 | **Multi-service schema sharing** | NuGet-packaged schemas for shared event contracts |
| 3.3 | **Advanced event sampling** | Configurable sampling rates for high-volume events with head/tail sampling strategies (as an OTEL processor) |
| 3.4 | **Schema documentation generator** | Auto-generate event catalog documentation from YAML |
| 3.5 | **Performance dashboard template** | Grafana/OTEL Collector config templates from schema metrics |
| 3.6 | **VS Code extension** | YAML schema IntelliSense, validation, event preview |
| 3.7 | **OtelEvents.Grpc integration pack** | Pre-built gRPC server/client interceptors for `grpc.call.started`, `grpc.call.completed`, `grpc.call.failed` events (replaces deferred "gRPC service events" item) |
| 3.8 | **OtelEvents.Azure.CosmosDb integration pack** | DiagnosticListener-based observer for `cosmosdb.query.executed`, `cosmosdb.point.read`, `cosmosdb.point.write` events with RU histograms |
| 3.9 | **OtelEvents.Azure.Storage integration pack** | Azure SDK pipeline policy for `storage.blob.*` and `storage.queue.*` events |
| 3.10 | **Schema file signing** | `dotnet otel-events sign` command for schema integrity verification in multi-team environments |

|---|---------|-------------|
| 3.1 | **Schema registry CLI** | `dotnet otel-events validate`, `dotnet otel-events generate`, `dotnet otel-events diff` commands |
| 3.2 | **Multi-service schema sharing** | NuGet-packaged schemas for shared event contracts |
| 3.3 | **Event sampling** | Configurable sampling rates for high-volume events (as an OTEL processor) |
| 3.4 | **Schema documentation generator** | Auto-generate event catalog documentation from YAML |
| 3.5 | **Performance dashboard template** | Grafana/OTEL Collector config templates from schema metrics |
| 3.6 | **VS Code extension** | YAML schema IntelliSense, validation, event preview |

---

## 4. Technical Architecture

### System Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                        BUILD TIME                                   │
│                                                                     │
│  ┌──────────┐    ┌──────────────┐    ┌────────────────────────┐    │
│  │  YAML    │───▶│  Schema      │───▶│  C# Source Generator   │    │
│  │  Schema  │    │  Parser      │    │  (MSBuild Task)        │    │
│  │  Files   │    │  + Validator  │    │                        │    │
│  └──────────┘    └──────────────┘    │  Outputs:              │    │
│                                      │  • [LoggerMessage]     │    │
│                                      │    partial methods     │    │
│                                      │  • Extension methods   │    │
│                                      │    (log + metrics)     │    │
│                                      │  • Meter/Counter/      │    │
│                                      │    Histogram statics   │    │
│                                      │  • Enum types          │    │
│                                      │  • JSON serializer ctx │    │
│                                      └────────────────────────┘    │
│                                                                     │
│  ┌────────────────────────────────────────────────────────────┐     │
│  │  Roslyn Analyzers                                          │     │
│  │  • OTEL001: Console.Write detected                          │     │
│  │  • OTEL002: Untyped ILogger usage (not via otel-events extension)   │     │
│  │  • OTEL003: String interpolation in event field             │     │
│  │  • OTEL004: Undefined event name                            │     │
│  └────────────────────────────────────────────────────────────┘     │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                        RUNTIME (OTEL SDK Pipeline)                  │
│                                                                     │
│  ┌──────────────────┐                                               │
│  │ Application Code │                                               │
│  │                  │── logger.RequestCompleted("POST", ...)        │
│  └────────┬─────────┘                                               │
│           │                                                         │
│           ▼                                                         │
│  ┌──────────────────────┐                                           │
│  │ otel-events Generated Code   │  Extension method on ILogger<T>           │
│  │                      │  • Calls [LoggerMessage] partial method   │
│  │                      │    → creates OTEL LogRecord natively      │
│  │                      │  • Records Histogram/Counter via Meter    │
│  │                      │    → creates OTEL Metric natively         │
│  └────────┬─────────────┘                                           │
│           │                                                         │
│           ▼                                                         │
│  ┌─────────────────────────────────────────────────────────┐        │
│  │              OTEL SDK LOG PIPELINE                       │        │
│  │                                                         │        │
│  │  ┌─────────────────────┐                                │        │
│  │  │ AllCausalityProc.   │  Adds otel_events.event_id (UUID v7)   │        │
│  │  │ (BaseProcessor      │  Adds otel_events.parent_event_id      │        │
│  │  │  <LogRecord>)       │  (from AsyncLocal context)     │        │
│  │  └──────────┬──────────┘                                │        │
│  │             │                                           │        │
│  │             ▼                                           │        │
│  │  ┌─────────────────────┐  ┌──────────────────────────┐  │        │
│  │  │ OtelEventsJsonExporter     │  │ OTLP Exporter            │  │        │
│  │  │ (BaseExporter       │  │ (standard OTEL)          │  │        │
│  │  │  <LogRecord>)       │  │                          │  │        │
│  │  │                     │  │ → OTEL Collector          │  │        │
│  │  │ → stdout JSONL      │  │ → Jaeger, Loki, etc.     │  │        │
│  │  │ (AI-optimized)      │  │                          │  │        │
│  │  └─────────────────────┘  └──────────────────────────┘  │        │
│  └─────────────────────────────────────────────────────────┘        │
│                                                                     │
│  ┌─────────────────────────────────────────────────────────┐        │
│  │              OTEL SDK METRICS PIPELINE                   │        │
│  │                                                         │        │
│  │  otel-events generated Meters (MyApp.Events.Http, etc.)          │        │
│  │  → Registered via .AddMeter("MyApp.Events.*")            │        │
│  │  → OTEL SDK handles aggregation + export (OTLP, etc.)    │        │
│  └─────────────────────────────────────────────────────────┘        │
│                                                                     │
│  ┌─────────────────────────────────────────────────────────┐        │
│  │  OTEL Trace Context (Activity.Current)                   │        │
│  │  • traceId, spanId flow automatically via OTEL           │        │
│  │  • otel-events reads them — does NOT propagate or create spans   │        │
│  └─────────────────────────────────────────────────────────┘        │
└─────────────────────────────────────────────────────────────────────┘
```

### How otel-events Extends OTEL

otel-events adds exactly **four components** to an existing OTEL setup. Everything else is standard OTEL.

| otel-events Component | OTEL Extension Point | What it does |
|---------------|---------------------|-------------|
| **OtelEvents.Schema** (build-time) | N/A — build-time only | Parses YAML → generates C# code that uses `[LoggerMessage]` + `Meter` + `Counter<T>` + `Histogram<T>` |
| **OtelEventsJsonExporter** | `BaseExporter<LogRecord>` | Formats `LogRecord`s as AI-optimized single-line JSONL to stdout/file |
| **OtelEventsCausalityProcessor** | `BaseProcessor<LogRecord>` | Adds `otel_events.event_id` (UUID v7) and `otel_events.parent_event_id` attributes to each `LogRecord` |
| **OtelEvents.Analyzers** | Roslyn `DiagnosticAnalyzer` | Compile-time enforcement of schema usage, detects `Console.Write`, validates patterns |

### What otel-events Does NOT Provide (OTEL Handles It)

| Responsibility | OTEL Component |
|---------------|---------------|
| Log export to backends | OTLP Exporter, Console Exporter, etc. |
| Trace correlation (`traceId`, `spanId`) | `Activity` / `ActivitySource` |
| Metrics aggregation & export | `MeterProvider`, OTLP Exporter |
| Sampling (traces/logs) | OTEL SDK sampling |
| Resource enrichment (`service.name`, etc.) | OTEL `ResourceBuilder` |
| Distributed context propagation | OTEL Propagators |
| `ILogger` → `LogRecord` bridging | OTEL `OpenTelemetryLoggerProvider` |
| Collector infrastructure | OTEL Collector |

### Technology Stack

| Layer | Technology | Rationale |
|-------|-----------|-----------|
| Language | C# 12+ | Latest features, primary constructor, raw string literals |
| Runtime | .NET 8+ (LTS), .NET 9 | AOT support, modern APIs, long-term support |
| Log emission | `[LoggerMessage]` source generator | Zero-alloc, AOT-friendly, native `ILogger` integration |
| Metrics | `System.Diagnostics.Metrics` (`Meter`, `Counter<T>`, `Histogram<T>`) | OTEL-native .NET metrics API |
| Tracing | `System.Diagnostics.Activity` | OTEL-native .NET tracing API |
| Serialization | System.Text.Json source generators | AOT-friendly, zero-alloc, no reflection |
| Code generation | MSBuild Task + Roslyn incremental source generators | Industry standard, IDE integration, cached incremental builds |
| Schema parsing | YamlDotNet | Most popular YAML library for .NET, well-maintained |
| OTEL SDK | OpenTelemetry .NET SDK (stable) | Official SDK, stable logs/metrics/traces APIs |
| Analyzers | Roslyn `DiagnosticAnalyzer` | Standard .NET analyzer infrastructure |
| Testing | xUnit + FluentAssertions + Verify | Modern .NET testing stack |
| Benchmarking | BenchmarkDotNet | Industry standard for .NET perf testing |

---

## 5. NuGet Package Structure

```
OtelEvents (meta-package — references all below)
├── OtelEvents.Schema                  — YAML parser, schema model, validation, code generator
├── OtelEvents.Exporter.Json           — Custom OTEL BaseExporter<LogRecord> for AI-optimized JSONL
├── OtelEvents.Causality               — Custom OTEL BaseProcessor<LogRecord> for eventId/parentEventId
├── OtelEvents.Analyzers               — Roslyn analyzers (Console.Write, ILogger, etc.)
└── OtelEvents.Testing                 — In-memory LogRecord collector, assertion extensions
```

### Package Dependencies

All dependencies are managed via **Central Package Management** (`Directory.Packages.props`) with exact version pins. OTEL SDK dependencies use bounded ranges `[1.9, 2.0)` to allow patch updates while preventing breaking major version changes. CI matrix tests against both the minimum and latest OTEL SDK versions within the range.

```xml
<!-- Directory.Packages.props -->
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="OpenTelemetry" Version="[1.9.0, 2.0.0)" />
    <PackageVersion Include="YamlDotNet" Version="16.1.3" />
    <PackageVersion Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" />
    <PackageVersion Include="System.Text.Json" Version="8.0.5" />
    <PackageVersion Include="Microsoft.Extensions.Logging" Version="8.0.1" />
  </ItemGroup>
</Project>
```

```
OtelEvents.Schema (build-time only — source generator + MSBuild task)
├── YamlDotNet [16.1.3] (pinned; safe-loading mode configured — see §16)
├── Microsoft.CodeAnalysis.CSharp [4.8.0]
└── (no runtime dependency — generates code that uses standard BCL + OTEL types)

OtelEvents.Exporter.Json (runtime — custom OTEL exporter)
├── OpenTelemetry [1.9, 2.0)
├── System.Text.Json [8.0.5]
└── (thin package — only the exporter + JSON serialization)

OtelEvents.Causality (runtime — custom OTEL processor)
├── OpenTelemetry [1.9, 2.0)
└── (thin package — only the processor + UUID v7 generation)

OtelEvents.Analyzers (build-time only — Roslyn analyzers)
└── Microsoft.CodeAnalysis.CSharp [4.8.0]
    (analyzer-only package — no runtime dependency)

OtelEvents.Testing (test-time only)
├── OpenTelemetry [1.9, 2.0)
├── Microsoft.Extensions.Logging [8.0.1]
└── (no external test framework dependency — works with any)
```


### Why This Split?

| Decision | Rationale |
|----------|-----------|
| `OtelEvents.Schema` is build-time only | Source generator + MSBuild task — never ships in app binaries. Can also be used by CLI tools and CI validation. |
| `OtelEvents.Exporter.Json` is separate and optional | Not every project needs JSONL stdout output. Teams using OTLP-only pipelines can skip it. |
| `OtelEvents.Causality` is separate and optional | Teams that don't need causal event trees can skip it. Keeps core footprint minimal. |
| `OtelEvents.Analyzers` is separate | Analyzers have strict packaging rules; keeps the analyzer DLL isolated. |
| `OtelEvents.Testing` is separate | Test infrastructure shouldn't be in production packages. |
| No `OtelEvents.Core` package | There is no otel-events runtime core — generated code uses only BCL types (`ILogger`, `Meter`, `Activity`) and OTEL SDK types. otel-events adds no runtime abstraction layer. |
| No `OtelEvents.Bridge.MicrosoftLogging` package | OTEL SDK already provides `OpenTelemetryLoggerProvider` which bridges `ILogger` → `LogRecord`. otel-events' generated code uses `ILogger` natively, so no bridge is needed. |

### Adoption Scenarios

| Scenario | Packages Needed |
|----------|----------------|
| Schema-driven events + OTLP export (no JSON stdout) | `OtelEvents.Schema` |
| Schema-driven events + AI-optimized JSON stdout | `OtelEvents.Schema` + `OtelEvents.Exporter.Json` |
| Full otel-events experience | `OtelEvents.Schema` + `OtelEvents.Exporter.Json` + `OtelEvents.Causality` + `OtelEvents.Analyzers` |
| CI schema validation only | `OtelEvents.Schema` (used via `dotnet otel-events validate` CLI) |
| Testing otel-events events | `OtelEvents.Testing` (in test projects) |

---

## 6. YAML Schema Specification

### Schema File Structure

Schema files use `.otel.yaml` or `.otel.yml` extension. A project can have multiple schema files — they are merged by the code generator.

### Full Schema Grammar

```yaml
# ─── Schema Header ───────────────────────────────────────────────────
schema:
  name: "MyService"                    # Required: schema name
  version: "1.0.0"                     # Required: semver
  namespace: "MyCompany.MyService"     # Required: C# namespace for generated code
  description: "Events for MyService"  # Optional: human-readable description
  meterName: "MyCompany.MyService"     # Optional: OTEL Meter name (defaults to namespace)

# ─── Reusable Field Definitions ──────────────────────────────────────
fields:
  userId:
    type: string
    description: "Unique user identifier"
    index: true                        # Marks field as queryable/indexed
    examples: ["usr_abc123"]

  durationMs:
    type: double
    description: "Duration in milliseconds"
    unit: "ms"

  httpStatusCode:
    type: int
    description: "HTTP response status code"
    index: true

  httpMethod:
    type: enum
    description: "HTTP method"
    values: [GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS]

  correlationId:
    type: string
    description: "Cross-service correlation identifier"
    index: true

# ─── Enum Type Definitions ──────────────────────────────────────────
enums:
  HealthStatus:
    description: "Application health state"
    values:
      - Healthy
      - Degraded
      - Unhealthy

  DependencyType:
    description: "Type of external dependency"
    values:
      - Database
      - HttpApi
      - MessageQueue
      - Cache
      - FileSystem

# ─── Event Definitions ──────────────────────────────────────────────
events:
  http.request.received:
    id: 1001                           # Required: unique EventId for [LoggerMessage]
    severity: INFO                     # TRACE|DEBUG|INFO|WARN|ERROR|FATAL
    description: "An HTTP request was received by the service"
    message: "HTTP {method} {path} received"
    fields:
      method:
        ref: httpMethod               # References reusable field definition
        required: true
      path:
        type: string
        required: true
        index: true
      userAgent:
        type: string
        required: false
      contentLength:
        type: long
        required: false
    metrics:
      http.request.count:
        type: counter
        unit: "requests"
        description: "Total HTTP requests received"
    tags:
      - api
      - http

  http.request.completed:
    id: 1002
    severity: INFO
    description: "An HTTP request completed processing"
    message: "HTTP {method} {path} completed with {statusCode} in {durationMs}ms"
    fields:
      method:
        ref: httpMethod
        required: true
      path:
        type: string
        required: true
        index: true
      statusCode:
        ref: httpStatusCode
        required: true
      durationMs:
        ref: durationMs
        required: true
    metrics:
      http.request.duration:
        type: histogram
        unit: "ms"
        description: "HTTP request duration"
        buckets: [5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000]
      http.response.count:
        type: counter
        unit: "responses"
        description: "Total HTTP responses sent"
        labels:
          - statusCode
    tags:
      - api
      - http

  db.query.executed:
    id: 2001
    severity: DEBUG
    description: "A database query was executed"
    message: "Query on {table} completed in {durationMs}ms returning {rowCount} rows"
    fields:
      table:
        type: string
        required: true
        index: true
      operation:
        type: enum
        values: [SELECT, INSERT, UPDATE, DELETE, MERGE]
        required: true
      durationMs:
        ref: durationMs
        required: true
      rowCount:
        type: int
        required: false
    metrics:
      db.query.duration:
        type: histogram
        unit: "ms"
        buckets: [1, 5, 10, 25, 50, 100, 250, 500, 1000]

  dependency.failed:
    id: 3001
    severity: ERROR
    description: "An external dependency call failed"
    message: "Dependency {dependencyName} ({dependencyType}) failed: {reason}"
    exception: true                   # Adds Exception parameter to generated method
    fields:
      dependencyName:
        type: string
        required: true
        index: true
      dependencyType:
        ref: DependencyType           # References enum type
        required: true
      reason:
        type: string
        required: true
      attemptNumber:
        type: int
        required: false
      durationMs:
        ref: durationMs
        required: false
    metrics:
      dependency.failure.count:
        type: counter
        description: "Total dependency failures"
        labels:
          - dependencyName
          - dependencyType

  app.health.changed:
    id: 4001
    severity: WARN
    description: "Application health status changed"
    message: "Health changed from {previousStatus} to {currentStatus}: {reason}"
    fields:
      previousStatus:
        ref: HealthStatus
        required: true
      currentStatus:
        ref: HealthStatus
        required: true
      reason:
        type: string
        required: true
```

### Field Sensitivity Classification

Every field definition supports an optional `sensitivity` attribute that classifies the data sensitivity level. This is used by the JSON exporter to apply redaction policies and by analyzers to warn about unprotected PII.

```yaml
fields:
  userId:
    type: string
    description: "Unique user identifier"
    sensitivity: pii              # Marks this field as containing PII
    index: true

  apiKey:
    type: string
    description: "API key for external service"
    sensitivity: credential       # Marks as credential — always redacted in non-Development

  hostName:
    type: string
    description: "Internal hostname"
    sensitivity: internal         # Internal infrastructure detail — redacted in Production

  httpMethod:
    type: string
    description: "HTTP request method"
    sensitivity: public           # Default — safe to emit in all environments
```

| Sensitivity Level | Description | Default Redaction Behavior |
|-------------------|-------------|---------------------------|
| `public` | Safe to emit in all environments. Default if not specified. | No redaction |
| `internal` | Internal infrastructure details (hostnames, paths, process IDs). | Redacted in `Production` profile; visible in `Development`/`Staging` |
| `pii` | Personally Identifiable Information (user IDs, emails, IP addresses, user agents). | Redacted in `Production`/`Staging` unless explicitly opted in; visible in `Development` |
| `credential` | Secrets, tokens, API keys, connection strings. | **Always redacted** in all environments. Analyzer OTEL009 warns if no redaction policy is applied. |

Redaction replaces the value with `"[REDACTED:{sensitivity}]"` (e.g., `"[REDACTED:pii]"`). The field key is still present in the JSON envelope — only the value is replaced.

### Field Value Length Limits

Field definitions support an optional `maxLength` attribute to prevent unbounded attribute values:

```yaml
fields:
  userAgent:
    type: string
    description: "User-Agent header value"
    sensitivity: pii
    maxLength: 512               # Truncate at 512 characters

  httpPath:
    type: string
    description: "Request path"
    maxLength: 256               # Truncate long paths
```

When a value exceeds `maxLength`, it is truncated with the suffix `"…[truncated]"`. A global default maximum is set via `OtelEventsJsonExporterOptions.MaxAttributeValueLength` (default: 4096 characters). Per-field `maxLength` overrides the global default.

### Field Types

| YAML Type | C# Type | JSON Type | Notes |
|-----------|---------|-----------|-------|
| `string` | `string` | `string` | UTF-8, max length governed by field `maxLength` attribute or global `MaxAttributeValueLength` (default: 4096) |
| `int` | `int` | `number` | 32-bit signed integer |
| `long` | `long` | `number` | 64-bit signed integer |
| `double` | `double` | `number` | IEEE 754 double-precision |
| `bool` | `bool` | `boolean` | |
| `datetime` | `DateTimeOffset` | `string` | ISO 8601 UTC format |
| `duration` | `TimeSpan` | `string` | ISO 8601 duration |
| `guid` | `Guid` | `string` | Standard GUID format |
| `enum` | Generated C# enum | `string` | Serialized as string name, not integer |
| `string[]` | `string[]` | `array` | Array of strings |
| `int[]` | `int[]` | `array` | Array of integers |
| `map` | `Dictionary<string, string>` | `object` | String key-value pairs |

### Schema Validation Rules (Build-Time)

| Rule | Error Code | Description |
|------|-----------|-------------|
| Unique event IDs | `OTEL_SCHEMA_001` | No duplicate event names within merged schemas |
| Valid severity | `OTEL_SCHEMA_002` | Severity must be one of: TRACE, DEBUG, INFO, WARN, ERROR, FATAL |
| Message template match | `OTEL_SCHEMA_003` | All `{placeholders}` in message must match a field name |
| Ref resolution | `OTEL_SCHEMA_004` | All `ref` values must resolve to a defined field or enum |
| Type validity | `OTEL_SCHEMA_005` | Field types must be from the supported type list |
| Event name format | `OTEL_SCHEMA_006` | Event names must be dot-namespaced, lowercase, alphanumeric + dots only |
| Required field completeness | `OTEL_SCHEMA_007` | Required fields must have a type (directly or via ref) |
| Metric type validity | `OTEL_SCHEMA_008` | Metric types must be: counter, histogram, gauge |
| Enum non-empty | `OTEL_SCHEMA_009` | Enum definitions must have at least one value |
| Semver version | `OTEL_SCHEMA_010` | Schema version must be valid semver |
| Reserved prefix | `OTEL_SCHEMA_011` | Event names and field names must not start with `otel_events.` |
| Unique numeric IDs | `OTEL_SCHEMA_012` | Each event `id` must be a unique integer (used as `[LoggerMessage]` EventId) |
| Meter name valid | `OTEL_SCHEMA_013` | Meter name must be a valid .NET identifier (dot-separated) |
| Sensitivity validity | `OTEL_SCHEMA_014` | `sensitivity` must be one of: `public`, `internal`, `pii`, `credential` |
| Max length validity | `OTEL_SCHEMA_015` | `maxLength` must be a positive integer when specified |
| Schema file size limit | `OTEL_SCHEMA_016` | Individual schema files must not exceed 1 MB |
| Event count limit | `OTEL_SCHEMA_017` | Merged schemas must not define more than 500 events total |
| Field count limit | `OTEL_SCHEMA_018` | Individual events must not define more than 50 fields |

### Schema Parsing Resource Limits

To prevent denial-of-service via malicious or excessively large schema files, the YAML parser enforces these resource limits:

| Limit | Value | Rationale |
|-------|-------|-----------|
| Max file size | 1 MB | Prevents memory exhaustion from large files |
| Max events per merged schema | 500 | Prevents excessive code generation |
| Max fields per event | 50 | Prevents unbounded attribute cardinality |
| Max YAML nesting depth | 20 | Prevents stack overflow in parser |
| Safe YAML loading | Enabled | YamlDotNet configured with `LoadingMode.Safe` — disables YAML tags, aliases, and anchors that could cause expansion attacks |

These limits are enforced at parse time (build time). Violations produce clear build errors with the corresponding `OTEL_SCHEMA_*` error code.


---

## 7. Generated Code Examples

### Input Schema

```yaml
schema:
  name: "MyService"
  version: "1.0.0"
  namespace: "MyCompany.MyService"
  meterName: "MyCompany.MyService.Events"

events:
  http.request.completed:
    id: 1002
    severity: INFO
    message: "HTTP {method} {path} completed with {statusCode} in {durationMs}ms"
    fields:
      method:
        type: enum
        values: [GET, POST, PUT, DELETE]
        required: true
      path:
        type: string
        required: true
        index: true
      statusCode:
        type: int
        required: true
      durationMs:
        type: double
        required: true
      userId:
        type: string
        required: false
        index: true
    metrics:
      http.request.duration:
        type: histogram
        unit: "ms"
        buckets: [5, 10, 25, 50, 100, 250, 500, 1000]
      http.request.count:
        type: counter
        unit: "requests"
    tags:
      - api
      - http
    exception: false
```

### Generated Output: Event Source Class

```csharp
// <auto-generated>
// Generated by otel-events Code Generator v2.0.0
// Schema: MyService v1.0.0
// DO NOT EDIT — changes will be overwritten on next build
// </auto-generated>

#nullable enable

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace MyCompany.MyService.Events;

/// <summary>
/// Category type for http.request events.
/// Used as ILogger&lt;HttpRequestEventSource&gt; category.
/// </summary>
public sealed class HttpRequestEventSource { }

/// <summary>
/// Generated events for http.request.* category.
/// Uses OTEL-native [LoggerMessage] source generator for log emission
/// and System.Diagnostics.Metrics for metric recording.
/// </summary>
public static partial class HttpRequestEvents
{
    // ─── OTEL Meter + Instruments (static, shared across all callers) ───
    private static readonly Meter s_meter = new(
        "MyCompany.MyService.Events.Http", "1.0.0");

    private static readonly Histogram<double> s_httpRequestDuration =
        s_meter.CreateHistogram<double>(
            "http.request.duration", "ms", "HTTP request duration");

    private static readonly Counter<long> s_httpRequestCount =
        s_meter.CreateCounter<long>(
            "http.request.count", "requests", "Total HTTP requests");

    // ─── [LoggerMessage] partial method (OTEL-native log emission) ──────
    [LoggerMessage(
        EventId = 1002,
        EventName = "http.request.completed",
        Level = LogLevel.Information,
        Message = "HTTP {Method} {Path} completed with {StatusCode} in {DurationMs}ms")]
    private static partial void LogHttpRequestCompleted(
        ILogger logger,
        string method,
        string path,
        int statusCode,
        double durationMs);

    // ─── [LoggerMessage] overload with optional userId ──────────────────
    [LoggerMessage(
        EventId = 1002,
        EventName = "http.request.completed",
        Level = LogLevel.Information,
        Message = "HTTP {Method} {Path} completed with {StatusCode} in {DurationMs}ms")]
    private static partial void LogHttpRequestCompletedWithUser(
        ILogger logger,
        string method,
        string path,
        int statusCode,
        double durationMs,
        string userId);

    /// <summary>
    /// Emits event: <c>http.request.completed</c>
    /// <para>An HTTP request completed processing.</para>
    /// <para>Emits structured log (OTEL LogRecord) + metrics (histogram + counter).</para>
    /// </summary>
    /// <param name="logger">The logger instance (ILogger&lt;HttpRequestEventSource&gt;)</param>
    /// <param name="method">HTTP method (required)</param>
    /// <param name="path">Request path (required, indexed)</param>
    /// <param name="statusCode">HTTP response status code (required)</param>
    /// <param name="durationMs">Duration in milliseconds (required)</param>
    /// <param name="userId">User identifier (optional, indexed)</param>
    public static void HttpRequestCompleted(
        this ILogger<HttpRequestEventSource> logger,
        HttpMethod method,
        string path,
        int statusCode,
        double durationMs,
        string? userId = null)
    {
        // Emit structured log via OTEL LogRecord
        var methodStr = method.ToStringFast();
        if (userId is not null)
        {
            LogHttpRequestCompletedWithUser(logger, methodStr, path, statusCode, durationMs, userId);
        }
        else
        {
            LogHttpRequestCompleted(logger, methodStr, path, statusCode, durationMs);
        }

        // Emit associated OTEL metrics
        var tags = new TagList
        {
            { "method", methodStr },
            { "status_code", statusCode },
        };
        s_httpRequestDuration.Record(durationMs, tags);
        s_httpRequestCount.Add(1, tags);
    }
}
```

### Generated Output: Enum Types

```csharp
// <auto-generated/>
#nullable enable

namespace MyCompany.MyService.Events;

/// <summary>HTTP method</summary>
public enum HttpMethod
{
    GET,
    POST,
    PUT,
    DELETE,
}

/// <summary>Fast enum-to-string conversion (no allocation).</summary>
public static class HttpMethodExtensions
{
    public static string ToStringFast(this HttpMethod value) => value switch
    {
        HttpMethod.GET => "GET",
        HttpMethod.POST => "POST",
        HttpMethod.PUT => "PUT",
        HttpMethod.DELETE => "DELETE",
        _ => value.ToString(),
    };
}
```

### Generated Output: JSON Serialization Context

```csharp
// <auto-generated/>
using System.Text.Json.Serialization;
using OtelEvents.Exporter.Json;

namespace MyCompany.MyService.Events;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(AllJsonEnvelope))]
[JsonSerializable(typeof(ExceptionData))]
[JsonSerializable(typeof(StackFrameData))]
internal partial class AllJsonContext : JsonSerializerContext
{
}
```

### Usage in Application Code

```csharp
using MyCompany.MyService.Events;

public class OrderController : ControllerBase
{
    // Standard ILogger<T> — works with any OTEL setup
    private readonly ILogger<HttpRequestEventSource> _logger;
    private readonly IOrderService _orderService;

    public OrderController(
        ILogger<HttpRequestEventSource> logger,
        IOrderService orderService)
    {
        _logger = logger;
        _orderService = orderService;
    }

    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder(CreateOrderRequest request)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var order = await _orderService.CreateAsync(request);

            // Type-safe, schema-enforced event — emits OTEL LogRecord + metrics
            _logger.HttpRequestCompleted(
                method: HttpMethod.POST,
                path: "/orders",
                statusCode: 201,
                durationMs: sw.Elapsed.TotalMilliseconds,
                userId: User.FindFirst("sub")?.Value);

            return Created($"/orders/{order.Id}", order);
        }
        catch (Exception ex)
        {
            // Exception event — structured exception in LogRecord
            _logger.DependencyFailed(
                dependencyName: "OrderDatabase",
                dependencyType: DependencyType.Database,
                reason: ex.Message,
                exception: ex);

            throw;
        }
    }
}
```

### DI Registration (Extends Existing OTEL Setup)

```csharp
var builder = WebApplication.CreateBuilder(args);

// Standard OTEL setup — otel-events extends it, doesn't replace it
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("order-service"))
    .WithLogging(logging =>
    {
        // otel-events extension: adds eventId/parentEventId to LogRecords
        logging.AddProcessor<OtelEventsCausalityProcessor>();

        // otel-events extension: AI-optimized JSON exporter (stdout)
        logging.AddOtelEventsJsonExporter(options =>
        {
            options.Output = OtelEventsJsonOutput.Stdout;
            options.SchemaVersion = "1.0.0";
        });

        // Standard OTEL: export to collector
        logging.AddOtlpExporter();
    })
    .WithMetrics(metrics =>
    {
        // Pick up otel-events generated meters
        metrics.AddMeter("MyCompany.MyService.Events.*");

        // Standard OTEL: export to collector
        metrics.AddOtlpExporter();
    })
    .WithTracing(tracing =>
    {
        // Standard OTEL: tracing (otel-events reads Activity.Current but doesn't create spans)
        tracing.AddAspNetCoreInstrumentation();
        tracing.AddOtlpExporter();
    });

// Severity filtering via standard .NET logging configuration
builder.Logging.AddFilter<OpenTelemetryLoggerProvider>("MyCompany.MyService.Events", LogLevel.Information);
builder.Logging.AddFilter<OpenTelemetryLoggerProvider>("MyCompany.MyService.Events.Db", LogLevel.Debug);
```

### What the Developer Gets

| Before otel-events | After otel-events |
|-----------|----------|
| Write `[LoggerMessage]` by hand for each event | YAML schema generates all `[LoggerMessage]` methods |
| Create `Meter`, `Counter`, `Histogram` manually | Schema generates metric instruments automatically |
| Forget to record metrics alongside log events | Generated extension method emits both atomically |
| Inconsistent message templates across services | Schema enforces identical templates everywhere |
| No IntelliSense for valid event fields | Generated extension methods provide full IntelliSense |
| No compile-time event validation | Analyzers catch `Console.Write`, untyped `ILogger`, wrong fields |
| No causal linking between events | `OtelEventsCausalityProcessor` adds `eventId`/`parentEventId` |
| Verbose/inconsistent JSON on stdout | `OtelEventsJsonExporter` writes predictable AI-optimized JSONL |

---

## 8. JSON Envelope Specification

### OtelEventsJsonExporter — Custom OTEL Log Exporter

The `OtelEventsJsonExporter` is a `BaseExporter<LogRecord>` that reads OTEL `LogRecord`s from the pipeline and serializes them as single-line AI-optimized JSONL. It is not a parallel output path — it IS an OTEL exporter, sitting alongside OTLP exporters in the standard OTEL pipeline.

### Complete Envelope Schema

Every `LogRecord` exported by `OtelEventsJsonExporter` produces a single JSON line (JSONL) with this structure:

```jsonc
{
  // ─── Mandatory Envelope (from OTEL LogRecord) ────────────────
  "timestamp": "2025-01-15T14:30:00.123456Z",   // LogRecord.Timestamp → ISO 8601 UTC, microsecond precision
  "event": "http.request.completed",              // LogRecord.EventId.Name (set by [LoggerMessage] EventName)
  "severity": "INFO",                             // LogRecord.LogLevel → mapped to otel-events severity string
  "severityNumber": 9,                            // OTEL numeric: 1-24
  "message": "HTTP GET /api/orders completed with 200 in 45.2ms",  // LogRecord.FormattedMessage
  "service": "order-service",                     // From OTEL Resource["service.name"]
  "environment": "production",                    // From OTEL Resource["deployment.environment"]

  // ─── Correlation (from OTEL context) ─────────────────────────
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736", // LogRecord.TraceId (from Activity.Current)
  "spanId": "00f067aa0ba902b7",                   // LogRecord.SpanId (from Activity.Current)
  "eventId": "evt_7f8a9b2c-3d4e-5f6a-7b8c-9d0e1f2a3b4c",       // From attribute "otel_events.event_id" (set by OtelEventsCausalityProcessor)
  "parentEventId": "evt_1a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d", // From attribute "otel_events.parent_event_id" (optional)

  // ─── Typed Payload (from LogRecord.Attributes) ───────────────
  "attr": {
    "method": "GET",                              // LogRecord state key-value pairs
    "path": "/api/orders",
    "statusCode": 200,
    "durationMs": 45.2,
    "userId": "usr_abc123"
  },

  // ─── Tags (from LogRecord.Attributes["otel_events.tags"]) ───────────
  "tags": ["api", "http"],

  // ─── Metadata ────────────────────────────────────────────────
  // ─── Metadata ────────────────────────────────────────────────
  "otel_events.v": "1.0.0",                              // Schema version (from exporter config)
  "otel_events.seq": 42                                   // Monotonic sequence number (per-process, assigned by exporter)
  // NOTE: "otel_events.host" and "otel_events.pid" are OMITTED by default (EmitHostInfo = false).
  // When opted in: "otel_events.host": "web-server-01", "otel_events.pid": 12345
}
```

### Exception Object (when exception is present)

```jsonc
{
  "timestamp": "2025-01-15T14:30:00.456789Z",
  "event": "dependency.failed",
  "severity": "ERROR",
  "severityNumber": 17,
  "message": "Dependency OrderDatabase (Database) failed: Connection timeout",
  "service": "order-service",
  "environment": "production",
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "spanId": "00f067aa0ba902b7",
  "eventId": "evt_9e8d7c6b-5a4f-3e2d-1c0b-9a8f7e6d5c4b",
  "parentEventId": "evt_7f8a9b2c-3d4e-5f6a-7b8c-9d0e1f2a3b4c",
  "attr": {
    "dependencyName": "OrderDatabase",
    "dependencyType": "Database",
    "reason": "Connection timeout"
  },
  "tags": ["dependency"],

  // ─── Structured Exception (from LogRecord.Exception) ─────────
  "exception": {
    "type": "System.Data.SqlClient.SqlException",
    "message": "Connection timeout expired",
    "stackTrace": [
      { "method": "SqlConnection.Open()", "file": "SqlConnection.cs", "line": 142 },
      { "method": "OrderRepository.GetByIdAsync()", "file": "OrderRepository.cs", "line": 67 },
      { "method": "OrderService.CreateAsync()", "file": "OrderService.cs", "line": 34 }
    ],
    "inner": {
      "type": "System.Net.Sockets.SocketException",
      "message": "No connection could be made because the target machine actively refused it",
      "stackTrace": [
        { "method": "Socket.Connect()", "file": "Socket.cs", "line": 298 }
      ]
    }
  },

  "otel_events.v": "1.0.0",
  "otel_events.seq": 43,
  "otel_events.host": "web-server-01",
  "otel_events.pid": 12345
}
```

### Envelope Rules

| Rule | Specification |
|------|--------------|
| **No null fields** | If a value is null/absent, the key is **omitted entirely** from the JSON object. No `"field": null`. |
| **Single line** | Every event is exactly one JSON line terminated by `\n`. No pretty-printing, ever. |
| **UTC timestamps** | All timestamps are ISO 8601 UTC with microsecond precision: `yyyy-MM-ddTHH:mm:ss.ffffffZ` |
| **Reserved prefix** | All keys starting with `otel_events.` are reserved for library metadata. User schemas must not use this prefix. At runtime, the exporter strips/renames any incoming `otel_events.*` attributes not set by otel-events components (see §16.4). |
| **eventId format** | `evt_` prefix + UUID v7 (time-sortable): `evt_{uuid}` |
| **Event name format** | Lowercase, dot-namespaced: `category.subcategory.action` (e.g., `http.request.completed`) |
| **Severity string** | Exactly one of: `TRACE`, `DEBUG`, `INFO`, `WARN`, `ERROR`, `FATAL` — always uppercase |
| **Severity number** | OTEL standard 1–24. Mapping: TRACE=1, DEBUG=5, INFO=9, WARN=13, ERROR=17, FATAL=21 |
| **Exception depth** | Maximum 5 levels of `.inner` nesting. If exceeded, deepest level includes `"truncated": true` |
| **Exception detail level** | Controlled by `ExceptionDetailLevel`: `Full` (method names only — file paths always omitted unless `EnvironmentProfile = Development`), `TypeAndMessage` (Production default), `TypeOnly` (minimal) |
| **Stack trace** | Array of frame objects: `{ "method": string }`. `file` and `line` fields included **only** when `EnvironmentProfile = Development`. In `Staging`/`Production`, file paths are always omitted to prevent information disclosure (OWASP-A04). |
| **Encoding** | UTF-8 throughout |
| **Monotonic sequence** | `otel_events.seq` is a 64-bit integer, monotonically increasing per process lifetime, starting at 1 |
| **Host/PID metadata** | `otel_events.host` and `otel_events.pid` are **opt-in** (`EmitHostInfo = false` by default). When omitted, these fields do not appear in the envelope. |
| **Attribute value length** | Values exceeding `MaxAttributeValueLength` (default: 4096) are truncated with `"…[truncated]"` suffix |
| **Message template safety** | Message templates must never interpolate user-controlled PII. The `[LoggerMessage]` source generator handles interpolation — schema authors define field references, not user input. |


### Severity Mapping Table

| otel-events Severity | severityNumber | OTEL Range | .NET LogLevel |
|-------------|---------------|------------|---------------|
| TRACE | 1 | 1–4 | Trace |
| DEBUG | 5 | 5–8 | Debug |
| INFO | 9 | 9–12 | Information |
| WARN | 13 | 13–16 | Warning |
| ERROR | 17 | 17–20 | Error |
| FATAL | 21 | 21–24 | Critical |

### How the Exporter Reads LogRecord Fields

The `OtelEventsJsonExporter` maps `LogRecord` fields to envelope fields as follows:

| LogRecord Field | Envelope Field | Source |
|----------------|---------------|--------|
| `Timestamp` | `timestamp` | OTEL SDK sets this |
| `CategoryName` | (not emitted directly) | Used for filtering, not envelope |
| `LogLevel` | `severity`, `severityNumber` | Mapped via severity table |
| `EventId.Name` | `event` | Set by `[LoggerMessage(EventName = "...")]` |
| `FormattedMessage` | `message` | Set by `[LoggerMessage(Message = "...")]` |
| `TraceId` | `traceId` | From `Activity.Current` (OTEL captures automatically) |
| `SpanId` | `spanId` | From `Activity.Current` (OTEL captures automatically) |
| `Exception` | `exception` | Serialized by otel-events' structured exception serializer |
| `Attributes` (state) | `attr` | Key-value pairs from `[LoggerMessage]` parameters |
| `Attributes["otel_events.event_id"]` | `eventId` | Set by `OtelEventsCausalityProcessor` |
| `Attributes["otel_events.parent_event_id"]` | `parentEventId` | Set by `OtelEventsCausalityProcessor` |
| `Attributes["otel_events.tags"]` | `tags` | Set by generated code as log scope or attribute |
| Resource `service.name` | `service` | OTEL resource (set at startup) |
| Resource `deployment.environment` | `environment` | OTEL resource (set at startup) |

### Non-otel-events LogRecords

The `OtelEventsJsonExporter` can export **any** `LogRecord`, not just those from otel-events generated code. For non-otel-events `LogRecord`s (e.g., from third-party libraries using `ILogger`):

- `event` = `LogRecord.EventId.Name` if set, otherwise `"dotnet.ilogger"` (fallback)
- `attr` = all state key-value pairs from the `LogRecord`, subject to allowlist/denylist filtering (see below)
- `eventId`/`parentEventId` = present only if `OtelEventsCausalityProcessor` is in the pipeline
- All other envelope fields are populated normally

This means the `OtelEventsJsonExporter` provides a **unified JSONL output** for otel-events generated events AND third-party `ILogger` calls, without requiring a separate bridge component.

#### Attribute Filtering for Non-otel-events LogRecords

Non-otel-events `LogRecord`s may contain sensitive data from third-party libraries (connection strings, tokens, PII). The exporter provides filtering controls:

```csharp
logging.AddOtelEventsJsonExporter(options =>
{
    // Allowlist: only emit these attributes from non-otel-events LogRecords
    options.AttributeAllowlist = ["RequestPath", "StatusCode", "ElapsedMs"];

    // Denylist: never emit these attributes (takes precedence over allowlist)
    options.AttributeDenylist = ["ConnectionString", "Password", "Token", "Secret"];

    // Regex-based value redaction: replace matching values with [REDACTED]
    options.RedactPatterns =
    [
        @"(?i)(password|pwd|secret|token|key|credential)\s*[=:]\s*\S+",
        @"Server=.*;(User Id|Password)=.*",   // Connection string patterns
        @"Bearer\s+[A-Za-z0-9\-._~+/]+=*",   // Bearer tokens
    ];
});
```

When neither allowlist nor denylist is configured, all attributes are emitted (backward-compatible default). When an allowlist is set, only listed attributes pass through. The denylist always takes precedence.

### OtelEventsJsonExporter Implementation

```csharp
/// <summary>
/// Custom OTEL Log Exporter that writes LogRecords as AI-optimized single-line JSONL.
/// Plugs into the standard OTEL log pipeline alongside OTLP, Console, or any other exporter.
/// </summary>
public sealed class OtelEventsJsonExporter : BaseExporter<LogRecord>
{
    private readonly Stream _output;
    private readonly OtelEventsJsonExporterOptions _options;
    private readonly StreamWriter _writer; // 32 KB buffer, flushed after each batch
    private long _seq; // monotonic sequence counter
    private static readonly TimeSpan s_lockTimeout = TimeSpan.FromMilliseconds(100);

    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        bool lockTaken = false;
        try
        {
            Monitor.TryEnter(_output, s_lockTimeout, ref lockTaken);
            if (!lockTaken)
            {
                Interlocked.Increment(ref _droppedBatches);
                s_exportDroppedCounter.Add(1);
                return ExportResult.Success; // Don't report failure for timeout — it's backpressure
            }

            foreach (var logRecord in batch)
            {
                // Strip/rename any incoming otel_events.* attributes not set by otel-events components
                SanitizeReservedPrefix(logRecord);

                var seq = Interlocked.Increment(ref _seq);
                // Uses Utf8JsonWriter with ArrayPool<byte>.Shared buffer pooling
                WriteJsonLine(logRecord, seq);
            }

            // Flush after each batch for crash-consistency.
            _writer.Flush();
            return ExportResult.Success;
        }
        catch (IOException ex)
        {
            s_exportErrorCounter.Add(1, new TagList { { "error_type", ex.GetType().Name } });
            OpenTelemetrySdkEventSource.Log.ExporterErrorResult(nameof(OtelEventsJsonExporter), ex.Message);
            return ExportResult.Failure;
        }
        finally
        {
            if (lockTaken) Monitor.Exit(_output);
        }
    }
}
```

### OtelEventsJsonExporterOptions

```csharp
/// <summary>Configuration for the otel-events JSON exporter.</summary>
public sealed class OtelEventsJsonExporterOptions
{
    /// <summary>Output target: Stdout, Stderr, or File.</summary>
    public OtelEventsJsonOutput Output { get; set; } = OtelEventsJsonOutput.Stdout;

    /// <summary>Schema version stamped into every envelope as "otel_events.v".</summary>
    public string SchemaVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Environment profile that adjusts multiple security-sensitive defaults at once.
    /// Default: Production (most restrictive).
    /// </summary>
    public OtelEventsEnvironmentProfile EnvironmentProfile { get; set; } = OtelEventsEnvironmentProfile.Production;

    /// <summary>
    /// Controls exception detail in the JSON envelope.
    /// Default depends on EnvironmentProfile:
    ///   Development → Full, Staging → TypeAndMessage, Production → TypeAndMessage.
    /// </summary>
    public ExceptionDetailLevel? ExceptionDetailLevel { get; set; }

    /// <summary>
    /// Emit "otel_events.host" and "otel_events.pid" in the envelope.
    /// Default: false. These fields may expose internal infrastructure details.
    /// </summary>
    public bool EmitHostInfo { get; set; } = false;

    /// <summary>
    /// Maximum length for any single attribute value (string fields).
    /// Default: 4096 characters.
    /// </summary>
    public int MaxAttributeValueLength { get; set; } = 4096;

    /// <summary>
    /// Allowlist of attribute names to emit for non-otel-events LogRecords.
    /// When set, only listed attributes pass through. Null = all attributes (default).
    /// </summary>
    public ISet<string>? AttributeAllowlist { get; set; }

    /// <summary>
    /// Denylist of attribute names to never emit. Takes precedence over allowlist.
    /// </summary>
    public ISet<string> AttributeDenylist { get; set; } = new HashSet<string>();

    /// <summary>
    /// Regex patterns for value-level redaction. Matching values are replaced with "[REDACTED]".
    /// </summary>
    public IList<string> RedactPatterns { get; set; } = [];

    /// <summary>
    /// Lock timeout for stream writes. Default: 100ms.
    /// </summary>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromMilliseconds(100);
}

/// <summary>Environment profiles adjust multiple security defaults simultaneously.</summary>
public enum OtelEventsEnvironmentProfile
{
    /// <summary>Most permissive: full exception details, all sensitivity levels visible.</summary>
    Development,
    /// <summary>Moderate: TypeAndMessage exceptions, PII fields redacted.</summary>
    Staging,
    /// <summary>Most restrictive (default): TypeAndMessage exceptions, PII and internal redacted.</summary>
    Production
}

/// <summary>Controls how much exception detail is included in the JSON envelope.</summary>
public enum ExceptionDetailLevel
{
    /// <summary>Type, message, stack trace (method names only), inner exceptions.</summary>
    Full,
    /// <summary>Type and message only. Default for Production/Staging.</summary>
    TypeAndMessage,
    /// <summary>Exception type name only. Minimal disclosure.</summary>
    TypeOnly
}
```

### Export Path Decision Guide

| Topology | When to Use | Configuration |
|----------|-------------|---------------|
| **Stdout-only** (recommended for containers) | Logs collected by sidecar/DaemonSet agent | `OtelEventsJsonExporter` only — no `AddOtlpExporter()` for logs |
| **OTLP-only** | Direct OTEL Collector connection | `AddOtlpExporter()` only — no `OtelEventsJsonExporter` |
| **Both** (use sparingly) | Need local stdout + direct OTLP | Both configured — accept 2× log export cost |
| **Stdout + Collector `filelog`** (recommended) | Best of both worlds | `OtelEventsJsonExporter` → stdout → Collector `filelog` receiver → OTLP |

See §17 for OTEL Collector configuration matching the otel-events envelope format.

### Stdout Write Buffer & Flush Strategy

| Setting | Value | Rationale |
|---------|-------|-----------|
| `StreamWriter` buffer size | 32 KB | Balance between syscall frequency and memory |
| Flush frequency | After each `Export(batch)` call | Crash-consistency: events visible after each OTEL SDK batch interval |
| OTEL SDK batch interval | Configurable (default: 5 seconds) | Controls how often the SDK calls `Export()` |
| Crash-consistency guarantee | Events flushed to OS buffer after each batch | Data loss window = one OTEL SDK batch interval (default 5s) |


---

## 9. Causality Processor

### Purpose

OTEL provides distributed trace correlation via `Activity` (`traceId`, `spanId`). But within a single trace/span, individual log events have no causal relationship. otel-events' `OtelEventsCausalityProcessor` adds `otel_events.event_id` and `otel_events.parent_event_id` attributes to every `LogRecord`, enabling construction of **causal event trees** within and across services.

### Design

```csharp
/// <summary>
/// OTEL Log Processor that adds causal linking attributes to LogRecords.
/// Generates a unique eventId (UUID v7) for every LogRecord.
/// Reads parentEventId from AsyncLocal context (set by application code).
/// </summary>
public sealed class OtelEventsCausalityProcessor : BaseProcessor<LogRecord>
{
    public override void OnEnd(LogRecord logRecord)
    {
        // Generate unique event ID (UUID v7 — time-sortable)
        var eventId = $"evt_{Uuid7.NewUuid7()}";
        logRecord.Attributes = AppendAttribute(
            logRecord.Attributes, "otel_events.event_id", eventId);

        // Read parent event ID from ambient context
        var parentEventId = OtelEventsCausalityContext.CurrentParentEventId;
        if (parentEventId is not null)
        {
            logRecord.Attributes = AppendAttribute(
                logRecord.Attributes, "otel_events.parent_event_id", parentEventId);
        }
    }
}
```

### Causal Context API

```csharp
/// <summary>
/// Ambient context for causal event linking.
/// Uses AsyncLocal to flow across async boundaries.
/// </summary>
public static class OtelEventsCausalityContext
{
    private static readonly AsyncLocal<string?> s_parentEventId = new();

    /// <summary>Gets or sets the current parent event ID.</summary>
    public static string? CurrentParentEventId
    {
        get => s_parentEventId.Value;
        set => s_parentEventId.Value = value;
    }

    /// <summary>
    /// Sets the parent event ID for the duration of the scope.
    /// Restores the previous value when disposed.
    /// </summary>
    public static IDisposable SetParent(string parentEventId)
        => new CausalityScope(parentEventId);
}

// Usage in application code:
public async Task ProcessOrder(OrderRequest request)
{
    // Emit parent event — eventId is auto-generated by OtelEventsCausalityProcessor
    _logger.OrderProcessingStarted(request.OrderId);

    // Set the causal parent for subsequent events
    using (OtelEventsCausalityContext.SetParent(lastEmittedEventId))
    {
        _logger.PaymentProcessed(request.OrderId, request.Amount);
        _logger.InventoryReserved(request.OrderId, request.Items.Count);
    }
}
```

### Causal Tree Example

```
evt_001: order.processing.started (orderId: "ORD-123")
├── evt_002: payment.processed (orderId: "ORD-123", parentEventId: evt_001)
├── evt_003: inventory.reserved (orderId: "ORD-123", parentEventId: evt_001)
└── evt_004: order.completed (orderId: "ORD-123", parentEventId: evt_001)
```

### EventId Generation

| Property | Specification |
|----------|--------------|
| Format | `evt_` prefix + UUID v7 (RFC 9562) |
| Uniqueness | Globally unique — no collisions across processes/machines |
| Time-sortable | UUID v7 encodes timestamp — events sort chronologically by ID |
| Performance | Generated in < 100ns (no syscall, no crypto random for timestamp portion) |

---

## 10. Roslyn Analyzer Rules

### Analyzer Package: `OtelEvents.Analyzers`

All analyzers are delivered as a NuGet analyzer package. They activate automatically when the package is referenced.

| Rule ID | Severity | Title | Description |
|---------|----------|-------|-------------|
| **OTEL001** | Warning | Console output detected | `Console.Write`, `Console.WriteLine`, `Console.Error.Write` detected. Use otel-events generated events instead. |
| **OTEL002** | Warning | Untyped ILogger usage | Direct `ILogger.Log*`, `ILogger.LogInformation`, etc. detected in application code without using an otel-events generated extension method. Use schema-defined events instead. |
| **OTEL003** | Error | String interpolation in event field | `$"..."` string interpolation passed to an otel-events generated event method parameter. otel-events handles message interpolation — pass raw values only. |
| **OTEL004** | Warning | Undefined event name | String literal that looks like an event name doesn't match any schema-defined event. |
| **OTEL005** | Info | Unused event definition | Schema defines an event that is never called in the codebase. |
| **OTEL006** | Warning | Exception not captured | `catch` block doesn't emit an otel-events event with the caught exception. |
| **OTEL007** | Warning | Debug.Write detected | `Debug.Write*`, `Trace.Write*` detected. Use otel-events generated events instead. |
| **OTEL008** | Error | Reserved prefix usage | Code uses `otel_events.` prefix in field names — this prefix is reserved for library metadata. |
| **OTEL009** | Warning | PII field without redaction policy | Schema field with `sensitivity: pii` or `sensitivity: credential` is used in code but no redaction policy is configured in `OtelEventsJsonExporterOptions`. Configure `EnvironmentProfile` or explicit `RedactPatterns`. |

### Analyzer Configuration

```editorconfig
# .editorconfig — otel-events analyzer severity overrides

# Promote Console.Write to error in production code
[src/**/*.cs]
dotnet_diagnostic.OTEL001.severity = error

# Allow direct ILogger in specific adapter files
[src/**/Adapters/**/*.cs]
dotnet_diagnostic.OTEL002.severity = none

# Keep string interpolation as error everywhere
dotnet_diagnostic.OTEL003.severity = error
```

### Suppression

```csharp
// Explicit suppression when needed (e.g., test code, infrastructure)
#pragma warning disable OTEL001 // Console output in test assertion
Console.WriteLine(capturedOutput);
#pragma warning restore OTEL001
```

---

## 11. Testing Strategy

### Test Pyramid

```
        ┌──────────┐
        │  E2E     │  3-5 tests: Full pipeline (YAML → codegen → OTEL LogRecord → JSON output)
        │  Tests   │
       ─┼──────────┼─
       │ Integration │  20-30 tests: Exporter output, processor behavior, DI wiring
       │   Tests     │
      ─┼─────────────┼─
     │  Unit Tests     │  200+ tests: Schema parsing, codegen, serialization, causality
     │                 │
     └─────────────────┘
```

### Test Categories

#### Unit Tests (OtelEvents.Schema)

| Test Area | Examples |
|-----------|---------|
| YAML parsing | Valid schema parses correctly; invalid YAML produces clear error |
| Field type resolution | All supported types map to correct C# types |
| Ref resolution | `ref: userId` resolves to defined field; missing ref errors |
| Message template parsing | `{method}` extracted; mismatched placeholders detected |
| Enum generation | Enum values parsed; empty enum rejected |
| Schema validation | All 13 validation rules tested with valid and invalid inputs |
| Schema merging | Multiple files merge correctly; duplicate event IDs error |
| Semver validation | Valid/invalid version strings |
| EventId uniqueness | Numeric event IDs are unique within merged schemas |
| Code generation | Generated code compiles and uses `[LoggerMessage]` correctly |

#### Unit Tests (OtelEvents.Exporter.Json)

| Test Area | Examples |
|-----------|---------|
| JSON serialization | Output matches envelope spec exactly; no null fields; single line |
| Exception serialization | Depth capping at 5; structured stack frames; inner exception chaining |
| Severity mapping | All 6 severities map to correct string and number |
| Sequence numbering | Monotonic, starts at 1, thread-safe |
| Timestamp formatting | ISO 8601 UTC with microsecond precision |
| LogRecord mapping | All `LogRecord` fields map to correct envelope fields |
| Non-otel-events LogRecords | Third-party `ILogger` calls produce valid JSONL with `"dotnet.ilogger"` event name |
| Attribute extraction | `LogRecord` state key-value pairs → `attr` object |

#### Unit Tests (OtelEvents.Causality)

| Test Area | Examples |
|-----------|---------|
| Event ID generation | UUID v7 format, `evt_` prefix, unique across calls, time-sortable |
| Parent event ID | `OtelEventsCausalityContext` sets/reads `parentEventId` via `AsyncLocal` |
| Scope disposal | `CausalityScope` restores previous parent on dispose |
| Processor behavior | `OtelEventsCausalityProcessor.OnEnd` adds `otel_events.event_id` attribute to `LogRecord` |
| Thread safety | Concurrent event ID generation produces no duplicates |

#### Unit Tests (OtelEvents.Schema — Code Generation)

| Test Area | Examples |
|-----------|---------|
| `[LoggerMessage]` generation | Correct EventId, EventName, Level, Message attributes |
| Extension method generation | Correct method name, parameters, return type on `ILogger<T>` |
| Required vs optional params | Required params are non-nullable; optional params have defaults |
| Enum type generation | Generated enum matches YAML values |
| Exception parameter | `exception: true` adds `Exception?` parameter |
| Meter + instruments | Generated `Meter`, `Counter<T>`, `Histogram<T>` statics |
| Namespace | Generated code uses schema namespace |
| IntelliSense docs | XML doc comments generated from schema descriptions |
| Tags | Schema-defined tags added as attributes |

#### Integration Tests

| Test Area | Examples |
|-----------|---------|
| Full pipeline | Emit event → OTEL pipeline → `OtelEventsJsonExporter` → JSONL output verified |
| Exporter + processor | `OtelEventsCausalityProcessor` enriches → `OtelEventsJsonExporter` formats → correct eventId in JSON |
| DI registration | `AddOtelEventsJsonExporter()` + `AddProcessor<OtelEventsCausalityProcessor>()` registers correctly |
| Configuration | `OtelEventsJsonExporterOptions` from appsettings.json / env vars apply correctly |
| Severity filtering | Events below minimum LogLevel are not emitted (standard .NET `AddFilter`) |
| Non-otel-events events | Third-party `ILogger` calls pass through exporter as valid JSONL |
| Metrics emission | Generated `Histogram`/`Counter` recordings are visible via OTEL `MeterProvider` |
| OTEL coexistence | `OtelEventsJsonExporter` runs alongside OTLP exporter without interference |

#### E2E Tests

| Test Area | Examples |
|-----------|---------|
| Schema → code → emit → verify | Full round-trip from YAML to JSONL output |
| ASP.NET integration | Generated events work in ASP.NET Core with `AddOpenTelemetry()` |
| OTEL export | LogRecords appear in OTEL collector (using test collector) |
| Metrics export | otel-events generated metrics appear in OTEL metrics pipeline |

### Test Infrastructure (OtelEvents.Testing)

```csharp
/// <summary>
/// In-memory OTEL LogRecord collector for testing.
/// Implements BaseExporter<LogRecord> — plugs into OTEL pipeline.
/// </summary>
public sealed class InMemoryLogExporter : BaseExporter<LogRecord>
{
    private readonly ConcurrentBag<ExportedLogRecord> _records = new();

    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        foreach (var record in batch)
        {
            _records.Add(new ExportedLogRecord(record));
        }
        return ExportResult.Success;
    }

    public IReadOnlyList<ExportedLogRecord> GetRecords() => _records.ToList();
    public ExportedLogRecord? FindByEventName(string eventName);
    public IEnumerable<ExportedLogRecord> FindAllByEventName(string eventName);
    public void AssertEventEmitted(string eventName);
    public void AssertNoEventEmitted(string eventName);
    public void Clear();
}

/// <summary>
/// Snapshot of a LogRecord for assertions (LogRecord is mutable/pooled in OTEL).
/// </summary>
public sealed record ExportedLogRecord
{
    public string? EventName { get; init; }
    public LogLevel LogLevel { get; init; }
    public string? FormattedMessage { get; init; }
    public IReadOnlyDictionary<string, object?> Attributes { get; init; }
    public Exception? Exception { get; init; }
    public ActivityTraceId TraceId { get; init; }
    public ActivitySpanId SpanId { get; init; }
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Test helper for setting up OTEL + otel-events in test projects.
/// </summary>
public static class OtelEventsTestHost
{
    public static (ILoggerFactory Factory, InMemoryLogExporter Exporter) Create();
    public static (ILoggerFactory Factory, InMemoryLogExporter Exporter) CreateWithCausality();
}

// Usage in tests:
[Fact]
public void HttpRequestCompleted_EmitsCorrectLogRecord()
{
    var (loggerFactory, exporter) = OtelEventsTestHost.Create();
    var logger = loggerFactory.CreateLogger<HttpRequestEventSource>();

    logger.HttpRequestCompleted(
        method: HttpMethod.GET,
        path: "/api/orders",
        statusCode: 200,
        durationMs: 45.2);

    var record = exporter.FindByEventName("http.request.completed");
    Assert.NotNull(record);
    Assert.Equal(LogLevel.Information, record.LogLevel);
    Assert.Equal(200, record.Attributes["StatusCode"]);
    Assert.Equal(45.2, record.Attributes["DurationMs"]);
    Assert.Contains("HTTP GET /api/orders completed with 200", record.FormattedMessage);
}

[Fact]
public void CausalityProcessor_AddsEventId()
{
    var (loggerFactory, exporter) = OtelEventsTestHost.CreateWithCausality();
    var logger = loggerFactory.CreateLogger<HttpRequestEventSource>();

    logger.HttpRequestCompleted(
        method: HttpMethod.GET,
        path: "/api/orders",
        statusCode: 200,
        durationMs: 45.2);

    var record = exporter.FindByEventName("http.request.completed");
    Assert.NotNull(record);
    var eventId = record.Attributes["otel_events.event_id"] as string;
    Assert.NotNull(eventId);
    Assert.StartsWith("evt_", eventId);
}
```

### Benchmarks (BenchmarkDotNet)

| Benchmark | Target | What It Measures |
|-----------|--------|-----------------|
| `EmitLoggerMessageEvent` | < 200ns | Time for `[LoggerMessage]` call (OTEL SDK baseline) |
| `EmitAllExtensionMethod` | < 500ns | Time for otel-events extension method (log + metrics recording) |
| `OtelEventsJsonExporterWrite` | < 1μs | Time to serialize one LogRecord → JSONL in exporter |
| `CausalityProcessorOnEnd` | < 200ns | Time for UUID v7 generation + attribute append |
| `ExceptionSerialization` | < 3μs | Serialize exception with 3 levels of nesting |
| `SequenceIncrement` | < 50ns | `Interlocked.Increment` contention test |
| `HighThroughput` | > 100K/s | Sustained event emission rate per process |

### Snapshot Testing (Verify)

JSON output is tested with snapshot testing to catch unintentional envelope format changes:

```csharp
[Fact]
public Task JsonOutput_MatchesSnapshot()
{
    var (loggerFactory, jsonCapture) = OtelEventsTestHost.CreateWithJsonCapture();
    var logger = loggerFactory.CreateLogger<HttpRequestEventSource>();

    logger.HttpRequestCompleted(
        method: HttpMethod.GET,
        path: "/api/orders",
        statusCode: 200,
        durationMs: 45.2);

    return Verify(jsonCapture.LastLine);
}
```

---

## 12. Non-Functional Requirements

### Performance

| Metric | Target | Measurement |
|--------|--------|-------------|
| otel-events extension method call (log + metrics) | < 500ns p95 | BenchmarkDotNet |
| OtelEventsJsonExporter per-record serialization | < 1μs p95 | BenchmarkDotNet |
| OtelEventsCausalityProcessor per-record processing | < 200ns p95 | BenchmarkDotNet |
| Throughput | > 100,000 events/s | BenchmarkDotNet, sustained |
| Memory allocation otel-events extension method | < 256 bytes/event | BenchmarkDotNet `[MemoryDiagnoser]` |
| GC pressure | No Gen2 collections under steady load | GC monitoring in integration tests |

Note: The OTEL SDK pipeline itself (batching, export) adds its own overhead. otel-events' targets apply to the otel-events specific code only. The total pipeline cost is otel-events overhead + OTEL SDK overhead.

### Zero-Allocation Strategy

| Component | Strategy |
|-----------|----------|
| `[LoggerMessage]` call | Compiler-generated source — zero-alloc by design (uses `LoggerMessage.Define`) |
| Metric recording | `TagList` is a struct with inline storage for ≤8 tags |
| JSON writing in exporter | `Utf8JsonWriter` writing directly to stream (no string intermediary) |
| Enum serialization | Pre-computed `string` via switch expression (no `Enum.ToString()`) |
| Event ID generation | UUID v7 from `Guid.CreateVersion7()` (.NET 9+) or custom implementation (.NET 8) |
| Sequence number | `Interlocked.Increment` (no lock, no allocation) |

### .NET Version Targets

| Package | Target Frameworks | Rationale |
|---------|-------------------|-----------|
| OtelEvents.Schema | `netstandard2.0` | MSBuild tasks/source generators must target netstandard2.0 |
| OtelEvents.Exporter.Json | `net8.0`, `net9.0` | Matches OTEL SDK targets, AOT requires .NET 8+ |
| OtelEvents.Causality | `net8.0`, `net9.0` | Matches OTEL SDK targets |
| OtelEvents.Analyzers | `netstandard2.0` | Roslyn analyzer requirement |
| OtelEvents.Testing | `net8.0`, `net9.0` | Matches runtime targets |

### AOT Compatibility

| Requirement | Implementation |
|-------------|---------------|
| No `System.Reflection.Emit` | `[LoggerMessage]` source generator + STJ source generators — no reflection |
| No `Type.GetType()` at runtime | Event types resolved at compile time by code generator |
| Trimmer-safe | `[DynamicallyAccessedMembers]` annotations where needed |
| `PublishAot = true` tested | CI includes AOT publish step |
| `IsAotCompatible = true` | Set on all runtime packages |

### Reliability

| Requirement | Specification |
|-------------|---------------|
| Exporter failure isolation | `OtelEventsJsonExporter` failure does not affect other OTEL exporters (OTEL SDK handles exporter isolation) |
| No application crashes | otel-events components must never throw exceptions that propagate to application code. OTEL SDK's processor/exporter error handling applies. |
| Backpressure | If stdout is blocked, `OtelEventsJsonExporter.Export()` may block until OTEL SDK's batch timeout fires, then drops. Standard OTEL batching behavior. |
| Graceful shutdown | OTEL SDK handles `Shutdown()` on all processors and exporters. otel-events implements `OnShutdown()` to flush pending JSON. |
| Thread safety | All public APIs must be thread-safe. `OtelEventsCausalityProcessor` uses `AsyncLocal` (thread-safe by design). `OtelEventsJsonExporter` uses `lock` around stream writes. |

### Observability (Self-Telemetry)

otel-events emits its own internal metrics for self-monitoring using OTEL's native `Meter`:

| Metric | Type | Description |
|--------|------|-------------|
| `otel_events.exporter.json.records_exported` | Counter | Total LogRecords exported by OtelEventsJsonExporter |
| `otel_events.exporter.json.export_errors` | Counter | Errors during JSON export |
| `otel_events.exporter.json.export_duration` | Histogram | Time to export a batch of LogRecords |
| `otel_events.causality.events_processed` | Counter | Total LogRecords processed by OtelEventsCausalityProcessor |

---

## 13. Success Metrics

### Adoption Metrics

| Metric | Target (6 months) | How Measured |
|--------|-------------------|-------------|
| NuGet downloads | 1,000+ | NuGet.org stats |
| GitHub stars | 100+ | GitHub stats |
| Projects using otel-events | 5+ production | User reports, GitHub dependency graph |
| Contributor PRs | 10+ | GitHub PR count |

### Quality Metrics

| Metric | Target | How Measured |
|--------|--------|-------------|
| Test coverage | > 90% line coverage | Coverlet + CI |
| Benchmark regression | < 5% regression between releases | BenchmarkDotNet in CI |
| Zero critical bugs in production | 0 P0 bugs reported | GitHub issues |
| API stability | No breaking changes after 1.0 | Semver, API compatibility checks |

### Developer Experience Metrics

| Metric | Target | How Measured |
|--------|--------|-------------|
| Time to first event (existing OTEL project) | < 5 minutes | User testing — add package, write YAML, build, emit |
| Time to first event (greenfield) | < 10 minutes | User testing — includes OTEL setup |
| Schema → working code | < 3 minutes | User testing |
| Analyzer adoption friction | Zero false positives reported | GitHub issues |
| Documentation completeness | All public APIs documented | DocFX coverage report |

---

## 14. Out of Scope & Explicit Non-Goals

### Explicit Non-Goals

| Non-Goal | Rationale |
|----------|-----------|
| **Standalone event model** | otel-events does NOT have its own `EventData`, `IOtelEventsSink`, or custom pipeline. It uses OTEL types natively. |
| **Replacing OTEL** | otel-events is an OTEL extension. It does not replace, wrap, or abstract OTEL. |
| **Custom pipeline** | otel-events does NOT implement enrichment, fan-out, or routing. OTEL SDK handles all of this. |
| **ILogger bridge** | OTEL already bridges `ILogger` → `LogRecord`. otel-events doesn't duplicate this. |
| **Log storage / querying** | otel-events produces output — it does not store or query. Use OTEL Collector → Loki/Elasticsearch/etc. |
| **Trace propagation** | OTEL handles W3C trace context propagation. otel-events reads `Activity.Current` but does not propagate. |
| **APM** | otel-events records metrics per-event but is not a full APM. Use OTEL + Datadog/New Relic/etc. |
| **Log rotation / file management** | JSONL goes to stdout. Log rotation is the container/OS responsibility. |
| **Pretty-printing / human-readable output** | Single-line JSONL everywhere. No exceptions. Use `jq` or log viewer tools for human reading. |
| **Configuration-driven event definitions** | Events are ALWAYS defined in YAML schemas at build time. No runtime event creation. |
| **Free-text messages** | Messages are ALWAYS template-generated from schemas. No `logger.Log("arbitrary message")` via otel-events. |
| **Supporting .NET Framework** | .NET 8+ only for runtime packages. Source generators and AOT require modern .NET. |
| **Console UI / structured console output** | No colors, no tables, no interactive console. JSONL only. |
| **Sampling decisions** | Phase 1–2 do not include event sampling. OTEL SDK provides trace/log sampling. otel-events may add schema-aware sampling as a processor in Phase 3. |

### Deferred to Future Versions

| Feature | Target Phase | Notes |
|---------|-------------|-------|
| Advanced event sampling | Phase 3 | `BaseProcessor<LogRecord>` with head/tail sampling strategies (basic severity filtering moved to Phase 1.8, rate limiting moved to Phase 2.8) |
| Schema registry CLI | Phase 3 | `dotnet otel-events validate`, `dotnet otel-events diff` |
| Multi-service shared schemas | Phase 3 | NuGet-packaged schema contracts |
| VS Code extension | Phase 3+ | YAML IntelliSense, event preview |
| Schema migration tooling | Phase 3+ | Automated schema version migration |
| gRPC service events | Phase 3+ | Built-in gRPC request/response events (like HTTP events) |
| Blazor/WASM support | TBD | Browser environment has different constraints |
| Custom OTEL metric exporter | TBD | JSON-formatted metrics output (like `OtelEventsJsonExporter` but for metrics) |

---

## Appendix A: Glossary

| Term | Definition |
|------|-----------|
| **Event** | A discrete, typed, schema-defined occurrence in a system — emitted as an OTEL `LogRecord` with associated metrics |
| **Schema** | YAML file defining events, their fields, metrics, and metadata |
| **Envelope** | The fixed JSON structure that `OtelEventsJsonExporter` writes for every `LogRecord` |
| **Exporter** | An OTEL `BaseExporter<T>` that sends telemetry to a destination. `OtelEventsJsonExporter` is otel-events' custom log exporter. |
| **Processor** | An OTEL `BaseProcessor<T>` that enriches or transforms telemetry in-flight. `OtelEventsCausalityProcessor` is otel-events' custom log processor. |
| **Codegen** | Source generator that creates C# code from YAML schemas — generates `[LoggerMessage]` methods + `Meter` instruments |
| **LogRecord** | OTEL's native log data type (`OpenTelemetry.Logs.LogRecord`). otel-events generates code that emits `LogRecord`s via `ILogger`. |
| **JSONL** | JSON Lines — one JSON object per line, newline-delimited |
| **Causal tree** | Directed graph of events linked by `otel_events.parent_event_id` → `otel_events.event_id` relationships |
| **`[LoggerMessage]`** | .NET source generator attribute that creates high-performance, zero-alloc `ILogger` extension methods |

## Appendix B: Decision Record Summary

| # | Decision | Choice | Alternatives Considered |
|---|----------|--------|------------------------|
| DR-001 | Architecture model | OTEL extension (not standalone) | Standalone library with custom pipeline; OTEL wrapper/abstraction |
| DR-002 | Event emission | `[LoggerMessage]` source generator → native `LogRecord` | Custom `EventData` struct; direct OTEL `Logger.EmitLog()` |
| DR-003 | Metrics emission | Native `Meter`/`Counter<T>`/`Histogram<T>` in generated code | Custom metrics abstraction; OTEL Logger attributes only |
| DR-004 | JSON output | Custom `BaseExporter<LogRecord>` (OtelEventsJsonExporter) | Parallel sink; custom `ILoggerProvider`; `ConsoleExporter` override |
| DR-005 | Causal linking | Custom `BaseProcessor<LogRecord>` (OtelEventsCausalityProcessor) | Application-level middleware; log scope; custom attributes in generated code |
| DR-006 | Event ID generation | Always generate, UUID v7, globally unique, `evt_` prefix | Optional IDs; sequential IDs; hash-based |
| DR-007 | Message format | Template-only from schema, rendered by `[LoggerMessage]` | Free-text allowed; template + override |
| DR-008 | Exception depth | Cap at 5, truncate with `"truncated": true` | Unlimited; cap at 3; cap at 10 |
| DR-009 | Tags | Schema-defined only, rigid | Runtime tags allowed; free-form labels |
| DR-010 | Serializer | System.Text.Json source generators | Newtonsoft.Json; MessagePack; custom |
| DR-011 | Metrics batching | OTEL SDK default batching (configurable) | Custom batching; always immediate |
| DR-012 | Pretty-print | Never — JSONL everywhere | Dev-mode pretty-print; conditional |
| DR-013 | ILogger bridge | Not needed — OTEL provides `OpenTelemetryLoggerProvider` | Custom bridge wrapping third-party ILogger calls |
| DR-014 | .NET version | .NET 8+ (LTS baseline) | .NET 6 support; netstandard2.0 runtime |
| DR-015 | Null handling | Omit field entirely (no `null` values in JSON) | Include `null`; use sentinel values |
| DR-016 | Code gen approach | MSBuild task + incremental source gen | T4 templates; runtime reflection; manual |
| DR-017 | DI registration | Extends `AddOpenTelemetry()` fluent API | Custom `AddOtelEvents()` method; separate DI registration |
| DR-018 | Integration pack naming prefix | `OtelEvents.*` (distinct from core `OtelEvents.*`) | Same `OtelEvents.*` prefix; `OtelEvents.IntegrationPacks.*`; `OtelEvents.Events.*` |
| DR-019 | Integration pack code distribution | Pre-compiled in NuGet (no consumer-side code generation) | Ship YAML only (require consumer to reference `OtelEvents.Schema`); Ship as source package |
| DR-020 | Integration pack meta-package inclusion | NOT included in `OtelEvents` meta-package | Included in meta-package; separate `OtelEvents` meta-package |
| DR-021 | Event ID range separation | Consumer: 1–9999, Integration packs: 10000+ | Shared range with collision detection; prefix-based disambiguation |
| DR-022 | Integration pack `OtelEvents.Causality` dependency | Optional (auto-detected at runtime) | Required; not supported |
| DR-023 | Static Meter vs IMeterFactory | Static `Meter` instances as default (Phase 1); optional `IMeterFactory`-based mode in Phase 2 (2.9) | `IMeterFactory` only; both as equal options. Trade-off: Static Meters are never disposed — acceptable for long-lived services. `IMeterFactory` provides DI-friendly, disposable meters. |
| DR-024 | PII field defaults | PII-capturing options (CaptureClientIp, CaptureUserAgent) default to `false` | Default `true` with opt-out; no PII capture at all |
| DR-025 | Exception detail level | Environment-based defaults: Development=Full, Staging/Production=TypeAndMessage | Always full; always minimal; configurable without environment concept |
| DR-026 | Host/PID metadata emission | Opt-in (`EmitHostInfo = false` by default) | Always emit; conditional on environment |

## Appendix C: File Structure

```
otel-events-dotnet/
├── README.md
├── ARCHITECTURE.md
├── CONTRIBUTING.md
├── SECURITY.md
├── CHANGELOG.md
├── LICENSE (MIT)
├── Directory.Build.props                    # Shared MSBuild properties
├── Directory.Packages.props                 # Central package management
├── OtelEvents.slnx
│
├── src/
│   ├── OtelEvents.Schema/
│   │   ├── OtelEvents.Schema.csproj
│   │   ├── Models/
│   │   │   ├── SchemaDefinition.cs
│   │   │   ├── EventDefinition.cs
│   │   │   ├── FieldDefinition.cs
│   │   │   ├── MetricDefinition.cs
│   │   │   └── EnumDefinition.cs
│   │   ├── Parsing/
│   │   │   ├── SchemaParser.cs
│   │   │   └── SchemaParseException.cs
│   │   ├── Validation/
│   │   │   ├── SchemaValidator.cs
│   │   │   └── ValidationResult.cs
│   │   ├── Merging/
│   │   │   └── SchemaMerger.cs
│   │   ├── CodeGen/
│   │   │   ├── AllSourceGenerator.cs         # Incremental source generator
│   │   │   ├── Emitters/
│   │   │   │   ├── LoggerMessageEmitter.cs   # Generates [LoggerMessage] partial methods
│   │   │   │   ├── ExtensionMethodEmitter.cs # Generates ILogger<T> extension methods
│   │   │   │   ├── MeterEmitter.cs           # Generates Meter/Counter/Histogram statics
│   │   │   │   ├── EnumTypeEmitter.cs        # Generates enum types + ToStringFast()
│   │   │   │   ├── EventSourceTypeEmitter.cs # Generates category marker types
│   │   │   │   └── JsonContextEmitter.cs     # Generates STJ serialization context
│   │   │   └── MSBuild/
│   │   │       └── AllGenerateTask.cs        # MSBuild task alternative
│   │   └── Cli/
│   │       └── ValidateCommand.cs            # dotnet otel-events validate (Phase 3)
│   │
│   ├── OtelEvents.Exporter.Json/
│   │   ├── OtelEvents.Exporter.Json.csproj
│   │   ├── OtelEventsJsonExporter.cs                # BaseExporter<LogRecord>
│   │   ├── OtelEventsJsonExporterOptions.cs         # Configuration (output target, schema version)
│   │   ├── AllJsonEnvelope.cs                # Envelope model for serialization
│   │   ├── ExceptionData.cs                  # Structured exception model
│   │   ├── ExceptionSerializer.cs            # Exception → structured JSON
│   │   ├── AllJsonSerializerContext.cs        # STJ source generator context
│   │   ├── SequenceCounter.cs                # Monotonic per-process seq counter
│   │   └── OtelEventsJsonExporterExtensions.cs      # .AddOtelEventsJsonExporter() extension method
│   │
│   ├── OtelEvents.Causality/
│   │   ├── OtelEvents.Causality.csproj
│   │   ├── OtelEventsCausalityProcessor.cs          # BaseProcessor<LogRecord>
│   │   ├── OtelEventsCausalityContext.cs            # AsyncLocal<string?> for parentEventId
│   │   ├── CausalityScope.cs                # IDisposable scope for setting parent
│   │   ├── Uuid7.cs                          # UUID v7 generator
│   │   └── AllCausalityExtensions.cs         # .AddProcessor<OtelEventsCausalityProcessor>() helpers
│   │
│   ├── OtelEvents.Analyzers/
│   │   ├── OtelEvents.Analyzers.csproj
│   │   ├── ConsoleWriteAnalyzer.cs           # OTEL001
│   │   ├── UntypedILoggerAnalyzer.cs         # OTEL002
│   │   ├── StringInterpolationAnalyzer.cs    # OTEL003
│   │   ├── UndefinedEventAnalyzer.cs         # OTEL004
│   │   ├── UnusedEventAnalyzer.cs            # OTEL005
│   │   ├── UncapturedExceptionAnalyzer.cs    # OTEL006
│   │   ├── DebugWriteAnalyzer.cs             # OTEL007
│   │   └── ReservedPrefixAnalyzer.cs         # OTEL008
│   │
│   └── OtelEvents.Testing/
│       ├── OtelEvents.Testing.csproj
│       ├── InMemoryLogExporter.cs            # BaseExporter<LogRecord> for tests
│       ├── ExportedLogRecord.cs              # Immutable snapshot of LogRecord
│       ├── OtelEventsTestHost.cs                    # Test setup helper
│       └── AssertionExtensions.cs            # Fluent assertions for ExportedLogRecord
│
├── tests/
│   ├── OtelEvents.Schema.Tests/
│   ├── OtelEvents.Exporter.Json.Tests/
│   ├── OtelEvents.Causality.Tests/
│   ├── OtelEvents.Analyzers.Tests/
│   ├── OtelEvents.Integration.Tests/
│   └── OtelEvents.Benchmarks/
│
├── samples/
│   ├── OtelEvents.Samples.WebApi/                   # ASP.NET Core Web API example
│   ├── OtelEvents.Samples.Worker/                   # Background worker example
│   └── OtelEvents.Samples.MinimalApi/               # Minimal API example
│
├── docs/
│   ├── adr/                                  # Architecture Decision Records
│   │   ├── 001-otel-extension-model.md
│   │   ├── 002-logger-message-codegen.md
│   │   ├── 003-message-template-only.md
│   │   └── ...
│   ├── schema-reference.md                   # Full YAML schema reference
│   ├── getting-started.md                    # Quick start guide (assumes OTEL exists)
│   ├── adopting-otel-events-in-existing-project.md   # Migration guide for OTEL-using projects
│   └── ai-investigation.md                   # How otel-events enables AI log analysis
│
├── schemas/
│   └── examples/
│       ├── web-api.otel.yaml                  # Example: Web API events
│       ├── worker.otel.yaml                   # Example: Background worker events
│       └── shared.otel.yaml                   # Example: Shared field definitions
│
└── .github/
    ├── workflows/
    │   ├── ci.yml                            # Build, test, benchmark
    │   ├── release.yml                       # NuGet publish
    │   └── security-scan.yml                 # Dependency + code scanning
    ├── ISSUE_TEMPLATE/
    │   ├── bug_report.yml
    │   ├── feature_request.yml
    │   └── schema_proposal.yml
    ├── pull_request_template.md
    └── dependabot.yml
```

---

## Appendix D: CI/CD Pipeline

### CI Workflow (`ci.yml`)

```yaml
# Triggers: push to main, all PRs
# Branch protection: requires CI pass, 1+ reviewer, no force-push to main
steps:
  1. Checkout
  2. Setup .NET 8 + .NET 9
  3. Restore (with Central Package Management — exact version pins)
  4. Build (Release configuration)
  5. Run analyzers (treat warnings as errors)
  6. Run unit tests (OtelEvents.*.Tests)
  7. Run integration tests (OtelEvents.Integration.Tests)
  8. Run benchmarks (comparison against baseline)
  9. AOT publish test (verify trimming + AOT compatibility)
  10. Generate coverage report (Coverlet → Codecov)
  11. Security scanning:
      a. SAST: CodeQL or Semgrep analysis
      b. Dependency audit: `dotnet list package --vulnerable`
      c. Secret scanning: Gitleaks pre-commit + CI check
  12. SBOM generation: `dotnet sbom generate` (CycloneDX format)
  13. CI matrix: test against minimum OTEL SDK version (1.9.0) AND latest stable
  14. Package NuGet packages (--no-build)
  15. Embed SBOM in NuGet packages
  16. Upload packages as artifacts
```

### Release Workflow (`release.yml`)

```yaml
# Triggers: GitHub Release created (tag v*)
# Protected: only maintainers can create releases
steps:
  1. Full CI pipeline (steps 1–16 above)
  2. Validate semver tag matches Directory.Build.props version
  3. NuGet package signing (code signing certificate via Azure Key Vault or local PFX)
  4. Push to NuGet.org (signed packages)
  5. Create GitHub Release with changelog and SBOM attachment
  6. (Samples) Build sample Dockerfiles with cosign signing (see §17)
```

### Branch Protection Rules

| Rule | Setting |
|------|---------|
| Required status checks | CI workflow must pass |
| Required reviewers | Minimum 1 reviewer |
| Dismiss stale reviews | Enabled |
| Require up-to-date branches | Enabled |
| Force push | Disabled on `main` |
| Delete branch on merge | Enabled |
| Signed commits | Recommended (not required) |

### Dependency Management

| Practice | Implementation |
|----------|---------------|
| Central Package Management | `Directory.Packages.props` with exact version pins |
| Automated updates | Dependabot configured for weekly NuGet updates |
| Vulnerable dependency detection | `dotnet list package --vulnerable` in CI |
| OTEL SDK version range | `[1.9.0, 2.0.0)` — allows patches, blocks breaking changes |
| CI matrix testing | Min version (1.9.0) + latest stable version of OTEL SDK |


---

## Appendix E: Adoption Story

### For Teams Already Using OTEL

```
Step 1: dotnet add package OtelEvents.Schema          (adds code generator)
Step 2: Create events.otel.yaml                 (define your events)
Step 3: Build                                  (generated code appears)
Step 4: Replace manual ILogger calls with      (use generated extension methods)
        generated extension methods
Step 5: (Optional) dotnet add package          (AI-optimized JSONL output)
        OtelEvents.Exporter.Json
Step 6: (Optional) dotnet add package          (causal event linking)
        OtelEvents.Causality
Step 7: (Optional) dotnet add package          (compile-time enforcement)
        OtelEvents.Analyzers
```

**Nothing breaks. Nothing changes.** The generated code uses `ILogger<T>` and `Meter`/`Counter`/`Histogram` — the same types the team already uses. otel-events just generates them from a schema instead of writing them by hand.

### For Greenfield Projects

```
Step 1: dotnet add package OtelEvents          (meta-package — everything)
Step 2: Configure AddOpenTelemetry() in        (standard OTEL setup)
        Program.cs
Step 3: Create events.otel.yaml                 (define your events)
Step 4: Build and use generated events         (type-safe, schema-enforced)
```

### Migration Path: Manual → otel-events

| What you have today | What otel-events replaces | What stays the same |
|--------------------|--------------------|---------------------|
| Hand-written `[LoggerMessage]` | otel-events generates `[LoggerMessage]` from YAML | The `LogRecord`s are identical — OTEL pipeline sees no difference |
| Hand-written `Meter`/`Counter`/`Histogram` | otel-events generates metric instruments from YAML | Metrics recordings are identical — OTEL pipeline sees no difference |
| OTEL `AddOtlpExporter()` | Nothing — keep your existing exporters | OTLP export works exactly the same |
| `builder.Logging.AddFilter(...)` | Nothing — keep your existing filters | Severity filtering works exactly the same |
| Third-party library `ILogger` output | Nothing — OTEL already captures it | Third-party logs flow through the same pipeline |

---

*This specification was authored on 2025-07-09, updated on 2025-07-10 to reflect the architectural pivot from standalone library to OTEL extension model, and updated on 2025-07-11 to incorporate Security Guardian (14 findings) and Platform Guardian (17 findings) review amendments including §16 Security & Privacy Requirements, §17 Container & Kubernetes Deployment Guide, PII classification framework, environment profiles, OTEL Collector topology, and Kubernetes deployment manifests. It is the foundational reference document for the otel-events project and should be maintained alongside the codebase as the single source of truth for project scope, design decisions, and standards.*
---

## 16. Security & Privacy Requirements

This section addresses security findings from the Security Guardian review and establishes the threat model, PII classification framework, and defense-in-depth measures for all otel-events components.

### 16.1 Threat Model

#### Trust Boundaries

```
┌──────────────────────────────────────────────────────────────────┐
│  TRUST BOUNDARY: Application Process                             │
│                                                                  │
│  ┌──────────────┐     ┌─────────────────┐     ┌──────────────┐  │
│  │ Application   │────▶│ otel-events Generated   │────▶│ OTEL SDK     │  │
│  │ Code          │     │ Code            │     │ Pipeline     │  │
│  │ (trusted)     │     │ (trusted)       │     │ (trusted)    │  │
│  └──────────────┘     └─────────────────┘     └──────┬───────┘  │
│                                                       │          │
│  ┌──────────────┐     ┌─────────────────┐            │          │
│  │ Third-party   │────▶│ ILogger bridge  │────────────┘          │
│  │ Libraries     │     │ (OTEL SDK)      │                       │
│  │ (semi-trusted)│     │                 │  ⚠ May emit PII      │
│  └──────────────┘     └─────────────────┘                       │
│                                                                  │
│  ┌──────────────┐                                                │
│  │ YAML Schema  │  ⚠ Parsed at build time — resource limits     │
│  │ Files         │    enforced (§6 parsing limits)               │
│  │ (untrusted    │                                                │
│  │  input)       │                                                │
│  └──────────────┘                                                │
└──────────────────────────────────────────────────────────────────┘
         │ stdout JSONL          │ OTLP (gRPC/HTTP)
         ▼                       ▼
┌─────────────────┐    ┌─────────────────┐
│ Log Collector    │    │ OTEL Collector  │
│ (Fluent Bit,     │    │ (see §17)       │
│  Vector, etc.)   │    │                 │
│ (trusted infra)  │    │ (trusted infra) │
└─────────────────┘    └─────────────────┘
```

#### Threat Vectors

| Threat | Vector | Mitigation |
|--------|--------|------------|
| **PII leakage in logs** | User-Agent, Client IP, user IDs emitted to log storage | Sensitivity classification (§6), default `false` for PII capture, `EnvironmentProfile` redaction |
| **Information disclosure via exceptions** | Stack traces expose file paths, internal class names, line numbers | `ExceptionDetailLevel` — `TypeAndMessage` in Production (no stack traces) |
| **Information disclosure via metadata** | `otel_events.host` and `otel_events.pid` expose infrastructure details | Opt-in only (`EmitHostInfo = false` default) |
| **Third-party library PII leakage** | Non-otel-events `ILogger` calls may include connection strings, tokens, PII | `AttributeAllowlist`/`AttributeDenylist`, `RedactPatterns` regex filtering |
| **Schema injection / DoS** | Malicious YAML files with excessive size, nesting, or YAML bombs | Safe YAML loading, resource limits (1 MB, 500 events, 50 fields, depth 20) |
| **Reserved prefix hijacking** | Application code setting `otel_events.*` attributes to spoof metadata | Runtime stripping of non-otel-events `otel_events.*` attributes in exporter |
| **Credential exposure in field values** | Connection strings, API keys, bearer tokens in attribute values | `sensitivity: credential` classification, regex-based `RedactPatterns`, defense-in-depth value sanitization |
| **Unbounded attribute values** | Extremely long string values causing memory pressure or log bloat | `MaxAttributeValueLength` (default: 4096), per-field `maxLength` |
| **AsyncLocal trust in causality** | `OtelEventsCausalityContext` uses `AsyncLocal` — any code in the async flow can set `parentEventId` | Documented trust assumption: causal context is set by trusted code within the process. Cross-process causality requires trace context (OTEL propagation), not `AsyncLocal`. |

### 16.2 PII Classification Framework

The `sensitivity` field attribute (§6) provides a compile-time classification system. At runtime, the exporter applies redaction based on the `EnvironmentProfile`:

| EnvironmentProfile | `public` | `internal` | `pii` | `credential` |
|--------------------|----------|------------|-------|--------------|
| `Development` | Visible | Visible | Visible | **REDACTED** |
| `Staging` | Visible | Visible | **REDACTED** | **REDACTED** |
| `Production` | Visible | **REDACTED** | **REDACTED** | **REDACTED** |

**Override behavior:** Individual fields can be explicitly opted in or out of redaction:

```csharp
logging.AddOtelEventsJsonExporter(options =>
{
    options.EnvironmentProfile = OtelEventsEnvironmentProfile.Production;

    // Override: allow userId (pii) in Production for this specific service
    // Requires documented legal basis (e.g., audit trail requirement)
    options.SensitivityOverrides = new Dictionary<string, bool>
    {
        ["userId"] = true,   // Allow despite pii classification
        ["hostName"] = false, // Redact despite internal classification
    };
});
```

### 16.3 Environment Profiles — Defaults Summary

| Setting | Development | Staging | Production |
|---------|-------------|---------|------------|
| `ExceptionDetailLevel` | `Full` | `TypeAndMessage` | `TypeAndMessage` |
| Stack trace file paths | Included | **Omitted** | **Omitted** |
| `EmitHostInfo` | `true` (overridable) | `false` | `false` |
| `pii` fields | Visible | **Redacted** | **Redacted** |
| `internal` fields | Visible | Visible | **Redacted** |
| `credential` fields | **Redacted** | **Redacted** | **Redacted** |
| `RedactPatterns` | Applied | Applied | Applied |
| `MaxAttributeValueLength` | 4096 | 4096 | 4096 |

### 16.4 Reserved Prefix Runtime Enforcement

At build time, the schema validator rejects field names starting with `otel_events.` (rule `OTEL_SCHEMA_011`). At runtime, the exporter enforces this for non-otel-events `LogRecord`s:

1. During `Export()`, iterate over `LogRecord.Attributes`.
2. Any attribute with key starting with `otel_events.` that was NOT set by `OtelEventsCausalityProcessor` or `OtelEventsJsonExporter` itself is **stripped** (removed from the exported envelope).
3. Increment `otel_events.exporter.json.reserved_prefix_stripped` counter for each occurrence.
4. This prevents application code or third-party libraries from spoofing otel-events metadata fields.

### 16.5 Defense-in-Depth Value Sanitization

As a last line of defense, the exporter applies pattern-based value sanitization to otel-events attribute values (not just non-otel-events LogRecords). This catches connection strings, tokens, and API keys that might be accidentally included in schema-defined fields:

**Default patterns (always active):**

```regex
# Connection strings
Server=.*;(User Id|Password|Pwd)=.*
Data Source=.*;(User ID|Password)=.*

# Bearer tokens
Bearer\s+[A-Za-z0-9\-._~+/]+=*

# Common API key patterns
(api[_-]?key|apikey|access[_-]?token|secret[_-]?key)\s*[=:]\s*\S{16,}
```

Values matching these patterns are replaced with `"[REDACTED:pattern]"`. This is applied AFTER sensitivity-based redaction and is non-configurable (always on as defense-in-depth).

### 16.6 Regulatory Compliance Considerations

otel-events does not enforce specific regulatory requirements but provides the mechanisms for compliance:

| Regulation | otel-events Mechanism |
|------------|---------------|
| **GDPR** (EU) | `sensitivity: pii` classification → redaction in Production; `CaptureClientIp`/`CaptureUserAgent` default `false`; documented data retention is the responsibility of the log storage backend |
| **CCPA** (California) | Same PII controls as GDPR apply |
| **HIPAA** (US Healthcare) | `sensitivity: credential` for PHI fields; teams must configure `EnvironmentProfile = Production` and audit `SensitivityOverrides`; otel-events does not provide encryption at rest (log storage responsibility) |
| **SOC 2** | Audit trail via `otel_events.event_id`, `otel_events.seq`, `traceId`; `otel_events.host`/`otel_events.pid` opt-in for attribution; SBOM generation in CI |

**Decision (OQ-PG-03):** otel-events provides PII classification and redaction mechanisms. Specific regulatory compliance configuration (which fields to redact, data retention, encryption at rest) is the responsibility of the deploying organization. otel-events' defaults are privacy-preserving (PII redacted in Production).

### 16.7 OWASP Reference Mapping

| OWASP Category | otel-events Mitigation |
|----------------|----------------|
| **A01:2021 — Broken Access Control** | `CaptureClientIp = false` by default; `sensitivity: pii` classification |
| **A04:2021 — Insecure Design** | `ExceptionDetailLevel`; no file paths in Production; `EmitHostInfo = false`; `sensitivity` framework |
| **A09:2021 — Security Logging and Monitoring Failures** | Structured, schema-defined events; `ExportResult.Failure` on I/O errors; self-telemetry metrics |

---

## 17. Container & Kubernetes Deployment Guide

This section provides deployment guidance for otel-events-instrumented .NET applications in containerized and Kubernetes environments. It addresses OTEL Collector topology, container specifications, resource sizing, TLS configuration, and operational recommendations.

### 17.1 OTEL Collector Deployment Topology

#### Recommended Architecture: DaemonSet `filelog` Receiver

The recommended deployment pattern for otel-events uses the OTEL Collector's `filelog` receiver to collect stdout JSONL output, avoiding dual export overhead:

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

**Decision (OQ-PG-01):** The recommended OTEL Collector topology is a **DaemonSet** on each Kubernetes node for log collection (via `filelog` receiver reading container stdout). For OTLP metrics and traces, use a **Gateway** Collector deployment behind a load balancer.

**Decision (OQ-PG-02):** The expected log collection agent for stdout JSONL is the **OTEL Collector** with the `filelog` receiver. Alternatives (Fluent Bit, Vector, Fluentd) are compatible but the Collector is preferred for its native OTEL support.

#### OTEL Collector Configuration for otel-events Envelope

```yaml
# otel-collector-config.yaml
receivers:
  filelog:
    include:
      - /var/log/pods/*/*/*.log
    operators:
      # Parse container runtime wrapper (CRI format)
      - type: regex_parser
        regex: '^(?P<time>[^ ]+) (?P<stream>stdout|stderr) (?P<logtag>[^ ]*) (?P<log>.*)$'
        timestamp:
          parse_from: attributes.time
          layout: '%Y-%m-%dT%H:%M:%S.%LZ'
      # Parse otel-events JSON envelope
      - type: json_parser
        parse_from: attributes.log
        timestamp:
          parse_from: attributes.timestamp
          layout: '%Y-%m-%dT%H:%M:%S.%fZ'
      # Move otel-events envelope fields to OTEL LogRecord attributes
      - type: move
        from: attributes.event
        to: attributes["event.name"]
      - type: move
        from: attributes.severity
        to: attributes["severity_text"]
      - type: severity_parser
        parse_from: attributes.severityNumber
        mapping:
          trace: 1
          debug: 5
          info: 9
          warn: 13
          error: 17
          fatal: 21
    include_file_name: false
    include_file_path: true
    storage: file_storage

  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317
        tls:
          cert_file: /etc/otel/certs/tls.crt
          key_file: /etc/otel/certs/tls.key
      http:
        endpoint: 0.0.0.0:4318
        tls:
          cert_file: /etc/otel/certs/tls.crt
          key_file: /etc/otel/certs/tls.key

processors:
  batch:
    timeout: 5s
    send_batch_size: 1000
  memory_limiter:
    check_interval: 5s
    limit_mib: 512
    spike_limit_mib: 128

exporters:
  otlp:
    endpoint: "otel-gateway.monitoring.svc.cluster.local:4317"
    tls:
      insecure: false
      ca_file: /etc/otel/certs/ca.crt
  loki:
    endpoint: "http://loki.monitoring.svc.cluster.local:3100/loki/api/v1/push"

service:
  pipelines:
    logs:
      receivers: [filelog]
      processors: [memory_limiter, batch]
      exporters: [otlp, loki]
```

#### Deployment Patterns Summary

| Pattern | Logs | Metrics | Traces | When to Use |
|---------|------|---------|--------|-------------|
| **DaemonSet (filelog)** | ✅ stdout → filelog | ❌ | ❌ | Log collection from all pods on a node |
| **Gateway (OTLP)** | Optional | ✅ | ✅ | Central aggregation of metrics/traces via OTLP |
| **Sidecar** | ✅ | ✅ | ✅ | Per-pod isolation (higher resource cost) |
| **DaemonSet + Gateway** | ✅ (DaemonSet) | ✅ (Gateway) | ✅ (Gateway) | **Recommended** — best balance of resource efficiency and reliability |

### 17.2 TLS Configuration for OTLP Endpoints

When using `AddOtlpExporter()` for direct OTLP export (without filelog), configure TLS:

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

**Environment variables for TLS (set via K8s Secret/ConfigMap):**

| Variable | Description |
|----------|-------------|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Collector endpoint (e.g., `https://collector:4317`) |
| `OTEL_EXPORTER_OTLP_CERTIFICATE` | Path to CA certificate for verifying Collector |
| `OTEL_EXPORTER_OTLP_CLIENT_CERTIFICATE` | Path to client certificate for mTLS |
| `OTEL_EXPORTER_OTLP_CLIENT_KEY` | Path to client private key for mTLS |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | `grpc` or `http/protobuf` |

### 17.3 Sample Dockerfile

```dockerfile
# ─── Build Stage ─────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/ src/
RUN dotnet restore src/MyService/MyService.csproj
RUN dotnet publish src/MyService/MyService.csproj \
    -c Release \
    -o /app \
    --no-restore \
    /p:PublishTrimmed=true \
    /p:PublishSingleFile=true

# Generate SBOM
RUN dotnet tool install --global Microsoft.Sbom.DotNetTool
RUN dotnet sbom generate -b /app -bc src/MyService -pn MyService -pv 1.0.0 -ps MyCompany

# ─── Runtime Stage (distroless, non-root) ────────────────────────
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-noble-chiseled-extra AS runtime

USER $APP_UID
WORKDIR /app
COPY --from=build /app .
COPY --from=build /app/_manifest /app/_manifest

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD ["wget", "--no-verbose", "--tries=1", "--spider", "http://localhost:8080/health"]

EXPOSE 8080
ENTRYPOINT ["./MyService"]
```

**Key security properties:**
- **Distroless base image** (`chiseled`) — minimal attack surface, no shell, no package manager
- **Non-root user** — container runs as non-privileged user
- **Single-file publish** — reduces writable file surface
- **SBOM embedded** — software bill of materials for vulnerability tracking

### 17.4 Kubernetes Deployment Manifest

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: my-service
  namespace: production
  labels:
    app: my-service
    version: v1.0.0
spec:
  replicas: 3
  selector:
    matchLabels:
      app: my-service
  template:
    metadata:
      labels:
        app: my-service
        version: v1.0.0
    spec:
      securityContext:
        runAsNonRoot: true
        runAsUser: 1654
        runAsGroup: 1654
        fsGroup: 1654
        seccompProfile:
          type: RuntimeDefault
      containers:
        - name: my-service
          image: ghcr.io/myorg/my-service:v1.0.0
          ports:
            - containerPort: 8080
              protocol: TCP
          securityContext:
            allowPrivilegeEscalation: false
            readOnlyRootFilesystem: true
            capabilities:
              drop: ["ALL"]
          resources:
            requests:
              cpu: 100m
              memory: 128Mi
            limits:
              cpu: 500m
              memory: 512Mi
          env:
            - name: OTELEVENTS__EnvironmentProfile
              value: "Production"
            - name: OTELEVENTS__EmitHostInfo
              value: "false"
            - name: OTEL_SERVICE_NAME
              value: "my-service"
            - name: OTEL_RESOURCE_ATTRIBUTES
              value: "deployment.environment=production,service.version=v1.0.0"
            - name: OTEL_EXPORTER_OTLP_ENDPOINT
              value: "https://otel-collector.monitoring.svc.cluster.local:4317"
            - name: OTEL_EXPORTER_OTLP_PROTOCOL
              value: "grpc"
            - name: OTEL_EXPORTER_OTLP_CERTIFICATE
              value: "/etc/otel/certs/ca.crt"
          volumeMounts:
            - name: otel-certs
              mountPath: /etc/otel/certs
              readOnly: true
            - name: tmp
              mountPath: /tmp
          livenessProbe:
            httpGet:
              path: /health
              port: 8080
            initialDelaySeconds: 10
            periodSeconds: 30
          readinessProbe:
            httpGet:
              path: /ready
              port: 8080
            initialDelaySeconds: 5
            periodSeconds: 10
          startupProbe:
            httpGet:
              path: /health
              port: 8080
            initialDelaySeconds: 5
            periodSeconds: 5
            failureThreshold: 12
      volumes:
        - name: otel-certs
          secret:
            secretName: otel-tls-certs
        - name: tmp
          emptyDir:
            sizeLimit: 100Mi
      terminationGracePeriodSeconds: 30
---
apiVersion: policy/v1
kind: PodDisruptionBudget
metadata:
  name: my-service-pdb
  namespace: production
spec:
  minAvailable: 2
  selector:
    matchLabels:
      app: my-service
---
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: my-service-hpa
  namespace: production
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: my-service
  minReplicas: 3
  maxReplicas: 20
  metrics:
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: 70
    - type: Resource
      resource:
        name: memory
        target:
          type: Utilization
          averageUtilization: 80
```

### 17.5 Resource Sizing Recommendations

| Throughput (events/sec) | CPU Request | CPU Limit | Memory Request | Memory Limit | Notes |
|------------------------|-------------|-----------|----------------|--------------|-------|
| < 1,000 | 50m | 200m | 64 Mi | 256 Mi | Small service, low event volume |
| 1,000–10,000 | 100m | 500m | 128 Mi | 512 Mi | Typical microservice |
| 10,000–50,000 | 250m | 1000m | 256 Mi | 1 Gi | High-throughput service |
| 50,000–100,000 | 500m | 2000m | 512 Mi | 2 Gi | Event-heavy service; monitor GC pressure |
| > 100,000 | 1000m+ | 4000m+ | 1 Gi+ | 4 Gi+ | Benchmark-specific; consider event sampling (Phase 1.8/2.8) |

**Notes:**
- otel-events overhead is ~500ns per event (log + metrics). At 100K events/s, otel-events consumes ~50ms of CPU per second.
- Memory overhead is dominated by OTEL SDK batching buffers, not otel-events components.
- Allocation rate at 100K events/s: ~24.4 MB/s (256 bytes/event). Monitor Gen2 GC collections — target < 3/min.
- The `Utf8JsonWriter` uses `ArrayPool<byte>.Shared` for buffer pooling. Pool size scales with throughput.

### 17.6 Network Policy

```yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: my-service-netpol
  namespace: production
spec:
  podSelector:
    matchLabels:
      app: my-service
  policyTypes:
    - Egress
  egress:
    - to:
        - namespaceSelector:
            matchLabels:
              kubernetes.io/metadata.name: monitoring
          podSelector:
            matchLabels:
              app: otel-collector
      ports:
        - protocol: TCP
          port: 4317
    - to:
        - namespaceSelector: {}
      ports:
        - protocol: UDP
          port: 53
        - protocol: TCP
          port: 53
```

### 17.7 Operational Decisions

**Decision (OQ-PG-04):** otel-events publishes **documentation and sample manifests only** — not a reference Helm chart. Helm charts are highly organization-specific (naming conventions, label standards, ingress controllers). The sample manifests in this section serve as a starting point.

**Decision (OQ-PG-05):** The default Meter creation strategy is **static `Meter` instances** (Phase 1). Optional `IMeterFactory`-based mode is available in Phase 2 (feature 2.9) for teams that need DI-friendly, disposable meters. Static Meters are acceptable for long-lived service processes but are not ideal for test scenarios. See DR-023 in Appendix B.

---

## Open Questions

### Resolved (Security & Platform Guardian Review)

| ID | Question | Decision |
|----|----------|----------|
| OQ-PG-01 | Intended OTEL Collector deployment topology? | DaemonSet for logs (filelog receiver), Gateway for OTLP metrics/traces. See §17.1. |
| OQ-PG-02 | Expected log collection agent for stdout JSONL? | OTEL Collector with `filelog` receiver (preferred). Fluent Bit, Vector compatible. See §17.1. |
| OQ-PG-03 | Regulatory compliance requirements (GDPR, CCPA, HIPAA)? | otel-events provides PII classification and redaction mechanisms; specific compliance configuration is deployer responsibility. See §16.6. |
| OQ-PG-04 | Should library publish reference Helm chart or docs-only? | Docs and sample manifests only — Helm charts are organization-specific. See §17.7. |
| OQ-PG-05 | Static Meter vs IMeterFactory default? | Static Meter as Phase 1 default; optional IMeterFactory mode in Phase 2 (2.9). See DR-023. |

### Open (Integration Packs — unchanged)

- [ ] **OQ-IP-01:** Should integration packs emit events at `DEBUG` or `INFO` severity by default?
- [ ] **OQ-IP-02:** Should `OtelEvents.Azure.CosmosDb` use `DiagnosticListener` or `RequestHandler`?
- [ ] **OQ-IP-03:** Should integration packs support `appsettings.json` configuration?
- [ ] **OQ-IP-04:** Should `OtelEvents.AspNetCore` capture request/response bodies as opt-in fields?
- [ ] **OQ-IP-05:** Should there be an `OtelEvents` meta-package?
- [ ] **OQ-IP-06:** Package naming — confirm `OtelEvents.*` prefix.
- [ ] **OQ-IP-07:** Should integration pack meters use static `Meter` or DI-injected `IMeterFactory`? (See resolved OQ-PG-05 — follow same decision as core.)


---

## Product Owner Guardian — Specification Ready

### For the Default Agent
The specification above is complete and ready to be saved as the project's foundational document.

1. **Save as** `SPECIFICATION.md` in the project root (`/Users/vbomfim/dev/otel-events-dotnet/SPECIFICATION.md`) — replacing the existing v1 specification
2. **Additionally**, consider creating these follow-up items:
   - A GitHub issue for **Phase 1 Epic** with sub-issues for each MVP feature (1.1–1.7)
   - An initial `README.md` scaffolded from the overview section
   - An `ARCHITECTURE.md` derived from sections 4 and 9
   - ADR files for the 17 confirmed decisions in Appendix B (especially DR-001: OTEL extension model)

### INVEST Assessment
The specification itself is NOT a ticket — it is a **project specification document**. Individual implementation tickets should be carved from Phase 1 features (1.1–1.7), each independently deliverable and testable:

| Feature | Estimated Size | Dependencies |
|---------|---------------|-------------|
| 1.1 Schema parser | Small (1 sprint) | None |
| 1.2 Code generator | Medium (1-2 sprints) | 1.1 |
| 1.3 JSON log exporter | Small (1 sprint) | None (uses OTEL LogRecord, not otel-events types) |
| 1.4 Causality processor | Small (1 sprint) | None (uses OTEL LogRecord, not otel-events types) |
| 1.5 Exception serialization | Small (1 sprint) | 1.3 (used by exporter) |
| 1.6 DI integration | Small (1 sprint) | 1.3, 1.4 |
| 1.7 Schema validation | Small (1 sprint) | 1.1 |

**Key improvement over v1:** Features 1.3 (exporter) and 1.4 (processor) have NO dependency on the code generator (1.2). They work on any `LogRecord`, not just otel-events generated ones. This means 3 workstreams can proceed in parallel:
- **Stream A:** Schema parser → Code generator → Schema validation (1.1 → 1.2 → 1.7)
- **Stream B:** JSON exporter → Exception serialization (1.3 → 1.5)
- **Stream C:** Causality processor (1.4)
## 15. Integration Packs

### 15.1 Overview & Design Philosophy

Integration packs are **pre-built NuGet packages** that provide curated YAML schemas, pre-generated code, and runtime middleware/interceptors for common .NET technologies. They deliver the core otel-events value proposition — schema-defined, structured, AI-optimized events — with **zero YAML authoring** by the consumer.

> **One package, consistent schema-defined events for your entire stack.**

#### What Integration Packs Are

| Property | Description |
|----------|-------------|
| **Pre-built schemas** | Each pack bundles a `.otel.yaml` schema file as an embedded resource (for documentation and tooling inspection) |
| **Pre-compiled code** | Generated `[LoggerMessage]` methods, `Meter`/`Counter`/`Histogram` instruments, and extension methods are compiled into the NuGet package — the consumer does NOT need `OtelEvents.Schema` at build time |
| **Runtime glue** | Middleware, interceptors, diagnostic listeners, or publishers that automatically emit schema-defined events from the target technology |
| **Complementary** | Works alongside existing OTEL auto-instrumentation (traces + metrics) — adds the **structured event** layer that OTEL does not provide |
| **Optional** | Each pack is independently installable. No pack depends on another pack. |

#### What Integration Packs Are NOT

| Non-Goal | Rationale |
|----------|-----------|
| Not a replacement for OTEL instrumentation | OTEL provides traces and metrics. Integration packs add schema-defined structured log events. Both coexist. |
| Not a wrapper/abstraction over SDKs | Packs observe SDK behavior via middleware/interceptors/listeners — they do NOT replace or wrap the underlying SDK. |
| Not mandatory | Teams can define their own events in YAML. Integration packs are a convenience for common infrastructure events. |

#### Naming Convention

Integration packs use the `OtelEvents.*` NuGet package prefix to distinguish them from the core `OtelEvents.*` infrastructure packages:

| Prefix | Purpose | Examples |
|--------|---------|---------|
| `OtelEvents.*` | Core infrastructure (schema parser, exporter, processor, analyzers, testing) | `OtelEvents.Schema`, `OtelEvents.Exporter.Json`, `OtelEvents.Causality` |
| `OtelEvents.*` | Pre-built integration packs for specific .NET technologies | `OtelEvents.AspNetCore`, `OtelEvents.Grpc` |

### 15.2 Architecture

#### How Integration Packs Work

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        PACK BUILD TIME (pack author)                        │
│                                                                             │
│  ┌─────────────────┐    ┌──────────────┐    ┌──────────────────────────┐    │
│  │  Pack YAML      │───▶│  OtelEvents.Schema  │───▶│  Pre-generated C#        │    │
│  │  Schema          │    │  (build-time) │    │  • [LoggerMessage] methods│    │
│  │  (embedded)      │    │              │    │  • Meter/Counter/Histogram│    │
│  └─────────────────┘    └──────────────┘    │  • Extension methods      │    │
│                                              └──────────────────────────┘    │
│                                                                             │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │  Runtime Glue (pack-specific)                                        │   │
│  │  • ASP.NET Core Middleware (OtelEvents.AspNetCore)                   │   │
│  │  • gRPC Interceptor (OtelEvents.Grpc)                               │   │
│  │  • DiagnosticListener (OtelEvents.Azure.*)                          │   │
│  │  • IHealthCheckPublisher (OtelEvents.HealthChecks)                  │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│                                                                             │
│  All compiled into: OtelEvents.AspNetCore.nupkg (etc.)                      │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                        CONSUMER RUNTIME                                     │
│                                                                             │
│  ┌────────────────────────────┐                                             │
│  │ App Code (Program.cs)      │                                             │
│  │                            │                                             │
│  │ builder.Services           │                                             │
│  │   .AddOtelEventsAspNetCore │  ← one-line registration                    │
│  │   ();                      │                                             │
│  └─────────────┬──────────────┘                                             │
│                │                                                            │
│                ▼                                                            │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │  Pack Middleware / Interceptor                                       │   │
│  │  • Observes request/response lifecycle                              │   │
│  │  • Calls pre-generated ILogger extension methods                    │   │
│  │  • Records pre-generated Meter instruments                          │   │
│  │  • (Optional) Creates OtelEventsCausalityContext scope                     │   │
│  └────────────────┬─────────────────────────────────────────────────────┘   │
│                   │                                                         │
│                   ▼                                                         │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  Standard OTEL SDK Pipeline                                         │    │
│  │  OtelEventsCausalityProcessor → OtelEventsJsonExporter + OTLP Exporter            │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────────┘
```

#### Event ID Reservation Ranges

Integration pack event IDs use reserved ranges to avoid collisions with consumer-defined schemas. Consumer schemas should use IDs in the 1–9999 range. Integration packs use 10000+.

| Range | Package | Description |
|-------|---------|-------------|
| 1–9999 | Consumer schemas | Application-defined events |
| 10001–10099 | `OtelEvents.AspNetCore` | HTTP request lifecycle events |
| 10101–10199 | `OtelEvents.Grpc` | gRPC call lifecycle events |
| 10201–10299 | `OtelEvents.Azure.CosmosDb` | CosmosDB operation events |
| 10301–10399 | `OtelEvents.Azure.Storage` | Azure Storage operation events |
| 10401–10499 | `OtelEvents.HealthChecks` | Health check events |
| 10500–19999 | Reserved | Future integration packs |

#### Meter Name Convention

Each integration pack registers its own OTEL `Meter` with a namespaced name:

| Package | Meter Name |
|---------|------------|
| `OtelEvents.AspNetCore` | `OtelEvents.AspNetCore` |
| `OtelEvents.Grpc` | `OtelEvents.Grpc` |
| `OtelEvents.Azure.CosmosDb` | `OtelEvents.Azure.CosmosDb` |
| `OtelEvents.Azure.Storage` | `OtelEvents.Azure.Storage` |
| `OtelEvents.HealthChecks` | `OtelEvents.HealthChecks` |

Consumers register all integration pack meters via: `metrics.AddMeter("OtelEvents.*")`

---

### 15.3 OtelEvents.AspNetCore

#### Package & Dependencies

```
OtelEvents.AspNetCore (runtime)
├── Microsoft.AspNetCore.Http (>= 8.0)     — ASP.NET Core HTTP abstractions
├── OpenTelemetry (>= 1.9)                 — OTEL SDK types
├── OtelEvents.Causality (>= 1.0) [optional]      — Causal scope per request (auto-detected)
└── (no dependency on OtelEvents.Schema — code is pre-generated)
```

**Target frameworks:** `net8.0`, `net9.0`

#### YAML Schema

```yaml
# ─── OtelEvents.AspNetCore — Bundled Schema ─────────────────────────────
# This schema is embedded in the NuGet package for documentation and
# tooling inspection. The C# code is pre-compiled — consumers do NOT
# need OtelEvents.Schema to use this pack.

schema:
  name: "OtelEvents.AspNetCore"
  version: "1.0.0"
  namespace: "OtelEvents.AspNetCore.Events"
  meterName: "OtelEvents.AspNetCore"
  description: "Schema-defined HTTP request lifecycle events for ASP.NET Core"

fields:
  httpMethod:
    type: string
    description: "HTTP request method (GET, POST, PUT, DELETE, PATCH, HEAD, OPTIONS)"
    index: true

  httpPath:
    type: string
    description: "Request path (route template when available, e.g., /api/orders/{id})"
    index: true

  httpStatusCode:
    type: int
    description: "HTTP response status code"
    index: true

  durationMs:
    type: double
    description: "Request processing duration in milliseconds"
    unit: "ms"

  contentLength:
    type: long
    description: "Request or response content length in bytes"
    unit: "bytes"

  userAgent:
    type: string
    description: "User-Agent header value"

  clientIp:
    type: string
    description: "Client IP address (from X-Forwarded-For or RemoteIpAddress)"

  httpRoute:
    type: string
    description: "Matched route template (e.g., /api/orders/{id})"
    index: true

  requestId:
    type: string
    description: "ASP.NET Core HttpContext.TraceIdentifier"
    index: true

  errorType:
    type: string
    description: "Exception type name for failed requests"
    index: true

events:
  http.request.received:
    id: 10001
    severity: INFO
    description: "An HTTP request was received by the service. Emitted at the start of request processing."
    message: "HTTP {httpMethod} {httpPath} received from {clientIp}"
    fields:
      httpMethod:
        ref: httpMethod
        required: true
      httpPath:
        ref: httpPath
        required: true
      userAgent:
        ref: userAgent
        required: false
      clientIp:
        ref: clientIp
        required: false
      contentLength:
        ref: contentLength
        required: false
      requestId:
        ref: requestId
        required: true
    metrics:
      otel.http.request.received.count:
        type: counter
        unit: "requests"
        description: "Total HTTP requests received"
        labels:
          - httpMethod
    tags:
      - http
      - aspnetcore

  http.request.completed:
    id: 10002
    severity: INFO
    description: "An HTTP request completed processing successfully (1xx–4xx status codes)."
    message: "HTTP {httpMethod} {httpPath} completed with {httpStatusCode} in {durationMs}ms"
    fields:
      httpMethod:
        ref: httpMethod
        required: true
      httpPath:
        ref: httpPath
        required: true
      httpRoute:
        ref: httpRoute
        required: false
      httpStatusCode:
        ref: httpStatusCode
        required: true
      durationMs:
        ref: durationMs
        required: true
      contentLength:
        ref: contentLength
        required: false
      requestId:
        ref: requestId
        required: true
    metrics:
      otel.http.request.duration:
        type: histogram
        unit: "ms"
        description: "HTTP request processing duration"
        buckets: [5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000]
      otel.http.response.count:
        type: counter
        unit: "responses"
        description: "Total HTTP responses by status code"
        labels:
          - httpMethod
          - httpStatusCode
      otel.http.response.size:
        type: histogram
        unit: "bytes"
        description: "HTTP response body size"
        buckets: [100, 1000, 10000, 100000, 1000000, 10000000]
    tags:
      - http
      - aspnetcore

  http.request.failed:
    id: 10003
    severity: ERROR
    description: "An HTTP request failed with an unhandled exception (5xx or exception thrown)."
    message: "HTTP {httpMethod} {httpPath} failed with {errorType} after {durationMs}ms"
    exception: true
    fields:
      httpMethod:
        ref: httpMethod
        required: true
      httpPath:
        ref: httpPath
        required: true
      httpRoute:
        ref: httpRoute
        required: false
      httpStatusCode:
        ref: httpStatusCode
        required: false
      durationMs:
        ref: durationMs
        required: true
      errorType:
        ref: errorType
        required: true
      requestId:
        ref: requestId
        required: true
    metrics:
      otel.http.request.error.count:
        type: counter
        unit: "errors"
        description: "Total HTTP request errors"
        labels:
          - httpMethod
          - errorType
    tags:
      - http
      - aspnetcore
      - error
```

#### Registration API

```csharp
// Program.cs — one-line registration
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .WithLogging(logging =>
    {
        logging.AddProcessor<OtelEventsCausalityProcessor>();  // otel-events core (optional)
        logging.AddOtelEventsJsonExporter();                   // otel-events core (optional)
    })
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("OtelEvents.*");               // Pick up all integration pack meters
        metrics.AddMeter("MyCompany.MyService.Events.*"); // Pick up app-defined meters
    });

// Integration pack — one line
builder.Services.AddOtelEventsAspNetCore();

// Or with configuration:
builder.Services.AddOtelEventsAspNetCore(options =>
{
    options.EnableCausalScope = true;             // Default: true (if OtelEvents.Causality is referenced)
    options.RecordRequestReceived = true;         // Default: true — emit http.request.received
    options.CaptureUserAgent = false;              // Default: true;             // Default: false — opt-in only (PII: GDPR/CCPA)
    options.CaptureClientIp = false;               // Default: true;              // Default: false — opt-in only (PII: GDPR/CCPA)
    options.UseRouteTemplate = true;              // Default: true — use route template instead of raw path
    options.ExcludePaths = ["/health", "/ready", "/metrics", "/favicon.ico"];  // Default: empty
    options.MaxPathLength = 256;                  // Default: 256 — truncate long paths
});

var app = builder.Build();

// Middleware is auto-registered via IStartupFilter — no manual UseMiddleware() needed.
// For explicit control:
// app.UseOtelEventsMiddleware();  // manual registration (overrides IStartupFilter)

app.MapControllers();
app.Run();
```

#### Implementation Approach

**Mechanism:** ASP.NET Core middleware registered via `IStartupFilter` (auto-registration at the outermost position in the pipeline).

```csharp
/// <summary>
/// ASP.NET Core middleware that emits schema-defined events for HTTP request lifecycle.
/// Registered at the outermost position in the pipeline via IStartupFilter.
/// </summary>
internal sealed class OtelEventsAspNetCoreMiddleware : IMiddleware
{
    private readonly ILogger<OtelEventsAspNetCoreEventSource> _logger;
    private readonly OtelEventsAspNetCoreOptions _options;

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (IsExcluded(context.Request.Path))
        {
            await next(context);
            return;
        }

        // Emit http.request.received
        if (_options.RecordRequestReceived)
        {
            _logger.HttpRequestReceived(
                httpMethod: context.Request.Method,
                httpPath: GetPath(context),
                userAgent: _options.CaptureUserAgent ? context.Request.Headers.UserAgent.ToString() : null,
                clientIp: _options.CaptureClientIp ? GetClientIp(context) : null,
                contentLength: context.Request.ContentLength,
                requestId: context.TraceIdentifier);
        }

        // Create causal scope — all events within this request share a parentEventId
        IDisposable? causalScope = null;
        if (_options.EnableCausalScope && OtelEventsCausalityContextAvailable)
        {
            causalScope = OtelEventsCausalityContext.SetParent(lastEmittedEventId);
        }

        var sw = Stopwatch.StartNew();
        try
        {
            await next(context);
            sw.Stop();

            // Emit http.request.completed (1xx–4xx)
            _logger.HttpRequestCompleted(
                httpMethod: context.Request.Method,
                httpPath: GetPath(context),
                httpRoute: GetRouteTemplate(context),
                httpStatusCode: context.Response.StatusCode,
                durationMs: sw.Elapsed.TotalMilliseconds,
                contentLength: context.Response.ContentLength,
                requestId: context.TraceIdentifier);
        }
        catch (Exception ex)
        {
            sw.Stop();

            // Emit http.request.failed (5xx / unhandled exception)
            _logger.HttpRequestFailed(
                httpMethod: context.Request.Method,
                httpPath: GetPath(context),
                httpRoute: GetRouteTemplate(context),
                httpStatusCode: context.Response.HasStarted ? context.Response.StatusCode : null,
                durationMs: sw.Elapsed.TotalMilliseconds,
                errorType: ex.GetType().Name,
                requestId: context.TraceIdentifier,
                exception: ex);

            throw; // Re-throw — middleware observes, never swallows
        }
        finally
        {
            causalScope?.Dispose();
        }
    }
}
```

**Key design choices:**
- Middleware is registered **at the outermost position** via `IStartupFilter` so it captures the full request lifecycle, including exception handling middleware.
- `http.request.received` is emitted **before** the pipeline runs.
- `http.request.completed` is emitted **after** the pipeline completes for non-exception responses.
- `http.request.failed` is emitted **only** for unhandled exceptions — not for 4xx responses (those are completed, not failed).
- The middleware **re-throws** exceptions — it observes, never interferes.
- Path uses route template when available (e.g., `/api/orders/{id}`) to avoid cardinality explosion.
- `causalScope` creates an `OtelEventsCausalityContext` scope so that all events emitted by application code during the request automatically get the request's `eventId` as their `parentEventId`.

#### What It Complements

| Existing OTEL Instrumentation | What It Provides | What OtelEvents.AspNetCore Adds |
|-------------------------------|------------------|---------------------------------|
| `OpenTelemetry.Instrumentation.AspNetCore` (traces) | `Activity` span per request with `http.request.method`, `http.route`, `http.response.status_code` | Schema-defined `LogRecord` events with otel-events envelope structure, causal scope, AI-optimized JSON |
| `OpenTelemetry.Instrumentation.AspNetCore` (metrics) | `http.server.request.duration` histogram, `http.server.active_requests` gauge | Schema-defined counter + histogram with pack-specific metric names, queryable by `httpMethod` + `httpStatusCode` |
| Neither | N/A | `http.request.received` event (start of request — not provided by OTEL auto-instrumentation) |
| Neither | N/A | `http.request.failed` with structured exception data and `errorType` field |
| Neither | N/A | **Causal scope** — all application events within a request share `parentEventId` |

**Coexistence rule:** `OtelEvents.AspNetCore` sits alongside `AddAspNetCoreInstrumentation()`. The OTEL instrumentation produces traces (spans) and its own metrics. The integration pack produces structured log events (LogRecords) and its own metrics. Both flow through the same OTEL pipeline. No conflict.

#### Example JSON Output

**Event: `http.request.received`**
```json
{"timestamp":"2025-07-15T14:30:00.123456Z","event":"http.request.received","severity":"INFO","severityNumber":9,"message":"HTTP POST /api/orders received from 10.0.0.42","service":"order-service","environment":"production","traceId":"4bf92f3577b34da6a3ce929d0e0e4736","spanId":"00f067aa0ba902b7","eventId":"evt_01914a2b-3c4d-7e5f-8a9b-0c1d2e3f4a5b","attr":{"httpMethod":"POST","httpPath":"/api/orders","userAgent":"Mozilla/5.0 (Windows NT 10.0; Win64; x64)","clientIp":"10.0.0.42","contentLength":256,"requestId":"0HN8PQRS:00000001"},"tags":["http","aspnetcore"],"otel_events.v":"1.0.0","otel_events.seq":101,"otel_events.host":"web-01","otel_events.pid":4821}
```

**Event: `http.request.completed`**
```json
{"timestamp":"2025-07-15T14:30:00.168912Z","event":"http.request.completed","severity":"INFO","severityNumber":9,"message":"HTTP POST /api/orders completed with 201 in 45.5ms","service":"order-service","environment":"production","traceId":"4bf92f3577b34da6a3ce929d0e0e4736","spanId":"00f067aa0ba902b7","eventId":"evt_01914a2b-4d5e-7f6a-9b0c-1d2e3f4a5b6c","parentEventId":"evt_01914a2b-3c4d-7e5f-8a9b-0c1d2e3f4a5b","attr":{"httpMethod":"POST","httpPath":"/api/orders","httpRoute":"/api/orders","httpStatusCode":201,"durationMs":45.5,"contentLength":512,"requestId":"0HN8PQRS:00000001"},"tags":["http","aspnetcore"],"otel_events.v":"1.0.0","otel_events.seq":104,"otel_events.host":"web-01","otel_events.pid":4821}
```

**Event: `http.request.failed`**
```json
{"timestamp":"2025-07-15T14:30:01.234567Z","event":"http.request.failed","severity":"ERROR","severityNumber":17,"message":"HTTP GET /api/orders/999 failed with SqlException after 2034.7ms","service":"order-service","environment":"production","traceId":"5cf93f4688c45eb7b4df030a1f1f5847","spanId":"11g178bb1cb013c8","eventId":"evt_01914a2c-5e6f-7a8b-0c1d-2e3f4a5b6c7d","parentEventId":"evt_01914a2c-4d5e-7f6a-9b0c-1d2e3f4a5b6c","attr":{"httpMethod":"GET","httpPath":"/api/orders/{id}","httpRoute":"/api/orders/{id}","durationMs":2034.7,"errorType":"SqlException","requestId":"0HN8PQRS:00000002"},"exception":{"type":"System.Data.SqlClient.SqlException","message":"Connection timeout expired","stackTrace":[{"method":"SqlConnection.Open()","file":"SqlConnection.cs","line":142}]},"tags":["http","aspnetcore","error"],"otel_events.v":"1.0.0","otel_events.seq":105,"otel_events.host":"web-01","otel_events.pid":4821}
```

#### Configuration

```csharp
/// <summary>Configuration for OtelEvents.AspNetCore integration pack.</summary>
public sealed class OtelEventsAspNetCoreOptions
{
    /// <summary>
    /// Enable causal scope per request. When true, all events emitted during
    /// request processing share a parentEventId pointing to the http.request.received event.
    /// Default: true (if OtelEvents.Causality is referenced; no-op otherwise).
    /// </summary>
    public bool EnableCausalScope { get; set; } = true;

    /// <summary>
    /// Emit http.request.received event at the start of each request.
    /// Set to false if only request completion/failure events are needed.
    /// Default: true.
    /// </summary>
    public bool RecordRequestReceived { get; set; } = true;

    /// <summary>Capture User-Agent header. Default: true.</summary>
    public bool CaptureUserAgent { get; set; } = false;

    /// <summary>Capture client IP address. Default: true.</summary>
    public bool CaptureClientIp { get; set; } = false;

    /// <summary>
    /// Use route template (e.g., /api/orders/{id}) instead of raw path.
    /// Prevents cardinality explosion in metrics labels.
    /// Default: true.
    /// </summary>
    public bool UseRouteTemplate { get; set; } = true;

    /// <summary>
    /// Request paths to exclude from event emission.
    /// Exact match and prefix match supported.
    /// Default: empty.
    /// </summary>
    public IList<string> ExcludePaths { get; set; } = [];

    /// <summary>
    /// Maximum path length before truncation. Prevents unbounded attribute sizes.
    /// Default: 256.
    /// </summary>
    public int MaxPathLength { get; set; } = 256;
}
```

---

### 15.4 OtelEvents.Grpc

#### Package & Dependencies

```
OtelEvents.Grpc (runtime)
├── Grpc.AspNetCore.Server (>= 2.60)       — gRPC server interceptor base
├── Grpc.Net.Client (>= 2.60)              — gRPC client interceptor base
├── OpenTelemetry (>= 1.9)                 — OTEL SDK types
├── OtelEvents.Causality (>= 1.0) [optional]      — Causal scope per call
└── (no dependency on OtelEvents.Schema — code is pre-generated)
```

**Target frameworks:** `net8.0`, `net9.0`

#### YAML Schema

```yaml
schema:
  name: "OtelEvents.Grpc"
  version: "1.0.0"
  namespace: "OtelEvents.Grpc.Events"
  meterName: "OtelEvents.Grpc"
  description: "Schema-defined gRPC call lifecycle events for server and client interceptors"

fields:
  grpcService:
    type: string
    description: "Fully qualified gRPC service name (e.g., greet.Greeter)"
    index: true

  grpcMethod:
    type: string
    description: "gRPC method name (e.g., SayHello)"
    index: true

  grpcStatusCode:
    type: int
    description: "gRPC status code (0=OK, 1=Cancelled, 2=Unknown, ...)"
    index: true

  grpcStatusDetail:
    type: string
    description: "gRPC status detail message"

  durationMs:
    type: double
    description: "Call duration in milliseconds"
    unit: "ms"

  requestSize:
    type: long
    description: "Request message size in bytes"
    unit: "bytes"

  responseSize:
    type: long
    description: "Response message size in bytes"
    unit: "bytes"

  grpcSide:
    type: enum
    description: "Whether event originates from server or client"
    values: [Server, Client]

  errorType:
    type: string
    description: "Exception type name for failed calls"
    index: true

enums:
  GrpcSide:
    description: "Side of gRPC communication"
    values:
      - Server
      - Client

events:
  grpc.call.started:
    id: 10101
    severity: INFO
    description: "A gRPC call started processing (server-side) or was initiated (client-side)."
    message: "gRPC {grpcSide} {grpcService}/{grpcMethod} started"
    fields:
      grpcService:
        ref: grpcService
        required: true
      grpcMethod:
        ref: grpcMethod
        required: true
      grpcSide:
        ref: grpcSide
        required: true
      requestSize:
        ref: requestSize
        required: false
    metrics:
      otel.grpc.call.started.count:
        type: counter
        unit: "calls"
        description: "Total gRPC calls started"
        labels:
          - grpcService
          - grpcMethod
          - grpcSide
    tags:
      - grpc

  grpc.call.completed:
    id: 10102
    severity: INFO
    description: "A gRPC call completed successfully."
    message: "gRPC {grpcSide} {grpcService}/{grpcMethod} completed with status {grpcStatusCode} in {durationMs}ms"
    fields:
      grpcService:
        ref: grpcService
        required: true
      grpcMethod:
        ref: grpcMethod
        required: true
      grpcSide:
        ref: grpcSide
        required: true
      grpcStatusCode:
        ref: grpcStatusCode
        required: true
      grpcStatusDetail:
        ref: grpcStatusDetail
        required: false
      durationMs:
        ref: durationMs
        required: true
      requestSize:
        ref: requestSize
        required: false
      responseSize:
        ref: responseSize
        required: false
    metrics:
      otel.grpc.call.duration:
        type: histogram
        unit: "ms"
        description: "gRPC call duration"
        buckets: [1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000]
      otel.grpc.call.completed.count:
        type: counter
        unit: "calls"
        description: "Total gRPC calls completed by status"
        labels:
          - grpcService
          - grpcMethod
          - grpcSide
          - grpcStatusCode
    tags:
      - grpc

  grpc.call.failed:
    id: 10103
    severity: ERROR
    description: "A gRPC call failed with an exception or non-OK status."
    message: "gRPC {grpcSide} {grpcService}/{grpcMethod} failed with {errorType} after {durationMs}ms"
    exception: true
    fields:
      grpcService:
        ref: grpcService
        required: true
      grpcMethod:
        ref: grpcMethod
        required: true
      grpcSide:
        ref: grpcSide
        required: true
      grpcStatusCode:
        ref: grpcStatusCode
        required: true
      grpcStatusDetail:
        ref: grpcStatusDetail
        required: false
      durationMs:
        ref: durationMs
        required: true
      errorType:
        ref: errorType
        required: true
    metrics:
      otel.grpc.call.error.count:
        type: counter
        unit: "errors"
        description: "Total gRPC call errors"
        labels:
          - grpcService
          - grpcMethod
          - grpcSide
          - grpcStatusCode
    tags:
      - grpc
      - error
```

#### Registration API

```csharp
// Server-side: register interceptor
builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<OtelEventsGrpcServerInterceptor>();
});

// Or via dedicated extension:
builder.Services.AddOtelEventsGrpc(options =>
{
    options.EnableServerInterceptor = true;       // Default: true
    options.EnableClientInterceptor = true;       // Default: true
    options.EnableCausalScope = true;             // Default: true
    options.CaptureMessageSize = true;            // Default: true
    options.ExcludeServices = [];                 // Exclude specific gRPC services
    options.ExcludeMethods = [];                  // Exclude specific methods
});

// Client-side: register on GrpcChannel
var channel = GrpcChannel.ForAddress("https://localhost:5001", new GrpcChannelOptions
{
    // OtelEvents.Grpc provides a convenience method for client interceptor
});

// Or via HttpClientFactory:
builder.Services.AddGrpcClient<Greeter.GreeterClient>(options =>
{
    options.Address = new Uri("https://localhost:5001");
}).AddOtelEventsInterceptor();
```

#### Implementation Approach

**Mechanism:** gRPC `Interceptor` (inherits `Grpc.Core.Interceptors.Interceptor`).

- **Server interceptor** (`OtelEventsGrpcServerInterceptor`): Overrides `UnaryServerHandler`, `ServerStreamingServerHandler`, `ClientStreamingServerHandler`, `DuplexStreamingServerHandler`.
- **Client interceptor** (`OtelEventsGrpcClientInterceptor`): Overrides `AsyncUnaryCall`, `AsyncServerStreamingCall`, etc.
- Both interceptors extract service name and method from `ServerCallContext.Method` / `Method<TRequest, TResponse>`.
- `grpcStatusCode` is read from `RpcException.StatusCode` or `ServerCallContext.Status`.
- Message size is computed from serialized protobuf size when `CaptureMessageSize = true`.

#### What It Complements

| Existing OTEL | What It Provides | What OtelEvents.Grpc Adds |
|---------------|------------------|-----------------------------|
| `OpenTelemetry.Instrumentation.GrpcNetClient` | Client-side trace spans | Schema-defined client-side structured log events |
| `OpenTelemetry.Instrumentation.AspNetCore` (gRPC experimental) | Server-side trace spans for gRPC | Schema-defined server-side structured log events with causal scope |
| Neither | N/A | `grpc.call.started` event, `grpc.call.failed` with structured exception, message size metrics |

#### Example JSON Output

**Event: `grpc.call.completed` (server-side)**
```json
{"timestamp":"2025-07-15T14:30:00.234567Z","event":"grpc.call.completed","severity":"INFO","severityNumber":9,"message":"gRPC Server greet.Greeter/SayHello completed with status 0 in 12.3ms","service":"greeting-service","environment":"production","traceId":"6df04g5799d56fc8c5ef141b2g2g6958","spanId":"22h289cc2dc124d9","eventId":"evt_01914a2d-6f7a-8b9c-0d1e-3f4a5b6c7d8e","attr":{"grpcService":"greet.Greeter","grpcMethod":"SayHello","grpcSide":"Server","grpcStatusCode":0,"durationMs":12.3,"requestSize":42,"responseSize":128},"tags":["grpc"],"otel_events.v":"1.0.0","otel_events.seq":201,"otel_events.host":"grpc-01","otel_events.pid":5932}
```

**Event: `grpc.call.failed` (client-side)**
```json
{"timestamp":"2025-07-15T14:30:01.345678Z","event":"grpc.call.failed","severity":"ERROR","severityNumber":17,"message":"gRPC Client inventory.InventoryService/ReserveStock failed with RpcException after 5023.1ms","service":"order-service","environment":"production","traceId":"7eg15h6800e67gd9d6fg252c3h3h7069","spanId":"33i390dd3ed235e0","eventId":"evt_01914a2e-7a8b-9c0d-1e2f-4a5b6c7d8e9f","attr":{"grpcService":"inventory.InventoryService","grpcMethod":"ReserveStock","grpcSide":"Client","grpcStatusCode":14,"grpcStatusDetail":"failed to connect to all addresses","durationMs":5023.1,"errorType":"RpcException"},"exception":{"type":"Grpc.Core.RpcException","message":"Status(StatusCode=\"Unavailable\", Detail=\"failed to connect to all addresses\")","stackTrace":[{"method":"GrpcChannel.ConnectAsync()","file":"GrpcChannel.cs","line":89}]},"tags":["grpc","error"],"otel_events.v":"1.0.0","otel_events.seq":202,"otel_events.host":"web-01","otel_events.pid":4821}
```

---

### 15.5 OtelEvents.Azure.CosmosDb

#### Package & Dependencies

```
OtelEvents.Azure.CosmosDb (runtime)
├── Microsoft.Azure.Cosmos (>= 3.36.0)    — CosmosDB .NET SDK (v3)
├── OpenTelemetry (>= 1.9)                — OTEL SDK types
├── OtelEvents.Causality (>= 1.0) [optional]     — Causal scope per operation
└── (no dependency on OtelEvents.Schema — code is pre-generated)
```

**Target frameworks:** `net8.0`, `net9.0`

#### YAML Schema

```yaml
schema:
  name: "OtelEvents.Azure.CosmosDb"
  version: "1.0.0"
  namespace: "OtelEvents.Azure.CosmosDb.Events"
  meterName: "OtelEvents.Azure.CosmosDb"
  description: "Schema-defined events for Azure CosmosDB operations"

fields:
  cosmosDatabase:
    type: string
    description: "CosmosDB database name"
    index: true

  cosmosContainer:
    type: string
    description: "CosmosDB container name"
    index: true

  cosmosPartitionKey:
    type: string
    description: "Partition key value used in the operation"
    index: true

  cosmosRequestCharge:
    type: double
    description: "Request Units (RU) consumed by the operation"
    unit: "RU"

  cosmosItemCount:
    type: int
    description: "Number of items returned or affected"

  cosmosStatusCode:
    type: int
    description: "CosmosDB HTTP status code (200, 201, 404, 429, etc.)"
    index: true

  cosmosSubStatusCode:
    type: int
    description: "CosmosDB sub-status code for detailed error diagnosis"

  cosmosOperationType:
    type: enum
    description: "Type of CosmosDB operation"
    values: [Query, PointRead, PointWrite, Delete, Patch, Batch]

  cosmosRegion:
    type: string
    description: "CosmosDB region that served the request"

  durationMs:
    type: double
    description: "Operation duration in milliseconds"
    unit: "ms"

  errorType:
    type: string
    description: "Exception type name for failed operations"
    index: true

  cosmosQueryText:
    type: string
    sensitivity: internal
    maxLength: 2048
    description: "SQL query text (when query capture is enabled; parameterized only)"

enums:
  CosmosOperationType:
    description: "Type of CosmosDB operation"
    values:
      - Query
      - PointRead
      - PointWrite
      - Delete
      - Patch
      - Batch

events:
  cosmosdb.query.executed:
    id: 10201
    severity: DEBUG
    description: "A CosmosDB SQL query was executed successfully."
    message: "CosmosDB query on {cosmosDatabase}/{cosmosContainer} returned {cosmosItemCount} items in {durationMs}ms ({cosmosRequestCharge} RU)"
    fields:
      cosmosDatabase:
        ref: cosmosDatabase
        required: true
      cosmosContainer:
        ref: cosmosContainer
        required: true
      cosmosPartitionKey:
        ref: cosmosPartitionKey
        required: false
      cosmosRequestCharge:
        ref: cosmosRequestCharge
        required: true
      cosmosItemCount:
        ref: cosmosItemCount
        required: true
      durationMs:
        ref: durationMs
        required: true
      cosmosStatusCode:
        ref: cosmosStatusCode
        required: true
      cosmosRegion:
        ref: cosmosRegion
        required: false
      cosmosQueryText:
        ref: cosmosQueryText
        required: false
    metrics:
      otel.cosmosdb.query.duration:
        type: histogram
        unit: "ms"
        description: "CosmosDB query duration"
        buckets: [1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000]
      otel.cosmosdb.query.ru:
        type: histogram
        unit: "RU"
        description: "CosmosDB query RU consumption"
        buckets: [1, 5, 10, 25, 50, 100, 250, 500, 1000, 5000]
      otel.cosmosdb.query.item.count:
        type: histogram
        unit: "items"
        description: "CosmosDB query result item count"
        buckets: [1, 10, 50, 100, 500, 1000]
    tags:
      - cosmosdb
      - query

  cosmosdb.query.failed:
    id: 10202
    severity: ERROR
    description: "A CosmosDB SQL query failed."
    message: "CosmosDB query on {cosmosDatabase}/{cosmosContainer} failed with {cosmosStatusCode} after {durationMs}ms ({cosmosRequestCharge} RU)"
    exception: true
    fields:
      cosmosDatabase:
        ref: cosmosDatabase
        required: true
      cosmosContainer:
        ref: cosmosContainer
        required: true
      cosmosPartitionKey:
        ref: cosmosPartitionKey
        required: false
      cosmosRequestCharge:
        ref: cosmosRequestCharge
        required: false
      durationMs:
        ref: durationMs
        required: true
      cosmosStatusCode:
        ref: cosmosStatusCode
        required: true
      cosmosSubStatusCode:
        ref: cosmosSubStatusCode
        required: false
      errorType:
        ref: errorType
        required: true
    metrics:
      otel.cosmosdb.error.count:
        type: counter
        unit: "errors"
        description: "Total CosmosDB operation errors"
        labels:
          - cosmosDatabase
          - cosmosContainer
          - cosmosStatusCode
    tags:
      - cosmosdb
      - query
      - error

  cosmosdb.point.read:
    id: 10203
    severity: DEBUG
    description: "A CosmosDB point read (ReadItemAsync) was executed."
    message: "CosmosDB point read on {cosmosDatabase}/{cosmosContainer} [{cosmosPartitionKey}] in {durationMs}ms ({cosmosRequestCharge} RU)"
    fields:
      cosmosDatabase:
        ref: cosmosDatabase
        required: true
      cosmosContainer:
        ref: cosmosContainer
        required: true
      cosmosPartitionKey:
        ref: cosmosPartitionKey
        required: true
      cosmosRequestCharge:
        ref: cosmosRequestCharge
        required: true
      durationMs:
        ref: durationMs
        required: true
      cosmosStatusCode:
        ref: cosmosStatusCode
        required: true
      cosmosRegion:
        ref: cosmosRegion
        required: false
    metrics:
      otel.cosmosdb.point.duration:
        type: histogram
        unit: "ms"
        description: "CosmosDB point operation duration"
        buckets: [1, 2, 5, 10, 25, 50, 100, 250, 500]
      otel.cosmosdb.point.ru:
        type: histogram
        unit: "RU"
        description: "CosmosDB point operation RU consumption"
        buckets: [1, 2, 5, 10, 25, 50, 100]
    tags:
      - cosmosdb
      - point

  cosmosdb.point.write:
    id: 10204
    severity: DEBUG
    description: "A CosmosDB point write (CreateItemAsync, UpsertItemAsync, ReplaceItemAsync) was executed."
    message: "CosmosDB point write on {cosmosDatabase}/{cosmosContainer} [{cosmosPartitionKey}] in {durationMs}ms ({cosmosRequestCharge} RU)"
    fields:
      cosmosDatabase:
        ref: cosmosDatabase
        required: true
      cosmosContainer:
        ref: cosmosContainer
        required: true
      cosmosPartitionKey:
        ref: cosmosPartitionKey
        required: true
      cosmosRequestCharge:
        ref: cosmosRequestCharge
        required: true
      durationMs:
        ref: durationMs
        required: true
      cosmosStatusCode:
        ref: cosmosStatusCode
        required: true
      cosmosRegion:
        ref: cosmosRegion
        required: false
    metrics:
      otel.cosmosdb.point.duration:
        type: histogram
        unit: "ms"
        description: "CosmosDB point operation duration"
        buckets: [1, 2, 5, 10, 25, 50, 100, 250, 500]
      otel.cosmosdb.point.ru:
        type: histogram
        unit: "RU"
        description: "CosmosDB point operation RU consumption"
        buckets: [1, 2, 5, 10, 25, 50, 100]
    tags:
      - cosmosdb
      - point
```

#### Registration API

```csharp
// Register CosmosDB event capture
builder.Services.AddOtelEventsCosmosDb(options =>
{
    options.CaptureQueryText = false;             // Default: false — do NOT capture SQL text (may contain PII)
    options.EnableCausalScope = true;             // Default: true
    options.CaptureRegion = true;                 // Default: true
    options.RuThreshold = 0;                      // Default: 0 — emit for all operations. Set to e.g., 10 to only emit for expensive operations.
    options.LatencyThresholdMs = 0;               // Default: 0 — emit for all. Set to e.g., 100 to only emit slow operations.
});

// Metrics registration
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("OtelEvents.*");
    });
```

#### Implementation Approach

**Mechanism:** `DiagnosticListener` subscriber for the `Azure.Cosmos.Operation` diagnostic source.

The Azure CosmosDB .NET SDK (v3, ≥ 3.36.0) emits `DiagnosticListener` events for every operation. `OtelEvents.Azure.CosmosDb` subscribes to these events and emits schema-defined `LogRecord`s.

```csharp
/// <summary>
/// Subscribes to Azure CosmosDB SDK diagnostic events and emits
/// schema-defined structured log events.
/// </summary>
internal sealed class OtelEventsCosmosDbDiagnosticObserver :
    IObserver<DiagnosticListener>,
    IObserver<KeyValuePair<string, object?>>
{
    // Subscribes to "Azure.Cosmos.Operation" DiagnosticListener
    // On operation completion: reads CosmosDiagnostics from the response
    // Extracts: database, container, partition key, RU, duration, status code, region
    // Emits appropriate event via pre-generated ILogger extension methods
}
```

**Fallback approach:** If the `DiagnosticListener` approach proves insufficient for capturing all required fields (e.g., partition key, query text), an alternative is a custom `RequestHandler` registered in `CosmosClientOptions`:

```csharp
// Alternative: RequestHandler pipeline
var cosmosClient = new CosmosClient(connectionString, new CosmosClientOptions
{
    CustomHandlers = { new OtelEventsCosmosDbRequestHandler(logger, options) }
});
```

**Design note:** The `DiagnosticListener` approach is preferred because it doesn't require the consumer to modify their `CosmosClient` construction. The `RequestHandler` approach is a fallback for cases where DiagnosticListener doesn't provide sufficient telemetry.

#### What It Complements

| Existing OTEL | What It Provides | What OtelEvents.Azure.CosmosDb Adds |
|---------------|------------------|------------------------------------|
| CosmosDB SDK distributed tracing (`Azure.Cosmos.Operation`) | Trace spans with `db.cosmosdb.request_charge`, `db.operation` | Schema-defined log events with full RU histograms, query item counts, partition key, region |
| CosmosDB SDK diagnostics logging | Unstructured diagnostic text at Warning+ latency | Structured events for otel-events operations with AI-optimized envelope format |
| Neither | N/A | RU consumption histograms, query result count distribution, error rate by container + status code |

#### Example JSON Output

**Event: `cosmosdb.query.executed`**
```json
{"timestamp":"2025-07-15T14:30:00.345678Z","event":"cosmosdb.query.executed","severity":"DEBUG","severityNumber":5,"message":"CosmosDB query on OrderDb/Orders returned 15 items in 23.4ms (42.5 RU)","service":"order-service","environment":"production","traceId":"4bf92f3577b34da6a3ce929d0e0e4736","spanId":"00f067aa0ba902b7","eventId":"evt_01914a2f-8b9c-0d1e-2f3a-5b6c7d8e9f0a","parentEventId":"evt_01914a2b-3c4d-7e5f-8a9b-0c1d2e3f4a5b","attr":{"cosmosDatabase":"OrderDb","cosmosContainer":"Orders","cosmosRequestCharge":42.5,"cosmosItemCount":15,"durationMs":23.4,"cosmosStatusCode":200,"cosmosRegion":"East US"},"tags":["cosmosdb","query"],"otel_events.v":"1.0.0","otel_events.seq":301,"otel_events.host":"web-01","otel_events.pid":4821}
```

**Event: `cosmosdb.point.read`**
```json
{"timestamp":"2025-07-15T14:30:00.456789Z","event":"cosmosdb.point.read","severity":"DEBUG","severityNumber":5,"message":"CosmosDB point read on OrderDb/Orders [customer-42] in 3.2ms (1.0 RU)","service":"order-service","environment":"production","traceId":"4bf92f3577b34da6a3ce929d0e0e4736","spanId":"00f067aa0ba902b7","eventId":"evt_01914a30-9c0d-1e2f-3a4b-6c7d8e9f0a1b","parentEventId":"evt_01914a2b-3c4d-7e5f-8a9b-0c1d2e3f4a5b","attr":{"cosmosDatabase":"OrderDb","cosmosContainer":"Orders","cosmosPartitionKey":"customer-42","cosmosRequestCharge":1.0,"durationMs":3.2,"cosmosStatusCode":200,"cosmosRegion":"East US"},"tags":["cosmosdb","point"],"otel_events.v":"1.0.0","otel_events.seq":302,"otel_events.host":"web-01","otel_events.pid":4821}
```

**Event: `cosmosdb.query.failed` (429 throttling)**
```json
{"timestamp":"2025-07-15T14:30:02.567890Z","event":"cosmosdb.query.failed","severity":"ERROR","severityNumber":17,"message":"CosmosDB query on OrderDb/Orders failed with 429 after 102.5ms (0.0 RU)","service":"order-service","environment":"production","traceId":"8fh26i7911f78he0e7gh363d4i4i8170","spanId":"44j401ee4fe346f1","eventId":"evt_01914a31-0d1e-2f3a-4b5c-7d8e9f0a1b2c","attr":{"cosmosDatabase":"OrderDb","cosmosContainer":"Orders","cosmosRequestCharge":0.0,"durationMs":102.5,"cosmosStatusCode":429,"cosmosSubStatusCode":3200,"errorType":"CosmosException"},"exception":{"type":"Microsoft.Azure.Cosmos.CosmosException","message":"Response status code does not indicate success: TooManyRequests (429); Substatus: 3200","stackTrace":[{"method":"CosmosClient.ExecuteQueryAsync()","file":"CosmosClient.cs","line":456}]},"tags":["cosmosdb","query","error"],"otel_events.v":"1.0.0","otel_events.seq":303,"otel_events.host":"web-01","otel_events.pid":4821}
```

---

### 15.6 OtelEvents.Azure.Storage

#### Package & Dependencies

```
OtelEvents.Azure.Storage (runtime)
├── Azure.Storage.Blobs (>= 12.19)        — Blob storage client
├── Azure.Storage.Queues (>= 12.17)       — Queue storage client
├── Azure.Core (>= 1.38)                  — Azure SDK pipeline policy base
├── OpenTelemetry (>= 1.9)                — OTEL SDK types
├── OtelEvents.Causality (>= 1.0) [optional]     — Causal scope per operation
└── (no dependency on OtelEvents.Schema — code is pre-generated)
```

**Target frameworks:** `net8.0`, `net9.0`

#### YAML Schema

```yaml
schema:
  name: "OtelEvents.Azure.Storage"
  version: "1.0.0"
  namespace: "OtelEvents.Azure.Storage.Events"
  meterName: "OtelEvents.Azure.Storage"
  description: "Schema-defined events for Azure Blob and Queue Storage operations"

fields:
  storageAccountName:
    type: string
    description: "Azure Storage account name"
    index: true

  storageContainerName:
    type: string
    description: "Blob container name"
    index: true

  storageQueueName:
    type: string
    description: "Queue name"
    index: true

  storageBlobName:
    type: string
    description: "Blob name (path within container)"

  storageBlobSize:
    type: long
    description: "Blob size in bytes"
    unit: "bytes"

  storageContentType:
    type: string
    description: "Content-Type of the blob"

  durationMs:
    type: double
    description: "Operation duration in milliseconds"
    unit: "ms"

  storageStatusCode:
    type: int
    description: "HTTP status code from Azure Storage API response"
    index: true

  errorType:
    type: string
    description: "Exception type name for failed operations"
    index: true

  storageMessageCount:
    type: int
    description: "Number of messages received from a queue"

events:
  storage.blob.uploaded:
    id: 10301
    severity: INFO
    description: "A blob was uploaded to Azure Blob Storage."
    message: "Blob uploaded to {storageContainerName}/{storageBlobName} ({storageBlobSize} bytes) in {durationMs}ms"
    fields:
      storageAccountName:
        ref: storageAccountName
        required: true
      storageContainerName:
        ref: storageContainerName
        required: true
      storageBlobName:
        ref: storageBlobName
        required: true
      storageBlobSize:
        ref: storageBlobSize
        required: true
      storageContentType:
        ref: storageContentType
        required: false
      durationMs:
        ref: durationMs
        required: true
      storageStatusCode:
        ref: storageStatusCode
        required: true
    metrics:
      otel.storage.blob.upload.duration:
        type: histogram
        unit: "ms"
        description: "Blob upload duration"
        buckets: [10, 50, 100, 250, 500, 1000, 2500, 5000, 10000]
      otel.storage.blob.upload.size:
        type: histogram
        unit: "bytes"
        description: "Blob upload size"
        buckets: [1024, 10240, 102400, 1048576, 10485760, 104857600]
      otel.storage.blob.upload.count:
        type: counter
        unit: "uploads"
        description: "Total blob uploads"
        labels:
          - storageContainerName
    tags:
      - storage
      - blob

  storage.blob.downloaded:
    id: 10302
    severity: INFO
    description: "A blob was downloaded from Azure Blob Storage."
    message: "Blob downloaded from {storageContainerName}/{storageBlobName} ({storageBlobSize} bytes) in {durationMs}ms"
    fields:
      storageAccountName:
        ref: storageAccountName
        required: true
      storageContainerName:
        ref: storageContainerName
        required: true
      storageBlobName:
        ref: storageBlobName
        required: true
      storageBlobSize:
        ref: storageBlobSize
        required: true
      storageContentType:
        ref: storageContentType
        required: false
      durationMs:
        ref: durationMs
        required: true
      storageStatusCode:
        ref: storageStatusCode
        required: true
    metrics:
      otel.storage.blob.download.duration:
        type: histogram
        unit: "ms"
        description: "Blob download duration"
        buckets: [10, 50, 100, 250, 500, 1000, 2500, 5000, 10000]
      otel.storage.blob.download.size:
        type: histogram
        unit: "bytes"
        description: "Blob download size"
        buckets: [1024, 10240, 102400, 1048576, 10485760, 104857600]
    tags:
      - storage
      - blob

  storage.blob.deleted:
    id: 10303
    severity: INFO
    description: "A blob was deleted from Azure Blob Storage."
    message: "Blob deleted from {storageContainerName}/{storageBlobName} in {durationMs}ms"
    fields:
      storageAccountName:
        ref: storageAccountName
        required: true
      storageContainerName:
        ref: storageContainerName
        required: true
      storageBlobName:
        ref: storageBlobName
        required: true
      durationMs:
        ref: durationMs
        required: true
      storageStatusCode:
        ref: storageStatusCode
        required: true
    metrics:
      otel.storage.blob.delete.count:
        type: counter
        unit: "deletes"
        description: "Total blob deletes"
        labels:
          - storageContainerName
    tags:
      - storage
      - blob

  storage.blob.failed:
    id: 10304
    severity: ERROR
    description: "An Azure Blob Storage operation failed."
    message: "Blob operation on {storageContainerName}/{storageBlobName} failed with {storageStatusCode} after {durationMs}ms"
    exception: true
    fields:
      storageAccountName:
        ref: storageAccountName
        required: true
      storageContainerName:
        ref: storageContainerName
        required: true
      storageBlobName:
        ref: storageBlobName
        required: false
      durationMs:
        ref: durationMs
        required: true
      storageStatusCode:
        ref: storageStatusCode
        required: true
      errorType:
        ref: errorType
        required: true
    metrics:
      otel.storage.blob.error.count:
        type: counter
        unit: "errors"
        description: "Total blob operation errors"
        labels:
          - storageContainerName
          - storageStatusCode
    tags:
      - storage
      - blob
      - error

  storage.queue.sent:
    id: 10305
    severity: INFO
    description: "A message was sent to an Azure Storage Queue."
    message: "Message sent to queue {storageQueueName} in {durationMs}ms"
    fields:
      storageAccountName:
        ref: storageAccountName
        required: true
      storageQueueName:
        ref: storageQueueName
        required: true
      durationMs:
        ref: durationMs
        required: true
      storageStatusCode:
        ref: storageStatusCode
        required: true
    metrics:
      otel.storage.queue.send.count:
        type: counter
        unit: "messages"
        description: "Total messages sent to queue"
        labels:
          - storageQueueName
      otel.storage.queue.send.duration:
        type: histogram
        unit: "ms"
        description: "Queue send duration"
        buckets: [1, 5, 10, 25, 50, 100, 250, 500]
    tags:
      - storage
      - queue

  storage.queue.received:
    id: 10306
    severity: INFO
    description: "Messages were received from an Azure Storage Queue."
    message: "Received {storageMessageCount} messages from queue {storageQueueName} in {durationMs}ms"
    fields:
      storageAccountName:
        ref: storageAccountName
        required: true
      storageQueueName:
        ref: storageQueueName
        required: true
      storageMessageCount:
        ref: storageMessageCount
        required: true
      durationMs:
        ref: durationMs
        required: true
      storageStatusCode:
        ref: storageStatusCode
        required: true
    metrics:
      otel.storage.queue.receive.count:
        type: counter
        unit: "receives"
        description: "Total queue receive operations"
        labels:
          - storageQueueName
      otel.storage.queue.receive.message.count:
        type: histogram
        unit: "messages"
        description: "Messages received per batch"
        buckets: [1, 5, 10, 20, 32]
    tags:
      - storage
      - queue

  storage.queue.failed:
    id: 10307
    severity: ERROR
    description: "An Azure Storage Queue operation failed."
    message: "Queue operation on {storageQueueName} failed with {storageStatusCode} after {durationMs}ms"
    exception: true
    fields:
      storageAccountName:
        ref: storageAccountName
        required: true
      storageQueueName:
        ref: storageQueueName
        required: false
      durationMs:
        ref: durationMs
        required: true
      storageStatusCode:
        ref: storageStatusCode
        required: true
      errorType:
        ref: errorType
        required: true
    metrics:
      otel.storage.queue.error.count:
        type: counter
        unit: "errors"
        description: "Total queue operation errors"
        labels:
          - storageQueueName
          - storageStatusCode
    tags:
      - storage
      - queue
      - error
```

#### Registration API

```csharp
// Register Azure Storage event capture
builder.Services.AddOtelEventsAzureStorage(options =>
{
    options.EnableBlobEvents = true;              // Default: true
    options.EnableQueueEvents = true;             // Default: true
    options.EnableCausalScope = true;             // Default: true
    options.ExcludeContainers = [];               // Exclude specific blob containers
    options.ExcludeQueues = [];                   // Exclude specific queues
});
```

#### Implementation Approach

**Mechanism:** Azure SDK `HttpPipelinePolicy` registered via `BlobClientOptions.AddPolicy()` and `QueueClientOptions.AddPolicy()`.

The Azure SDK for .NET uses an HTTP pipeline architecture. Custom policies can be inserted to observe requests and responses. `OtelEvents.Azure.Storage` adds a policy that:

1. Intercepts outgoing Azure Storage REST API requests.
2. Determines the operation type from the request URI and method (e.g., PUT to `/container/blob` = upload).
3. Measures duration.
4. On response: reads status code, content length, headers.
5. Emits the appropriate schema-defined event.

```csharp
/// <summary>
/// Azure SDK pipeline policy that observes Blob and Queue operations
/// and emits schema-defined structured events.
/// </summary>
internal sealed class OtelEventsStoragePipelinePolicy : HttpPipelinePolicy
{
    public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
    {
        var sw = Stopwatch.StartNew();
        ProcessNext(message, pipeline);
        sw.Stop();
        EmitEvent(message, sw.Elapsed.TotalMilliseconds);
    }

    public override async ValueTask ProcessAsync(
        HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
    {
        var sw = Stopwatch.StartNew();
        await ProcessNextAsync(message, pipeline);
        sw.Stop();
        EmitEvent(message, sw.Elapsed.TotalMilliseconds);
    }
}
```

**Registration pattern:** The pack provides extension methods on `BlobClientOptions` and `QueueClientOptions` for manual registration, plus an `IServiceCollection` extension for auto-registration:

```csharp
// Auto-registration via DI (configures default client options)
builder.Services.AddOtelEventsAzureStorage();

// Or manual registration on specific clients:
var blobOptions = new BlobClientOptions();
blobOptions.AddOtelEventsPolicy();
var blobClient = new BlobServiceClient(connectionString, blobOptions);
```

#### What It Complements

| Existing OTEL | What It Provides | What OtelEvents.Azure.Storage Adds |
|---------------|------------------|-------------------------------------|
| Azure SDK distributed tracing (`Azure.*` DiagnosticSource) | Trace spans per HTTP request to Azure Storage | Schema-defined events per logical operation (upload, download, delete, send, receive) |
| Neither | N/A | Blob size histograms, upload/download duration distribution, queue message batch size metrics |
| Neither | N/A | Operation-type-specific events (upload vs download vs delete) instead of generic HTTP spans |

#### Example JSON Output

**Event: `storage.blob.uploaded`**
```json
{"timestamp":"2025-07-15T14:30:00.567890Z","event":"storage.blob.uploaded","severity":"INFO","severityNumber":9,"message":"Blob uploaded to invoices/INV-2025-001.pdf (245760 bytes) in 134.2ms","service":"invoice-service","environment":"production","traceId":"4bf92f3577b34da6a3ce929d0e0e4736","spanId":"00f067aa0ba902b7","eventId":"evt_01914a32-1e2f-3a4b-5c6d-8e9f0a1b2c3d","attr":{"storageAccountName":"prodstore01","storageContainerName":"invoices","storageBlobName":"INV-2025-001.pdf","storageBlobSize":245760,"storageContentType":"application/pdf","durationMs":134.2,"storageStatusCode":201},"tags":["storage","blob"],"otel_events.v":"1.0.0","otel_events.seq":401,"otel_events.host":"web-01","otel_events.pid":4821}
```

**Event: `storage.queue.sent`**
```json
{"timestamp":"2025-07-15T14:30:00.678901Z","event":"storage.queue.sent","severity":"INFO","severityNumber":9,"message":"Message sent to queue order-processing in 8.3ms","service":"order-service","environment":"production","traceId":"4bf92f3577b34da6a3ce929d0e0e4736","spanId":"00f067aa0ba902b7","eventId":"evt_01914a33-2f3a-4b5c-6d7e-9f0a1b2c3d4e","attr":{"storageAccountName":"prodstore01","storageQueueName":"order-processing","durationMs":8.3,"storageStatusCode":201},"tags":["storage","queue"],"otel_events.v":"1.0.0","otel_events.seq":402,"otel_events.host":"web-01","otel_events.pid":4821}
```

---

### 15.7 OtelEvents.HealthChecks

#### Package & Dependencies

```
OtelEvents.HealthChecks (runtime)
├── Microsoft.Extensions.Diagnostics.HealthChecks (>= 8.0) — Health check abstractions
├── OpenTelemetry (>= 1.9)                                 — OTEL SDK types
├── OtelEvents.Causality (>= 1.0) [optional]                      — Causal scope
└── (no dependency on OtelEvents.Schema — code is pre-generated)
```

**Target frameworks:** `net8.0`, `net9.0`

#### YAML Schema

```yaml
schema:
  name: "OtelEvents.HealthChecks"
  version: "1.0.0"
  namespace: "OtelEvents.HealthChecks.Events"
  meterName: "OtelEvents.HealthChecks"
  description: "Schema-defined events for .NET health check execution and state changes"

fields:
  healthComponent:
    type: string
    description: "Name of the health check component (e.g., 'CosmosDb', 'Redis', 'SqlServer')"
    index: true

  healthStatus:
    type: enum
    description: "Health check result status"
    values: [Healthy, Degraded, Unhealthy]

  healthPreviousStatus:
    type: enum
    description: "Previous health check status (for state change events)"
    values: [Healthy, Degraded, Unhealthy]

  healthDurationMs:
    type: double
    description: "Health check execution duration in milliseconds"
    unit: "ms"

  healthDescription:
    type: string
    description: "Health check result description or reason"

  healthTotalChecks:
    type: int
    description: "Total number of health checks executed in the report"

  healthOverallStatus:
    type: enum
    description: "Overall health report status (aggregate of all checks)"
    values: [Healthy, Degraded, Unhealthy]

enums:
  HealthStatus:
    description: "Health check result status"
    values:
      - Healthy
      - Degraded
      - Unhealthy

events:
  health.check.executed:
    id: 10401
    severity: DEBUG
    description: "A health check was executed. Emitted for every health check poll cycle."
    message: "Health check {healthComponent} completed with {healthStatus} in {healthDurationMs}ms"
    fields:
      healthComponent:
        ref: healthComponent
        required: true
      healthStatus:
        ref: healthStatus
        required: true
      healthDurationMs:
        ref: healthDurationMs
        required: true
      healthDescription:
        ref: healthDescription
        required: false
    metrics:
      otel.health.check.duration:
        type: histogram
        unit: "ms"
        description: "Health check execution duration"
        buckets: [1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000]
      otel.health.check.status:
        type: gauge
        unit: "status"
        description: "Current health check status (0=Healthy, 1=Degraded, 2=Unhealthy)"
        labels:
          - healthComponent
    tags:
      - health

  health.state.changed:
    id: 10402
    severity: WARN
    description: >
      A health check component's status changed from one state to another.
      Only emitted when a state TRANSITION occurs — not on every poll.
      Severity is WARN for any state change (including recovery to Healthy)
      to ensure visibility.
    message: "Health state changed: {healthComponent} {healthPreviousStatus} → {healthStatus}: {healthDescription}"
    fields:
      healthComponent:
        ref: healthComponent
        required: true
      healthPreviousStatus:
        ref: healthPreviousStatus
        required: true
      healthStatus:
        ref: healthStatus
        required: true
      healthDurationMs:
        ref: healthDurationMs
        required: true
      healthDescription:
        ref: healthDescription
        required: false
    metrics:
      otel.health.state.change.count:
        type: counter
        unit: "transitions"
        description: "Total health state transitions"
        labels:
          - healthComponent
          - healthPreviousStatus
          - healthStatus
    tags:
      - health
      - state-change

  health.report.completed:
    id: 10403
    severity: DEBUG
    description: "A full health check report was completed (all checks executed in one cycle)."
    message: "Health report completed: {healthOverallStatus} ({healthTotalChecks} checks) in {healthDurationMs}ms"
    fields:
      healthOverallStatus:
        ref: healthOverallStatus
        required: true
      healthTotalChecks:
        ref: healthTotalChecks
        required: true
      healthDurationMs:
        ref: healthDurationMs
        required: true
    metrics:
      otel.health.report.duration:
        type: histogram
        unit: "ms"
        description: "Full health report execution duration"
        buckets: [10, 50, 100, 250, 500, 1000, 2500, 5000, 10000]
    tags:
      - health
```

#### Registration API

```csharp
// Register health check event publisher
builder.Services.AddHealthChecks()
    .AddCheck<CosmosDbHealthCheck>("CosmosDb")
    .AddCheck<RedisHealthCheck>("Redis")
    .AddCheck<SqlServerHealthCheck>("SqlServer");

// Integration pack — one line
builder.Services.AddOtelEventsHealthChecks(options =>
{
    options.EmitExecutedEvents = true;            // Default: true — emit health.check.executed per poll
    options.EmitStateChangedEvents = true;        // Default: true — emit health.state.changed on transitions
    options.EmitReportCompletedEvents = true;     // Default: true — emit health.report.completed per cycle
    options.SuppressHealthyExecutedEvents = false; // Default: false — set to true to only emit non-Healthy check events
    options.EnableCausalScope = true;             // Default: true
});
```

#### Implementation Approach

**Mechanism:** `IHealthCheckPublisher` implementation registered as a singleton service.

.NET's health check system supports `IHealthCheckPublisher` — services that receive `HealthReport` after each poll cycle. The integration pack implements this interface.

```csharp
/// <summary>
/// IHealthCheckPublisher that emits schema-defined events for health check
/// execution results and state changes.
/// </summary>
internal sealed class OtelEventsHealthCheckPublisher : IHealthCheckPublisher
{
    private readonly ILogger<OtelEventsHealthCheckEventSource> _logger;
    private readonly OtelEventsHealthCheckOptions _options;
    private readonly ConcurrentDictionary<string, HealthStatus> _previousStates = new();

**Bounded state tracking:** The `_previousStates` dictionary has a maximum capacity of 1,000 entries. If a system registers more than 1,000 unique health check names (which would indicate a configuration issue), new entries are rejected and a warning is logged. Health check component names should be static and finite — dynamically generated names are not supported.


    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        foreach (var entry in report.Entries)
        {
            // Emit health.check.executed (every poll)
            if (_options.EmitExecutedEvents &&
                !(_options.SuppressHealthyExecutedEvents && entry.Value.Status == HealthStatus.Healthy))
            {
                _logger.HealthCheckExecuted(
                    healthComponent: entry.Key,
                    healthStatus: MapStatus(entry.Value.Status),
                    healthDurationMs: entry.Value.Duration.TotalMilliseconds,
                    healthDescription: entry.Value.Description);
            }

            // Emit health.state.changed (only on transitions)
            if (_options.EmitStateChangedEvents)
            {
                var previousStatus = _previousStates.GetOrAdd(entry.Key, entry.Value.Status);
                if (previousStatus != entry.Value.Status)
                {
                    _logger.HealthStateChanged(
                        healthComponent: entry.Key,
                        healthPreviousStatus: MapStatus(previousStatus),
                        healthStatus: MapStatus(entry.Value.Status),
                        healthDurationMs: entry.Value.Duration.TotalMilliseconds,
                        healthDescription: entry.Value.Description);

                    _previousStates[entry.Key] = entry.Value.Status;
                }
            }
        }

        // Emit health.report.completed (aggregate)
        if (_options.EmitReportCompletedEvents)
        {
            _logger.HealthReportCompleted(
                healthOverallStatus: MapStatus(report.Status),
                healthTotalChecks: report.Entries.Count,
                healthDurationMs: report.TotalDuration.TotalMilliseconds);
        }

        return Task.CompletedTask;
    }
}
```

**State change detection:** The publisher maintains a `ConcurrentDictionary<string, HealthStatus>` that tracks the last-known status of each health check component. On the first poll, the initial status is recorded without emitting a state change event. Subsequent polls compare against the stored value — a `health.state.changed` event is emitted only when the status differs.

**Relationship to existing Phase 2 item 2.4:** The existing specification defines Phase 2 feature 2.4 as "Health/readiness events — Built-in schema for application lifecycle events: startup, ready, degraded, shutdown." `OtelEvents.HealthChecks` is **complementary** — 2.4 covers application lifecycle (process-level), while this pack covers component-level health check results from `Microsoft.Extensions.Diagnostics.HealthChecks`. They coexist.

#### What It Complements

| Existing .NET Feature | What It Provides | What OtelEvents.HealthChecks Adds |
|-----------------------|------------------|------------------------------------|
| `IHealthCheck` + `/health` endpoint | JSON response with check status for load balancers/K8s | Schema-defined log events for every poll cycle and state transitions |
| `IHealthCheckPublisher` (built-in) | Publication hook (no event emission) | Structured events with duration histograms, state change counters, AI-optimized JSON |
| ASP.NET Health UI / third-party dashboards | Visual health status | Machine-readable state change events for automated alerting and AI investigation |

#### Example JSON Output

**Event: `health.check.executed`**
```json
{"timestamp":"2025-07-15T14:30:30.000000Z","event":"health.check.executed","severity":"DEBUG","severityNumber":5,"message":"Health check CosmosDb completed with Healthy in 12.4ms","service":"order-service","environment":"production","eventId":"evt_01914a34-3a4b-5c6d-7e8f-0a1b2c3d4e5f","attr":{"healthComponent":"CosmosDb","healthStatus":"Healthy","healthDurationMs":12.4},"tags":["health"],"otel_events.v":"1.0.0","otel_events.seq":501,"otel_events.host":"web-01","otel_events.pid":4821}
```

**Event: `health.state.changed`**
```json
{"timestamp":"2025-07-15T14:31:00.000000Z","event":"health.state.changed","severity":"WARN","severityNumber":13,"message":"Health state changed: Redis Healthy → Degraded: Connection pool exhausted (23/25 connections in use)","service":"order-service","environment":"production","eventId":"evt_01914a35-4b5c-6d7e-8f9a-1b2c3d4e5f6a","attr":{"healthComponent":"Redis","healthPreviousStatus":"Healthy","healthStatus":"Degraded","healthDurationMs":5023.1,"healthDescription":"Connection pool exhausted (23/25 connections in use)"},"tags":["health","state-change"],"otel_events.v":"1.0.0","otel_events.seq":510,"otel_events.host":"web-01","otel_events.pid":4821}
```

**Event: `health.report.completed`**
```json
{"timestamp":"2025-07-15T14:31:00.050000Z","event":"health.report.completed","severity":"DEBUG","severityNumber":5,"message":"Health report completed: Degraded (3 checks) in 5045.2ms","service":"order-service","environment":"production","eventId":"evt_01914a36-5c6d-7e8f-9a0b-2c3d4e5f6a7b","attr":{"healthOverallStatus":"Degraded","healthTotalChecks":3,"healthDurationMs":5045.2},"tags":["health"],"otel_events.v":"1.0.0","otel_events.seq":513,"otel_events.host":"web-01","otel_events.pid":4821}
```

---

### 15.8 Integration Pack Testing Strategy

Each integration pack includes its own test suite following the project's testing pyramid (Section 11).

#### Unit Tests (per pack)

| Test Area | Examples |
|-----------|---------|
| Event emission | Each event type is emitted with correct field values, severity, and event name |
| Optional fields | Null/absent optional fields are omitted (not emitted as null) |
| Metric recording | Histogram, counter, and gauge records with correct values and labels |
| Configuration | Exclude patterns work; feature flags toggle event emission |
| Path/route sanitization | Long paths truncated; route template used instead of raw path (ASP.NET Core) |
| State change detection | `health.state.changed` only on transitions, not every poll (HealthChecks) |
| Error handling | Pack middleware/interceptor never swallows exceptions; always re-throws |

#### Integration Tests (per pack)

| Test Area | Examples |
|-----------|---------|
| Full OTEL pipeline | Pack emits event → OTEL pipeline → `OtelEventsJsonExporter` → verify JSONL output |
| DI registration | `AddOtelEvents*()` registers all required services correctly |
| Causal scope | Events within a request/call share `parentEventId` when `OtelEvents.Causality` is present |
| OTEL coexistence | Pack runs alongside `AddAspNetCoreInstrumentation()` without conflict |
| Metrics pipeline | Pack-generated metrics visible in `MeterProvider` |

#### E2E Tests (integration packs, combined)

| Test Area | Examples |
|-----------|---------|
| Multi-pack stack | ASP.NET Core + CosmosDB + HealthChecks all emit events in a single request |
| Causal tree | HTTP request parent → CosmosDB query child → verify causal chain in JSON output |
| Performance | Pack middleware adds < 100μs overhead per request (BenchmarkDotNet) |

#### Benchmarks

| Benchmark | Target | What It Measures |
|-----------|--------|-----------------|
| `AspNetCoreMiddlewareOverhead` | < 100μs p95 | Middleware processing time (excluding request pipeline) |
| `GrpcInterceptorOverhead` | < 50μs p95 | Interceptor processing time (excluding gRPC call) |
| `CosmosDbObserverOverhead` | < 50μs p95 | Diagnostic observer processing time |
| `StoragePolicyOverhead` | < 50μs p95 | Pipeline policy processing time |
| `HealthCheckPublisherOverhead` | < 25μs p95 | Publisher processing time per check |

---

### 15.9 Phase Assignment

| Pack | Phase | Rationale |
|------|-------|-----------|
| **OtelEvents.AspNetCore** | **Phase 2** (2.6) | Most universal use case — every ASP.NET Core API benefits. Builds on Phase 1 core. |
| **OtelEvents.HealthChecks** | **Phase 2** (2.7) | Complements existing Phase 2 item 2.4 (app lifecycle events). Simple implementation via `IHealthCheckPublisher`. |
| **OtelEvents.Grpc** | **Phase 3** (3.7) | Second most common RPC mechanism. Replaces the deferred "gRPC service events" item from Section 14. |
| **OtelEvents.Azure.CosmosDb** | **Phase 3** (3.8) | Azure-specific — lower priority than universal HTTP/gRPC packs. |
| **OtelEvents.Azure.Storage** | **Phase 3** (3.9) | Azure-specific — lower priority than universal HTTP/gRPC packs. |

#### Implementation Order Within Each Phase

**Phase 2 (Integration Packs):**
1. **2.6 OtelEvents.AspNetCore** — highest value, most common scenario
2. **2.7 OtelEvents.HealthChecks** — simple, complements 2.4

**Phase 3 (Integration Packs):**
1. **3.7 OtelEvents.Grpc** — replaces deferred item "gRPC service events"
2. **3.8 OtelEvents.Azure.CosmosDb** — Azure data tier
3. **3.9 OtelEvents.Azure.Storage** — Azure storage tier

---

### 15.10 Complete Registration Example (All Integration Packs)

```csharp
var builder = WebApplication.CreateBuilder(args);

// ─── Standard OTEL Setup ────────────────────────────────────────────────
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("order-service"))
    .WithLogging(logging =>
    {
        // otel-events core: causal linking
        logging.AddProcessor<OtelEventsCausalityProcessor>();
        // otel-events core: AI-optimized JSON stdout
        logging.AddOtelEventsJsonExporter(options =>
        {
            options.Output = OtelEventsJsonOutput.Stdout;
            options.SchemaVersion = "1.0.0";
        });
        // Standard OTEL: export to collector
        logging.AddOtlpExporter();
    })
    .WithMetrics(metrics =>
    {
        // Pick up otel-events integration pack meters + app-defined meters
        metrics.AddMeter("OtelEvents.*");
        metrics.AddMeter("MyCompany.OrderService.Events.*");
        metrics.AddOtlpExporter();
    })
    .WithTracing(tracing =>
    {
        // Standard OTEL auto-instrumentation (traces — complementary)
        tracing.AddAspNetCoreInstrumentation();
        tracing.AddGrpcClientInstrumentation();
        tracing.AddOtlpExporter();
    });

// ─── Integration Packs ─────────────────────────────────────────────────
builder.Services.AddOtelEventsAspNetCore(options =>
{
    options.ExcludePaths = ["/health", "/ready", "/metrics"];
});

builder.Services.AddOtelEventsGrpc();

builder.Services.AddOtelEventsCosmosDb(options =>
{
    options.CaptureQueryText = false;  // PII safety
});

builder.Services.AddOtelEventsAzureStorage();

builder.Services.AddOtelEventsHealthChecks(options =>
{
    options.SuppressHealthyExecutedEvents = true;  // Only emit non-Healthy + state changes
});

// ─── Standard .NET HealthChecks ─────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddCheck<CosmosDbHealthCheck>("CosmosDb")
    .AddCheck<RedisHealthCheck>("Redis");

// ─── Standard .NET Logging Filters ──────────────────────────────────────
builder.Logging.AddFilter<OpenTelemetryLoggerProvider>(
    "OtelEvents.AspNetCore", LogLevel.Information);
builder.Logging.AddFilter<OpenTelemetryLoggerProvider>(
    "OtelEvents.Azure.CosmosDb", LogLevel.Debug);
builder.Logging.AddFilter<OpenTelemetryLoggerProvider>(
    "OtelEvents.HealthChecks", LogLevel.Debug);

var app = builder.Build();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();
```

---

## Addendum: Updates to Existing Sections

> **Note:** The following updates should be applied to existing sections. They are listed here as addenda — the existing section content remains unchanged, and these items are added to the appropriate tables and lists.

### Addendum to Section 3: Core Features by Priority

**Add to Phase 2 — Production Readiness table:**

| # | Feature | Description |
|---|---------|-------------|
| 2.6 | **OtelEvents.AspNetCore integration pack** | Pre-built ASP.NET Core middleware that auto-emits schema-defined `http.request.received`, `http.request.completed`, `http.request.failed` events with causal scope per request |
| 2.7 | **OtelEvents.HealthChecks integration pack** | Pre-built `IHealthCheckPublisher` that emits `health.check.executed` and `health.state.changed` events; complements 2.4 lifecycle events |

**Add to Phase 3 — Ecosystem & Scale table:**

| # | Feature | Description |
|---|---------|-------------|
| 3.7 | **OtelEvents.Grpc integration pack** | Pre-built gRPC server/client interceptors for `grpc.call.started`, `grpc.call.completed`, `grpc.call.failed` events (replaces deferred "gRPC service events" item) |
| 3.8 | **OtelEvents.Azure.CosmosDb integration pack** | DiagnosticListener-based observer for `cosmosdb.query.executed`, `cosmosdb.point.read`, `cosmosdb.point.write` events with RU histograms |
| 3.9 | **OtelEvents.Azure.Storage integration pack** | Azure SDK pipeline policy for `storage.blob.*` and `storage.queue.*` events |

### Addendum to Section 5: NuGet Package Structure

**Add to the package tree:**

```
OtelEvents (meta-package — references all below)
├── OtelEvents.Schema                  — YAML parser, schema model, validation, code generator
├── OtelEvents.Exporter.Json           — Custom OTEL BaseExporter<LogRecord> for AI-optimized JSONL
├── OtelEvents.Causality               — Custom OTEL BaseProcessor<LogRecord> for eventId/parentEventId
├── OtelEvents.Analyzers               — Roslyn analyzers (Console.Write, ILogger, etc.)
├── OtelEvents.Testing                 — In-memory LogRecord collector, assertion extensions
│
└── Integration Packs (separate packages, not part of meta-package):
    ├── OtelEvents.AspNetCore        — ASP.NET Core middleware for HTTP request events    [Phase 2]
    ├── OtelEvents.HealthChecks      — IHealthCheckPublisher for health check events      [Phase 2]
    ├── OtelEvents.Grpc              — gRPC server/client interceptors for call events    [Phase 3]
    ├── OtelEvents.Azure.CosmosDb    — DiagnosticListener for CosmosDB operation events   [Phase 3]
    └── OtelEvents.Azure.Storage     — Pipeline policy for Blob/Queue operation events    [Phase 3]
```

**Add to Adoption Scenarios table:**

| Scenario | Packages Needed |
|----------|----------------|
| Auto-events for ASP.NET Core APIs | `OtelEvents.Schema` (or not — pack is self-contained) + `OtelEvents.AspNetCore` |
| Full stack with ASP.NET Core + CosmosDB | `OtelEvents.Exporter.Json` + `OtelEvents.Causality` + `OtelEvents.AspNetCore` + `OtelEvents.Azure.CosmosDb` |
| Health monitoring with state change alerts | `OtelEvents.HealthChecks` |
| Full integration pack suite | `OtelEvents.Exporter.Json` + `OtelEvents.Causality` + `OtelEvents.AspNetCore` + `OtelEvents.Grpc` + `OtelEvents.Azure.CosmosDb` + `OtelEvents.Azure.Storage` + `OtelEvents.HealthChecks` |

**Add to "Why This Split?" table:**

| Decision | Rationale |
|----------|-----------|
| Integration packs are NOT part of the `OtelEvents` meta-package | They bring external dependencies (ASP.NET Core, gRPC, Azure SDKs) — shouldn't be pulled transitively. Opt-in per technology. |
| Integration packs use `OtelEvents.*` prefix | Distinguishes pre-built packs from core infrastructure. Clear marketing: "OtelEvents for [technology]." |
| Integration packs ship pre-compiled code | Consumers don't need `OtelEvents.Schema` at build time. Reduces dependency graph and build complexity. |
| Each pack is independently versioned | Packs may release on different cadences than core — Azure SDK updates, gRPC updates, etc. |

### Addendum to Section 14: Out of Scope & Explicit Non-Goals

**Update "Deferred to Future Versions" table — replace the "gRPC service events" row:**

| Feature | Target Phase | Notes |
|---------|-------------|-------|
| ~~gRPC service events~~ | ~~Phase 3+~~ | ~~Built-in gRPC request/response events~~ → **Now covered by `OtelEvents.Grpc` integration pack (Phase 3, item 3.7)** |

**Add to "Deferred to Future Versions" table:**

| Feature | Target Phase | Notes |
|---------|-------------|-------|
| OtelEvents.EntityFrameworkCore | Phase 3+ | Integration pack for EF Core query events (DiagnosticListener-based) |
| OtelEvents.MassTransit | Phase 3+ | Integration pack for MassTransit message bus events |
| OtelEvents.Azure.ServiceBus | Phase 3+ | Integration pack for Azure Service Bus operations |
| OtelEvents.Redis | Phase 3+ | Integration pack for StackExchange.Redis operations |
| OtelEvents.HttpClient | Phase 3+ | Integration pack for outbound `HttpClient` calls |
| Custom integration pack SDK | Phase 3+ | Tooling/template for third parties to create their own integration packs |

### Addendum to Appendix C: File Structure

**Add to the `src/` directory:**

```
├── src/
│   ├── ... (existing core packages) ...
│   │
│   ├── OtelEvents.AspNetCore/
│   │   ├── OtelEvents.AspNetCore.csproj
│   │   ├── Events/
│   │   │   ├── OtelEventsAspNetCoreEventSource.cs    # Logger category type
│   │   │   └── HttpRequestEvents.g.cs                # Pre-generated event methods
│   │   ├── OtelEventsAspNetCoreMiddleware.cs          # IMiddleware implementation
│   │   ├── OtelEventsAspNetCoreStartupFilter.cs       # IStartupFilter for auto-registration
│   │   ├── OtelEventsAspNetCoreOptions.cs             # Configuration options
│   │   ├── OtelEventsAspNetCoreExtensions.cs          # AddOtelEventsAspNetCore() extension
│   │   └── Schemas/
│   │       └── aspnetcore.otel.yaml                    # Bundled schema (embedded resource)
│   │
│   ├── OtelEvents.Grpc/
│   │   ├── OtelEvents.Grpc.csproj
│   │   ├── Events/
│   │   │   ├── OtelEventsGrpcEventSource.cs
│   │   │   └── GrpcCallEvents.g.cs
│   │   ├── OtelEventsGrpcServerInterceptor.cs
│   │   ├── OtelEventsGrpcClientInterceptor.cs
│   │   ├── OtelEventsGrpcOptions.cs
│   │   ├── OtelEventsGrpcExtensions.cs
│   │   └── Schemas/
│   │       └── grpc.otel.yaml
│   │
│   ├── OtelEvents.Azure.CosmosDb/
│   │   ├── OtelEvents.Azure.CosmosDb.csproj
│   │   ├── Events/
│   │   │   ├── OtelEventsCosmosDbEventSource.cs
│   │   │   └── CosmosDbEvents.g.cs
│   │   ├── OtelEventsCosmosDbDiagnosticObserver.cs
│   │   ├── OtelEventsCosmosDbOptions.cs
│   │   ├── OtelEventsCosmosDbExtensions.cs
│   │   └── Schemas/
│   │       └── cosmosdb.otel.yaml
│   │
│   ├── OtelEvents.Azure.Storage/
│   │   ├── OtelEvents.Azure.Storage.csproj
│   │   ├── Events/
│   │   │   ├── OtelEventsStorageEventSource.cs
│   │   │   └── StorageEvents.g.cs
│   │   ├── OtelEventsStoragePipelinePolicy.cs
│   │   ├── OtelEventsStorageOptions.cs
│   │   ├── OtelEventsStorageExtensions.cs
│   │   └── Schemas/
│   │       └── storage.otel.yaml
│   │
│   └── OtelEvents.HealthChecks/
│       ├── OtelEvents.HealthChecks.csproj
│       ├── Events/
│       │   ├── OtelEventsHealthCheckEventSource.cs
│       │   └── HealthCheckEvents.g.cs
│       ├── OtelEventsHealthCheckPublisher.cs
│       ├── OtelEventsHealthCheckOptions.cs
│       ├── OtelEventsHealthCheckExtensions.cs
│       └── Schemas/
│           └── healthchecks.otel.yaml
│
├── tests/
│   ├── ... (existing test projects) ...
│   ├── OtelEvents.AspNetCore.Tests/
│   ├── OtelEvents.Grpc.Tests/
│   ├── OtelEvents.Azure.CosmosDb.Tests/
│   ├── OtelEvents.Azure.Storage.Tests/
│   ├── OtelEvents.HealthChecks.Tests/
│   └── OtelEvents.Integration.Tests/          # Cross-pack integration tests
│
├── samples/
│   ├── ... (existing samples) ...
│   └── OtelEvents.Samples.IntegrationPacks/          # Sample showing all packs together
```

### Addendum to Appendix E: Adoption Story

**Add new section after "Migration Path: Manual → otel-events":**

```
### With Integration Packs (fastest path)

Step 1: dotnet add package OtelEvents.Exporter.Json     (AI-optimized JSONL output)
Step 2: dotnet add package OtelEvents.Causality         (causal event linking)
Step 3: dotnet add package OtelEvents.AspNetCore (auto HTTP request events)
Step 4: dotnet add package OtelEvents.HealthChecks (auto health check events)
Step 5: Add 3 lines to Program.cs:
        builder.Services.AddOtelEventsAspNetCore();
        builder.Services.AddOtelEventsHealthChecks();
        metrics.AddMeter("OtelEvents.*");
Step 6: Run — consistent, schema-defined events flowing immediately.

No YAML to write. No code to generate. No [LoggerMessage] to define.
Infrastructure events are handled by integration packs.
Application-specific events can be added later via OtelEvents.Schema + YAML.
```

**Update "Time to first event" targets in Section 13 (Success Metrics):**

| Metric | Target | How Measured |
|--------|--------|-------------|
| Time to first event (integration pack) | < 2 minutes | User testing — add package, add one line to Program.cs, run |

### Addendum to Appendix B: Decision Record Summary

**Add:**

| # | Decision | Choice | Alternatives Considered |
|---|----------|--------|------------------------|
| DR-018 | Integration pack naming prefix | `OtelEvents.*` (distinct from core `OtelEvents.*`) | Same `OtelEvents.*` prefix; `OtelEvents.IntegrationPacks.*`; `OtelEvents.Events.*` |
| DR-019 | Integration pack code distribution | Pre-compiled in NuGet (no consumer-side code generation) | Ship YAML only (require consumer to reference `OtelEvents.Schema`); Ship as source package |
| DR-020 | Integration pack meta-package inclusion | NOT included in `OtelEvents` meta-package | Included in meta-package; separate `OtelEvents` meta-package |
| DR-021 | Event ID range separation | Consumer: 1–9999, Integration packs: 10000+ | Shared range with collision detection; prefix-based disambiguation |
| DR-022 | Integration pack `OtelEvents.Causality` dependency | Optional (auto-detected at runtime) | Required; not supported |

---

## Open Questions (Integration Packs)

- [ ] **OQ-IP-01:** Should integration packs emit events at `DEBUG` or `INFO` severity by default? Current spec uses `INFO` for request lifecycle events and `DEBUG` for data operations. Teams may want different defaults.
- [ ] **OQ-IP-02:** Should `OtelEvents.Azure.CosmosDb` use `DiagnosticListener` (non-invasive, fewer fields) or `RequestHandler` (requires CosmosClient setup change, more fields)? Needs prototyping.
- [ ] **OQ-IP-03:** Should integration packs support `appsettings.json` configuration in addition to code-based `options =>` configuration? Standard pattern for .NET libraries.
- [ ] **OQ-IP-04:** Should `OtelEvents.AspNetCore` capture request/response bodies as opt-in fields? PII risk but high diagnostic value.
- [ ] **OQ-IP-05:** Should there be an `OtelEvents` meta-package that references all integration packs? Or is explicit per-pack installation the preferred model?
- [ ] **OQ-IP-06:** Package naming — confirm `OtelEvents.*` prefix vs. aligning with core `OtelEvents.*` prefix. Needs marketing/branding decision.
- [ ] **OQ-IP-07:** Should integration pack meters share the same meter instance pattern (static readonly) as core otel-events generated code, or use DI-injected `IMeterFactory` (the newer .NET 8+ pattern)?
```

---
