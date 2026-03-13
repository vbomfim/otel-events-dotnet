# Contributing to otel-events-dotnet

Thank you for your interest in contributing! This guide covers everything you
need to get started.

## Prerequisites

| Tool | Version |
|------|---------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 8.0 or later |
| [Git](https://git-scm.com/) | 2.x or later |

## Building and testing

```bash
# Clone the repository
git clone https://github.com/vbomfim/otel-events-dotnet.git
cd otel-events-dotnet

# Build the entire solution
dotnet build

# Run all tests
dotnet test

# Run tests with code-coverage collection
dotnet test --collect:"XPlat Code Coverage"

# Build in Release mode (mirrors CI)
dotnet build -c Release
dotnet test --no-build -c Release
```

## Project structure

```text
otel-events-dotnet/
├── src/
│   ├── OtelEvents.Schema              # YAML parser & C# code generator
│   ├── OtelEvents.Exporter.Json       # JSONL exporter, severity filter, rate limiter, sampler
│   ├── OtelEvents.Causality           # Causal event linking (UUID v7)
│   ├── OtelEvents.AspNetCore          # ASP.NET Core HTTP event instrumentation
│   ├── OtelEvents.Grpc                # gRPC event instrumentation
│   ├── OtelEvents.Azure.CosmosDb      # CosmosDB event instrumentation
│   ├── OtelEvents.Azure.Storage       # Azure Storage event instrumentation
│   ├── OtelEvents.Subscriptions       # In-process event bus
│   ├── OtelEvents.Analyzers           # Roslyn analyzers
│   ├── OtelEvents.Testing             # In-memory exporter & test assertions
│   └── OtelEvents.Cli                 # CLI library (validate, generate, diff, docs)
├── tests/                             # One test project per src project
├── tools/
│   └── OtelEvents.Cli                 # CLI entry-point (dotnet tool)
├── docs/                              # User guide, security, deployment, dashboards
├── Directory.Build.props              # Shared build settings
├── Directory.Packages.props           # Central package management
└── OtelEvents.slnx                    # Solution file
```

## Pull-request process

1. **Open or find an issue** — every PR should reference an issue.
2. **Branch from `main`** — use a descriptive name
   (e.g., `feat/42-add-widget`, `fix/99-null-check`).
3. **Write tests first** — follow TDD (Red → Green → Refactor).
4. **Keep commits small** — one logical change per commit.
5. **Run the full build locally** before pushing:
   ```bash
   dotnet build -c Release && dotnet test --no-build -c Release
   ```
6. **Open a PR against `main`** — fill in the template and link the issue.
7. **Address review feedback** — the CI pipeline and Guardian agents will run
   automatically (see [SDLC pipeline](#sdlc-pipeline) below).

## Coding standards

The repository enforces conventions through `.editorconfig` and
`Directory.Build.props`. Key rules:

| Rule | Setting |
|------|---------|
| Target framework | `net8.0` |
| Namespace style | **File-scoped** (`namespace X;`) |
| Warnings as errors | **Enabled** (`TreatWarningsAsErrors`) |
| Analysis level | `latest-all` |
| Nullable reference types | **Enabled** |
| Implicit usings | **Enabled** |
| Indentation | 4 spaces (2 for XML, JSON, YAML) |
| Private fields | `_camelCase` |
| Static private fields | `s_camelCase` |
| Public/internal members | `PascalCase` |
| Interfaces | Prefix with `I` |

Additional guidelines:

- **Prefer `sealed`** for concrete classes that are not designed for inheritance.
- **Use pattern matching** for type checks and null checks.
- **Keep functions small** — aim for fewer than 20 lines per method.
- **Name booleans as questions** — `isActive`, `hasPermission`, `canEdit`.
- **No abbreviations** unless universally understood (`id`, `url`, `api` are
  fine; `usr`, `mgr`, `svc` are not).

## Test requirements

- **All new code must have accompanying unit tests.**
- Use [xUnit](https://xunit.net/) with `[Fact]` and `[Theory]` attributes.
- Use **OtelEvents.Testing** (`InMemoryLogExporter`, `LogAssertions`,
  `OtelEventsTestHost`) for assertions on emitted events.
- Place tests in the matching `tests/<ProjectName>.Tests/` project.
- Cover happy paths, edge cases (null, empty, boundary), and error paths.
- The CI pipeline collects code coverage via
  [coverlet](https://github.com/coverlet-coverage/coverlet) — avoid reducing
  overall coverage.

## SDLC pipeline

Every pull request is validated by CI and reviewed by Guardian agents:

| Stage | What happens |
|-------|-------------|
| **CI build** | `dotnet build -c Release` — must compile with zero warnings. |
| **CI test** | `dotnet test --no-build -c Release` — all tests must pass. |
| **Vulnerability scan** | `dotnet list package --vulnerable` — no known vulnerabilities allowed. |
| **Code Review Guardian** | Automated review for complexity, duplication, naming, and design. |
| **Security Guardian** | Automated scan for OWASP Top 10 risks, hardcoded secrets, and PII leaks. |
| **QA Guardian** | Integration and E2E test coverage analysis. |

> **Tip:** Run the analyzers locally (`dotnet build`) to catch issues before
> pushing — the `OtelEvents.Analyzers` package is included in the solution.

## Reporting issues

- Use [GitHub Issues](https://github.com/vbomfim/otel-events-dotnet/issues) to
  report bugs or request features.
- Include reproduction steps, expected behaviour, and actual behaviour.
- For security vulnerabilities, follow the process in
  [SECURITY.md](SECURITY.md).

## License

By contributing you agree that your contributions will be licensed under the
[MIT License](LICENSE).
