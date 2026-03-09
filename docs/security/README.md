# Security & Privacy Documentation

This directory contains standalone security and privacy documentation for the ALL project, extracted from [SPECIFICATION.md §16](../../SPECIFICATION.md).

## Documents

| Document | Description | Spec Reference |
|----------|-------------|----------------|
| [Threat Model](threat-model.md) | Trust boundary diagram, threat vectors, and mitigations | §16.1 |
| [PII Classification](pii-classification.md) | Sensitivity levels, redaction matrix, and override configuration | §16.2 |
| [Environment Profiles](environment-profiles.md) | Development, Staging, and Production default settings | §16.3 |
| [OWASP Mapping](owasp-mapping.md) | OWASP Top 10 (2021) reference mapping to ALL mitigations | §16.7 |

## Quick Reference

- **PII is redacted by default** in Production — see [PII Classification](pii-classification.md)
- **Stack traces are omitted** in Production and Staging — see [Environment Profiles](environment-profiles.md)
- **Credentials are always redacted** in all environments — see [PII Classification](pii-classification.md)
- **Defense-in-depth regex patterns** catch leaked tokens and connection strings — see [Threat Model](threat-model.md)

## Related

- [SECURITY.md](../../SECURITY.md) — Vulnerability reporting policy and response SLA
- [SPECIFICATION.md §16](../../SPECIFICATION.md) — Full security and privacy specification
