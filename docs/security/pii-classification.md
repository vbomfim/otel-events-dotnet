# PII Classification Framework

> Source: [SPECIFICATION.md §16.2](../../SPECIFICATION.md)

The `sensitivity` field attribute in the YAML schema provides a compile-time classification system for personally identifiable information (PII) and credentials. At runtime, the exporter applies redaction based on the configured `EnvironmentProfile`.

## Sensitivity Levels

| Level | Description | Example Fields |
|-------|-------------|----------------|
| `public` | Safe to emit in all environments | Event names, status codes, durations |
| `internal` | Internal infrastructure details | Host names, process IDs, internal service names |
| `pii` | Personally identifiable information | User IDs, email addresses, IP addresses, User-Agent strings |
| `credential` | Secrets and authentication material | API keys, bearer tokens, connection strings, passwords |

## Redaction Matrix by Environment Profile

| EnvironmentProfile | `public` | `internal` | `pii` | `credential` |
|--------------------|----------|------------|-------|--------------|
| **Development** | ✅ Visible | ✅ Visible | ✅ Visible | 🔒 **REDACTED** |
| **Staging** | ✅ Visible | ✅ Visible | 🔒 **REDACTED** | 🔒 **REDACTED** |
| **Production** | ✅ Visible | 🔒 **REDACTED** | 🔒 **REDACTED** | 🔒 **REDACTED** |

### Key Principles

- **Credentials are always redacted** — even in Development, `credential`-classified fields are never emitted in plaintext.
- **Production is most restrictive** — only `public` fields are visible by default.
- **Defaults are privacy-preserving** — PII capture flags (`CaptureClientIp`, `CaptureUserAgent`) default to `false`.

## Schema Example

Define sensitivity in the YAML schema at the field level:

```yaml
events:
  UserLoggedIn:
    fields:
      userId:
        type: string
        sensitivity: pii
      email:
        type: string
        sensitivity: pii
      loginMethod:
        type: string
        sensitivity: public
      apiKey:
        type: string
        sensitivity: credential
```

## Sensitivity Overrides

Individual fields can be explicitly opted in or out of redaction at runtime. This is useful when a specific service has a documented legal basis (e.g., audit trail requirement) to emit PII in Production.

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

### Override Guidelines

- **Document the legal basis** for any `pii` or `credential` override in Production.
- **Prefer narrow overrides** — override specific fields, not entire sensitivity levels.
- **Audit overrides regularly** — review `SensitivityOverrides` as part of security reviews.

## Regulatory Compliance

otel-events provides the classification and redaction mechanisms. Specific regulatory compliance configuration is the responsibility of the deploying organization.

| Regulation | otel-events Mechanism |
|------------|---------------|
| **GDPR** (EU) | `sensitivity: pii` classification → redaction in Production; `CaptureClientIp`/`CaptureUserAgent` default `false`; data retention is the responsibility of the log storage backend |
| **CCPA** (California) | Same PII controls as GDPR apply |
| **HIPAA** (US Healthcare) | `sensitivity: credential` for PHI fields; teams must configure `EnvironmentProfile = Production` and audit `SensitivityOverrides`; otel-events does not provide encryption at rest (log storage responsibility) |
| **SOC 2** | Audit trail via `all.event_id`, `all.seq`, `traceId`; `all.host`/`all.pid` opt-in for attribution; SBOM generation in CI |

> **Decision (OQ-PG-03):** otel-events provides PII classification and redaction mechanisms. Specific regulatory compliance configuration (which fields to redact, data retention, encryption at rest) is the responsibility of the deploying organization. otel-events' defaults are privacy-preserving (PII redacted in Production).

## Related

- [Threat Model](threat-model.md) — Defense-in-depth value sanitization patterns
- [Environment Profiles](environment-profiles.md) — Full per-environment settings table
- [OWASP Mapping](owasp-mapping.md) — OWASP alignment for PII controls
