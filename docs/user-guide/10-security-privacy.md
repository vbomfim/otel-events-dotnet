# Chapter 10 — Security & Privacy

otel-events provides a layered security model for controlling what data appears in log output. Sensitivity classification in schemas, environment-aware redaction, and defense-in-depth patterns work together to prevent accidental data exposure.

---

## Sensitivity Field Classification

Every field in a YAML schema supports an optional `sensitivity` attribute that classifies the data sensitivity level:

```yaml
fields:
  httpMethod:
    type: string
    description: "HTTP request method"
    sensitivity: public        # Safe everywhere (default)

  hostName:
    type: string
    description: "Internal hostname"
    sensitivity: internal      # Infrastructure detail

  userId:
    type: string
    description: "Unique user identifier"
    sensitivity: pii           # Personally Identifiable Information

  apiKey:
    type: string
    description: "API key for external service"
    sensitivity: credential    # Secrets — always redacted
```

### Sensitivity Levels

| Level | Description | Examples |
|---|---|---|
| `public` | Safe to emit in all environments. Default if not specified | Event names, status codes, durations, HTTP methods |
| `internal` | Internal infrastructure details | Hostnames, process IDs, internal paths, container names |
| `pii` | Personally Identifiable Information | User IDs, email addresses, IP addresses, user agents |
| `credential` | Secrets, tokens, and keys | API keys, passwords, connection strings, bearer tokens |

### How Redaction Works

When a field's sensitivity level requires redaction in the current `EnvironmentProfile`, the **value** is replaced with `"[REDACTED:{sensitivity}]"`. The field key remains present in the JSON output — only the value is masked:

```json
{
  "event": "user.login",
  "attr": {
    "userId": "[REDACTED:pii]",
    "apiKey": "[REDACTED:credential]",
    "statusCode": 200
  }
}
```

---

## Redaction by EnvironmentProfile

The `OtelEventsEnvironmentProfile` controls which sensitivity levels are redacted. The matrix below shows what's visible in each profile:

| Sensitivity | Development | Staging | Production |
|---|---|---|---|
| `public` | ✅ Visible | ✅ Visible | ✅ Visible |
| `internal` | ✅ Visible | ✅ Visible | 🔒 `[REDACTED:internal]` |
| `pii` | ✅ Visible | 🔒 `[REDACTED:pii]` | 🔒 `[REDACTED:pii]` |
| `credential` | 🔒 `[REDACTED:credential]` | 🔒 `[REDACTED:credential]` | 🔒 `[REDACTED:credential]` |

### Additional Profile Defaults

| Setting | Development | Staging | Production |
|---|---|---|---|
| `ExceptionDetailLevel` | `Full` (type, message, stack trace, inner exceptions) | `TypeAndMessage` | `TypeAndMessage` |
| Stack trace file paths | Included | Omitted | Omitted |
| `EmitHostInfo` (`otel_events.host`, `otel_events.pid`) | `true` | `false` | `false` |

### Key Design Decision

- `credential` fields are **always redacted**, even in `Development`. If you need a credential value in local debugging, inspect it in the debugger, not the log output.
- `Production` is the **default** profile. If `ASPNETCORE_ENVIRONMENT` and `DOTNET_ENVIRONMENT` are both unset, otel-events defaults to `Production` — the most restrictive mode. This is a fail-closed design.

---

## SensitivityOverrides

For cases where you need to override the default redaction behavior for specific fields, `SensitivityOverrides` allows explicit opt-in or opt-out:

```csharp
logging.AddOtelEventsJsonExporter(options =>
{
    options.EnvironmentProfile = OtelEventsEnvironmentProfile.Production;

    options.SensitivityOverrides = new Dictionary<string, bool>
    {
        // Allow userId in Production (e.g., for audit trail requirements)
        ["userId"] = true,

        // Force-redact hostName even though it's "internal"
        // and would normally be visible in Staging
        ["hostName"] = false,
    };
});
```

### Guidelines for SensitivityOverrides

| ✅ Do | ❌ Don't |
|---|---|
| Document the legal basis for Production PII overrides | Override without documented justification |
| Prefer narrow, field-level overrides | Downgrade the entire `EnvironmentProfile` |
| Audit overrides as part of security reviews | Add overrides and forget about them |
| Use for specific compliance requirements (HIPAA audit trails) | Use as a shortcut to avoid fixing the schema |

---

## Defense-in-Depth Patterns

Beyond schema-level sensitivity classification, otel-events provides runtime safeguards that catch sensitive data regardless of schema configuration.

### RedactPatterns

User-configured regex patterns for value-level redaction. Every string attribute value is tested against these patterns:

```csharp
logging.AddOtelEventsJsonExporter(options =>
{
    options.RedactPatterns =
    [
        @"(?i)(password|pwd|secret|token|key|credential)\s*[=:]\s*\S+",
        @"Server=.*;(User Id|Password)=.*",
        @"Bearer\s+[A-Za-z0-9\-._~+/]+=*",
    ];
});
```

Matching values are replaced with `[REDACTED]`. Each pattern is tested with a **50ms timeout** — if a pattern match exceeds the timeout, the value is replaced with `[REDACTED:timeout]` (fail-closed security).

### Built-In Defense Patterns

The exporter includes **hard-coded patterns** that are always active and cannot be disabled:

| Pattern | Catches |
|---|---|
| `Server=.*;(User Id\|Password\|Pwd)=.*` | Connection strings with credentials |
| `Data Source=.*;(User ID\|Password)=.*` | Connection strings (alternate format) |
| `Bearer\s+[A-Za-z0-9\-._~+/]+=*` | Bearer tokens |
| `(api[_-]?key\|apikey\|access[_-]?token\|secret[_-]?key)\s*[=:]\s*\S{16,}` | API keys and secrets |

These patterns act as a safety net — even if a field is not annotated with `sensitivity: credential` in the schema, connection strings and tokens are caught and redacted at the value level.

### AttributeDenylist

Block specific attribute names from ever appearing in the output:

```csharp
logging.AddOtelEventsJsonExporter(options =>
{
    options.AttributeDenylist = new HashSet<string>
    {
        "ConnectionString",
        "Password",
        "Token",
        "Secret",
    };
});
```

Denied attributes are **completely excluded** from the JSON envelope — neither the key nor the value is emitted. The denylist takes precedence over the `AttributeAllowlist`.

### AttributeAllowlist

For non-otel-events `LogRecord`s (from third-party libraries), restrict which attributes are emitted:

```csharp
logging.AddOtelEventsJsonExporter(options =>
{
    // Only emit these attributes from non-otel-events LogRecords
    options.AttributeAllowlist = new HashSet<string>
    {
        "RequestPath",
        "StatusCode",
        "ElapsedMs",
    };
});
```

When set, only the listed attributes pass through. When `null` (default), all attributes are emitted.

### Processing Order

The exporter applies security controls in this order:

1. **Reserved prefix stripping** — remove unauthorized `otel_events.*` attributes
2. **AttributeDenylist** — exclude denied attribute names entirely
3. **AttributeAllowlist** — filter non-otel-events attributes to allowed names
4. **Built-in defense patterns** — hard-coded credential detection (always active)
5. **RedactPatterns** — user-configured regex patterns
6. **Value truncation** — truncate strings exceeding `MaxAttributeValueLength`

---

## Exception Detail Levels

The `ExceptionDetailLevel` controls how much exception information appears in the JSON output:

| Level | Type | Message | Stack Trace | File Paths | Inner Exceptions |
|---|---|---|---|---|---|
| `Full` | ✅ | ✅ | ✅ (method names) | Development only | ✅ (up to 5 levels) |
| `TypeAndMessage` | ✅ | ✅ | ❌ | ❌ | ✅ (without traces) |
| `TypeOnly` | ✅ | ❌ | ❌ | ❌ | ❌ |

### Default by Profile

- **Development** → `Full` (maximum debugging information)
- **Staging** → `TypeAndMessage` (diagnose issues without leaking internals)
- **Production** → `TypeAndMessage` (type and message sufficient for alerting)

### Explicit Override

```csharp
logging.AddOtelEventsJsonExporter(options =>
{
    options.EnvironmentProfile = OtelEventsEnvironmentProfile.Production;
    // Override: use TypeOnly for minimal exception disclosure
    options.ExceptionDetailLevel = ExceptionDetailLevel.TypeOnly;
});
```

### Inner Exception Depth Limit

otel-events caps inner exception serialization at **5 levels** of nesting. This prevents unbounded recursion from circular or deeply nested exception chains.

---

## Roslyn Analyzer: ALL009

The `ALL009` analyzer provides **compile-time** PII detection. It scans for string literals that match common PII field names:

```csharp
// ⚠️ ALL009: PII field without redaction policy
activity.SetTag("user.email", email);
span.AddTag("customer.ssn", ssn);
```

### Detected Patterns

`email`, `phone`, `ssn`, `social_security`, `credit_card`, `card_number`, `password`, `date_of_birth`, `passport`, `national_id`, `ip_address`, `driver_license` (and their camelCase equivalents).

### Configuring Severity

```editorconfig
# .editorconfig — promote ALL009 to error in production code
[src/**/*.cs]
dotnet_diagnostic.ALL009.severity = error

# Suppress in test code
[tests/**/*.cs]
dotnet_diagnostic.ALL009.severity = none
```

---

## Regulatory Compliance Quick Reference

| Regulation | otel-events Controls |
|---|---|
| **GDPR** | `sensitivity: pii` → Production redaction. `CaptureClientIp` and `CaptureUserAgent` default to `false` |
| **CCPA** | Same as GDPR — PII classification covers California consumer data |
| **HIPAA** | Use `sensitivity: credential` for PHI. Configure `EnvironmentProfile.Production`. Document all `SensitivityOverrides` |
| **SOC 2** | Audit trail via `otel_events.event_id`, monotonic `seq`, `traceId`. Opt-in host/PID attribution via `EmitHostInfo` |

---

## Further Reading

- [`docs/security/threat-model.md`](../security/threat-model.md) — complete threat model with 9 threat vectors and mitigations
- [`docs/security/pii-classification.md`](../security/pii-classification.md) — detailed PII classification guidance
- [`docs/security/environment-profiles.md`](../security/environment-profiles.md) — per-profile defaults reference
- [`docs/security/owasp-mapping.md`](../security/owasp-mapping.md) — OWASP Top 10 mapping

---

## Next Steps

- [Chapter 7 — Configuration](07-configuration.md) — `EnvironmentProfile`, `RedactPatterns`, and `AttributeDenylist` configuration
- [Chapter 11 — Advanced Topics](11-advanced-topics.md) — rate limiting, sampling, schema signing
