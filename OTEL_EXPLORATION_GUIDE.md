# OtelEvents.HealthChecks Implementation Guide

## 1. SPECIFICATION SECTION 15.7 - Complete Details

### Package & Dependencies
```
OtelEvents.HealthChecks (runtime)
├── Microsoft.Extensions.Diagnostics.HealthChecks (>= 8.0) — Health check abstractions
├── OpenTelemetry (>= 1.9)                                 — OTEL SDK types
├── All.Causality (>= 1.0) [optional]                      — Causal scope
└── (no dependency on All.Schema — code is pre-generated)
```

Target frameworks: `net8.0`, `net9.0`

### YAML Schema Fields

**Global Fields:**
- `healthComponent` (string, indexed) - Name of health check component (e.g., 'CosmosDb', 'Redis', 'SqlServer')
- `healthStatus` (enum: Healthy/Degraded/Unhealthy) - Health check result status
- `healthPreviousStatus` (enum: Healthy/Degraded/Unhealthy) - Previous status for state change events
- `healthDurationMs` (double, unit: "ms") - Health check execution duration
- `healthDescription` (string) - Health check result description or reason
- `healthTotalChecks` (int) - Total number of health checks in report
- `healthOverallStatus` (enum: Healthy/Degraded/Unhealthy) - Aggregate status

### Three Events

**Event 1: `health.check.executed` (ID: 10401, Severity: DEBUG)**
- Description: A health check was executed. Emitted for every health check poll cycle.
- Message: "Health check {healthComponent} completed with {healthStatus} in {healthDurationMs}ms"
- Fields: healthComponent, healthStatus, healthDurationMs, healthDescription (optional)
- Metrics:
  - `otel.health.check.duration` (histogram) with buckets: [1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000]
  - `otel.health.check.status` (gauge, 0=Healthy, 1=Degraded, 2=Unhealthy) with label: healthComponent
- Tags: health

**Event 2: `health.state.changed` (ID: 10402, Severity: WARN)**
- Description: Health check component's status changed (transition only, not every poll)
- Message: "Health state changed: {healthComponent} {healthPreviousStatus} → {healthStatus}: {healthDescription}"
- Fields: healthComponent, healthPreviousStatus, healthStatus, healthDurationMs, healthDescription (optional)
- Metrics:
  - `otel.health.state.change.count` (counter) with labels: healthComponent, healthPreviousStatus, healthStatus
- Tags: health, state-change

**Event 3: `health.report.completed` (ID: 10403, Severity: DEBUG)**
- Description: A full health check report was completed (all checks executed in one cycle)
- Message: "Health report completed: {healthOverallStatus} ({healthTotalChecks} checks) in {healthDurationMs}ms"
- Fields: healthOverallStatus, healthTotalChecks, healthDurationMs
- Metrics:
  - `otel.health.report.duration` (histogram) with buckets: [10, 50, 100, 250, 500, 1000, 2500, 5000, 10000]
- Tags: health

### Registration API

```csharp
// Standard health checks
builder.Services.AddHealthChecks()
    .AddCheck<CosmosDbHealthCheck>("CosmosDb")
    .AddCheck<RedisHealthCheck>("Redis")
    .AddCheck<SqlServerHealthCheck>("SqlServer");

// Integration pack — one line
builder.Services.AddOtelEventsHealthChecks(options =>
{
    options.EmitExecutedEvents = true;            // Default: true
    options.EmitStateChangedEvents = true;        // Default: true
    options.EmitReportCompletedEvents = true;     // Default: true
    options.SuppressHealthyExecutedEvents = false; // Default: false (set true to only emit non-Healthy)
    options.EnableCausalScope = true;             // Default: true
});
```

### Implementation Approach

**Mechanism:** `IHealthCheckPublisher` implementation registered as singleton

The publisher implements .NET's built-in `IHealthCheckPublisher` interface - services that receive `HealthReport` after each poll cycle.

**Key Implementation Details:**

```csharp
internal sealed class OtelEventsHealthCheckPublisher : IHealthCheckPublisher
{
    private readonly ILogger<OtelEventsHealthCheckEventSource> _logger;
    private readonly OtelEventsHealthCheckOptions _options;
    private readonly ConcurrentDictionary<string, HealthStatus> _previousStates = new();
    
    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        foreach (var entry in report.Entries)
        {
            // Emit health.check.executed (every poll)
            if (_options.EmitExecutedEvents &&
                !(_options.SuppressHealthyExecutedEvents && entry.Value.Status == HealthStatus.Healthy))
            {
                _logger.HealthCheckExecuted(
                    healthComponent: entry.Key,
                    healthStatus: MapStatus(entry.Value.Status),
                    healthDurationMs: entry.Value.Duration.TotalMilliseconds,
                    healthDescription: entry.Value.Description);
            }

            // Emit health.state.changed (only on transitions)
            if (_options.EmitStateChangedEvents)
            {
                var previousStatus = _previousStates.GetOrAdd(entry.Key, entry.Value.Status);
                if (previousStatus != entry.Value.Status)
                {
                    _logger.HealthStateChanged(
                        healthComponent: entry.Key,
                        healthPreviousStatus: MapStatus(previousStatus),
                        healthStatus: MapStatus(entry.Value.Status),
                        healthDurationMs: entry.Value.Duration.TotalMilliseconds,
                        healthDescription: entry.Value.Description);
                    
                    _previousStates[entry.Key] = entry.Value.Status;
                }
            }
        }

        // Emit health.report.completed (aggregate)
        if (_options.EmitReportCompletedEvents)
        {
            _logger.HealthReportCompleted(
                healthOverallStatus: MapStatus(report.Status),
                healthTotalChecks: report.Entries.Count,
                healthDurationMs: report.TotalDuration.TotalMilliseconds);
        }

        return Task.CompletedTask;
    }
}
```

**Bounded State Tracking:** _previousStates dictionary max capacity: 1,000 entries. If more than 1,000 unique health check names are registered, new entries rejected + warning logged.

**Relationship to Phase 2 Item 2.4:** Complementary to "Health/readiness events — Built-in schema for application lifecycle events: startup, ready, degraded, shutdown". This pack covers component-level health check results from Microsoft.Extensions.Diagnostics.HealthChecks. They coexist.

### Example JSON Output

**health.check.executed:**
```json
{"timestamp":"2025-07-15T14:30:30.000000Z","event":"health.check.executed","severity":"DEBUG","severityNumber":5,"message":"Health check CosmosDb completed with Healthy in 12.4ms","service":"order-service","environment":"production","eventId":"evt_01914a34-3a4b-5c6d-7e8f-0a1b2c3d4e5f","attr":{"healthComponent":"CosmosDb","healthStatus":"Healthy","healthDurationMs":12.4},"tags":["health"],"all.v":"1.0.0","all.seq":501,"all.host":"web-01","all.pid":4821}
```

**health.state.changed:**
```json
{"timestamp":"2025-07-15T14:31:00.000000Z","event":"health.state.changed","severity":"WARN","severityNumber":13,"message":"Health state changed: Redis Healthy → Degraded: Connection pool exhausted (23/25 connections in use)","service":"order-service","environment":"production","eventId":"evt_01914a35-4b5c-6d7e-8f9a-1b2c3d4e5f6a","attr":{"healthComponent":"Redis","healthPreviousStatus":"Healthy","healthStatus":"Degraded","healthDurationMs":5023.1,"healthDescription":"Connection pool exhausted (23/25 connections in use)"},"tags":["health","state-change"],"all.v":"1.0.0","all.seq":510,"all.host":"web-01","all.pid":4821}
```

**health.report.completed:**
```json
{"timestamp":"2025-07-15T14:31:00.050000Z","event":"health.report.completed","severity":"DEBUG","severityNumber":5,"message":"Health report completed: Degraded (3 checks) in 5045.2ms","service":"order-service","environment":"production","eventId":"evt_01914a36-5c6d-7e8f-9a0b-2c3d4e5f6a7b","attr":{"healthOverallStatus":"Degraded","healthTotalChecks":3,"healthDurationMs":5045.2},"tags":["health"],"all.v":"1.0.0","all.seq":513,"all.host":"web-01","all.pid":4821}
```

---

## 2. ARCHITECTURE OVERVIEW

### Integration Pack Architecture (Section 15.2)

**Build Time (Pack Author):**
```
Pack YAML Schema (embedded)
    ↓
All.Schema (build-time) → Pre-generated C# code
    ↓
- [LoggerMessage] methods
- Meter/Counter/Histogram
- Extension methods
    ↓
Compiled into NuGet package

Runtime Glue (pack-specific):
- ASP.NET Core Middleware (OtelEvents.AspNetCore)
- gRPC Interceptor (OtelEvents.Grpc)
- DiagnosticListener (OtelEvents.Azure.*)
- IHealthCheckPublisher (OtelEvents.HealthChecks)
```

**Consumer Runtime:**
```
App Code (Program.cs) - builder.Services.AddOtelEventsHealthChecks()
    ↓
Pack IHealthCheckPublisher registered as singleton
    ↓
Observes HealthReport from health check polling
    ↓
Calls pre-generated ILogger extension methods
    ↓
Records pre-generated Meter instruments
    ↓
(Optional) Creates AllCausalityContext scope
    ↓
Standard OTEL SDK Pipeline
    ↓
AllCausalityProcessor → AllJsonExporter + OTLP Exporter
```

### Event ID Reservation (10401-10499)

| Range | Package | Description |
|-------|---------|-------------|
| 1–9999 | Consumer schemas | Application-defined events |
| 10001–10099 | OtelEvents.AspNetCore | HTTP request lifecycle |
| 10101–10199 | OtelEvents.Grpc | gRPC call lifecycle |
| 10201–10299 | OtelEvents.Azure.CosmosDb | CosmosDB operations |
| 10301–10399 | OtelEvents.Azure.Storage | Azure Storage operations |
| **10401–10499** | **OtelEvents.HealthChecks** | **Health check events** |
| 10500–19999 | Reserved | Future integration packs |

### Meter Name Convention

Each pack registers its own OTEL `Meter` with namespaced name: `OtelEvents.HealthChecks`

Consumers register all integration pack meters: `metrics.AddMeter("OtelEvents.*")`

---

## 3. PROJECT STRUCTURE & CSPROJ FILES

### Solution File (OtelEvents.slnx)

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/All.Analyzers/All.Analyzers.csproj" />
    <Project Path="src/All.Causality/All.Causality.csproj" />
    <Project Path="src/All.Exporter.Json/All.Exporter.Json.csproj" />
    <Project Path="src/All.Schema/All.Schema.csproj" />
    <Project Path="src/All.Testing/All.Testing.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/All.Analyzers.Tests/All.Analyzers.Tests.csproj" />
    <Project Path="tests/All.Causality.Tests/All.Causality.Tests.csproj" />
    <Project Path="tests/All.Exporter.Json.Tests/All.Exporter.Json.Tests.csproj" />
    <Project Path="tests/All.Schema.Tests/All.Schema.Tests.csproj" />
  </Folder>
</Solution>
```

### All .csproj Files & Target Frameworks

**Central Configuration (Directory.Build.props):**
```xml
<TargetFramework>net10.0</TargetFramework>
<ImplicitUsings>enable</ImplicitUsings>
<Nullable>enable</Nullable>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<LangVersion>latest</LangVersion>
<AnalysisLevel>latest-all</AnalysisLevel>
```

**All Source Projects:**
1. `All.Analyzers/All.Analyzers.csproj` - Target: net10.0
2. `All.Causality/All.Causality.csproj` - Target: net10.0
3. `All.Exporter.Json/All.Exporter.Json.csproj` - Target: net10.0
4. `All.Schema/All.Schema.csproj` - Target: net10.0 (but marked with NoWarn)
5. `All.Testing/All.Testing.csproj` - Target: net10.0

**All Test Projects:**
1. `All.Analyzers.Tests/All.Analyzers.Tests.csproj` - Target: net10.0, IsPackable: false
2. `All.Causality.Tests/All.Causality.Tests.csproj` - Target: net10.0, IsPackable: false
3. `All.Exporter.Json.Tests/All.Exporter.Json.Tests.csproj` - Target: net10.0, IsPackable: false
4. `All.Schema.Tests/All.Schema.Tests.csproj` - Target: net10.0, IsPackable: false
5. `All.Integration.Tests/All.Integration.Tests.csproj` - Target: net10.0, IsPackable: false (appears to be new)

---

## 4. EXISTING INTEGRATION PACK STRUCTURE PATTERNS

### Core Infrastructure Packages (All.*)

**All.Schema** - YAML parser & code generator
- Parses YAML schema documents
- Generates: Enum types, LoggerMessage extensions, Metrics classes, typed Emit methods
- Auto-generated header comment: `// <auto-generated> // Generated by ALL Code Generator // DO NOT EDIT`
- Namespace pattern: Generated code placed in schema-defined namespace

**All.Causality** - Causal event linking processor
- `AllCausalityProcessor` - BaseProcessor that enriches LogRecords with all.event_id and all.parent_event_id
- `AllCausalityExtensions.cs` - Public registration method
- `AllCausalityContext.cs` - Manages causal context
- `AllCausalScope.cs` - Creates scope boundaries
- `Uuid7.cs` - UUID v7 generation with "evt_" prefix

**All.Exporter.Json** - AI-optimized JSON exporter
- `AllJsonExporter.cs` - Custom BaseExporter<LogRecord>
- `AllJsonExporterExtensions.cs` - Public registration via `AddAllJsonExporter()`
- `AllJsonExporterOptions.cs` - Configuration options
- `AllEnvironmentProfile.cs` - Environment detection
- `ExceptionDetailLevel.cs` - Exception serialization control

**All.Testing** - Testing utilities
- In-memory collector for LogRecords
- Assertion extensions (appears to have placeholder structure)

---

## 5. HOW CORE OTEL EVENTS LIBRARY WORKS

### Key Interfaces/Classes for Emitting Events

**Event Emission Pattern:**

All events follow a two-step pattern generated by CodeGenerator:

1. **ILogger Extension Method (LoggerMessage):**
   - Auto-generated with `[LoggerMessage]` attribute
   - Parameters: ILogger logger + all event fields
   - Handles structured logging
   
   Example:
   ```csharp
   [LoggerMessage(
       EventId = 10401,
       Level = LogLevel.Debug,
       Message = "Health check {healthComponent} completed with {healthStatus} in {healthDurationMs}ms")]
   public static partial void HealthCheckExecuted(
       this ILogger logger,
       string healthComponent,
       string healthStatus,
       double healthDurationMs,
       string? healthDescription);
   ```

2. **Typed Emit Extension (Combines Logging + Metrics):**
   - High-level convenience method
   - Calls the LoggerMessage method + records metrics
   - Named `Emit{EventName}`
   
   Example:
   ```csharp
   public static void EmitHealthCheckExecuted(
       this ILogger logger,
       string healthComponent,
       string healthStatus,
       double healthDurationMs,
       string? healthDescription)
   {
       // Call LoggerMessage
       OtelEventsHealthCheckLoggerExtensions.HealthCheckExecuted(
           logger, healthComponent, healthStatus, healthDurationMs, healthDescription);
       
       // Record metrics
       var tags = new TagList
       {
           { "healthComponent", healthComponent }
       };
       OtelEventsHealthCheckMetrics.HealthCheckDuration.Record(
           healthDurationMs, tags);
       OtelEventsHealthCheckMetrics.HealthCheckStatus.Record(
           MapStatusToInt(healthStatus), tags);
   }
   ```

### Generated Code Structure

**OtelEventsHealthCheckEvents.g.cs contains:**
- `OtelEventsHealthCheckLoggerExtensions` class - All [LoggerMessage] methods
- `OtelEventsHealthCheckMetrics` class - Static Meter + instrument fields
- `OtelEventsHealthCheckEventExtensions` class - All Emit* methods
- All enum types (HealthStatus)

**Metrics Class Pattern:**
```csharp
public static class OtelEventsHealthCheckMetrics
{
    private static readonly Meter s_meter = new Meter(
        "OtelEvents.HealthChecks", "1.0.0");
    
    public static readonly Histogram<double> HealthCheckDuration = 
        s_meter.CreateHistogram<double>(
            "otel.health.check.duration", "ms", 
            "Health check execution duration");
    
    public static readonly ObservableGauge<int> HealthCheckStatus = 
        s_meter.CreateObservableGauge<int>(
            "otel.health.check.status", "status",
            "Current health check status (0=Healthy, 1=Degraded, 2=Unhealthy)");
}
```

---

## 6. TEST FRAMEWORK & PATTERNS

### Framework & Tools

**Test Framework:** xUnit 2.9.3
**Test SDK:** Microsoft.NET.Test.Sdk 17.14.1
**Code Analysis Testing:** Microsoft.CodeAnalysis.CSharp.Analyzer.Testing.XUnit 1.1.2
**Code Coverage:** coverlet.collector 6.0.4

### Test Project Structure

**Test Projects Pattern:**
```xml
<PropertyGroup>
  <IsPackable>false</IsPackable>
  <NoWarn>CA1307;CA1310;CA1707</NoWarn>
</PropertyGroup>

<ItemGroup>
  <Using Include="Xunit" />  <!-- Global using Xunit -->
</ItemGroup>

<ItemGroup>
  <ProjectReference Include="..\..\src\All.Schema\All.Schema.csproj" />
</ItemGroup>
```

### Mocking & Test Patterns

From AllCausalityProcessorTests:

```csharp
public class AllCausalityProcessorTests : IDisposable
{
    private readonly TestLogExporter _exporter;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;

    public AllCausalityProcessorTests()
    {
        _exporter = new TestLogExporter();
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.AddProcessor(new AllCausalityProcessor());
                options.AddProcessor(new SimpleLogRecordExportProcessor(_exporter));
            });
            builder.SetMinimumLevel(LogLevel.Trace);
        });
        _logger = _loggerFactory.CreateLogger("TestLogger");
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
    }

    [Fact]
    public void OnEnd_AddsEventIdAttribute()
    {
        // Act
        _logger.LogInformation("Test message");

        // Assert
        var records = _exporter.GetRecords();
        Assert.Single(records);
        Assert.True(
            records[0].Attributes.ContainsKey("all.event_id"),
            "LogRecord should have 'all.event_id' attribute");
    }
}
```

**Key Patterns:**
- IDisposable for resource cleanup
- TestLogExporter mock to capture LogRecords
- LoggerFactory.Create() for isolated logger instances
- SimpleLogRecordExportProcessor to pipe to mock exporter
- Arrange-Act-Assert pattern
- Assert methods: Assert.Single, Assert.True, Assert.Matches, etc.

### Test Categories

**All.Schema.Tests:**
- CodeGeneratorTests - Generated C# code validation
- SchemaParserTests - YAML schema parsing
- SchemaMergerTests - Multi-file schema handling
- SchemaEdgeCaseTests - Edge cases

**All.Causality.Tests:**
- AllCausalityProcessorTests - Causal linking
- AllCausalScopeTests - Scope lifecycle
- AllCausalityContextTests - Context tracking
- Uuid7Tests - UUID generation
- TestLogExporter - Mock exporter

**All.Exporter.Json.Tests:**
- AllJsonExporterTests - JSON output
- AllSeverityFilterProcessorTests - Severity filtering
- AllSeverityFilterExtensionsTests - Filter registration

**All.Integration.Tests:**
- Multi-pack scenarios (appears new/placeholder)

---

## 7. DI REGISTRATION PATTERN

### All.Causality Registration

```csharp
// AllCausalityExtensions.cs
public static LoggerProviderBuilder AddAllCausalityProcessor(
    this LoggerProviderBuilder builder)
{
    ArgumentNullException.ThrowIfNull(builder);
    return builder.AddProcessor(new AllCausalityProcessor());
}

// Usage:
builder.Logging.AddOpenTelemetry(logging =>
    logging.AddAllCausalityProcessor());
```

### All.Exporter.Json Registration

```csharp
// AllJsonExporterExtensions.cs
public static LoggerProviderBuilder AddAllJsonExporter(
    this LoggerProviderBuilder builder,
    Action<AllJsonExporterOptions>? configure = null)
{
    ArgumentNullException.ThrowIfNull(builder);

    var options = new AllJsonExporterOptions();
    configure?.Invoke(options);

    var exporter = new AllJsonExporter(options);
    var processor = new SimpleLogRecordExportProcessor(exporter);

    return builder.AddProcessor(processor);
}

// Usage:
builder.Logging.AddOpenTelemetry(logging =>
    logging.AddAllJsonExporter(options =>
    {
        options.Output = AllJsonOutput.Stdout;
    }));
```

### Expected HealthChecks Registration Pattern

Should follow the same pattern:

```csharp
public static IServiceCollection AddOtelEventsHealthChecks(
    this IServiceCollection services,
    Action<OtelEventsHealthCheckOptions>? configure = null)
{
    ArgumentNullException.ThrowIfNull(services);

    var options = new OtelEventsHealthCheckOptions();
    configure?.Invoke(options);

    services.AddSingleton(options);
    services.AddSingleton<IHealthCheckPublisher, OtelEventsHealthCheckPublisher>();

    return services;
}
```

---

## 8. NUGET PACKAGES REFERENCED

### Core Runtime Dependencies (Directory.Packages.props)

- **OpenTelemetry** v1.15.0 - OTEL SDK types and processors
- **YamlDotNet** v16.3.0 - YAML parsing
- **System.Text.Json** v10.0.3 - JSON handling
- **Microsoft.Extensions.Logging** v10.0.3 - Logging abstractions
- **Microsoft.Extensions.Logging.Abstractions** v10.0.3 - ILogger interface

### Build-Time Dependencies

- **Microsoft.CodeAnalysis.CSharp** v4.13.0 - Roslyn analyzers

### Test Dependencies

- **xunit** v2.9.3 - Test framework
- **xunit.runner.visualstudio** v3.1.4 - Test runner
- **Microsoft.NET.Test.Sdk** v17.14.1 - Test SDK
- **Microsoft.CodeAnalysis.CSharp.Analyzer.Testing.XUnit** v1.1.2 - Analyzer testing
- **coverlet.collector** v6.0.4 - Code coverage

### For HealthChecks Integration Pack

**Additional Dependencies Needed:**
- **Microsoft.Extensions.Diagnostics.HealthChecks** (>= 8.0) - IHealthCheckPublisher
- **Microsoft.Extensions.DependencyInjection** (>= 10.0) - Services extension methods

---

## 9. SOLUTION FILE ORGANIZATION

### OtelEvents.slnx Structure

```
<Solution>
  <!-- Source projects: Core infrastructure -->
  <Folder Name="/src/">
    <Project Path="src/All.Analyzers/All.Analyzers.csproj" />
    <Project Path="src/All.Causality/All.Causality.csproj" />
    <Project Path="src/All.Exporter.Json/All.Exporter.Json.csproj" />
    <Project Path="src/All.Schema/All.Schema.csproj" />
    <Project Path="src/All.Testing/All.Testing.csproj" />
    
    <!-- Integration packs will go here in future -->
    <!-- <Project Path="src/OtelEvents.HealthChecks/OtelEvents.HealthChecks.csproj" /> -->
  </Folder>
  
  <!-- Test projects -->
  <Folder Name="/tests/">
    <Project Path="tests/All.Analyzers.Tests/All.Analyzers.Tests.csproj" />
    <Project Path="tests/All.Causality.Tests/All.Causality.Tests.csproj" />
    <Project Path="tests/All.Exporter.Json.Tests/All.Exporter.Json.Tests.csproj" />
    <Project Path="tests/All.Schema.Tests/All.Schema.Tests.csproj" />
    
    <!-- Test projects for integration packs will go here -->
    <!-- <Project Path="tests/OtelEvents.HealthChecks.Tests/OtelEvents.HealthChecks.Tests.csproj" /> -->
  </Folder>
</Solution>
```

### Folder Organization Pattern

- `/src/` - Source code (Core infrastructure + Integration packs)
- `/tests/` - Test projects (tests for each source project)
- `Directory.Build.props` - Centralized build properties
- `Directory.Packages.props` - Centralized package versions

---

## 10. EXISTING HEALTH CHECK RELATED CODE

### Current Health Check References

**Schema Tests (SchemaParserTests.cs):**
- Test enum definitions with HealthStatus values (Healthy, Degraded, Unhealthy)
- Used as test data for YAML parsing

**No Existing Implementation Code:**
- No OtelEvents.HealthChecks package yet
- No IHealthCheckPublisher implementation
- No health check integration pack code

### Health Check Infrastructure Available in .NET

The project relies on:
- `Microsoft.Extensions.Diagnostics.HealthChecks` - Standard .NET health check system
- `IHealthCheck` - Interface for implementing health checks
- `IHealthCheckPublisher` - Hook for receiving health reports
- `HealthReport` - Contains entries with status, duration, description
- `HealthStatus` enum - Healthy, Degraded, Unhealthy

---

## IMPLEMENTATION SUMMARY

To implement OtelEvents.HealthChecks:

### Project Structure Needed

```
src/OtelEvents.HealthChecks/
├── OtelEvents.HealthChecks.csproj
├── Properties/
│   └── AssemblyInfo.cs (if needed)
├── OtelEventsHealthCheckOptions.cs
├── OtelEventsHealthCheckPublisher.cs (IHealthCheckPublisher)
├── OtelEventsHealthCheckExtensions.cs (AddOtelEventsHealthChecks)
├── Internal/
│   ├── OtelEventsHealthCheckEventSource.cs (logger category)
│   └── StatusMapper.cs (HealthStatus → string conversion)
└── Events/
    └── OtelEventsHealthCheckEvents.g.cs (pre-generated from YAML)

tests/OtelEvents.HealthChecks.Tests/
├── OtelEvents.HealthChecks.Tests.csproj
├── OtelEventsHealthCheckPublisherTests.cs
├── RegistrationTests.cs
├── StateChangeDetectionTests.cs
├── MetricRecordingTests.cs
└── TestHealthCheckPublisher.cs (mock)
```

### Key Implementation Points

1. **Schema YAML** - Embedded resource with 3 events, 7 fields, 5 metrics
2. **Generated Code** - Pre-compiled into package (LoggerMessage extensions, Metrics class, Emit methods)
3. **Publisher** - IHealthCheckPublisher implementation with state tracking dictionary (max 1000 entries)
4. **Options** - OtelEventsHealthCheckOptions with 4 boolean flags
5. **Registration** - AddOtelEventsHealthChecks() extension method on IServiceCollection
6. **Tests** - Unit + integration tests covering state changes, metric recording, configuration
7. **Benchmarks** - Target < 25μs p95 per check publisher overhead

### Dependencies

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks" Version="10.0.3" />
  <PackageReference Include="OpenTelemetry" />
  <!-- No dependency on All.Schema - code is pre-generated -->
</ItemGroup>

<ItemGroup>
  <InternalsVisibleTo Include="OtelEvents.HealthChecks.Tests" />
</ItemGroup>
```

### DI Registration Integration

The package must be added to `Directory.Packages.props` and referenced:
- Main package targeting net8.0, net9.0
- Test package targeting net10.0
- Both in their respective folders with standard structure
