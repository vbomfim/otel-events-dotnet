using OtelEvents.Schema.CodeGen;
using OtelEvents.Schema.Models;
using OtelEvents.Schema.Parsing;
using OtelEvents.Schema.Validation;
using Microsoft.CodeAnalysis.CSharp;

namespace OtelEvents.Schema.Tests;

/// <summary>
/// Tests for the built-in lifecycle schema (Feature 2.4).
/// Validates parsing, validation, enum generation, LoggerMessage methods,
/// metrics, and Roslyn-verified C# output from the lifecycle.all.yaml schema.
/// </summary>
public class LifecycleSchemaTests
{
    private readonly SchemaParser _parser = new();
    private readonly SchemaValidator _validator = new();
    private readonly CodeGenerator _generator = new();

    // ═══════════════════════════════════════════════════════════════
    // YAML CONTENT — matches src/OtelEvents.Schema/Schemas/lifecycle.all.yaml
    // ═══════════════════════════════════════════════════════════════

    private const string LifecycleYaml = """
        schema:
          name: Lifecycle
          version: "1.0.0"
          namespace: OtelEvents.Events.Lifecycle
          description: "Built-in schema for application lifecycle and health events"
          meterName: otel_events.lifecycle

        enums:
          HealthStatus:
            description: "Application health state"
            values:
              - Healthy
              - Degraded
              - Unhealthy

          LifecyclePhase:
            description: "Application lifecycle phase"
            values:
              - Starting
              - Started
              - Stopping
              - Stopped

        events:
          app.lifecycle.changed:
            id: 9001
            severity: INFO
            description: "Application lifecycle phase transition"
            message: "Application lifecycle changed to {phase}"
            fields:
              phase:
                type: enum
                ref: LifecyclePhase
                required: true
              reason:
                type: string
            metrics:
              app.lifecycle.transitions:
                type: counter

          app.health.changed:
            id: 9002
            severity: WARN
            description: "Application health status changed"
            message: "Application health changed from {previousStatus} to {currentStatus}: {reason}"
            fields:
              previousStatus:
                type: enum
                ref: HealthStatus
                required: true
              currentStatus:
                type: enum
                ref: HealthStatus
                required: true
              reason:
                type: string
                required: true
            metrics:
              app.health.changes:
                type: counter
                labels:
                  - currentStatus

          app.startup.completed:
            id: 9003
            severity: INFO
            description: "Application startup completed successfully"
            message: "Application startup completed in {durationMs}ms"
            fields:
              durationMs:
                type: long
                required: true
            metrics:
              app.startup.durationMs:
                type: histogram

          app.shutdown.initiated:
            id: 9004
            severity: INFO
            description: "Application shutdown has been initiated"
            message: "Application shutdown initiated: {reason}"
            fields:
              reason:
                type: string
                required: true
        """;

    // ═══════════════════════════════════════════════════════════════
    // 1. SCHEMA PARSING
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Parse_LifecycleSchema_ReturnsSuccess()
    {
        var result = _parser.Parse(LifecycleYaml, LifecycleYaml.Length);

        Assert.True(result.IsSuccess, $"Parse failed: {string.Join(", ", result.Errors)}");
        Assert.NotNull(result.Document);
    }

    [Fact]
    public void Parse_LifecycleSchema_HasCorrectHeader()
    {
        var result = _parser.Parse(LifecycleYaml, LifecycleYaml.Length);

        var header = result.Document!.Schema;
        Assert.Equal("Lifecycle", header.Name);
        Assert.Equal("1.0.0", header.Version);
        Assert.Equal("OtelEvents.Events.Lifecycle", header.Namespace);
        Assert.Equal("otel_events.lifecycle", header.MeterName);
        Assert.Equal("Built-in schema for application lifecycle and health events", header.Description);
    }

    [Fact]
    public void Parse_LifecycleSchema_HasTwoEnums()
    {
        var result = _parser.Parse(LifecycleYaml, LifecycleYaml.Length);

        Assert.Equal(2, result.Document!.Enums.Count);
    }

    [Fact]
    public void Parse_LifecycleSchema_HealthStatusEnum_HasThreeValues()
    {
        var result = _parser.Parse(LifecycleYaml, LifecycleYaml.Length);

        var healthEnum = result.Document!.Enums.First(e => e.Name == "HealthStatus");
        Assert.Equal(3, healthEnum.Values.Count);
        Assert.Contains("Healthy", healthEnum.Values);
        Assert.Contains("Degraded", healthEnum.Values);
        Assert.Contains("Unhealthy", healthEnum.Values);
        Assert.Equal("Application health state", healthEnum.Description);
    }

    [Fact]
    public void Parse_LifecycleSchema_LifecyclePhaseEnum_HasFourValues()
    {
        var result = _parser.Parse(LifecycleYaml, LifecycleYaml.Length);

        var phaseEnum = result.Document!.Enums.First(e => e.Name == "LifecyclePhase");
        Assert.Equal(4, phaseEnum.Values.Count);
        Assert.Contains("Starting", phaseEnum.Values);
        Assert.Contains("Started", phaseEnum.Values);
        Assert.Contains("Stopping", phaseEnum.Values);
        Assert.Contains("Stopped", phaseEnum.Values);
        Assert.Equal("Application lifecycle phase", phaseEnum.Description);
    }

    [Fact]
    public void Parse_LifecycleSchema_HasFourEvents()
    {
        var result = _parser.Parse(LifecycleYaml, LifecycleYaml.Length);

        Assert.Equal(4, result.Document!.Events.Count);
    }

    [Fact]
    public void Parse_LifecycleSchema_LifecycleChangedEvent_HasCorrectFields()
    {
        var result = _parser.Parse(LifecycleYaml, LifecycleYaml.Length);

        var evt = result.Document!.Events.First(e => e.Name == "app.lifecycle.changed");
        Assert.Equal(9001, evt.Id);
        Assert.Equal(Severity.Info, evt.Severity);
        Assert.Equal("Application lifecycle changed to {phase}", evt.Message);
        Assert.Equal(2, evt.Fields.Count);

        var phaseField = evt.Fields.First(f => f.Name == "phase");
        Assert.Equal(FieldType.Enum, phaseField.Type);
        Assert.Equal("LifecyclePhase", phaseField.Ref);
        Assert.True(phaseField.Required);

        var reasonField = evt.Fields.First(f => f.Name == "reason");
        Assert.Equal(FieldType.String, reasonField.Type);
        Assert.False(reasonField.Required);
    }

    [Fact]
    public void Parse_LifecycleSchema_HealthChangedEvent_HasCorrectFields()
    {
        var result = _parser.Parse(LifecycleYaml, LifecycleYaml.Length);

        var evt = result.Document!.Events.First(e => e.Name == "app.health.changed");
        Assert.Equal(9002, evt.Id);
        Assert.Equal(Severity.Warn, evt.Severity);
        Assert.Equal(3, evt.Fields.Count);

        var prevField = evt.Fields.First(f => f.Name == "previousStatus");
        Assert.Equal(FieldType.Enum, prevField.Type);
        Assert.Equal("HealthStatus", prevField.Ref);
        Assert.True(prevField.Required);

        var currField = evt.Fields.First(f => f.Name == "currentStatus");
        Assert.Equal(FieldType.Enum, currField.Type);
        Assert.Equal("HealthStatus", currField.Ref);
        Assert.True(currField.Required);
    }

    [Fact]
    public void Parse_LifecycleSchema_StartupCompletedEvent_HasDurationField()
    {
        var result = _parser.Parse(LifecycleYaml, LifecycleYaml.Length);

        var evt = result.Document!.Events.First(e => e.Name == "app.startup.completed");
        Assert.Equal(9003, evt.Id);
        Assert.Equal(Severity.Info, evt.Severity);
        Assert.Single(evt.Fields);

        var durationField = evt.Fields[0];
        Assert.Equal("durationMs", durationField.Name);
        Assert.Equal(FieldType.Long, durationField.Type);
        Assert.True(durationField.Required);
    }

    [Fact]
    public void Parse_LifecycleSchema_ShutdownInitiatedEvent_HasReasonField()
    {
        var result = _parser.Parse(LifecycleYaml, LifecycleYaml.Length);

        var evt = result.Document!.Events.First(e => e.Name == "app.shutdown.initiated");
        Assert.Equal(9004, evt.Id);
        Assert.Equal(Severity.Info, evt.Severity);
        Assert.Single(evt.Fields);

        var reasonField = evt.Fields[0];
        Assert.Equal("reason", reasonField.Name);
        Assert.Equal(FieldType.String, reasonField.Type);
        Assert.True(reasonField.Required);
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. SCHEMA VALIDATION
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Validate_LifecycleSchema_IsValid()
    {
        var parseResult = _parser.Parse(LifecycleYaml, LifecycleYaml.Length);
        Assert.True(parseResult.IsSuccess);

        var validationResult = _validator.Validate(parseResult.Document!);

        Assert.True(validationResult.IsValid,
            $"Validation failed: {string.Join(", ", validationResult.Errors)}");
    }

    [Fact]
    public void Validate_LifecycleSchema_AllEventNamesAreUnique()
    {
        var parseResult = _parser.Parse(LifecycleYaml, LifecycleYaml.Length);
        var validationResult = _validator.Validate(parseResult.Document!);

        Assert.DoesNotContain(validationResult.Errors,
            e => e.Code == ErrorCodes.DuplicateEventName);
    }

    [Fact]
    public void Validate_LifecycleSchema_AllEventIdsAreUnique()
    {
        var parseResult = _parser.Parse(LifecycleYaml, LifecycleYaml.Length);
        var validationResult = _validator.Validate(parseResult.Document!);

        Assert.DoesNotContain(validationResult.Errors,
            e => e.Code == ErrorCodes.DuplicateEventId);
    }

    [Fact]
    public void Validate_LifecycleSchema_AllRefsResolve()
    {
        var parseResult = _parser.Parse(LifecycleYaml, LifecycleYaml.Length);
        var validationResult = _validator.Validate(parseResult.Document!);

        Assert.DoesNotContain(validationResult.Errors,
            e => e.Code == ErrorCodes.UnresolvedRef);
    }

    [Fact]
    public void Validate_LifecycleSchema_MessageTemplatesMatch()
    {
        var parseResult = _parser.Parse(LifecycleYaml, LifecycleYaml.Length);
        var validationResult = _validator.Validate(parseResult.Document!);

        Assert.DoesNotContain(validationResult.Errors,
            e => e.Code == ErrorCodes.MessageTemplateMismatch);
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. CODE GENERATION — LOGGER MESSAGE METHODS
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Generate_LifecycleSchema_ProducesEventsFile()
    {
        var doc = ParseAndValidateLifecycleSchema();
        var files = _generator.GenerateFromSchema(doc);

        Assert.Contains(files, f => f.FileName == "LifecycleEvents.g.cs");
    }

    [Fact]
    public void Generate_LifecycleSchema_HasLoggerMessageForAllFourEvents()
    {
        var doc = ParseAndValidateLifecycleSchema();
        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        // All four LoggerMessage methods
        Assert.Contains("public static partial void AppLifecycleChanged(", content);
        Assert.Contains("public static partial void AppHealthChanged(", content);
        Assert.Contains("public static partial void AppStartupCompleted(", content);
        Assert.Contains("public static partial void AppShutdownInitiated(", content);
    }

    [Fact]
    public void Generate_LifecycleSchema_HasCorrectEventIds()
    {
        var doc = ParseAndValidateLifecycleSchema();
        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        Assert.Contains("EventId = 9001", content);
        Assert.Contains("EventId = 9002", content);
        Assert.Contains("EventId = 9003", content);
        Assert.Contains("EventId = 9004", content);
    }

    [Fact]
    public void Generate_LifecycleSchema_HasCorrectLogLevels()
    {
        var doc = ParseAndValidateLifecycleSchema();
        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        // 9001 (INFO), 9003 (INFO), 9004 (INFO) — three INFO events
        // 9002 (WARN) — one WARNING event
        Assert.Contains("Level = LogLevel.Warning", content);

        // Count the LogLevel.Information occurrences (should be at least 3)
        var infoCount = content.Split("Level = LogLevel.Information").Length - 1;
        Assert.True(infoCount >= 3, $"Expected at least 3 LogLevel.Information but found {infoCount}");
    }

    [Fact]
    public void Generate_LifecycleSchema_HasCorrectNamespace()
    {
        var doc = ParseAndValidateLifecycleSchema();
        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        Assert.Contains("namespace OtelEvents.Events.Lifecycle;", content);
    }

    [Fact]
    public void Generate_LifecycleSchema_HasLoggerExtensionsClass()
    {
        var doc = ParseAndValidateLifecycleSchema();
        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        Assert.Contains("public static partial class LifecycleLoggerExtensions", content);
    }

    [Fact]
    public void Generate_LifecycleSchema_HealthChanged_HasEnumParameters()
    {
        var doc = ParseAndValidateLifecycleSchema();
        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        Assert.Contains("HealthStatus previousStatus", content);
        Assert.Contains("HealthStatus currentStatus", content);
    }

    [Fact]
    public void Generate_LifecycleSchema_LifecycleChanged_HasEnumParameter()
    {
        var doc = ParseAndValidateLifecycleSchema();
        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        Assert.Contains("LifecyclePhase phase", content);
    }

    [Fact]
    public void Generate_LifecycleSchema_StartupCompleted_HasLongDuration()
    {
        var doc = ParseAndValidateLifecycleSchema();
        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        Assert.Contains("long durationMs", content);
    }

    [Fact]
    public void Generate_LifecycleSchema_ShutdownInitiated_HasReasonParameter()
    {
        var doc = ParseAndValidateLifecycleSchema();
        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        Assert.Contains("string reason", content);
    }

    [Fact]
    public void Generate_LifecycleSchema_LifecycleChanged_OptionalReasonIsNullable()
    {
        var doc = ParseAndValidateLifecycleSchema();
        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        // The "reason" field in app.lifecycle.changed is optional → nullable
        Assert.Contains("string? reason", content);
    }

    // ═══════════════════════════════════════════════════════════════
    // 4. CODE GENERATION — ENUMS
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Generate_LifecycleSchema_ProducesHealthStatusEnumFile()
    {
        var doc = ParseAndValidateLifecycleSchema();
        var files = _generator.GenerateFromSchema(doc);

        var enumFile = files.First(f => f.FileName == "HealthStatus.g.cs");
        Assert.Contains("public enum HealthStatus", enumFile.Content);
        Assert.Contains("Healthy,", enumFile.Content);
        Assert.Contains("Degraded,", enumFile.Content);
        Assert.Contains("Unhealthy,", enumFile.Content);
    }

    [Fact]
    public void Generate_LifecycleSchema_ProducesLifecyclePhaseEnumFile()
    {
        var doc = ParseAndValidateLifecycleSchema();
        var files = _generator.GenerateFromSchema(doc);

        var enumFile = files.First(f => f.FileName == "LifecyclePhase.g.cs");
        Assert.Contains("public enum LifecyclePhase", enumFile.Content);
        Assert.Contains("Starting,", enumFile.Content);
        Assert.Contains("Started,", enumFile.Content);
        Assert.Contains("Stopping,", enumFile.Content);
        Assert.Contains("Stopped,", enumFile.Content);
    }

    [Fact]
    public void Generate_LifecycleSchema_HealthStatusEnum_HasToStringFast()
    {
        var doc = ParseAndValidateLifecycleSchema();
        var files = _generator.GenerateFromSchema(doc);

        var enumFile = files.First(f => f.FileName == "HealthStatus.g.cs");
        Assert.Contains("public static string ToStringFast(this HealthStatus value)", enumFile.Content);
        Assert.Contains("HealthStatus.Healthy => \"Healthy\"", enumFile.Content);
        Assert.Contains("HealthStatus.Degraded => \"Degraded\"", enumFile.Content);
        Assert.Contains("HealthStatus.Unhealthy => \"Unhealthy\"", enumFile.Content);
    }

    [Fact]
    public void Generate_LifecycleSchema_LifecyclePhaseEnum_HasToStringFast()
    {
        var doc = ParseAndValidateLifecycleSchema();
        var files = _generator.GenerateFromSchema(doc);

        var enumFile = files.First(f => f.FileName == "LifecyclePhase.g.cs");
        Assert.Contains("public static string ToStringFast(this LifecyclePhase value)", enumFile.Content);
        Assert.Contains("LifecyclePhase.Starting => \"Starting\"", enumFile.Content);
        Assert.Contains("LifecyclePhase.Stopped => \"Stopped\"", enumFile.Content);
    }

    [Fact]
    public void Generate_LifecycleSchema_EnumsHaveCorrectNamespace()
    {
        var doc = ParseAndValidateLifecycleSchema();
        var files = _generator.GenerateFromSchema(doc);

        var healthFile = files.First(f => f.FileName == "HealthStatus.g.cs");
        var phaseFile = files.First(f => f.FileName == "LifecyclePhase.g.cs");

        Assert.Contains("namespace OtelEvents.Events.Lifecycle;", healthFile.Content);
        Assert.Contains("namespace OtelEvents.Events.Lifecycle;", phaseFile.Content);
    }

    [Fact]
    public void Generate_LifecycleSchema_EnumsHaveDescriptions()
    {
        var doc = ParseAndValidateLifecycleSchema();
        var files = _generator.GenerateFromSchema(doc);

        var healthFile = files.First(f => f.FileName == "HealthStatus.g.cs");
        var phaseFile = files.First(f => f.FileName == "LifecyclePhase.g.cs");

        Assert.Contains("Application health state", healthFile.Content);
        Assert.Contains("Application lifecycle phase", phaseFile.Content);
    }

    // ═══════════════════════════════════════════════════════════════
    // 5. CODE GENERATION — METRICS
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Generate_LifecycleSchema_HasMetricsClass()
    {
        var doc = ParseAndValidateLifecycleSchema();
        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        Assert.Contains("public static class LifecycleMetrics", content);
    }

    [Fact]
    public void Generate_LifecycleSchema_HasCorrectMeterName()
    {
        var doc = ParseAndValidateLifecycleSchema();
        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        Assert.Contains("new Meter(\"otel_events.lifecycle\"", content);
    }

    [Fact]
    public void Generate_LifecycleSchema_TransitionsCounter()
    {
        var doc = ParseAndValidateLifecycleSchema();
        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        Assert.Contains("Counter<long>", content);
        Assert.Contains("CreateCounter<long>(\"app.lifecycle.transitions\"", content);
    }

    [Fact]
    public void Generate_LifecycleSchema_HealthChangesCounter()
    {
        var doc = ParseAndValidateLifecycleSchema();
        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        Assert.Contains("CreateCounter<long>(\"app.health.changes\"", content);
    }

    [Fact]
    public void Generate_LifecycleSchema_StartupDurationHistogram()
    {
        var doc = ParseAndValidateLifecycleSchema();
        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        Assert.Contains("Histogram<double>", content);
        Assert.Contains("CreateHistogram<double>(\"app.startup.durationMs\"", content);
    }

    [Fact]
    public void Generate_LifecycleSchema_HealthChangesCounter_HasLabels()
    {
        var doc = ParseAndValidateLifecycleSchema();
        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        // The health.changed emit method should create a TagList with currentStatus label
        Assert.Contains("TagList", content);
        Assert.Contains("\"currentStatus\"", content);
    }

    // ═══════════════════════════════════════════════════════════════
    // 6. CODE GENERATION — EMIT METHODS
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Generate_LifecycleSchema_HasEmitMethodsForAllEvents()
    {
        var doc = ParseAndValidateLifecycleSchema();
        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        Assert.Contains("EmitAppLifecycleChanged", content);
        Assert.Contains("EmitAppHealthChanged", content);
        Assert.Contains("EmitAppStartupCompleted", content);
        Assert.Contains("EmitAppShutdownInitiated", content);
    }

    [Fact]
    public void Generate_LifecycleSchema_HasEventExtensionsClass()
    {
        var doc = ParseAndValidateLifecycleSchema();
        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        Assert.Contains("public static class LifecycleEventExtensions", content);
    }

    [Fact]
    public void Generate_LifecycleSchema_EmitMethodCallsLoggerMethod()
    {
        var doc = ParseAndValidateLifecycleSchema();
        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        Assert.Contains("LifecycleLoggerExtensions.AppLifecycleChanged(logger", content);
        Assert.Contains("LifecycleLoggerExtensions.AppHealthChanged(logger", content);
        Assert.Contains("LifecycleLoggerExtensions.AppStartupCompleted(logger", content);
        Assert.Contains("LifecycleLoggerExtensions.AppShutdownInitiated(logger", content);
    }

    [Fact]
    public void Generate_LifecycleSchema_EmitStartupCompleted_RecordsHistogram()
    {
        var doc = ParseAndValidateLifecycleSchema();
        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        // Histogram records the durationMs field value
        Assert.Contains(".Record(", content);
    }

    // ═══════════════════════════════════════════════════════════════
    // 7. GENERATED FILE COUNT
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Generate_LifecycleSchema_ProducesThreeFiles()
    {
        var doc = ParseAndValidateLifecycleSchema();
        var files = _generator.GenerateFromSchema(doc);

        // 1 events file + 2 enum files = 3 total
        Assert.Equal(3, files.Count);
    }

    // ═══════════════════════════════════════════════════════════════
    // 8. ROSLYN SYNTAX VERIFICATION
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Generate_LifecycleSchema_AllGeneratedFilesAreValidCSharp()
    {
        var doc = ParseAndValidateLifecycleSchema();
        var files = _generator.GenerateFromSchema(doc);

        foreach (var file in files)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(file.Content);
            var diagnostics = syntaxTree.GetDiagnostics().ToList();
            Assert.Empty(diagnostics);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 9. EMBEDDED RESOURCE — YAML FILE ACCESSIBLE IN PACKAGE
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void BuiltInSchema_LifecycleYaml_IsEmbeddedResource()
    {
        var assembly = typeof(SchemaParser).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();

        Assert.Contains(resourceNames,
            n => n.Contains("lifecycle.all.yaml", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuiltInSchema_LifecycleYaml_CanBeParsedFromEmbeddedResource()
    {
        var assembly = typeof(SchemaParser).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .First(n => n.Contains("lifecycle.all.yaml", StringComparison.OrdinalIgnoreCase));

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        var yaml = reader.ReadToEnd();

        var result = _parser.Parse(yaml, yaml.Length);
        Assert.True(result.IsSuccess, $"Embedded resource parse failed: {string.Join(", ", result.Errors)}");
        Assert.Equal(4, result.Document!.Events.Count);
    }

    // ═══════════════════════════════════════════════════════════════
    // 10. BUILT-IN SCHEMA CONVENIENCE API
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void BuiltInSchemas_LoadLifecycleSchema_ReturnsSuccess()
    {
        var result = BuiltInSchemas.LoadLifecycleSchema();

        Assert.True(result.IsSuccess, $"LoadLifecycleSchema failed: {string.Join(", ", result.Errors)}");
        Assert.Equal("Lifecycle", result.Document!.Schema.Name);
        Assert.Equal(4, result.Document.Events.Count);
        Assert.Equal(2, result.Document.Enums.Count);
    }

    [Fact]
    public void BuiltInSchemas_GetLifecycleSchemaYaml_ReturnsNonEmptyContent()
    {
        var yaml = BuiltInSchemas.GetLifecycleSchemaYaml();

        Assert.False(string.IsNullOrWhiteSpace(yaml));
        Assert.Contains("schema:", yaml);
        Assert.Contains("app.lifecycle.changed", yaml);
        Assert.Contains("app.health.changed", yaml);
    }

    // ═══════════════════════════════════════════════════════════════
    // HELPER
    // ═══════════════════════════════════════════════════════════════

    private SchemaDocument ParseAndValidateLifecycleSchema()
    {
        var parseResult = _parser.Parse(LifecycleYaml, LifecycleYaml.Length);
        Assert.True(parseResult.IsSuccess, $"Parse failed: {string.Join(", ", parseResult.Errors)}");

        var validationResult = _validator.Validate(parseResult.Document!);
        Assert.True(validationResult.IsValid,
            $"Validation failed: {string.Join(", ", validationResult.Errors)}");

        return parseResult.Document!;
    }
}
