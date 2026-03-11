# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.2.0-beta.1] - 2026-03-11

### Fixed

- NuGet package metadata (README, description, license, tags)
- All user guide chapters reviewed and corrected
- Deployment docs fixes (HEALTHCHECK, SDK version, Collector config)
- CI hardening (vulnerability scanning, coverage, symbol packages)
- MSBuild auto-codegen (schemas generate on build)

### Added

- CHANGELOG.md and CONTRIBUTING.md
- global.json for SDK pinning
- SLI/SLO recommendation docs

## [0.1.0] - 2026-03-11

### Added

#### Core packages

- **OtelEvents.Schema** — YAML schema parser with validation (duplicate IDs, missing
  fields, type mismatches), C# code generator (`[LoggerMessage]` partial methods,
  extension methods, `Meter`/`Counter`/`Histogram` statics, enums), schema versioning,
  multi-file packaging, cryptographic schema signing, and embedded lifecycle schema.
- **OtelEvents.Exporter.Json** — AI-optimised single-line JSONL exporter
  (`BaseExporter<LogRecord>`) with severity-based filtering
  (`OtelEventsSeverityFilterProcessor`), rate limiting by event category
  (`OtelEventsRateLimitProcessor`), head/tail sampling
  (`OtelEventsSamplingProcessor`), configurable exception serialization
  (`Full`, `TypeAndMessage`, `TypeOnly`), PII/sensitivity redaction with pattern
  matching and timeouts, environment-aware defaults (Development, Staging,
  Production), and structured attribute serialization.
- **OtelEvents.Causality** — causal event-linking processor
  (`OtelEventsCausalityProcessor`) adding `otel_events.event_id` and
  `otel_events.parent_event_id` attributes via UUID v7 (time-sortable) IDs,
  `AsyncLocal`-based ambient causal context, and explicit `OtelEventsCausalScope`
  management for causal-tree construction.

#### Integration packs (zero-code instrumentation)

- **OtelEvents.AspNetCore** — ASP.NET Core middleware emitting
  `http.request.received`, `http.request.completed`, `http.request.failed`,
  `http.connection.failed`, `http.auth.failed`, and `http.throttled` events.
- **OtelEvents.Grpc** — gRPC server (`OtelEventsGrpcServerInterceptor`) and client
  (`OtelEventsGrpcClientInterceptor`) interceptors emitting `grpc.call.started`,
  `grpc.call.completed`, `grpc.call.failed`, connection, auth, and throttle events.
- **OtelEvents.Azure.CosmosDb** — `DiagnosticListener`-based observer emitting
  `cosmosdb.query.executed`, `cosmosdb.query.failed`, `cosmosdb.point.read`,
  `cosmosdb.point.write`, `cosmosdb.auth.failed`, and `cosmosdb.throttled` events,
  with `CosmosQuerySanitizer` for query redaction.
- **OtelEvents.Azure.Storage** — Azure SDK `HttpPipelinePolicy`
  (`OtelEventsStoragePipelinePolicy`) emitting `storage.blob.uploaded`,
  `storage.blob.downloaded`, `storage.blob.deleted`, `storage.queue.sent`,
  `storage.queue.received`, connection, auth, and throttle events, with
  `StorageOperationClassifier`.
- **OtelEvents.HealthChecks** — `IHealthCheckPublisher` implementation
  (`OtelEventsHealthCheckPublisher`) emitting `health.check.executed` and
  `health.state.changed` events.

#### Developer tools

- **OtelEvents.Subscriptions** — in-process event bus with
  `OtelEventsSubscriptionProcessor`, `OtelEventHandler<T>`, and
  `OtelEventsSubscriptionBuilder` for reactive handlers (circuit breakers, token
  refresh, alerting) via lambda or DI-based registration.
- **OtelEvents.Analyzers** — Roslyn analyzers enforcing logging hygiene:
  OTEL001 (Console.Write), OTEL002 (untyped `ILogger`), OTEL003 (string
  interpolation in event messages), OTEL004 (undefined event name),
  OTEL005 (PII without redaction), OTEL006 (reserved prefix), OTEL007
  (Debug.Write), exception-not-captured analysis, and unused-event detection.
- **OtelEvents.Testing** — `InMemoryLogExporter`, `ExportedLogRecord`,
  `LogAssertions` (fluent assertions on emitted events), and
  `OtelEventsTestHost` for setting up an in-memory OTEL pipeline in unit tests.
- **OtelEvents.Cli** — CLI tool (`dotnet otel-events`) with `validate`,
  `generate`, `diff`, and `docs` commands for schema validation, C# source
  generation, version comparison, and event-catalog documentation.

#### Documentation

- 12-chapter user guide covering introduction, core concepts, getting started,
  schema reference, integration packs, configuration, testing, CLI tool,
  security & privacy, advanced topics, migration guide, and FAQ.
- Security documentation: threat model, PII classification matrix, environment
  profiles, and OWASP Top 10 mapping.
- Deployment documentation: container and Kubernetes deployment guide, resource
  sizing, TLS configuration, and Kubernetes manifests (Deployment, Service, HPA,
  PDB, NetworkPolicy).
- Observability documentation: SLI/SLO recommendations with multi-window
  burn-rate alerting, and pre-built Grafana dashboard template.
- Full project specification (v2.0).

#### Tests

- ~1,500 unit tests across 12 test projects using xUnit, covering all packages,
  edge cases, and error paths.

[unreleased]: https://github.com/vbomfim/otel-events-dotnet/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/vbomfim/otel-events-dotnet/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/vbomfim/otel-events-dotnet/releases/tag/v0.1.0
