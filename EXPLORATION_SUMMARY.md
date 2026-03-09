# OTEL Events .NET Repository - Comprehensive Exploration Summary

## 1. OVERALL PROJECT STRUCTURE

### Directory Layout
```
/Users/vbomfim/dev/otel-events-dotnet/
├── .editorconfig
├── .git
├── .github
├── .gitignore
├── CODE_PATTERNS.md
├── Directory.Build.props
├── Directory.Packages.props
├── IMPLEMENTATION_INDEX.md
├── LICENSE (MIT)
├── OTEL_EXPLORATION_GUIDE.md
├── QUICK_REFERENCE.md
├── README.md
├── SECURITY.md
├── SPECIFICATION.md (4968 lines - detailed architecture, schemas, requirements)
├── OtelEvents.slnx (Solution file)
│
├── src/
│   ├── OtelEvents.Analyzers/                # Roslyn analyzers for logging hygiene
│   ├── OtelEvents.Causality/                # Causal event linking processor
│   ├── OtelEvents.Exporter.Json/            # JSON log exporter for OTEL pipeline
│   ├── OtelEvents.Schema/                   # YAML schema parser & code generator
│   ├── OtelEvents.Testing/                  # Test utilities
│   ├── OtelEvents.AspNetCore/        # ASP.NET Core integration
│   └── OtelEvents.HealthChecks/      # Health checks integration
│
└── tests/
    ├── OtelEvents.Analyzers.Tests/
    ├── OtelEvents.Causality.Tests/
    ├── OtelEvents.Exporter.Json.Tests/
    ├── OtelEvents.Schema.Tests/
    ├── OtelEvents.Testing.Tests/
    ├── OtelEvents.AspNetCore.Tests/
    └── OtelEvents.HealthChecks.Tests/
```

### Solution File
**File:** `/Users/vbomfim/dev/otel-events-dotnet/OtelEvents.slnx`
- Uses modern slnx format (not legacy .sln)
- 8 source projects + 8 test projects (organized in /src and /tests folders)

### Build Configuration
**File:** `/Users/vbomfim/dev/otel-events-dotnet/Directory.Build.props`
```xml
<TargetFramework>net10.0</TargetFramework>
<ImplicitUsings>enable</ImplicitUsings>
<Nullable>enable</Nullable>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<LangVersion>latest</LangVersion>
<AnalysisLevel>latest-all</AnalysisLevel>
```

## 2. ALL.SCHEMA PROJECT OVERVIEW

### Project File
**Path:** `/Users/vbomfim/dev/otel-events-dotnet/src/OtelEvents.Schema/OtelEvents.Schema.csproj`
- **NoWarn:** CA1002, CA1032, CA1064, CA1305, CA1307, CA1720, CA1822, CA1062, CA1859
- **Dependencies:** YamlDotNet
- **Visibility:** InternalsVisibleTo = OtelEvents.Schema.Tests
- **EmbeddedResource:** `lifecycle.all.yaml` (built-in schema)

### Directory Structure
```
src/OtelEvents.Schema/
├── OtelEvents.Schema.csproj
├── BuiltInSchemas.cs              # Access to embedded lifecycle schema
├── bin/
├── obj/
├── CodeGen/
│   ├── CodeGenerator.cs           # Main code generation engine (625 lines)
│   ├── GeneratedFile.cs           # Record for generated file output
│   ├── NamingHelper.cs            # Naming convention utilities
│   └── TypeMapper.cs              # YAML-to-C# type mapping
├── Comparison/
│   ├── SchemaChange.cs            # Single change between versions
│   ├── SchemaChangeKind.cs        # Enum: EventAdded/Removed/FieldAdded/Removed/FieldTypeChanged
│   ├── SchemaComparer.cs          # Compares two schemas for breaking/non-breaking changes
│   └── SchemaComparisonResult.cs  # Result of comparison
├── Models/
│   ├── SchemaDocument.cs
│   ├── SchemaHeader.cs
│   ├── EventDefinition.cs
│   ├── FieldDefinition.cs
│   ├── MetricDefinition.cs
│   ├── EnumDefinition.cs
│   ├── FieldType.cs               # Enum: String, Int, Long, Double, Bool, DateTime, Duration, Guid, Enum, StringArray, IntArray, Map
│   ├── MetricType.cs              # Enum: Counter, Histogram, Gauge
│   ├── Severity.cs                # Enum: Trace, Debug, Info, Warn, Error, Fatal
│   ├── Sensitivity.cs             # Enum: Public, Internal, Pii, Credential
│   └── MeterLifecycle.cs           # Enum: Static, DI
├── Parsing/
│   ├── SchemaParser.cs            # YAML parser (542 lines)
│   ├── ParseResult.cs             # Parse result type
│   └── SchemaMerger.cs            # Merges multiple schemas + resolves imports
├── Schemas/
│   └── lifecycle.all.yaml         # Built-in lifecycle schema
└── Validation/
    ├── SchemaValidator.cs         # Validates against ALL_SCHEMA_001-026 rules (430 lines)
    ├── ValidationResult.cs        # Validation result type
    ├── ErrorCodes.cs              # Error code constants
    └── SchemaError.cs             # Error information
```

## 3. SPECIFICATION - SECTION §3.1 SCHEMA REGISTRY CLI

From SPECIFICATION.md line 143, 155:
```
| 3.1 | **Schema registry CLI** | `dotnet all validate`, `dotnet all generate`, `dotnet all diff` commands |
```

**Phase:** Phase 3 (Deferred)
**Commands planned:**
- `dotnet all validate` - Validate schema files
- `dotnet all generate` - Generate C# from schemas
- `dotnet all diff` - Compare schema versions for breaking changes

Currently not yet implemented in source, but infrastructure exists for it.

## 4. KEY IMPLEMENTATIONS

### 4.1 SchemaParser
**File:** `/Users/vbomfim/dev/otel-events-dotnet/src/OtelEvents.Schema/Parsing/SchemaParser.cs`
**Namespace:** `OtelEvents.Schema.Parsing`
**Class:** `public sealed class SchemaParser`

#### Public API
```csharp
// Main parsing method
public ParseResult Parse(string yamlContent, long fileSizeBytes)

// File-based parsing
public ParseResult ParseFile(string filePath)
```

#### Internal Constants
- `MaxFileSizeBytes = 1_048_576` (1 MB)
- `MaxNestingDepth = 20`

#### Key Features
- YamlDotNet-based YAML parsing (safe loading mode)
- Resource limit enforcement (file size, nesting depth)
- YAML bomb attack prevention (rejects anchors `&` and aliases `*`)
- Parses YAML into strongly-typed `SchemaDocument`

#### Parses These YAML Sections
```
schema:
  name, version, namespace, description, meterName, meterLifecycle

imports: [list of import paths]

fields: [shared field definitions]

enums: [enum type definitions]

events: [event definitions with id, severity, message, fields, metrics, tags]
```

#### Return Type
```csharp
public sealed class ParseResult
{
    public SchemaDocument? Document { get; }
    public IReadOnlyList<SchemaError> Errors { get; }
    public bool IsSuccess => Document is not null && Errors.Count == 0;
}
```

#### Exceptions
- `SchemaParseException` - Internal exception for parse errors (converted to SchemaError)
- `NestingDepthException` - For nesting depth violations

---

### 4.2 SchemaValidator
**File:** `/Users/vbomfim/dev/otel-events-dotnet/src/OtelEvents.Schema/Validation/SchemaValidator.cs`
**Namespace:** `OtelEvents.Schema.Validation`
**Class:** `public sealed partial class SchemaValidator` (uses regex source generators)

#### Public API
```csharp
// Single document validation
public ValidationResult Validate(SchemaDocument document)

// Multiple documents validation (checks cross-document constraints)
public ValidationResult Validate(IReadOnlyList<SchemaDocument> documents)
```

#### Internal Constants
- `MaxEventCount = 500` (total across merged schemas)
- `MaxFieldsPerEvent = 50`

#### Validation Rules (ALL_SCHEMA_001-026)
1. **ALL_SCHEMA_001** - No duplicate event names
2. **ALL_SCHEMA_002** - Valid severity (TRACE, DEBUG, INFO, WARN, ERROR, FATAL)
3. **ALL_SCHEMA_003** - Message template placeholders match fields
4. **ALL_SCHEMA_004** - Ref values resolve to defined fields/enums
5. **ALL_SCHEMA_005** - Valid field types
6. **ALL_SCHEMA_006** - Event names: lowercase, dot-namespaced (regex: `^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*)+$`)
7. **ALL_SCHEMA_007** - Required fields must have type or ref
8. **ALL_SCHEMA_008** - Valid metric types (counter, histogram, gauge)
9. **ALL_SCHEMA_009** - Non-empty enums
10. **ALL_SCHEMA_010** - Valid semver version
11. **ALL_SCHEMA_011** - Reserved prefix "all." not allowed
12. **ALL_SCHEMA_012** - Unique event IDs
13. **ALL_SCHEMA_013** - Valid meter name (dot-separated identifiers)
14. **ALL_SCHEMA_014** - Valid sensitivity (public, internal, pii, credential)
15. **ALL_SCHEMA_015** - Valid maxLength (positive integer)
16. **ALL_SCHEMA_016** - File size ≤ 1 MB
17. **ALL_SCHEMA_017** - Total events ≤ 500
18. **ALL_SCHEMA_018** - Fields per event ≤ 50
19. **ALL_SCHEMA_019** - No YAML aliases/anchors
20. **ALL_SCHEMA_020** - No duplicate enum values
21. **ALL_SCHEMA_021** - Valid namespace (dot-separated identifiers)
22. **ALL_SCHEMA_022** - Valid schema name (C# identifier)
23. **ALL_SCHEMA_023** - Valid enum values (C# identifiers)
24. **ALL_SCHEMA_024** - Import path traversal guard
25. **ALL_SCHEMA_025** - Schema version compatibility (same major version)
26. **ALL_SCHEMA_026** - Valid meterLifecycle (static, di)

#### Return Type
```csharp
public sealed class ValidationResult
{
    public IReadOnlyList<SchemaError> Errors { get; }
    public bool IsValid => Errors.Count == 0;
    
    public static ValidationResult Success() => new([]);
    public static ValidationResult Failure(IReadOnlyList<SchemaError> errors) => new(errors);
}
```

#### Regular Expressions (Source-Generated)
- `EventNameRegex` - `^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*)+$`
- `SemverRegex` - `^\d+\.\d+\.\d+(-[a-zA-Z0-9]+(\.[a-zA-Z0-9]+)*)?(\+[a-zA-Z0-9]+(\.[a-zA-Z0-9]+)*)?$`
- `MeterNameRegex` - `^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$`
- `NamespaceRegex` - `^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$`
- `CSharpIdentifierRegex` - `^[A-Za-z_][A-Za-z0-9_]*$`
- `PlaceholderRegex` - `\{(\w+)\}` (extracts message template placeholders)

---

### 4.3 CodeGenerator
**File:** `/Users/vbomfim/dev/otel-events-dotnet/src/OtelEvents.Schema/CodeGen/CodeGenerator.cs`
**Namespace:** `OtelEvents.Schema.CodeGen`
**Class:** `public sealed class CodeGenerator` (625 lines)

#### Public API
```csharp
// Main entry point: generates all C# source files from a schema
public IReadOnlyList<GeneratedFile> GenerateFromSchema(SchemaDocument doc)
```

#### Return Type
```csharp
public sealed record GeneratedFile(string FileName, string Content);
```

#### Code Generation Flow
1. **Enums** - One file per enum: `{EnumName}.g.cs`
   - Enum type with XML documentation
   - Extension class with `ToStringFast()` method for zero-allocation enum-to-string conversion
   
2. **Events** (if any) - Single file: `{SchemaName}Events.g.cs`
   - **Metadata class** (`{SchemaName}Metadata`) - schema version + name constants
   - **Logger extensions class** (`{SchemaName}LoggerExtensions`) - `[LoggerMessage]` partial methods
   - **Metrics class** (`{SchemaName}Metrics` or `{SchemaName}MetricsFactory`) - Meter instruments
     - Static mode: `private static readonly Meter s_meter`
     - DI mode: `IMeterFactory`-injected, `IDisposable` implementation
   - **Event extensions class** (`{SchemaName}EventExtensions`) - Emit methods (calls logger + records metrics)

3. **DI Registration** (if DI mode + has metrics) - `{SchemaName}ServiceCollectionExtensions.cs`

#### Generated Code Features
- Auto-generated header comment
- `#nullable enable`
- Proper using statements
- XML documentation from schema descriptions
- Full type safety (no `object` casts)
- `[LoggerMessage]` attribute with EventId, LogLevel, Message template
- Extension methods on `ILogger` for fluent API

#### Helper Classes
```csharp
// NamingHelper - Naming convention conversions
public static class NamingHelper
{
    public static string ToPascalCase(string name)           // "order.placed" → "OrderPlaced"
    public static string ToCamelCase(string name)            // "OrderId" → "orderId"
    public static string ToMethodName(string eventName)      // "order.placed" → "OrderPlaced"
    public static string ToMetricFieldName(string name)      // "order.count" → "s_orderCount"
    public static string ToMetricPropertyName(string name)   // "order.count" → "OrderCount"
    public static string GetLastSegment(string dottedName)   // "a.b.c" → "c"
    public static string SanitizeIdentifier(string name)     // Ensures valid C# identifier
}

// TypeMapper - YAML-to-C# type mappings
public static class TypeMapper
{
    public static string ToCSharpType(FieldType fieldType)   // FieldType → C# type name
    public static string GetFieldCSharpType(FieldDefinition) // Handles enums + refs
    public static string ToLogLevel(Severity severity)       // Severity → LogLevel enum
    public static string GetMetricClrType(MetricType)        // Counter→long, Histogram→double
    public static string GetInstrumentCreationMethod(MetricType) // "CreateCounter", etc.
    public static bool IsNumericType(FieldType)              // Can be used in Histogram
}
```

---

### 4.4 SchemaComparer
**File:** `/Users/vbomfim/dev/otel-events-dotnet/src/OtelEvents.Schema/Comparison/SchemaComparer.cs`
**Namespace:** `OtelEvents.Schema.Comparison`
**Class:** `public sealed class SchemaComparer`

#### Public API
```csharp
// Compare old schema to new schema
public SchemaComparisonResult Compare(SchemaDocument oldSchema, SchemaDocument newSchema)
```

#### Change Detection
- **Events Removed** → Breaking change
- **Events Added** → Non-breaking
- **Fields Removed** → Breaking change
- **Fields Added** → Non-breaking
- **Field Type Changed** → Breaking change

#### Return Type
```csharp
public sealed class SchemaChange
{
    public required SchemaChangeKind Kind { get; init; }        // EventAdded, EventRemoved, etc.
    public required string Name { get; init; }                  // Event name or "eventName.fieldName"
    public required string Description { get; init; }           // Human-readable description
    public bool IsBreaking { get; }                             // True if breaking change
    public override string ToString()
}

public enum SchemaChangeKind
{
    EventAdded,
    EventRemoved,
    FieldAdded,
    FieldRemoved,
    FieldTypeChanged
}

public sealed class SchemaComparisonResult
{
    public IReadOnlyList<SchemaChange> Changes { get; }
    public bool HasBreakingChanges { get; }
    public int BreakingChangeCount { get; }
}
```

---

### 4.5 SchemaMerger (Supporting Class)
**File:** `/Users/vbomfim/dev/otel-events-dotnet/src/OtelEvents.Schema/Parsing/SchemaMerger.cs`
**Namespace:** `OtelEvents.Schema.Parsing`
**Class:** `public sealed class SchemaMerger`

#### Public API
```csharp
public SchemaMerger(SchemaParser parser, SchemaValidator validator)

// Merge already-parsed documents
public MergeResult Merge(IReadOnlyList<SchemaDocument> documents)

// Parse and merge files from disk, resolving imports
public MergeResult MergeFromFile(string primaryFilePath)
```

#### Features
- Resolves imports relative to current file directory
- Prevents circular imports (uses `HashSet<string>` for visited tracking)
- Path traversal guard: imports must stay within schema directory (security check)
- Validates merged result using SchemaValidator
- Produces unified merged document

#### Return Type
```csharp
public sealed class MergeResult
{
    public SchemaDocument? Document { get; }
    public IReadOnlyList<SchemaError> Errors { get; }
    public bool IsSuccess => Document is not null && Errors.Count == 0;
}
```

---

## 5. DATA MODELS

### SchemaDocument
```csharp
namespace OtelEvents.Schema.Models;

public sealed class SchemaDocument
{
    public required SchemaHeader Schema { get; init; }
    public List<string> Imports { get; init; } = [];
    public List<FieldDefinition> Fields { get; init; } = [];
    public List<EnumDefinition> Enums { get; init; } = [];
    public List<EventDefinition> Events { get; init; } = [];
}
```

### SchemaHeader
```csharp
public sealed class SchemaHeader
{
    public required string Name { get; init; }                      // Schema name
    public required string Version { get; init; }                   // Semver
    public required string Namespace { get; init; }                 // C# namespace
    public string? Description { get; init; }
    public string? MeterName { get; init; }                         // OTEL meter name
    public MeterLifecycle MeterLifecycle { get; init; } = MeterLifecycle.Static;
}
```

### EventDefinition
```csharp
public sealed class EventDefinition
{
    public required string Name { get; init; }                      // "http.request.received"
    public required int Id { get; init; }                           // Unique numeric ID
    public required Severity Severity { get; init; }                // Log level
    public string? Description { get; init; }
    public required string Message { get; init; }                   // Template: "Request {method} {path}"
    public bool Exception { get; init; }                            // Can accept exception?
    public List<FieldDefinition> Fields { get; init; } = [];
    public List<MetricDefinition> Metrics { get; init; } = [];
    public List<string> Tags { get; init; } = [];
}
```

### FieldDefinition
```csharp
public sealed class FieldDefinition
{
    public required string Name { get; init; }
    public FieldType? Type { get; init; }                           // Nullable (can use ref)
    public string? Description { get; init; }
    public bool Required { get; init; }
    public Sensitivity Sensitivity { get; init; } = Sensitivity.Public;
    public int? MaxLength { get; init; }                            // For strings
    public bool Index { get; init; }                                // Queryable?
    public string? Ref { get; init; }                               // Reference to shared field/enum
    public string? Unit { get; init; }                              // "ms", "bytes", etc.
    public List<string>? Examples { get; init; }
    public List<string>? Values { get; init; }                      // Inline enum values
    
    // Raw values for validation error reporting
    internal string? RawType { get; init; }
    internal string? RawSensitivity { get; init; }
    internal string? RawMaxLength { get; init; }
}
```

### MetricDefinition
```csharp
public sealed class MetricDefinition
{
    public required string Name { get; init; }
    public MetricType Type { get; init; }
    public string? Unit { get; init; }
    public string? Description { get; init; }
    public List<double>? Buckets { get; init; }                     // For histogram
    public List<string>? Labels { get; init; }                      // Tag labels
    
    // Raw type for validation error reporting
    internal string? RawType { get; init; }
}
```

### EnumDefinition
```csharp
public sealed class EnumDefinition
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public List<string> Values { get; init; } = [];                 // Enum values
}
```

### Enums
```csharp
// FieldType
public enum FieldType
{
    String, Int, Long, Double, Bool, DateTime, Duration, Guid,
    Enum, StringArray, IntArray, Map
}

// MetricType
public enum MetricType { Counter, Histogram, Gauge }

// Severity
public enum Severity { Trace, Debug, Info, Warn, Error, Fatal }

// Sensitivity
public enum Sensitivity { Public, Internal, Pii, Credential }

// MeterLifecycle
public enum MeterLifecycle { Static, DI }
```

---

## 6. ERROR CODES & ERROR REPORTING

### SchemaError
```csharp
public sealed class SchemaError
{
    public string Code { get; set; }
    public string Message { get; set; }
}
```

### ErrorCodes Class
**File:** `/Users/vbomfim/dev/otel-events-dotnet/src/OtelEvents.Schema/Validation/ErrorCodes.cs`

All 26 error codes as `public const string`:
- DuplicateEventName = "ALL_SCHEMA_001"
- InvalidSeverity = "ALL_SCHEMA_002"
- ... (through ALL_SCHEMA_026)

---

## 7. SCHEMA FILES

### Built-in Lifecycle Schema
**File:** `/Users/vbomfim/dev/otel-events-dotnet/src/OtelEvents.Schema/Schemas/lifecycle.all.yaml`
- Namespace: `OtelEvents.Events.Lifecycle`
- Version: `1.0.0`
- Meter: `all.lifecycle`
- Enums: `HealthStatus`, `LifecyclePhase`
- Events: `app.lifecycle.changed`, `app.health.changed`, `app.startup.completed`, `app.shutdown.initiated`

### Other Schema Files
- `/Users/vbomfim/dev/otel-events-dotnet/src/OtelEvents.AspNetCore/Schemas/aspnetcore.all.yaml` - ASP.NET Core events

---

## 8. TEST FRAMEWORK & PATTERNS

### Test Framework
**Framework:** xUnit
**File:** `/Users/vbomfim/dev/otel-events-dotnet/tests/OtelEvents.Schema.Tests/OtelEvents.Schema.Tests.csproj`

**Dependencies:**
```xml
<PackageReference Include="coverlet.collector" />
<PackageReference Include="Microsoft.CodeAnalysis.CSharp" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
<PackageReference Include="Microsoft.NET.Test.Sdk" />
<PackageReference Include="xunit" />
<PackageReference Include="xunit.runner.visualstudio" />
```

**Global Usings:**
```xml
<Using Include="Xunit" />
```

### Test Classes

#### SchemaParserTests
**File:** `/Users/vbomfim/dev/otel-events-dotnet/tests/OtelEvents.Schema.Tests/SchemaParserTests.cs`
- Tests for YAML schema parsing validation
- Uses `[Fact]` attributes for test methods
- Helper string constants with triple-quoted YAML (multiline strings)
- Pattern: Parse → Assert (IsSuccess/Document properties)

#### SchemaValidatorTests
**File:** `/Users/vbomfim/dev/otel-events-dotnet/tests/OtelEvents.Schema.Tests/SchemaValidatorTests.cs`
- Tests for all 26 validation rules (ALL_SCHEMA_001-026)
- Each rule has dedicated test(s)
- Pattern: Create schema with violation → Validate → Assert error code present
- Helper methods: `CreateMinimalDoc()`, `CreateEvent()`, `CreateField()`
- Uses `Assert.False(result.IsValid)` and `Assert.Contains(result.Errors, e => e.Code == ErrorCodes.XXX)`

#### CodeGeneratorTests
**File:** `/Users/vbomfim/dev/otel-events-dotnet/tests/OtelEvents.Schema.Tests/CodeGeneratorTests.cs`
- Large test class (~80+ test methods)
- Tests code generation for: enums, events, metrics, extensions, DI registration
- Pattern: Create schema → Generate → Assert content includes expected C# constructs
- Uses `Assert.Contains(fileContent, "expected text")` for code verification
- Helper methods: `CreateMinimalSchema()`, `CreateSchemaWithEvent()`, `CreateEvent()`, etc.
- **Test categories:**
  1. Basic structure (empty schemas, file generation)
  2. LoggerMessage generation (attributes, partial methods, signatures)
  3. Metrics (static/DI modes, counters, histograms)
  4. Emit methods (signatures, metric recording, tag lists)
  5. Enums (type generation, ToStringFast, descriptions)
  6. DI registration (service collection extensions)
  7. Security & metadata tests

#### SchemaComparerTests
**File:** `/Users/vbomfim/dev/otel-events-dotnet/tests/OtelEvents.Schema.Tests/SchemaComparerTests.cs`
- Tests schema comparison/diffing logic
- Pattern: Compare old → new schema → Assert changes and breaking status
- Helper methods: `CreateSchema()`, `CreateEvent()`, `CreateField()`
- Validates: identical schemas, event changes, field changes, breaking classification

#### Additional Test Files
- `SchemaEdgeCaseTests.cs` - Edge cases, security tests (YAML bombs, etc.)
- `SchemaMergerTests.cs` - Schema merging logic, import resolution
- `NamingHelperTests.cs` - Naming convention conversions
- `TypeMapperTests.cs` - Type mappings
- `LifecycleSchemaTests.cs` - Built-in lifecycle schema tests
- `CodeGeneratorSecurityTests.cs` - Code generation security constraints
- `CodeGeneratorMetadataTests.cs` - Metadata class generation

### Assertion Patterns
```csharp
Assert.True(result.IsSuccess)
Assert.False(result.IsValid)
Assert.NotNull(result.Document)
Assert.Empty(files)
Assert.Single(result.Changes)
Assert.Contains(files, f => f.FileName.Contains("Events"))
Assert.Contains(fileContent, "<auto-generated>")
Assert.Equal("expected", actual)
Assert.Contains(result.Errors, e => e.Code == ErrorCodes.SomeCode)
```

### Naming Conventions
- Test method names: `MethodUnderTest_Condition_ExpectedOutcome`
  - E.g., `Parse_ValidMinimalSchema_ReturnsSuccess`
  - E.g., `Compare_EventAdded_ReportsNonBreakingChange`
- Helper test classes: `CreateMinimalSchema()`, `CreateEvent()`, `CreateField()`
- Region separators: `// ═════════════════════════════════════` and `// ── Description ──────────────────`

---

## 9. PROJECT-LEVEL PATTERNS

### SDK & Versions
- **.NET Target:** net10.0 (modern .NET, no legacy support)
- **C# Version:** latest
- **Analysis Level:** latest-all
- **Nullable:** enabled globally
- **Implicit Usings:** enabled globally
- **Warnings:** All treated as errors (`TreatWarningsAsErrors`)

### Dependencies (OtelEvents.Schema)
- **YamlDotNet** - YAML parsing

### Dependencies (OtelEvents.Schema.Tests)
- xUnit
- xunit.runner.visualstudio
- coverlet.collector (code coverage)
- Microsoft.CodeAnalysis.CSharp (for code generation tests)
- Microsoft.Extensions.Logging.Abstractions

### Code Generation
- No external code generators in OtelEvents.Schema itself
- Uses YamlDotNet for parsing
- CodeGenerator produces C# source text (not invoking external generators)
- Regex source generators (`[GeneratedRegex]`) in SchemaValidator

### Embedded Resources
- `lifecycle.all.yaml` embedded in OtelEvents.Schema.csproj
- Accessible via `BuiltInSchemas.LoadLifecycleSchema()` or `.GetLifecycleSchemaYaml()`

---

## 10. SPECIFICATION DETAILS - CLI SECTION

### §3.1 Schema Registry CLI (Phase 3, Deferred)

From SPECIFICATION.md section on deferred features:
```
| Schema registry CLI | Phase 3 | `dotnet all validate`, `dotnet all diff` |
```

**Planned commands:**
1. **`dotnet all validate [schema-file]`** - Validate schema file(s)
2. **`dotnet all generate [schema-file] [--output-dir]`** - Generate C# code from schema
3. **`dotnet all diff [old-schema] [new-schema]`** - Compare schemas for breaking changes

**Notes from spec:**
- Currently infrastructure supports the core operations (Parse, Validate, Compare, Generate)
- CLI project/tool not yet implemented in Phase 1/2
- Awaiting Phase 3 implementation
- Reference in spec: line 1850 - "Schema registry CLI | Phase 3"

### File structure from SPECIFICATION (Appendix C)
```
OtelEvents.Schema/
├── Cli/
│   └── ValidateCommand.cs            # dotnet all validate (Phase 3)
```
(Deferred, not yet implemented)

---

## 11. KEY DESIGN PATTERNS

### Result Pattern
- `ParseResult` (Document + Errors)
- `ValidationResult` (Errors only, IsValid property)
- `SchemaComparisonResult` (Changes list)
- `MergeResult` (Document + Errors)
- All use IsSuccess/IsValid to check status
- Errors never null (always IReadOnlyList)

### Sealed Classes
- All models use `sealed` (SchemaDocument, EventDefinition, etc.)
- All utility classes use `sealed` (SchemaParser, CodeGenerator, etc.)
- Prevents unintended subclassing

### Namespace Organization
- `OtelEvents.Schema.Models` - Data model classes
- `OtelEvents.Schema.Parsing` - Parser + merger
- `OtelEvents.Schema.Validation` - Validator + error codes
- `OtelEvents.Schema.Comparison` - Comparer + change types
- `OtelEvents.Schema.CodeGen` - Code generation + helpers

### Extension Methods for Type Parsing
```csharp
public static class FieldTypeExtensions
{
    public static bool TryParseFieldType(string yamlType, out FieldType fieldType)
}
// Similar for Severity, Sensitivity, MetricType, MeterLifecycle
```

### Init-only Properties
- All models use `required` + `init` properties
- Enforces immutability post-construction
- Example: `public required string Name { get; init; }`

### Partial Classes for Source Generators
- SchemaValidator is `partial` (uses `[GeneratedRegex]`)

### Internal Access Control
- `ParseResult.Success()` and `.Failure()` are `internal`
- Only Parsing namespace can create results
- Public API only exposes read-only properties

---

## 12. CURRENT STATE & GAPS

### Implemented
✅ SchemaParser - Full YAML parsing with security constraints
✅ SchemaValidator - All 26 validation rules
✅ CodeGenerator - C# source generation for events, metrics, enums
✅ SchemaComparer - Breaking/non-breaking change detection
✅ SchemaMerger - Multi-file schema merging + import resolution
✅ Data models - All schema structure types
✅ Built-in Lifecycle schema
✅ Comprehensive test coverage (xUnit)

### Not Implemented (Phase 3+)
❌ CLI tool (`dotnet all validate`, `generate`, `diff`)
❌ Schema documentation generator
❌ Schema file signing (`dotnet all sign`)
❌ VS Code extension
❌ Event sampling processor
❌ Advanced gRPC/Cosmos integration packs (some basic packs exist)

---

## 13. QUICK REFERENCE

### Using SchemaParser
```csharp
var parser = new SchemaParser();
var result = parser.Parse(yamlContent, yamlContent.Length);
if (result.IsSuccess)
{
    var doc = result.Document;
    // Use doc
}
else
{
    foreach (var error in result.Errors)
        Console.WriteLine($"{error.Code}: {error.Message}");
}
```

### Using SchemaValidator
```csharp
var validator = new SchemaValidator();
var result = validator.Validate(document);
if (!result.IsValid)
{
    foreach (var error in result.Errors)
        Console.WriteLine($"{error.Code}: {error.Message}");
}
```

### Using CodeGenerator
```csharp
var gen = new CodeGenerator();
var files = gen.GenerateFromSchema(document);
foreach (var file in files)
{
    File.WriteAllText(file.FileName, file.Content);
}
```

### Using SchemaComparer
```csharp
var comparer = new SchemaComparer();
var result = comparer.Compare(oldSchema, newSchema);
Console.WriteLine($"Breaking changes: {result.BreakingChangeCount}");
foreach (var change in result.Changes.Where(c => c.IsBreaking))
    Console.WriteLine(change.Description);
```

### Built-in Lifecycle Schema
```csharp
var result = BuiltInSchemas.LoadLifecycleSchema();
var yaml = BuiltInSchemas.GetLifecycleSchemaYaml();
```

---

## 14. FILE PATHS SUMMARY

**Core Implementation:**
- `/Users/vbomfim/dev/otel-events-dotnet/src/OtelEvents.Schema/Parsing/SchemaParser.cs`
- `/Users/vbomfim/dev/otel-events-dotnet/src/OtelEvents.Schema/Validation/SchemaValidator.cs`
- `/Users/vbomfim/dev/otel-events-dotnet/src/OtelEvents.Schema/CodeGen/CodeGenerator.cs`
- `/Users/vbomfim/dev/otel-events-dotnet/src/OtelEvents.Schema/Comparison/SchemaComparer.cs`
- `/Users/vbomfim/dev/otel-events-dotnet/src/OtelEvents.Schema/Parsing/SchemaMerger.cs`

**Models:**
- `/Users/vbomfim/dev/otel-events-dotnet/src/OtelEvents.Schema/Models/*.cs`

**Schemas:**
- `/Users/vbomfim/dev/otel-events-dotnet/src/OtelEvents.Schema/Schemas/lifecycle.all.yaml`

**Tests:**
- `/Users/vbomfim/dev/otel-events-dotnet/tests/OtelEvents.Schema.Tests/*.cs`

**Documentation:**
- `/Users/vbomfim/dev/otel-events-dotnet/SPECIFICATION.md`
- `/Users/vbomfim/dev/otel-events-dotnet/README.md`

