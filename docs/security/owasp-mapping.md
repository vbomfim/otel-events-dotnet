# OWASP Reference Mapping

> Source: [SPECIFICATION.md §16.7](../../SPECIFICATION.md)

This document maps ALL's security controls to the [OWASP Top 10 (2021)](https://owasp.org/Top10/) categories.

## Mapping

| OWASP Category | ALL Mitigation |
|---|---|
| **[A01:2021 — Broken Access Control](https://owasp.org/Top10/A01_2021-Broken_Access_Control/)** | `CaptureClientIp = false` by default; `sensitivity: pii` classification prevents accidental exposure of access-control-relevant data |
| **[A04:2021 — Insecure Design](https://owasp.org/Top10/A04_2021-Insecure_Design/)** | `ExceptionDetailLevel` controls stack trace exposure; no file paths in Production; `EmitHostInfo = false` default; `sensitivity` classification framework provides secure-by-default data handling |
| **[A09:2021 — Security Logging and Monitoring Failures](https://owasp.org/Top10/A09_2021-Security_Logging_and_Monitoring_Failures/)** | Structured, schema-defined events ensure consistent logging; `ExportResult.Failure` on I/O errors enables monitoring; self-telemetry metrics provide observability into the logging pipeline itself |

## How ALL Addresses Each Category

### A01 — Broken Access Control

ALL reduces the risk of access-control-relevant data leaking into logs:

- **Client IP capture is disabled by default** (`CaptureClientIp = false`) — preventing accidental logging of IP addresses that could be used for access control decisions.
- **PII classification** — fields like `userId`, `email`, and `clientIp` are classified as `sensitivity: pii` and redacted in Production and Staging environments.
- See [PII Classification](pii-classification.md) for the full redaction matrix.

### A04 — Insecure Design

ALL implements secure-by-default design patterns for information disclosure:

- **Exception details** — `ExceptionDetailLevel` defaults to `TypeAndMessage` in Production and Staging, preventing stack trace leakage that could reveal internal architecture.
- **Host information** — `EmitHostInfo = false` by default, preventing infrastructure detail disclosure.
- **Sensitivity framework** — compile-time classification ensures developers consciously label fields that carry security-sensitive data.
- See [Environment Profiles](environment-profiles.md) for per-environment defaults.

### A09 — Security Logging and Monitoring Failures

ALL improves logging quality and reliability:

- **Schema-driven events** — YAML-defined event schemas enforce consistent, structured logging across the application, reducing the risk of missing or malformed log entries.
- **Export failure signaling** — the exporter returns `ExportResult.Failure` on I/O errors, enabling the OTEL SDK pipeline to trigger retry or alerting mechanisms.
- **Self-telemetry** — ALL emits metrics about its own operation (e.g., `all.exporter.json.reserved_prefix_stripped`), providing observability into the logging pipeline itself.

## Related

- [Threat Model](threat-model.md) — Full threat vectors and mitigations
- [PII Classification](pii-classification.md) — Sensitivity levels and redaction
- [Environment Profiles](environment-profiles.md) — Per-environment security defaults
