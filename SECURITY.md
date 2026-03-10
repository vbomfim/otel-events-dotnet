# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| latest  | :white_check_mark: |

## Reporting a Vulnerability

If you discover a security vulnerability in otel-events-dotnet, please report it responsibly:

1. **Do NOT open a public GitHub issue** for security vulnerabilities
2. **Use GitHub Security Advisories**: Go to the [Security tab](https://github.com/vbomfim/otel-events-dotnet/security/advisories/new) and create a private security advisory
3. **Or email**: Include "SECURITY" in the subject line

### What to include

- Description of the vulnerability
- Steps to reproduce
- Affected versions
- Potential impact
- Suggested fix (if any)

### Response Timeline

- **Acknowledgment**: Within 48 hours
- **Initial assessment**: Within 1 week
- **Fix or mitigation**: Depends on severity
  - Critical: Within 72 hours
  - High: Within 1 week
  - Medium/Low: Next release cycle

### Scope

This policy covers the `otel-events-dotnet` library packages:
- `OtelEvents.Schema`
- `OtelEvents.Exporter.Json`
- `OtelEvents.Causality`
- `OtelEvents.Analyzers`
- `OtelEvents.Testing`
- `OtelEvents.*` integration packs

### Security Design

See [SPECIFICATION.md §16](SPECIFICATION.md) for the project's security and privacy requirements, threat model, PII classification framework, and OWASP reference mapping.
