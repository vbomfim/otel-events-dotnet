# Chapter 9 — CLI Tool Reference

The otel-events CLI (`dotnet otel-events`) provides commands for schema validation, code generation, version comparison, and documentation generation. Install it as a .NET global or local tool.

---

## Installation

```bash
# Global tool
dotnet tool install --global OtelEvents.Cli

# Local tool (recommended for CI)
dotnet new tool-manifest
dotnet tool install OtelEvents.Cli
```

After installation, the tool is available as `dotnet otel-events`:

```bash
dotnet otel-events --help
```

---

## Commands

### `dotnet otel-events validate <path>`

Parse and validate a `.all.yaml` schema file. Reports all validation errors with structured error codes.

```bash
dotnet otel-events validate schemas/orders.all.yaml
```

**Success output:**

```
✓ Schema 'OrderEvents' is valid.
```

**Error output** (to stderr):

```
ALL_SCHEMA_001: Duplicate event name 'order.placed' at line 42
ALL_SCHEMA_003: Message template placeholder '{orderId}' does not match any field name
ALL_SCHEMA_006: Event name 'OrderPlaced' is invalid — must be lowercase, dot-namespaced

3 error(s)
```

**Exit codes:**

| Code | Meaning |
|---|---|
| `0` | Schema is valid |
| `1` | Validation errors found, or file not found |

---

### `dotnet otel-events generate <path> -o <output>`

Generate C# source files from a validated schema. Creates `[LoggerMessage]` partial methods, extension methods, metric instruments, and enum types.

```bash
dotnet otel-events generate schemas/orders.all.yaml -o src/Generated/
```

**Success output:**

```
Generated: src/Generated/OrderEventSource.g.cs
Generated: src/Generated/OrderStatus.g.cs
Generated: src/Generated/OrderEventsMetrics.g.cs

3 file(s) generated.
```

**Options:**

| Option | Description |
|---|---|
| `-o`, `--output` | Output directory for generated files (required). Created if it doesn't exist |

**Exit codes:**

| Code | Meaning |
|---|---|
| `0` | Code generated successfully |
| `1` | Validation error, parse error, or file not found |

**What gets generated:**

| File | Contents |
|---|---|
| `{SchemaName}EventSource.g.cs` | `[LoggerMessage]` partial methods + extension methods on `ILogger<T>` |
| `{EnumName}.g.cs` | One file per enum type defined in the schema |
| `{SchemaName}Metrics.g.cs` | Static or DI-based `Meter`/`Counter`/`Histogram` instances |
| `{SchemaName}MetricsServiceCollectionExtensions.g.cs` | DI registration extension (only for `meter_lifecycle: di`) |

---

### `dotnet otel-events diff <old> <new>`

Compare two schema versions and classify changes as breaking or non-breaking. Essential for CI pipelines that guard against backward-incompatible changes.

```bash
dotnet otel-events diff schemas/v1/orders.all.yaml schemas/v2/orders.all.yaml
```

**Compatible output (no breaking changes):**

```
✓ [OK] Event 'order.refunded' added
✓ [OK] Field 'order.placed.notes' added

0 breaking change(s), 2 compatible change(s).
```

**Breaking changes output:**

```
✗ [BREAKING] Event 'order.cancelled' removed
✗ [BREAKING] Field 'order.placed.customerId' removed
✗ [BREAKING] Field 'order.placed.amount' type changed (int → double)
✓ [OK] Event 'order.refunded' added

3 breaking change(s), 1 compatible change(s).
```

**Exit codes:**

| Code | Meaning |
|---|---|
| `0` | Schemas are compatible (no breaking changes) |
| `1` | Error (file not found, parse error) |
| `2` | Breaking changes detected |

### Change Classification

| Change | Breaking? |
|---|---|
| Event added | ✅ No |
| Event removed | ❌ Yes |
| Field added | ✅ No |
| Field removed | ❌ Yes |
| Field type changed | ❌ Yes |

### CI Pipeline Usage

```yaml
# GitHub Actions: fail the build on breaking schema changes
- name: Check schema compatibility
  run: |
    dotnet otel-events diff schemas/main/orders.all.yaml schemas/pr/orders.all.yaml
    # Exit code 2 = breaking changes → build fails
```

---

### `dotnet otel-events docs <path> -o <output>`

Generate Markdown documentation from a schema file. Produces an event catalog with descriptions, fields, types, and tags.

```bash
# Write to file
dotnet otel-events docs schemas/orders.all.yaml -o docs/event-catalog.md

# Write to stdout (pipe to other tools)
dotnet otel-events docs schemas/orders.all.yaml
```

**Options:**

| Option | Description |
|---|---|
| `-o`, `--output` | Output file path. When omitted, writes to stdout |

**Example output:**

```markdown
# OrderEvents — Event Catalog

Schema version: 1.0.0

## Events

### order.placed
**Severity:** INFO  
**Message:** Order {orderId} placed by {customerId} for {amount}

| Field | Type | Required | Description |
|---|---|---|---|
| orderId | string | ✅ | Unique order identifier |
| customerId | string | ✅ | Customer who placed the order |
| amount | double | ✅ | Order total amount |

**Tags:** commerce, orders
```

**Exit codes:**

| Code | Meaning |
|---|---|
| `0` | Documentation generated successfully |
| `1` | Error (file not found, parse error) |

---

## Exit Code Summary

| Exit Code | Meaning | Commands |
|---|---|---|
| **0** | Success (valid schema, generated code, compatible schemas, docs written) | All commands |
| **1** | Error (file not found, parse error, validation error) | All commands |
| **2** | Breaking changes detected (not an error — schema parsed successfully, but changes are incompatible) | `diff` only |

---

## Schema Validation Error Codes

When `validate` or `generate` reports errors, each error includes a structured code:

| Error Code | Description |
|---|---|
| `ALL_SCHEMA_001` | Duplicate event name |
| `ALL_SCHEMA_002` | Invalid severity (must be TRACE, DEBUG, INFO, WARN, ERROR, FATAL) |
| `ALL_SCHEMA_003` | Message template placeholder doesn't match any field name |
| `ALL_SCHEMA_004` | Unresolved `ref` (field or enum reference not found) |
| `ALL_SCHEMA_005` | Invalid field type |
| `ALL_SCHEMA_006` | Invalid event name format (must be lowercase, dot-namespaced) |
| `ALL_SCHEMA_007` | Required field missing type (directly or via ref) |
| `ALL_SCHEMA_008` | Invalid metric type (must be counter, histogram, or gauge) |
| `ALL_SCHEMA_009` | Empty enum definition |
| `ALL_SCHEMA_010` | Invalid semver version |
| `ALL_SCHEMA_011` | Reserved `all.` prefix used in event or field name |
| `ALL_SCHEMA_012` | Duplicate numeric event ID |
| `ALL_SCHEMA_013` | Invalid meter name |
| `ALL_SCHEMA_014` | Invalid sensitivity value |
| `ALL_SCHEMA_015` | Invalid `maxLength` value |
| `ALL_SCHEMA_016` | Schema file exceeds 1 MB size limit |
| `ALL_SCHEMA_017` | Merged schemas exceed 500 event limit |
| `ALL_SCHEMA_018` | Event exceeds 50 field limit |

---

## Common Workflows

### Validate Before Commit (Git Hook)

```bash
#!/bin/sh
# .git/hooks/pre-commit
for schema in $(git diff --cached --name-only -- '*.all.yaml'); do
    dotnet otel-events validate "$schema" || exit 1
done
```

### Generate Code in MSBuild

```xml
<!-- In your .csproj — regenerate code on build -->
<Target Name="OtelEventsCodeGen" BeforeTargets="CoreCompile"
        Inputs="@(OtelEventsSchema)" Outputs="$(IntermediateOutputPath)OtelEventsGenerated\%(Filename).g.cs">
  <Exec Command="dotnet otel-events generate %(OtelEventsSchema.Identity) -o $(IntermediateOutputPath)OtelEventsGenerated/" />
</Target>

<ItemGroup>
  <OtelEventsSchema Include="schemas\*.all.yaml" />
  <Compile Include="$(IntermediateOutputPath)OtelEventsGenerated\**\*.g.cs" />
</ItemGroup>
```

### Schema Diff in PR Reviews

```yaml
# GitHub Actions workflow
name: Schema Compatibility Check
on: pull_request

jobs:
  check:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Get main schemas
        run: git show origin/main:schemas/orders.all.yaml > /tmp/old.all.yaml

      - name: Compare schemas
        run: dotnet otel-events diff /tmp/old.all.yaml schemas/orders.all.yaml
```

---

## Next Steps

- [Chapter 7 — Configuration](07-configuration.md) — configure the JSON exporter and processors
- [Chapter 10 — Security & Privacy](10-security-privacy.md) — sensitivity classification in schemas
