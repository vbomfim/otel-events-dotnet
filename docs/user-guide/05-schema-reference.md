# Chapter 5 — Schema Reference

This chapter is the complete reference for the `.otel.yaml` schema format. Every attribute, type, and validation rule is documented here.

---

## Schema File Conventions

- Extension: `.otel.yaml` or `.otel.yml`
- A project can have **multiple schema files** — they are merged by the code generator
- Event names must be globally unique across all merged schemas
- Event numeric IDs must be globally unique across all merged schemas

---

## Full Schema Grammar

```yaml
# ─── Schema Header (required) ───────────────────────────────────────
schema:
  name: "MyService"                    # Required: schema name
  version: "1.0.0"                     # Required: semver
  namespace: "MyCompany.MyService"     # Required: C# namespace for generated code
  description: "Events for MyService"  # Optional: human-readable description
  meterName: "MyCompany.MyService"     # Optional: OTEL Meter name (defaults to namespace)

# ─── Enum Type Definitions (optional) ────────────────────────────────
enums:
  EnumName:
    description: "Enum description"    # Optional
    values:
      - Value1
      - Value2
      - Value3

# ─── Event Definitions (required) ────────────────────────────────────
events:
  category.subcategory.action:         # Event name: dot-namespaced, lowercase
    id: 1001                           # Required: unique numeric EventId
    severity: INFO                     # Required: TRACE|DEBUG|INFO|WARN|ERROR|FATAL
    description: "What happened"       # Optional: event description
    message: "Template with {field}"   # Required: message template
    exception: false                   # Optional: adds Exception parameter (default: false)
    fields:                            # Required: at least one field
      - fieldName                      # Shorthand: just a name (string, optional)
      - otherField: { required: true } # With annotation
      - piiField: { sensitivity: pii } # With sensitivity
      - bounded: { maxLength: 512 }    # With max length
    metrics:                           # Optional: associated metrics
      metric.name:
        type: counter                  # counter, histogram, or gauge
        unit: "requests"               # Optional: unit of measurement
        description: "What it counts"  # Optional
        buckets: [5, 10, 25, 50]       # Optional: histogram bucket boundaries
        labels:                        # Optional: tag labels from fields
          - fieldName
    tags:                              # Optional: static categorization labels
      - category
      - subcategory
```

---

## Schema Header

| Property | Required | Type | Description |
|----------|----------|------|-------------|
| `name` | ✅ | string | Schema name — used for generated class names |
| `version` | ✅ | string | Semantic version (e.g., `"1.0.0"`) |
| `namespace` | ✅ | string | C# namespace for generated code |
| `description` | ❌ | string | Human-readable description |
| `meterName` | ❌ | string | OTEL Meter name. Defaults to `namespace` if omitted |

---

## Fields

**All fields are strings.** The schema no longer supports typed fields (`int`, `bool`, `datetime`, etc.). Every field is emitted as a `string` attribute in the generated code. This simplifies the schema, aligns with OpenTelemetry's attribute model (which favors string attributes for log events), and eliminates type-mapping complexity.

### Shorthand List Syntax (Recommended)

Fields are defined as a YAML list. Each entry can be a plain name or a name with annotations:

```yaml
fields:
  - orderId                           # just a name (string, optional)
  - customerId: { sensitivity: pii }  # with annotation
  - amount: { required: true }        # with required
  - notes: { maxLength: 1024 }        # with max length
  - region: { index: true }           # with index
```

Multiple annotations can be combined:

```yaml
fields:
  - email: { required: true, sensitivity: pii, maxLength: 256 }
```

### Field Attributes

| Attribute | Required | Type | Default | Description |
|-----------|----------|------|---------|-------------|
| `name` | ✅ | string | — | Field name (the YAML key or list entry name) |
| `description` | ❌ | string | — | Documentation for generated XML doc comments |
| `required` | ❌ | bool | `false` | Whether the field is required. Required fields are non-nullable in generated code |
| `sensitivity` | ❌ | string | `public` | Data sensitivity: `public`, `internal`, `pii`, `credential` |
| `maxLength` | ❌ | int | — | Maximum string length. Values exceeding this are truncated with `"…[truncated]"` |
| `index` | ❌ | bool | `false` | Marks the field as queryable/indexed (documentation hint) |

---

## Severity Levels

| YAML Value | .NET LogLevel | OTEL severityNumber | Range |
|------------|---------------|---------------------|-------|
| `TRACE` | `Trace` | 1 | 1–4 |
| `DEBUG` | `Debug` | 5 | 5–8 |
| `INFO` | `Information` | 9 | 9–12 |
| `WARN` | `Warning` | 13 | 13–16 |
| `ERROR` | `Error` | 17 | 17–20 |
| `FATAL` | `Critical` | 21 | 21–24 |

---

## Metrics

Each event can define zero or more metrics in the `metrics:` block. The code generator creates OTEL `Counter<T>` and `Histogram<T>` instruments that are recorded automatically when the event is emitted.

### Metric Types

| Type | OTEL Instrument | C# Type | Description |
|------|----------------|---------|-------------|
| `counter` | `Counter<long>` | `long` | Monotonically increasing count |
| `histogram` | `Histogram<double>` | `double` | Distribution of values |
| `gauge` | — | — | Reserved for future use |

### Metric Attributes

| Attribute | Required | Type | Description |
|-----------|----------|------|-------------|
| `type` | ✅ | string | `counter`, `histogram`, or `gauge` |
| `unit` | ❌ | string | Unit of measurement (e.g., `"ms"`, `"requests"`, `"bytes"`) |
| `description` | ❌ | string | Human-readable description |
| `buckets` | ❌ | number[] | Histogram bucket boundaries (only for `histogram` type) |
| `labels` | ❌ | string[] | Field names to use as metric tag labels |

### Metric Example

```yaml
events:
  http.request.completed:
    id: 1002
    severity: INFO
    message: "HTTP {method} {path} completed with {statusCode} in {durationMs}ms"
    fields:
      - method: { required: true }
      - path: { required: true }
      - statusCode: { required: true }
      - durationMs: { required: true }
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
```

---

## Enums

Define enum types at the top level. Enum values are generated as **plain strings** — the `enums:` block serves as documentation and validation, but all enum fields are transmitted as string attributes:

```yaml
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
```

### Generated Code

For each enum, the code generator produces a C# `enum` type and a `ToStringFast()` extension method (zero-allocation, switch-based). At runtime, enum fields are serialized as their string name:

```csharp
// Generated enum
public enum HealthStatus { Healthy, Degraded, Unhealthy }

// Generated fast conversion
public static class HealthStatusExtensions
{
    public static string ToStringFast(this HealthStatus value) => value switch
    {
        HealthStatus.Healthy => "Healthy",
        HealthStatus.Degraded => "Degraded",
        HealthStatus.Unhealthy => "Unhealthy",
        _ => value.ToString(),
    };
}
```

---

## Sensitivity Classification

| Level | Description | Default Redaction |
|-------|-------------|-------------------|
| `public` | Safe in all environments. **Default** if not specified | No redaction |
| `internal` | Internal infrastructure details | Redacted in `Production` |
| `pii` | Personally Identifiable Information | Redacted in `Production` and `Staging` |
| `credential` | Secrets, tokens, API keys | **Always redacted** in all environments |

```yaml
fields:
  - httpMethod                         # Safe everywhere (default: public)
  - hostName: { sensitivity: internal }  # Redacted in Production
  - userId: { sensitivity: pii }         # Redacted in Staging + Production
  - apiKey: { sensitivity: credential }  # Always redacted
```

See [Chapter 10 — Security & Privacy](10-security-privacy.md) for the complete redaction matrix.

---

## Field Value Length Limits

Use `maxLength` to prevent unbounded attribute values:

```yaml
fields:
  - userAgent: { sensitivity: pii, maxLength: 512 }   # Truncated at 512 characters
  - httpPath: { maxLength: 256 }                       # Truncated at 256 characters
```

Values exceeding `maxLength` are truncated with `"…[truncated]"`. The global default maximum is `MaxAttributeValueLength` (4096 characters).

---

## Event Name Conventions

Event names must be:
- **Lowercase**
- **Dot-namespaced**: `category.subcategory.action`
- **Alphanumeric + dots only**
- **Must not start with `otel_events.`** (reserved prefix)

Examples:

```
✅ http.request.completed
✅ order.placed
✅ db.query.executed
✅ dependency.failed

❌ OrderPlaced          (not lowercase)
❌ order-placed         (hyphens not allowed)
❌ otel_events.custom.event     (reserved prefix)
```

---

## Exception Events

Set `exception: true` to add an `Exception?` parameter to the generated method:

```yaml
events:
  order.failed:
    id: 1003
    severity: ERROR
    message: "Order {orderId} failed: {reason}"
    exception: true            # Adds Exception parameter
    fields:
      - orderId: { required: true }
      - reason: { required: true }
```

Generated usage:

```csharp
try
{
    // ...
}
catch (Exception ex)
{
    _logger.OrderFailed(
        orderId: "ORD-001",
        reason: ex.Message,
        exception: ex);         // Exception is captured in the LogRecord
    throw;
}
```

---

## Schema Validation Rules

The schema parser validates at build time. Violations produce clear build errors:

| Error Code | Description |
|-----------|-------------|
| `OTEL_SCHEMA_001` | Duplicate event name within merged schemas |
| `OTEL_SCHEMA_002` | Invalid severity (must be TRACE, DEBUG, INFO, WARN, ERROR, FATAL) |
| `OTEL_SCHEMA_003` | Message template `{placeholder}` does not match any field name |
| `OTEL_SCHEMA_006` | Invalid event name format (must be lowercase, dot-namespaced) |
| `OTEL_SCHEMA_008` | Invalid metric type (must be counter, histogram, or gauge) |
| `OTEL_SCHEMA_009` | Empty enum definition (must have at least one value) |
| `OTEL_SCHEMA_010` | Invalid semver version |
| `OTEL_SCHEMA_011` | Reserved `otel_events.` prefix used in event or field name |
| `OTEL_SCHEMA_012` | Duplicate numeric event ID |
| `OTEL_SCHEMA_013` | Invalid meter name (must be valid .NET dot-separated identifier) |
| `OTEL_SCHEMA_014` | Invalid sensitivity value |
| `OTEL_SCHEMA_015` | Invalid `maxLength` value (must be positive integer) |
| `OTEL_SCHEMA_016` | Schema file exceeds 1 MB size limit |
| `OTEL_SCHEMA_017` | Merged schemas exceed 500 event limit |
| `OTEL_SCHEMA_018` | Event exceeds 50 field limit |

---

## Resource Limits

To prevent denial-of-service via malicious or excessively large schema files:

| Limit | Value | Rationale |
|-------|-------|-----------|
| Max file size | 1 MB | Prevents memory exhaustion |
| Max events per merged schema | 500 | Prevents excessive code generation |
| Max fields per event | 50 | Prevents unbounded attribute cardinality |
| Max YAML nesting depth | 20 | Prevents stack overflow in parser |
| Safe YAML loading | Enabled | Disables YAML tags, aliases, and anchors (expansion attacks) |

---

## Generated Output

For each schema, the code generator produces:

| File | Contents |
|------|----------|
| `{SchemaName}EventSource.g.cs` | `[LoggerMessage]` partial methods + extension methods on `ILogger<T>` |
| `{EnumName}.g.cs` | One file per enum type defined in the schema |
| `{SchemaName}Metrics.g.cs` | Static `Meter` / `Counter` / `Histogram` instances |
| `{SchemaName}MetricsServiceCollectionExtensions.g.cs` | DI registration extension (only for `meter_lifecycle: di`) |

---

## Backward Compatibility

The following properties are **accepted but ignored** by the schema parser for backward compatibility with older schema files:

| Property | Status | Notes |
|----------|--------|-------|
| `type` | Accepted, ignored | All fields are strings. Specifying `type: string` (or any other type) is harmless but has no effect |
| `ref` | Removed | Field referencing is no longer supported. Inline all field definitions directly |
| `values` | Removed | Inline enum values on fields are no longer supported. Use top-level `enums:` instead |
| `examples` | Removed | Example values are no longer part of the schema |
| `unit` | Removed | Unit of measurement on fields is removed. Units on **metrics** are still supported |

### Migrating from Map Syntax

The old map-style field syntax still works and is parsed correctly:

```yaml
# Old syntax — still accepted
fields:
  orderId:
    type: string        # accepted but ignored
    required: true
  customerId:
    type: string        # accepted but ignored
    sensitivity: pii
```

However, the **recommended** syntax is the shorthand list format:

```yaml
# New syntax — recommended
fields:
  - orderId: { required: true }
  - customerId: { sensitivity: pii }
```

Both forms produce identical generated code.

---

## Next Steps

- [Chapter 6 — Integration Packs](06-integration-packs.md) — pre-built schemas for HTTP, gRPC, CosmosDB, Storage
- [Chapter 9 — CLI Tool](09-cli-tool.md) — `dotnet otel-events validate`, `generate`, `diff`, `docs`
