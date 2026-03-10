# Chapter 1 — Introduction: What is otel-events?

## The Problem with Freestyle Logging

Picture a .NET team with ten developers building an order-processing service. Each developer logs in their own style:

```csharp
// Developer A
_logger.LogInformation("Order {OrderId} placed by {UserId} for ${Amount}", orderId, userId, amount);

// Developer B
_logger.LogInformation("New order created: id={Id}, customer={Customer}, total={Total}",
    order.Id, request.CustomerId, request.Total);

// Developer C
_logger.LogInformation($"Order placed - {orderId} by user {userId}");  // string interpolation!

// Developer D (forgot to log entirely)
```

Four developers, four styles, one event. Now multiply this by 200 events across 50 services over two years. The result:

1. **Inconsistent logging** — Different developers write different messages for the same events. No central schema governs what events exist or what fields they carry.

2. **Boilerplate proliferation** — Using `[LoggerMessage]` correctly requires writing partial methods, creating `Meter`/`Counter`/`Histogram` instances, wiring up `TagList`s, and keeping metrics and logs in sync. It's repetitive, error-prone, and tedious.

3. **Missing causal correlation** — OTEL's `Activity` handles distributed traces, but causal relationships between events *within* a service ("this error happened BECAUSE of that timeout") are invisible. No standard mechanism links a `LogRecord` to its causal parent.

4. **No AI-optimized output** — OTEL's default exporters produce output for backends (OTLP, Jaeger), but local container output (stdout) is either unstructured text or verbose JSON. There is no exporter that produces compact, single-line, AI-investigation-friendly JSONL.

5. **Logging hygiene decay** — Teams start with good practices, then drift. Someone adds `Console.WriteLine` for debugging and it stays. Third-party libraries use `ILogger` with uncontrolled formats. No compile-time enforcement exists.

---

## What is otel-events?

**otel-events** is an **extension to the OpenTelemetry .NET SDK**. It is not a standalone observability library, not a replacement for OTEL, and not a wrapper around it.

> otel-events is NOT a replacement for OpenTelemetry. It is an extension that adds schema-driven code generation, AI-optimized JSON export, and causal event linking to your existing OTEL pipeline.

otel-events extends the standard OpenTelemetry pipeline with exactly four components. Everything else is standard OTEL:

| otel-events Component | OTEL Extension Point | What it does |
|---------------|---------------------|-------------|
| **OtelEvents.Schema** (build-time) | N/A — build-time only | Parses YAML → generates C# code that uses `[LoggerMessage]` + `Meter` + `Counter<T>` + `Histogram<T>` |
| **OtelEventsJsonExporter** | `BaseExporter<LogRecord>` | Formats `LogRecord`s as AI-optimized single-line JSONL to stdout/file |
| **OtelEventsCausalityProcessor** | `BaseProcessor<LogRecord>` | Adds `all.event_id` (UUID v7) and `all.parent_event_id` to each `LogRecord` |
| **OtelEvents.Analyzers** | Roslyn `DiagnosticAnalyzer` | Compile-time enforcement — detects `Console.Write`, validates schema usage |

### Core Thesis

> Every observable thing that happens in a system is an **event**. Events are **defined in YAML schemas**, not improvised as string templates. A code generator turns schemas into **type-safe C# methods** that emit native OpenTelemetry `LogRecord`s and `Meter` recordings. The output is **structured, predictable, and machine-readable** — optimized for both human debugging and AI-powered investigation.

---

## How otel-events Extends OTEL

otel-events does not replace, wrap, or abstract OpenTelemetry. Projects already using the OpenTelemetry .NET SDK can **adopt otel-events incrementally** — add a package, point it at a YAML schema, and get type-safe, schema-enforced events flowing through their existing OTEL pipeline. No migration. No replacement. No parallel infrastructure.

### What otel-events provides

- **Schema-driven events** — Define events in YAML, get type-safe C# extension methods via code generation
- **AI-optimized JSON export** — Compact, single-line JSONL output optimized for machine investigation
- **Causal event linking** — Track cause-and-effect relationships between events via `eventId`/`parentEventId`
- **Compile-time enforcement** — Roslyn analyzers catch `Console.Write`, untyped `ILogger` usage, and schema violations

### What OTEL handles (otel-events does NOT provide these)

| Responsibility | OTEL Component |
|---------------|---------------|
| Log export to backends | OTLP Exporter, Console Exporter |
| Trace correlation (`traceId`, `spanId`) | `Activity` / `ActivitySource` |
| Metrics aggregation & export | `MeterProvider`, OTLP Exporter |
| Distributed context propagation | OTEL Propagators |
| `ILogger` → `LogRecord` bridging | `OpenTelemetryLoggerProvider` |
| Resource enrichment (`service.name`) | OTEL `ResourceBuilder` |

---

## Packages

otel-events is split into focused packages — use only what you need:

| Package | Description | When to use |
|---------|-------------|-------------|
| `OtelEvents.Schema` | YAML parser, schema model, validation, code generator | Always — this is the core value |
| `OtelEvents.Exporter.Json` | Custom OTEL `BaseExporter<LogRecord>` for AI-optimized JSONL | When you want structured JSONL on stdout |
| `OtelEvents.Causality` | Custom OTEL `BaseProcessor<LogRecord>` for causal event linking | When you want cause-and-effect trees |
| `OtelEvents.Analyzers` | Roslyn analyzers for logging hygiene enforcement | When you want compile-time rules |
| `OtelEvents.Testing` | In-memory `LogRecord` collector and assertion extensions | In test projects |

### Adoption Scenarios

| Scenario | Packages Needed |
|----------|----------------|
| Schema-driven events + OTLP export (no JSON stdout) | `OtelEvents.Schema` |
| Schema-driven events + AI-optimized JSON stdout | `OtelEvents.Schema` + `OtelEvents.Exporter.Json` |
| Full otel-events experience | `OtelEvents.Schema` + `OtelEvents.Exporter.Json` + `OtelEvents.Causality` + `OtelEvents.Analyzers` |
| Greenfield project (everything) | `OtelEvents` (meta-package) |

---

## Design Philosophy

| Principle | Implementation |
|-----------|---------------|
| **OTEL-native** | Generated code creates `LogRecord`s and `Meter` recordings directly — no intermediate types |
| **Schema-first** | Events defined in YAML → C# generated → compile-time enforced |
| **Type-safe** | No `string.Format`, no anonymous objects — typed methods with IntelliSense |
| **Incremental adoption** | Teams already on OTEL add otel-events packages and get value immediately |
| **Consistent** | Every JSON output line is the same AI-optimized envelope |
| **AI-optimized** | Predictable structure, causal trees, structured exceptions |
| **Zero-friction** | Works with `dotnet add package`, extends existing OTEL setup |
| **AOT-ready** | `[LoggerMessage]` source generator, System.Text.Json source generators, no reflection |

---

## Next Steps

- [Chapter 2 — otel-events vs Plain OTEL](02-otel-events-vs-plain-otel.md) — see the before and after, side by side
- [Chapter 4 — Getting Started](04-getting-started.md) — emit your first event in 10 minutes
