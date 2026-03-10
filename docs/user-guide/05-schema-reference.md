# Chapter 5 — Schema Reference

This chapter is the complete reference for the `.all.yaml` schema format. Every attribute, type, and validation rule is documented here.

---

## Schema File Conventions

- Extension: `.all.yaml` or `.all.yml`
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

# ─── Reusable Field Definitions (optional) ───────────────────────────
fields:
  fieldName:
    type: string                       # Required: field type (see type table)
    description: "Field description"   # Optional: documentation
    sensitivity: public                # Optional: public|internal|pii|credential
    index: true                        # Optional: marks field as queryable
    maxLength: 256                     # Optional: max string length (truncated)
    unit: "ms"                         # Optional: unit of measurement
    examples: ["value1", "value2"]     # Optional: example values

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
      fieldName:
        type: string                   # Direct type definition
        required: true                 # Required: true or false
        sensitivity: pii               # Optional: override field-level sensitivity
        maxLength: 512                 # Optional: override field-level maxLength
      otherField:
        ref: fieldName                 # Reference to a reusable field definition
        required: false
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

## Field Types

| YAML Type | C# Type | JSON Type | Notes |
|-----------|---------|-----------|-------|
| `string` | `string` | `string` | Max length governed by `maxLength` or `MaxAttributeValueLength` (default: 4096) |
| `int` | `int` | `number` | 32-bit signed integer |
| `long` | `long` | `number` | 64-bit signed integer |
| `double` | `double` | `number` | IEEE 754 double-precision |
| `bool` | `bool` | `boolean` | |
| `datetime` | `DateTimeOffset` | `string` | ISO 8601 UTC format |
| `duration` | `TimeSpan` | `string` | ISO 8601 duration format |
| `guid` | `Guid` | `string` | Standard GUID format |
| `enum` | Generated C# enum | `string` | Serialized as string name, not integer |
| `string[]` | `string[]` | `array` | Array of strings |
| `int[]` | `int[]` | `array` | Array of integers |
| `map` | `Dictionary<string, string>` | `object` | String key-value pairs |

---

## Field Attributes

| Attribute | Required | Type | Default | Description |
|-----------|----------|------|---------|-------------|
| `type` | ✅* | string | — | Field type (see table above). *Not required if `ref` is used |
| `ref` | ❌ | string | — | Reference to a reusable field definition. Mutually exclusive with `type` |
| `description` | ❌ | string | — | Documentation for generated XML doc comments |
| `required` | ✅ | bool | — | Whether the field is required. Required fields are non-nullable in generated code |
| `sensitivity` | ❌ | string | `public` | Data sensitivity: `public`, `internal`, `pii`, `credential` |
| `index` | ❌ | bool | `false` | Marks the field as queryable/indexed (documentation hint) |
| `maxLength` | ❌ | int | — | Maximum string length. Values exceeding this are truncated with `"…[truncated]"` |
| `unit` | ❌ | string | — | Unit of measurement (documentation + metric instrument unit) |
| `examples` | ❌ | string[] | — | Example values (documentation hint) |
| `values` | ❌ | string[] | — | Enum values when `type: enum`. Required when type is `enum` |

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
      method:
        type: string
        required: true
      path:
        type: string
        required: true
      statusCode:
        type: int
        required: true
      durationMs:
        type: double
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
```

---

## Enums

Define enum types that can be referenced by events:

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

For each enum, the code generator produces:

1. A C# `enum` type
2. A `ToStringFast()` extension method (zero-allocation, switch-based)

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

### Inline Enums

Enums can also be defined inline within an event field:

```yaml
events:
  db.query.executed:
    id: 2001
    severity: DEBUG
    message: "Query on {table} ({operation}) completed"
    fields:
      table:
        type: string
        required: true
      operation:
        type: enum
        values: [SELECT, INSERT, UPDATE, DELETE, MERGE]
        required: true
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
  httpMethod:
    type: string
    sensitivity: public        # Safe everywhere

  hostName:
    type: string
    sensitivity: internal      # Redacted in Production

  userId:
    type: string
    sensitivity: pii           # Redacted in Staging + Production

  apiKey:
    type: string
    sensitivity: credential    # Always redacted
```

See [Chapter 10 — Security & Privacy](10-security-privacy.md) for the complete redaction matrix.

---

## Field Value Length Limits

Use `maxLength` to prevent unbounded attribute values:

```yaml
fields:
  userAgent:
    type: string
    sensitivity: pii
    maxLength: 512            # Truncated at 512 characters

  httpPath:
    type: string
    maxLength: 256            # Truncated at 256 characters
```

Values exceeding `maxLength` are truncated with `"…[truncated]"`. The global default maximum is `MaxAttributeValueLength` (4096 characters).

---

## Event Name Conventions

Event names must be:
- **Lowercase**
- **Dot-namespaced**: `category.subcategory.action`
- **Alphanumeric + dots only**
- **Must not start with `all.`** (reserved prefix)

Examples:

```
✅ http.request.completed
✅ order.placed
✅ db.query.executed
✅ dependency.failed

❌ OrderPlaced          (not lowercase)
❌ order-placed         (hyphens not allowed)
❌ all.custom.event     (reserved prefix)
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
      orderId:
        type: string
        required: true
      reason:
        type: string
        required: true
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
| `ALL_SCHEMA_001` | Duplicate event name within merged schemas |
| `ALL_SCHEMA_002` | Invalid severity (must be TRACE, DEBUG, INFO, WARN, ERROR, FATAL) |
| `ALL_SCHEMA_003` | Message template `{placeholder}` does not match any field name |
| `ALL_SCHEMA_004` | Unresolved `ref` (field or enum reference not found) |
| `ALL_SCHEMA_005` | Invalid field type |
| `ALL_SCHEMA_006` | Invalid event name format (must be lowercase, dot-namespaced) |
| `ALL_SCHEMA_007` | Required field missing type (directly or via ref) |
| `ALL_SCHEMA_008` | Invalid metric type (must be counter, histogram, or gauge) |
| `ALL_SCHEMA_009` | Empty enum definition (must have at least one value) |
| `ALL_SCHEMA_010` | Invalid semver version |
| `ALL_SCHEMA_011` | Reserved `all.` prefix used in event or field name |
| `ALL_SCHEMA_012` | Duplicate numeric event ID |
| `ALL_SCHEMA_013` | Invalid meter name (must be valid .NET dot-separated identifier) |
| `ALL_SCHEMA_014` | Invalid sensitivity value |
| `ALL_SCHEMA_015` | Invalid `maxLength` value (must be positive integer) |
| `ALL_SCHEMA_016` | Schema file exceeds 1 MB size limit |
| `ALL_SCHEMA_017` | Merged schemas exceed 500 event limit |
| `ALL_SCHEMA_018` | Event exceeds 50 field limit |

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

## Next Steps

- [Chapter 6 — Integration Packs](06-integration-packs.md) — pre-built schemas for HTTP, gRPC, CosmosDB, Storage
- [Chapter 9 — CLI Tool](09-cli-tool.md) — `dotnet otel-events validate`, `generate`, `diff`, `docs`
