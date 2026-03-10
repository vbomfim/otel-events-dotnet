# Environment Profiles

> Source: [SPECIFICATION.md §16.3](../../SPECIFICATION.md)

otel-events defines three environment profiles that control security-sensitive defaults. Each profile progressively restricts information disclosure as the environment moves closer to production.

## Defaults Summary

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

## Profile Descriptions

### Development

The most permissive profile. Designed for local development and debugging.

- Full exception details including stack traces and file paths
- PII and internal fields are visible for debugging
- Host information (`all.host`, `all.pid`) emitted by default (overridable)
- **Credentials are still redacted** — even in Development

### Staging

Mirrors Production redaction for PII but keeps internal fields visible for troubleshooting pre-production issues.

- Exception details limited to type and message (no stack traces)
- PII fields are redacted
- Internal fields remain visible for infrastructure debugging
- Host information not emitted by default

### Production

The most restrictive profile. Only `public`-classified fields are visible.

- Exception details limited to type and message
- Both PII and internal fields are redacted
- Host information not emitted by default
- Defense-in-depth regex patterns always active (all profiles)

## Configuration

```csharp
logging.AddOtelEventsJsonExporter(options =>
{
    options.EnvironmentProfile = OtelEventsEnvironmentProfile.Production;
});
```

## Related

- [PII Classification](pii-classification.md) — Full redaction matrix and override configuration
- [Threat Model](threat-model.md) — Threat vectors mitigated by environment profiles
