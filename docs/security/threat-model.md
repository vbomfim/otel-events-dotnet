# Threat Model

> Source: [SPECIFICATION.md §16.1](../../SPECIFICATION.md)

This document describes the trust boundaries, threat vectors, and mitigations for ALL components.

## Trust Boundaries

```
┌──────────────────────────────────────────────────────────────────┐
│  TRUST BOUNDARY: Application Process                             │
│                                                                  │
│  ┌──────────────┐     ┌─────────────────┐     ┌──────────────┐  │
│  │ Application   │────▶│ ALL Generated   │────▶│ OTEL SDK     │  │
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
│ (Fluent Bit,     │    │                 │
│  Vector, etc.)   │    │                 │
│ (trusted infra)  │    │ (trusted infra) │
└─────────────────┘    └─────────────────┘
```

### Key Trust Assumptions

- **Application code** and **ALL-generated code** run within the same process and are fully trusted.
- **Third-party libraries** are semi-trusted — they access `ILogger` through the OTEL SDK bridge and may emit PII unintentionally.
- **YAML schema files** are untrusted input parsed at build time with enforced resource limits (1 MB max size, 500 events, 50 fields per event, depth 20).
- **Log collectors** and **OTEL Collector** are trusted infrastructure components outside the application process boundary.
- **AsyncLocal trust**: `AllCausalityContext` uses `AsyncLocal<T>` — any code in the async flow can set `parentEventId`. Cross-process causality requires OTEL trace context propagation, not `AsyncLocal`.

## Threat Vectors

| Threat | Vector | Mitigation |
|--------|--------|------------|
| **PII leakage in logs** | User-Agent, Client IP, user IDs emitted to log storage | Sensitivity classification (§6), default `false` for PII capture, `EnvironmentProfile` redaction — see [PII Classification](pii-classification.md) |
| **Information disclosure via exceptions** | Stack traces expose file paths, internal class names, line numbers | `ExceptionDetailLevel` — `TypeAndMessage` in Production (no stack traces) — see [Environment Profiles](environment-profiles.md) |
| **Information disclosure via metadata** | `all.host` and `all.pid` expose infrastructure details | Opt-in only (`EmitHostInfo = false` default) |
| **Third-party library PII leakage** | Non-ALL `ILogger` calls may include connection strings, tokens, PII | `AttributeAllowlist`/`AttributeDenylist`, `RedactPatterns` regex filtering |
| **Schema injection / DoS** | Malicious YAML files with excessive size, nesting, or YAML bombs | Safe YAML loading, resource limits (1 MB, 500 events, 50 fields, depth 20) |
| **Reserved prefix hijacking** | Application code setting `all.*` attributes to spoof metadata | Runtime stripping of non-ALL `all.*` attributes in exporter (§16.4) |
| **Credential exposure in field values** | Connection strings, API keys, bearer tokens in attribute values | `sensitivity: credential` classification, regex-based `RedactPatterns`, defense-in-depth value sanitization |
| **Unbounded attribute values** | Extremely long string values causing memory pressure or log bloat | `MaxAttributeValueLength` (default: 4096), per-field `maxLength` |
| **AsyncLocal trust in causality** | Any code in the async flow can set `parentEventId` | Documented trust assumption — causal context is set by trusted code within the process |

## Defense-in-Depth Value Sanitization

As a last line of defense, the exporter applies pattern-based value sanitization to **all** attribute values. This catches connection strings, tokens, and API keys that might be accidentally included in schema-defined fields.

### Default Patterns (Always Active)

```regex
# Connection strings
Server=.*;(User Id|Password|Pwd)=.*
Data Source=.*;(User ID|Password)=.*

# Bearer tokens
Bearer\s+[A-Za-z0-9\-._~+/]+=*

# Common API key patterns
(api[_-]?key|apikey|access[_-]?token|secret[_-]?key)\s*[=:]\s*\S{16,}
```

Values matching these patterns are replaced with `"[REDACTED:pattern]"`. This sanitization:

- Is applied **after** sensitivity-based redaction
- Is **non-configurable** (always active as defense-in-depth)
- Covers **all** attribute values, not just non-ALL LogRecords

## Reserved Prefix Runtime Enforcement

At build time, the schema validator rejects field names starting with `all.` (rule `ALL_SCHEMA_011`). At runtime, the exporter enforces this for non-ALL `LogRecord`s:

1. During `Export()`, iterate over `LogRecord.Attributes`.
2. Any attribute with key starting with `all.` that was **not** set by `AllCausalityProcessor` or `AllJsonExporter` itself is **stripped**.
3. Increment `all.exporter.json.reserved_prefix_stripped` counter for each occurrence.
4. This prevents application code or third-party libraries from spoofing ALL metadata fields.

## Related

- [PII Classification](pii-classification.md) — Sensitivity levels and redaction rules
- [Environment Profiles](environment-profiles.md) — Per-environment security defaults
- [OWASP Mapping](owasp-mapping.md) — OWASP Top 10 alignment
