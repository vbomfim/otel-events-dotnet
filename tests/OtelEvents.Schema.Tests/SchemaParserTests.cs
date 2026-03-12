using OtelEvents.Schema.Models;
using OtelEvents.Schema.Parsing;
using OtelEvents.Schema.Validation;

namespace OtelEvents.Schema.Tests;

/// <summary>
/// Tests for YAML schema parsing — validates that valid .otel.yaml files
/// are correctly parsed into strongly-typed SchemaDocument models.
/// </summary>
public class SchemaParserTests
{
    private readonly SchemaParser _parser = new();

    private const string ValidMinimalYaml = """
        schema:
          name: "TestService"
          version: "1.0.0"
          namespace: "Test.Namespace"
        """;

    [Fact]
    public void Parse_ValidMinimalSchema_ReturnsSuccess()
    {
        var result = _parser.Parse(ValidMinimalYaml, ValidMinimalYaml.Length);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Document);
        Assert.Equal("TestService", result.Document.Schema.Name);
        Assert.Equal("1.0.0", result.Document.Schema.Version);
        Assert.Equal("Test.Namespace", result.Document.Schema.Namespace);
    }

    [Fact]
    public void Parse_SchemaWithAllHeaderFields_ParsesCorrectly()
    {
        var yaml = """
            schema:
              name: "MyService"
              version: "2.1.0"
              namespace: "MyCompany.MyService"
              description: "Events for MyService"
              meterName: "MyCompany.MyService.Meters"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        var header = result.Document!.Schema;
        Assert.Equal("MyService", header.Name);
        Assert.Equal("2.1.0", header.Version);
        Assert.Equal("MyCompany.MyService", header.Namespace);
        Assert.Equal("Events for MyService", header.Description);
        Assert.Equal("MyCompany.MyService.Meters", header.MeterName);
    }

    [Fact]
    public void Parse_SchemaWithImports_ParsesImportPaths()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            imports:
              - "shared/common.otel.yaml"
              - "shared/http.otel.yaml"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Document!.Imports.Count);
        Assert.Equal("shared/common.otel.yaml", result.Document.Imports[0]);
        Assert.Equal("shared/http.otel.yaml", result.Document.Imports[1]);
    }

    [Fact]
    public void Parse_SchemaWithSharedFields_ParsesFieldDefinitions()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            fields:
              userId:
                type: string
                description: "Unique user identifier"
                index: true
                examples: ["usr_abc123"]
              durationMs:
                type: double
                description: "Duration in milliseconds"
                unit: "ms"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Document!.Fields.Count);

        var userId = result.Document.Fields[0];
        Assert.Equal("userId", userId.Name);
        Assert.Equal("Unique user identifier", userId.Description);
        Assert.True(userId.Index);

        var duration = result.Document.Fields[1];
        Assert.Equal("durationMs", duration.Name);
    }

    [Fact]
    public void Parse_SchemaWithEnums_ParsesEnumDefinitions()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            enums:
              HealthStatus:
                description: "Application health state"
                values:
                  - Healthy
                  - Degraded
                  - Unhealthy
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Document!.Enums);

        var healthEnum = result.Document.Enums[0];
        Assert.Equal("HealthStatus", healthEnum.Name);
        Assert.Equal("Application health state", healthEnum.Description);
        Assert.Equal(3, healthEnum.Values.Count);
        Assert.Contains("Healthy", healthEnum.Values);
        Assert.Contains("Degraded", healthEnum.Values);
        Assert.Contains("Unhealthy", healthEnum.Values);
    }

    [Fact]
    public void Parse_SchemaWithEvent_ParsesEventDefinition()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              http.request.received:
                id: 1001
                severity: INFO
                description: "An HTTP request was received"
                message: "HTTP {method} {path} received"
                fields:
                  method:
                    type: string
                    required: true
                  path:
                    type: string
                    required: true
                    index: true
                tags:
                  - api
                  - http
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Document!.Events);

        var evt = result.Document.Events[0];
        Assert.Equal("http.request.received", evt.Name);
        Assert.Equal(1001, evt.Id);
        Assert.Equal(Severity.Info, evt.Severity);
        Assert.Equal("An HTTP request was received", evt.Description);
        Assert.Equal("HTTP {method} {path} received", evt.Message);
        Assert.Equal(2, evt.Fields.Count);
        Assert.Equal(2, evt.Tags.Count);
        Assert.Contains("api", evt.Tags);
        Assert.Contains("http", evt.Tags);
    }

    [Fact]
    public void Parse_EventWithRefField_StillParsesSuccessfully()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            fields:
              httpMethod:
                type: enum
                values: [GET, POST, PUT]
            events:
              http.request.received:
                id: 1001
                severity: INFO
                message: "HTTP {method} received"
                fields:
                  method:
                    ref: httpMethod
                    required: true
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        var field = result.Document!.Events[0].Fields[0];
        Assert.Equal("method", field.Name);
        Assert.True(field.Required);
    }

    [Fact]
    public void Parse_EventWithMetrics_ParsesMetricDefinitions()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              http.request.completed:
                id: 1002
                severity: INFO
                message: "HTTP completed in {durationMs}ms"
                fields:
                  durationMs:
                    type: double
                    required: true
                  statusCode:
                    type: int
                    required: true
                metrics:
                  http.request.duration:
                    type: histogram
                    unit: "ms"
                    description: "HTTP request duration"
                    buckets: [5, 10, 25, 50, 100]
                  http.response.count:
                    type: counter
                    unit: "responses"
                    description: "Total HTTP responses"
                    labels:
                      - statusCode
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        var metrics = result.Document!.Events[0].Metrics;
        Assert.Equal(2, metrics.Count);

        var histogram = metrics[0];
        Assert.Equal("http.request.duration", histogram.Name);
        Assert.Equal(MetricType.Histogram, histogram.Type);
        Assert.Equal("ms", histogram.Unit);
        Assert.NotNull(histogram.Buckets);
        Assert.Equal(5, histogram.Buckets.Count);

        var counter = metrics[1];
        Assert.Equal("http.response.count", counter.Name);
        Assert.Equal(MetricType.Counter, counter.Type);
        Assert.NotNull(counter.Labels);
        Assert.Single(counter.Labels);
        Assert.Equal("statusCode", counter.Labels[0]);
    }

    [Fact]
    public void Parse_EventWithException_ParsesExceptionFlag()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              dependency.failed:
                id: 3001
                severity: ERROR
                message: "Dependency {name} failed"
                exception: true
                fields:
                  name:
                    type: string
                    required: true
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        Assert.True(result.Document!.Events[0].Exception);
    }

    [Fact]
    public void Parse_FieldWithSensitivity_ParsesSensitivityAttribute()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              user.login:
                id: 5001
                severity: INFO
                message: "User {userId} logged in"
                fields:
                  userId:
                    type: string
                    required: true
                    sensitivity: pii
                  apiKey:
                    type: string
                    sensitivity: credential
                  hostName:
                    type: string
                    sensitivity: internal
                  method:
                    type: string
                    sensitivity: public
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        var fields = result.Document!.Events[0].Fields;
        Assert.Equal(Sensitivity.Pii, fields[0].Sensitivity);
        Assert.Equal(Sensitivity.Credential, fields[1].Sensitivity);
        Assert.Equal(Sensitivity.Internal, fields[2].Sensitivity);
        Assert.Equal(Sensitivity.Public, fields[3].Sensitivity);
    }

    [Fact]
    public void Parse_FieldWithMaxLength_ParsesMaxLengthAttribute()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              http.request.received:
                id: 1001
                severity: INFO
                message: "HTTP {path} received"
                fields:
                  path:
                    type: string
                    required: true
                    maxLength: 256
                  userAgent:
                    type: string
                    maxLength: 512
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        var fields = result.Document!.Events[0].Fields;
        Assert.Equal(256, fields[0].MaxLength);
        Assert.Equal(512, fields[1].MaxLength);
    }

    [Fact]
    public void Parse_AllFieldTypes_BackwardCompatible()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              test.field.types:
                id: 9999
                severity: DEBUG
                message: "Test"
                fields:
                  f1:
                    type: string
                  f2:
                    type: int
                  f3:
                    type: long
                  f4:
                    type: double
                  f5:
                    type: bool
                  f6:
                    type: datetime
                  f7:
                    type: duration
                  f8:
                    type: guid
                  f9:
                    type: enum
                    values: [A, B]
                  f10:
                    type: "string[]"
                  f11:
                    type: "int[]"
                  f12:
                    type: map
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        var fields = result.Document!.Events[0].Fields;
        Assert.Equal(12, fields.Count);
    }

    [Fact]
    public void Parse_AllSeverityLevels_ParsesCorrectly()
    {
        var severities = new[] { "TRACE", "DEBUG", "INFO", "WARN", "ERROR", "FATAL" };
        var expected = new[] { Severity.Trace, Severity.Debug, Severity.Info, Severity.Warn, Severity.Error, Severity.Fatal };

        for (int i = 0; i < severities.Length; i++)
        {
            var yaml = $"""
                schema:
                  name: "TestService"
                  version: "1.0.0"
                  namespace: "Test.Namespace"
                events:
                  test.event{i}:
                    id: {1000 + i}
                    severity: {severities[i]}
                    message: "Test"
                """;

            var result = _parser.Parse(yaml, yaml.Length);
            Assert.True(result.IsSuccess, $"Failed to parse severity {severities[i]}");
            Assert.Equal(expected[i], result.Document!.Events[0].Severity);
        }
    }

    [Fact]
    public void Parse_FileSizeExceedsLimit_ReturnsError()
    {
        // Simulate a file larger than 1 MB
        var result = _parser.Parse(ValidMinimalYaml, SchemaParser.MaxFileSizeBytes + 1);

        Assert.False(result.IsSuccess);
        Assert.Single(result.Errors);
        Assert.Equal(ErrorCodes.FileSizeExceeded, result.Errors[0].Code);
    }

    [Fact]
    public void Parse_DeeplyNestedYaml_ReturnsError()
    {
        // Build deeply nested YAML that exceeds depth limit
        var yaml = "schema:\n  name: \"Test\"\n  version: \"1.0.0\"\n  namespace: \"Test\"\nevents:\n";
        var nested = "  e.a:\n    id: 1\n    severity: INFO\n    message: \"Test\"\n    fields:\n";
        // Build deeply nested mapping
        var depth = "";
        for (int i = 0; i < 25; i++)
        {
            depth += new string(' ', 6 + i * 2) + $"level{i}:\n";
            depth += new string(' ', 8 + i * 2) + $"l{i}:\n";
        }
        yaml += nested + depth;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Message.Contains("nesting depth"));
    }

    [Fact]
    public void Parse_EmptyDocument_ReturnsError()
    {
        var result = _parser.Parse("", 0);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Parse_InvalidYamlSyntax_ReturnsError()
    {
        var yaml = "schema:\n  name: [invalid: {yaml: syntax";

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Parse_MissingSchemaBlock_ReturnsError()
    {
        var yaml = """
            events:
              test.event:
                id: 1
                severity: INFO
                message: "Test"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Parse_FieldWithDefaultSensitivity_DefaultsToPublic()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              test.event:
                id: 1
                severity: INFO
                message: "Test {name}"
                fields:
                  name:
                    type: string
                    required: true
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        Assert.Equal(Sensitivity.Public, result.Document!.Events[0].Fields[0].Sensitivity);
    }

    [Fact]
    public void Parse_FieldWithInlineEnumValues_StillParsesSuccessfully()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              db.query.executed:
                id: 2001
                severity: DEBUG
                message: "Query on {table}"
                fields:
                  table:
                    type: string
                    required: true
                  operation:
                    type: enum
                    values: [SELECT, INSERT, UPDATE, DELETE]
                    required: true
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        var opField = result.Document!.Events[0].Fields[1];
        Assert.Equal("operation", opField.Name);
        Assert.True(opField.Required);
    }

    [Fact]
    public void Parse_FullSpecExample_ParsesSuccessfully()
    {
        // This mirrors the full example from the specification §6
        var yaml = """
            schema:
              name: "MyService"
              version: "1.0.0"
              namespace: "MyCompany.MyService"
              description: "Events for MyService"
              meterName: "MyCompany.MyService"
            fields:
              userId:
                type: string
                description: "Unique user identifier"
                index: true
                examples: ["usr_abc123"]
              durationMs:
                type: double
                description: "Duration in milliseconds"
                unit: "ms"
              httpStatusCode:
                type: int
                description: "HTTP response status code"
                index: true
            enums:
              HealthStatus:
                description: "Application health state"
                values:
                  - Healthy
                  - Degraded
                  - Unhealthy
            events:
              http.request.received:
                id: 1001
                severity: INFO
                description: "An HTTP request was received by the service"
                message: "HTTP {method} {path} received"
                fields:
                  method:
                    type: string
                    required: true
                  path:
                    type: string
                    required: true
                    index: true
                metrics:
                  http.request.count:
                    type: counter
                    unit: "requests"
                    description: "Total HTTP requests received"
                tags:
                  - api
                  - http
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        var doc = result.Document!;
        Assert.Equal("MyService", doc.Schema.Name);
        Assert.Equal(3, doc.Fields.Count);
        Assert.Single(doc.Enums);
        Assert.Single(doc.Events);

        var evt = doc.Events[0];
        Assert.Equal("http.request.received", evt.Name);
        Assert.Equal(1001, evt.Id);
        Assert.Equal(Severity.Info, evt.Severity);
        Assert.Equal(2, evt.Fields.Count);
        Assert.Single(evt.Metrics);
        Assert.Equal(2, evt.Tags.Count);
    }

    // ═══════════════════════════════════════════════════════════════
    // SHORTHAND LIST SYNTAX FOR FIELDS (new in Issue #42)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Parse_EventFieldsAsSequence_SimpleNames_ParsesCorrectly()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              order.placed:
                id: 1001
                severity: INFO
                message: "Order {orderId} placed"
                fields:
                  - orderId
                  - customerId
                  - amount
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        var fields = result.Document!.Events[0].Fields;
        Assert.Equal(3, fields.Count);
        Assert.Equal("orderId", fields[0].Name);
        Assert.Equal("customerId", fields[1].Name);
        Assert.Equal("amount", fields[2].Name);
    }

    [Fact]
    public void Parse_EventFieldsAsSequence_WithAnnotations_ParsesCorrectly()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              user.login:
                id: 2001
                severity: INFO
                message: "User {userId} logged in"
                fields:
                  - userId: { sensitivity: pii, required: true }
                  - email: { sensitivity: pii }
                  - region
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        var fields = result.Document!.Events[0].Fields;
        Assert.Equal(3, fields.Count);

        Assert.Equal("userId", fields[0].Name);
        Assert.Equal(Sensitivity.Pii, fields[0].Sensitivity);
        Assert.True(fields[0].Required);

        Assert.Equal("email", fields[1].Name);
        Assert.Equal(Sensitivity.Pii, fields[1].Sensitivity);
        Assert.False(fields[1].Required);

        Assert.Equal("region", fields[2].Name);
        Assert.Equal(Sensitivity.Public, fields[2].Sensitivity);
    }

    [Fact]
    public void Parse_SharedFieldsAsSequence_ParsesCorrectly()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            fields:
              - orderId
              - customerId: { sensitivity: pii }
              - amount: { required: true }
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        var fields = result.Document!.Fields;
        Assert.Equal(3, fields.Count);
        Assert.Equal("orderId", fields[0].Name);
        Assert.Equal("customerId", fields[1].Name);
        Assert.Equal(Sensitivity.Pii, fields[1].Sensitivity);
        Assert.Equal("amount", fields[2].Name);
        Assert.True(fields[2].Required);
    }

    [Fact]
    public void Parse_OldMapSyntaxWithType_StillParsesSuccessfully()
    {
        // Backward compat: old type: key is silently accepted but ignored
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              order.placed:
                id: 1001
                severity: INFO
                message: "Order {orderId} for {amount}"
                fields:
                  orderId:
                    type: string
                    required: true
                  amount:
                    type: double
                    required: true
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        var fields = result.Document!.Events[0].Fields;
        Assert.Equal(2, fields.Count);
        Assert.Equal("orderId", fields[0].Name);
        Assert.True(fields[0].Required);
        Assert.Equal("amount", fields[1].Name);
        Assert.True(fields[1].Required);
    }

    [Fact]
    public void Parse_OldMapSyntaxWithRef_StillParsesSuccessfully()
    {
        // Backward compat: old ref: key is silently accepted but ignored
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            fields:
              userId:
                type: string
                description: "User ID"
            events:
              test.event:
                id: 1
                severity: INFO
                message: "User {method}"
                fields:
                  method:
                    ref: userId
                    required: true
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        var field = result.Document!.Events[0].Fields[0];
        Assert.Equal("method", field.Name);
        Assert.True(field.Required);
    }

    [Fact]
    public void Parse_FieldWithMaxLengthInSequence_ParsesCorrectly()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              test.event:
                id: 1
                severity: INFO
                message: "Path {path}"
                fields:
                  - path: { required: true, maxLength: 256 }
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        var field = result.Document!.Events[0].Fields[0];
        Assert.Equal("path", field.Name);
        Assert.True(field.Required);
        Assert.Equal(256, field.MaxLength);
    }
}
