# Chapter 7 — Configuration

ALL components are configured through standard .NET mechanisms: `appsettings.json` binding, programmatic `Action<TOptions>` delegates, and environment variable overrides. This chapter covers every configurable option.

---

## appsettings.json Structure

ALL uses two primary configuration sections under the `All` root key:

```json
{
  "All": {
    "Exporter": {
      "Output": "Stdout",
      "SchemaVersion": "1.0.0",
      "EnvironmentProfile": "Production",
      "ExceptionDetailLevel": "TypeAndMessage",
      "EmitHostInfo": false,
      "MaxAttributeValueLength": 4096,
      "LockTimeout": "00:00:00.100"
    },
    "Filter": {
      "MinSeverity": "Warning"
    }
  }
}
```

### How Binding Works

The `AddAllJsonExporter` and `AddAllSeverityFilter` extension methods accept an `IConfiguration` instance and call `.Bind()` on the appropriate section:

```csharp
// Exporter — binds "All:Exporter" section
builder.AddAllJsonExporter(configuration);

// Filter — binds "All:Filter" section
builder.AddAllSeverityFilter(configuration, innerProcessor);
```

Under the hood, this reads the section and maps JSON properties to the options class:

```csharp
var section = configuration.GetSection("All:Exporter");
var options = new AllJsonExporterOptions();
section.Bind(options);
```

Rate limiting and sampling are configured **programmatically only** (not from appsettings.json) because they require `Action<TOptions>` delegates.

---

## Environment Variable Overrides

.NET's configuration system allows environment variables to override `appsettings.json` values using **double underscores** (`__`) as section separators:

| Environment Variable | Overrides Config Path |
|---|---|
| `ALL__Exporter__Output` | `All:Exporter:Output` |
| `ALL__Exporter__FilePath` | `All:Exporter:FilePath` |
| `ALL__Exporter__EnvironmentProfile` | `All:Exporter:EnvironmentProfile` |
| `ALL__Exporter__ExceptionDetailLevel` | `All:Exporter:ExceptionDetailLevel` |
| `ALL__Exporter__EmitHostInfo` | `All:Exporter:EmitHostInfo` |
| `ALL__Exporter__MaxAttributeValueLength` | `All:Exporter:MaxAttributeValueLength` |
| `ALL__Exporter__SchemaVersion` | `All:Exporter:SchemaVersion` |
| `ALL__Filter__MinSeverity` | `All:Filter:MinSeverity` |

### Example: Override Output Target in Production

```bash
# In appsettings.json: Output = "Stdout"
# Override at deploy time:
export ALL__Exporter__Output=File
export ALL__Exporter__FilePath=/var/log/events.jsonl
```

### Precedence Order (Highest → Lowest)

1. **Environment variables** (`ALL__*`)
2. **appsettings.{Environment}.json** (e.g., `appsettings.Production.json`)
3. **appsettings.json**
4. **Programmatic configuration** (`Action<TOptions>`)
5. **Built-in defaults**

---

## EnvironmentProfile Auto-Detection

The `EnvironmentProfileDetector` automatically determines the `AllEnvironmentProfile` from standard .NET environment variables:

1. Checks `ASPNETCORE_ENVIRONMENT` first (ASP.NET Core standard)
2. Falls back to `DOTNET_ENVIRONMENT` (.NET Generic Host standard)
3. Matches case-insensitively: `"Development"`, `"Staging"`, `"Production"`
4. **Defaults to `Production`** if neither variable is set or the value is unrecognized

Auto-detection **only** applies when `EnvironmentProfile` is **not** explicitly set in configuration:

```csharp
// Auto-detection logic in AddAllJsonExporter:
if (section["EnvironmentProfile"] is null)
{
    options.EnvironmentProfile = EnvironmentProfileDetector.Detect();
}
```

### What Each Profile Controls

| Setting | Development | Staging | Production |
|---|---|---|---|
| `ExceptionDetailLevel` (default) | `Full` | `TypeAndMessage` | `TypeAndMessage` |
| `public` fields | ✅ Visible | ✅ Visible | ✅ Visible |
| `internal` fields | ✅ Visible | ✅ Visible | 🔒 Redacted |
| `pii` fields | ✅ Visible | 🔒 Redacted | 🔒 Redacted |
| `credential` fields | 🔒 Redacted | 🔒 Redacted | 🔒 Redacted |

> **Tip:** Always explicitly set `EnvironmentProfile` in production deployments rather than relying on auto-detection, so the behavior is obvious from the configuration file.

---

## AllJsonExporterOptions Reference

**Configuration section:** `All:Exporter`

| Property | Type | Default | Description |
|---|---|---|---|
| `Output` | `AllJsonOutput` | `Stdout` | Output target: `Stdout`, `Stderr`, or `File` |
| `FilePath` | `string?` | `null` | File path when `Output` is `File` |
| `SchemaVersion` | `string` | `"1.0.0"` | Schema version stamped into every envelope as `all.v` |
| `EnvironmentProfile` | `AllEnvironmentProfile` | `Production` | Security-sensitive defaults preset. Auto-detected from `ASPNETCORE_ENVIRONMENT` or `DOTNET_ENVIRONMENT` if not explicitly set |
| `ExceptionDetailLevel` | `ExceptionDetailLevel?` | `null` | Exception detail in JSON. When `null`, resolved from `EnvironmentProfile` |
| `EmitHostInfo` | `bool` | `false` | Emit `all.host` and `all.pid` in the envelope. May expose infrastructure details |
| `MaxAttributeValueLength` | `int` | `4096` | Maximum length for any single string attribute value. Values exceeding this are truncated with `"…[truncated]"` |
| `AttributeAllowlist` | `ISet<string>?` | `null` | When set, only listed attributes pass through for non-ALL LogRecords. `null` = all pass |
| `AttributeDenylist` | `ISet<string>` | `[]` | Attributes to never emit. Takes precedence over allowlist |
| `RedactPatterns` | `IList<string>` | `[]` | Regex patterns for value-level redaction. Matching values replaced with `[REDACTED]` |
| `LockTimeout` | `TimeSpan` | `100ms` | Lock timeout for stream writes. Batches that cannot acquire the lock are dropped |

### ExceptionDetailLevel Resolution

When `ExceptionDetailLevel` is `null` (the default), it resolves based on `EnvironmentProfile`:

```csharp
internal ExceptionDetailLevel ResolvedExceptionDetailLevel =>
    ExceptionDetailLevel ?? EnvironmentProfile switch
    {
        AllEnvironmentProfile.Development => ExceptionDetailLevel.Full,
        AllEnvironmentProfile.Staging     => ExceptionDetailLevel.TypeAndMessage,
        AllEnvironmentProfile.Production  => ExceptionDetailLevel.TypeAndMessage,
        _                                 => ExceptionDetailLevel.TypeAndMessage,
    };
```

### AllJsonOutput Enum

| Value | Description |
|---|---|
| `Stdout` | Standard output — recommended for containers (default) |
| `Stderr` | Standard error |
| `File` | File output — path specified in `FilePath` |

### ExceptionDetailLevel Enum

| Value | Includes |
|---|---|
| `Full` | Type, message, stack trace (method names only), inner exceptions (up to 5 levels) |
| `TypeAndMessage` | Type and message only (default for Production/Staging) |
| `TypeOnly` | Exception type name only — minimal disclosure |

---

## AllSeverityFilterOptions Reference

**Configuration section:** `All:Filter`

| Property | Type | Default | Description |
|---|---|---|---|
| `MinSeverity` | `LogLevel` | `Information` | Minimum severity for events to pass through. Events below this level are dropped |
| `EventNameOverrides` | `Dictionary<string, LogLevel>` | `{}` | Per-event-name severity overrides. Supports exact names and wildcard patterns with trailing `*` |

### Example: Fine-Grained Severity Control

```json
{
  "All": {
    "Filter": {
      "MinSeverity": "Warning"
    }
  }
}
```

```csharp
// Programmatic: per-event overrides
builder.AddAllSeverityFilter(
    configure: opts =>
    {
        opts.MinSeverity = LogLevel.Warning;
        // Allow debug-level health checks through
        opts.EventNameOverrides["health.check.*"] = LogLevel.Debug;
        // Only emit errors for a noisy event
        opts.EventNameOverrides["db.query.executed"] = LogLevel.Error;
    },
    innerProcessor: exportProcessor);
```

### Severity Mapping

| YAML Severity | .NET LogLevel | OTEL severityNumber |
|---|---|---|
| `TRACE` | `Trace` | 1–4 |
| `DEBUG` | `Debug` | 5–8 |
| `INFO` | `Information` | 9–12 |
| `WARN` | `Warning` | 13–16 |
| `ERROR` | `Error` | 17–20 |
| `FATAL` | `Critical` | 21–24 |

---

## AllRateLimitOptions Reference

**Configuration:** Programmatic only (`Action<AllRateLimitOptions>`)

| Property | Type | Default | Description |
|---|---|---|---|
| `DefaultMaxEventsPerWindow` | `int` | `0` | Default max events per window. `0` = unlimited |
| `EventLimits` | `Dictionary<string, int>` | `{}` | Per-event-name rate limits. Exact names take precedence over wildcard patterns. Values = max events per `Window`. `0` = unlimited for that event |
| `Window` | `TimeSpan` | `1 second` | Time window for rate calculation |

### Example

```csharp
builder.AddAllRateLimiter(
    configure: opts =>
    {
        opts.DefaultMaxEventsPerWindow = 1000;
        opts.Window = TimeSpan.FromSeconds(1);
        opts.EventLimits["db.query.*"] = 100;
        opts.EventLimits["noisy.event"] = 10;
    },
    innerProcessor: exportProcessor);
```

> **Note:** Exact event name matches take precedence over wildcard patterns. A bare `"*"` wildcard is not allowed — use `DefaultMaxEventsPerWindow` instead.

---

## AllSamplingOptions Reference

**Configuration:** Programmatic only (`Action<AllSamplingOptions>`)

| Property | Type | Default | Description |
|---|---|---|---|
| `Strategy` | `AllSamplingStrategy` | `Head` | `Head` (probability at arrival) or `Tail` (error-aware) |
| `DefaultSamplingRate` | `double` | `1.0` | Default probability (0.0–1.0). `1.0` = sample everything |
| `EventRates` | `Dictionary<string, double>` | `{}` | Per-event-name sampling rates. Exact matches take precedence over wildcards |
| `AlwaysSampleErrors` | `bool` | `true` | Always sample error-level events. Only applies to `Tail` strategy |
| `ErrorMinLevel` | `LogLevel` | `Error` | Minimum `LogLevel` qualifying as "error" for `AlwaysSampleErrors` |

### Sampling Strategies

| Strategy | Behavior |
|---|---|
| **Head** | Pure probability-based sampling at event arrival. Fast and predictable — each event independently sampled |
| **Tail** | Error-aware sampling. Errors always pass (when `AlwaysSampleErrors = true`), non-errors sampled at configured rate |

### Example: Tail Sampling with Per-Event Rates

```csharp
builder.AddAllSampler(
    configure: opts =>
    {
        opts.Strategy = AllSamplingStrategy.Tail;
        opts.DefaultSamplingRate = 0.1;          // Sample 10% by default
        opts.AlwaysSampleErrors = true;          // Always keep errors
        opts.ErrorMinLevel = LogLevel.Error;
        opts.EventRates["db.query.*"] = 0.01;    // 1% for DB queries
        opts.EventRates["health.check"] = 0.001; // 0.1% for health checks
    },
    innerProcessor: exportProcessor);
```

---

## Complete Configuration Example

### appsettings.json

```json
{
  "All": {
    "Exporter": {
      "Output": "Stdout",
      "SchemaVersion": "1.0.0",
      "EnvironmentProfile": "Production",
      "EmitHostInfo": false,
      "MaxAttributeValueLength": 4096,
      "AttributeDenylist": ["ConnectionString", "Password", "Token"]
    },
    "Filter": {
      "MinSeverity": "Information"
    }
  }
}
```

### Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .WithLogging(logging =>
    {
        // Causality processor
        logging.AddProcessor<AllCausalityProcessor>();

        // JSON exporter — bound from appsettings.json
        logging.AddAllJsonExporter(builder.Configuration);

        // Severity filter — bound from appsettings.json
        var exportProcessor = new SimpleLogRecordExportProcessor(
            new AllJsonExporter(new AllJsonExporterOptions()));
        logging.AddAllSeverityFilter(builder.Configuration, exportProcessor);

        // Rate limiting — programmatic only
        logging.AddAllRateLimiter(
            opts =>
            {
                opts.DefaultMaxEventsPerWindow = 1000;
                opts.EventLimits["db.query.*"] = 100;
            },
            exportProcessor);

        // Sampling — programmatic only
        logging.AddAllSampler(
            opts =>
            {
                opts.Strategy = AllSamplingStrategy.Tail;
                opts.DefaultSamplingRate = 0.5;
                opts.AlwaysSampleErrors = true;
            },
            exportProcessor);
    });
```

---

## Next Steps

- [Chapter 8 — Testing](08-testing.md) — validate your configuration with `All.Testing`
- [Chapter 10 — Security & Privacy](10-security-privacy.md) — deep dive into `EnvironmentProfile` redaction behavior
