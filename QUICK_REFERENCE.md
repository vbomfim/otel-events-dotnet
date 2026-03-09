# OtelEvents.HealthChecks - Quick Reference Card

## Project Structure

```
src/OtelEvents.HealthChecks/
├── OtelEvents.HealthChecks.csproj           # Target: net8.0, net9.0
├── OtelEventsHealthCheckPublisher.cs        # IHealthCheckPublisher impl
├── OtelEventsHealthCheckOptions.cs          # 5 config options
├── OtelEventsHealthCheckExtensions.cs       # AddOtelEventsHealthChecks()
├── OtelEventsHealthCheckEventSource.cs      # Logger category
└── Events/
    └── OtelEventsHealthCheckEvents.g.cs     # Pre-generated (400+ lines)

tests/OtelEvents.HealthChecks.Tests/
├── OtelEvents.HealthChecks.Tests.csproj
├── OtelEventsHealthCheckPublisherTests.cs   # Unit tests
├── RegistrationTests.cs                     # DI integration tests
├── StateChangeDetectionTests.cs             # Logic tests
└── TestLogExporter.cs                       # Mock exporter
```

## Dependencies to Add

### Directory.Packages.props (Add these package versions)
```xml
<PackageVersion Include="Microsoft.Extensions.Diagnostics.HealthChecks" Version="10.0.3" />
```

### OtelEvents.HealthChecks.csproj
```xml
<PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks" />
<PackageReference Include="OpenTelemetry" />
```

## Events Summary

| ID | Event | Severity | Trigger | When |
|----|----|----------|---------|------|
| 10401 | health.check.executed | DEBUG | Check runs | Every poll |
| 10402 | health.state.changed | WARN | Status differs | Transition only |
| 10403 | health.report.completed | DEBUG | Polling done | Every cycle |

## Configuration Options

```csharp
builder.Services.AddOtelEventsHealthChecks(options =>
{
    options.EmitExecutedEvents = true;           // Every poll event
    options.EmitStateChangedEvents = true;       // Transition event
    options.EmitReportCompletedEvents = true;    // Aggregate event
    options.SuppressHealthyExecutedEvents = false; // Hide "healthy"?
    options.EnableCausalScope = true;            // Link to parent event
});
```

## Metrics Generated

```
otel.health.check.duration         → Histogram (buckets: [1,5,10,25,50,100,250,500,1000,2500,5000])
otel.health.check.status           → Gauge (0=Healthy, 1=Degraded, 2=Unhealthy)
otel.health.state.change.count     → Counter
otel.health.report.duration        → Histogram (buckets: [10,50,100,250,500,1000,2500,5000,10000])

Labels: healthComponent, healthPreviousStatus, healthStatus (when applicable)
Meter Name: "OtelEvents.HealthChecks" version "1.0.0"
```

## Key Implementation Details

### State Tracking
- **Dictionary:** `ConcurrentDictionary<string, HealthStatus> _previousStates`
- **Max capacity:** 1,000 entries
- **First poll:** No state-changed event (initial value recorded)
- **Transitions:** Only emit state-changed when status differs

### Publisher Logic
```csharp
public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
{
    foreach (var entry in report.Entries)
    {
        // 1. Emit executed (optional suppress for Healthy)
        // 2. Emit state-changed (only on transitions)
    }
    // 3. Emit report-completed (aggregate)
}
```

### Event Emission Pattern
```csharp
// 1. Call LoggerMessage (pre-generated)
logger.HealthCheckExecuted(healthComponent, healthStatus, ...);

// 2. Record metrics with TagList
var tags = new TagList { { "healthComponent", healthComponent } };
OtelEventsHealthCheckMetrics.HealthCheckDuration.Record(durationMs, tags);
```

## Registration Pattern

```csharp
public static IServiceCollection AddOtelEventsHealthChecks(
    this IServiceCollection services,
    Action<OtelEventsHealthCheckOptions>? configure = null)
{
    var options = new OtelEventsHealthCheckOptions();
    configure?.Invoke(options);
    
    services.AddSingleton(options);
    services.AddSingleton<IHealthCheckPublisher>(
        sp => new OtelEventsHealthCheckPublisher(
            sp.GetRequiredService<ILogger<OtelEventsHealthCheckEventSource>>(),
            options));
    
    return services;
}
```

## Test Framework

- **Framework:** xUnit 2.9.3
- **Mock:** TestLogExporter (captures LogRecords)
- **Setup:** LoggerFactory.Create() + SimpleLogRecordExportProcessor
- **Cleanup:** IDisposable

## Generated Code Files

### OtelEventsHealthCheckEvents.g.cs (Pre-compiled)

**3 Parts:**

1. **LoggerExtensions Class** (3 methods)
   - HealthCheckExecuted()
   - HealthStateChanged()
   - HealthReportCompleted()

2. **Metrics Class** (4 instruments)
   - HealthCheckDuration (Histogram)
   - HealthCheckStatus (Gauge)
   - HealthStateChangeCount (Counter)
   - HealthReportDuration (Histogram)

3. **EventExtensions Class** (3 methods)
   - EmitHealthCheckExecuted()
   - EmitHealthStateChanged()
   - EmitHealthReportCompleted()

## CSPROJ Template (Runtime Package)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <NoWarn>CA1062;CA1822;CA2000</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="OtelEvents.HealthChecks.Tests" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks" />
    <PackageReference Include="OpenTelemetry" />
  </ItemGroup>
</Project>
```

## CSPROJ Template (Test Package)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <NoWarn>CA1307;CA1707</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\OtelEvents.HealthChecks\OtelEvents.HealthChecks.csproj" />
  </ItemGroup>
</Project>
```

## Key Constraints

- **Nullable:** Enable (required)
- **Warnings as Errors:** TreatWarningsAsErrors=true
- **Auto-generated Header:** Must include in generated files
- **Namespace:** `OtelEvents.HealthChecks.Events` (generated code)
- **Logger Category:** Use `OtelEventsHealthCheckEventSource` for filtering
- **Benchmarks:** < 25μs p95 per check publisher overhead
- **State Capacity:** 1,000 unique health check names max

## Files to Create

**Runtime Package:**
1. ✓ OtelEventsHealthCheckPublisher.cs
2. ✓ OtelEventsHealthCheckOptions.cs
3. ✓ OtelEventsHealthCheckExtensions.cs
4. ✓ OtelEventsHealthCheckEventSource.cs
5. ✓ OtelEventsHealthCheckEvents.g.cs (pre-generated)

**Test Package:**
1. ✓ OtelEventsHealthCheckPublisherTests.cs
2. ✓ RegistrationTests.cs
3. ✓ StateChangeDetectionTests.cs
4. ✓ MetricRecordingTests.cs
5. ✓ TestLogExporter.cs (if needed)

**Solution Changes:**
1. ✓ Update OtelEvents.slnx to add both projects
2. ✓ Add package versions to Directory.Packages.props

## Common Issues & Solutions

| Issue | Solution |
|-------|----------|
| State-changed on first poll | This is expected! GetOrAdd just records initial value, no event |
| Null health checks | Optional fields allowed - log null as missing, not string "null" |
| > 1000 checks | Dictionary at capacity - log warning, don't track new checks |
| Missing metrics | Ensure Meter registered: `metrics.AddMeter("OtelEvents.*")` |
| Logger not emitting | Check LogLevel filter in configuration |

## Relationship to Other Packs

- **Complements Phase 2.4:** Health/readiness events (application lifecycle)
- **This pack:** Component-level health checks (IHealthCheckPublisher)
- **Coexist with:** OTEL auto-instrumentation, other integration packs
- **Target frameworks:** net8.0, net9.0 (like all integration packs)

## Phase Assignment

- **Phase:** 2.7 (part of Phase 2 Integration Packs)
- **Priority:** Second (after AspNetCore which is 2.6)
- **Event ID Range:** 10401-10499

## Complete Documentation References

- **SPECIFICATION.md:** Section 15.7 (OtelEvents.HealthChecks)
- **Full Guide:** OTEL_EXPLORATION_GUIDE.md
- **Code Examples:** CODE_PATTERNS.md

---

**Ready to implement? Start with:**
1. Create project directories (src/OtelEvents.HealthChecks, tests/...)
2. Copy CSPROJ templates
3. Copy generated code template
4. Implement 4 runtime classes
5. Write unit & integration tests
6. Update solution file & package props

**Benchmarks:**
- Target: < 25μs p95 per health check
- Measure with: BenchmarkDotNet
