using OtelEvents.Schema.Models;
using OtelEvents.Schema.Parsing;
using OtelEvents.Schema.Validation;

namespace OtelEvents.Schema.Tests;

/// <summary>
/// Tests for schema validation rules ALL_SCHEMA_001 through ALL_SCHEMA_018.
/// Each test targets a specific error code.
/// </summary>
public class SchemaValidatorTests
{
    private readonly SchemaParser _parser = new();
    private readonly SchemaValidator _validator = new();

    /// <summary>
    /// Helper: parse YAML and then validate, returning validation result.
    /// </summary>
    private ValidationResult ParseAndValidate(string yaml)
    {
        var parseResult = _parser.Parse(yaml, yaml.Length);
        Assert.True(parseResult.IsSuccess, $"Parse failed: {string.Join(", ", parseResult.Errors)}");
        return _validator.Validate(parseResult.Document!);
    }

    // ── ALL_SCHEMA_001: Unique event names ──────────────────────────────

    [Fact]
    public void Validate_DuplicateEventName_ReturnsALL_SCHEMA_001()
    {
        var doc1 = CreateMinimalDoc(events:
        [
            CreateEvent("http.request.received", 1001),
        ]);
        var doc2 = CreateMinimalDoc(events:
        [
            CreateEvent("http.request.received", 1002),
        ]);

        var result = _validator.Validate([doc1, doc2]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.DuplicateEventName);
    }

    // ── ALL_SCHEMA_002: Valid severity ───────────────────────────────────

    [Fact]
    public void Validate_InvalidSeverity_ParseReturnsError()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              test.event:
                id: 1
                severity: INVALID_SEV
                message: "Test"
            """;

        // Invalid severity causes a parse error (thrown during parsing)
        var parseResult = _parser.Parse(yaml, yaml.Length);
        Assert.False(parseResult.IsSuccess);
        Assert.Contains(parseResult.Errors, e => e.Code == ErrorCodes.InvalidSeverity);
    }

    // ── ALL_SCHEMA_003: Message template match ──────────────────────────

    [Fact]
    public void Validate_MessagePlaceholderNotMatchingField_ReturnsALL_SCHEMA_003()
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
                message: "User {userId} did {action}"
                fields:
                  userId:
                    type: string
                    required: true
            """;

        var result = ParseAndValidate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Code == ErrorCodes.MessageTemplateMismatch &&
            e.Message.Contains("action"));
    }

    [Fact]
    public void Validate_AllPlaceholdersMatchFields_NoError()
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
                message: "User {userId} did {action}"
                fields:
                  userId:
                    type: string
                    required: true
                  action:
                    type: string
                    required: true
            """;

        var result = ParseAndValidate(yaml);

        Assert.True(result.IsValid);
    }

    // ── ALL_SCHEMA_004: Ref resolution ──────────────────────────────────

    [Fact]
    public void Validate_UnresolvedRef_ReturnsALL_SCHEMA_004()
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
                message: "Method {method}"
                fields:
                  method:
                    ref: nonExistentField
                    required: true
            """;

        var result = ParseAndValidate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.UnresolvedRef);
    }

    [Fact]
    public void Validate_RefResolvesToSharedField_NoError()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            fields:
              httpMethod:
                type: string
                description: "HTTP method"
            events:
              test.event:
                id: 1
                severity: INFO
                message: "Method {method}"
                fields:
                  method:
                    ref: httpMethod
                    required: true
            """;

        var result = ParseAndValidate(yaml);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_RefResolvesToEnum_NoError()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            enums:
              HealthStatus:
                description: "Health state"
                values:
                  - Healthy
                  - Unhealthy
            events:
              test.event:
                id: 1
                severity: INFO
                message: "Status {status}"
                fields:
                  status:
                    ref: HealthStatus
                    required: true
            """;

        var result = ParseAndValidate(yaml);

        Assert.True(result.IsValid);
    }

    // ── ALL_SCHEMA_005: Type validity ───────────────────────────────────

    [Fact]
    public void Validate_InvalidFieldType_ReturnsALL_SCHEMA_005()
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
                message: "Value {value}"
                fields:
                  value:
                    type: foobar
                    required: true
            """;

        var result = ParseAndValidate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidType);
    }

    // ── ALL_SCHEMA_006: Event name format ───────────────────────────────

    [Theory]
    [InlineData("NotLowerCase")]
    [InlineData("no-dashes-allowed")]
    [InlineData("singleword")]
    [InlineData("has spaces")]
    [InlineData("123.starts.with.number")]
    public void Validate_InvalidEventNameFormat_ReturnsALL_SCHEMA_006(string eventName)
    {
        var doc = CreateMinimalDoc(events:
        [
            CreateEvent(eventName, 1),
        ]);

        var result = _validator.Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidEventNameFormat);
    }

    [Theory]
    [InlineData("http.request")]
    [InlineData("db.query.executed")]
    [InlineData("app.health.changed")]
    public void Validate_ValidEventNameFormat_NoError(string eventName)
    {
        var doc = CreateMinimalDoc(events:
        [
            CreateEvent(eventName, 1),
        ]);

        var result = _validator.Validate(doc);

        // Should not contain event name format errors
        Assert.DoesNotContain(result.Errors, e => e.Code == ErrorCodes.InvalidEventNameFormat);
    }

    // ── ALL_SCHEMA_007: Required field completeness ─────────────────────

    [Fact]
    public void Validate_RequiredFieldWithoutTypeOrRef_ReturnsALL_SCHEMA_007()
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
                message: "Value {value}"
                fields:
                  value:
                    required: true
            """;

        var result = ParseAndValidate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.RequiredFieldMissingType);
    }

    // ── ALL_SCHEMA_008: Metric type validity ────────────────────────────

    [Fact]
    public void Validate_InvalidMetricType_ReturnsALL_SCHEMA_008()
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
                message: "Test"
                metrics:
                  test.metric:
                    type: invalid_metric
                    description: "Bad metric"
            """;

        var result = ParseAndValidate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidMetricType);
    }

    [Theory]
    [InlineData("counter")]
    [InlineData("histogram")]
    [InlineData("gauge")]
    public void Validate_ValidMetricType_NoMetricError(string metricType)
    {
        var yaml = $"""
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              test.event:
                id: 1
                severity: INFO
                message: "Test"
                metrics:
                  test.metric:
                    type: {metricType}
                    description: "Valid metric"
            """;

        var result = ParseAndValidate(yaml);

        Assert.DoesNotContain(result.Errors, e => e.Code == ErrorCodes.InvalidMetricType);
    }

    // ── ALL_SCHEMA_009: Enum non-empty ──────────────────────────────────

    [Fact]
    public void Validate_EmptyEnum_ReturnsALL_SCHEMA_009()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            enums:
              EmptyEnum:
                description: "This enum has no values"
                values: []
            """;

        var result = ParseAndValidate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.EmptyEnum);
    }

    // ── ALL_SCHEMA_010: Semver version ──────────────────────────────────

    [Theory]
    [InlineData("not-semver")]
    [InlineData("1.0")]
    [InlineData("v1.0.0")]
    [InlineData("1")]
    [InlineData("abc")]
    public void Validate_InvalidSemver_ReturnsALL_SCHEMA_010(string version)
    {
        var doc = CreateMinimalDoc(version: version);

        var result = _validator.Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidSemver);
    }

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("2.1.3")]
    [InlineData("1.0.0-alpha")]
    [InlineData("1.0.0-beta.1")]
    [InlineData("1.0.0+build.123")]
    public void Validate_ValidSemver_NoSemverError(string version)
    {
        var doc = CreateMinimalDoc(version: version);

        var result = _validator.Validate(doc);

        Assert.DoesNotContain(result.Errors, e => e.Code == ErrorCodes.InvalidSemver);
    }

    // ── ALL_SCHEMA_011: Reserved prefix ─────────────────────────────────

    [Fact]
    public void Validate_EventNameWithReservedPrefix_ReturnsALL_SCHEMA_011()
    {
        var doc = CreateMinimalDoc(events:
        [
            CreateEvent("all.internal.event", 1),
        ]);

        var result = _validator.Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.ReservedPrefix);
    }

    [Fact]
    public void Validate_FieldNameWithReservedPrefix_ReturnsALL_SCHEMA_011()
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
                message: "Test"
                fields:
                  all.reserved:
                    type: string
            """;

        var result = ParseAndValidate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.ReservedPrefix);
    }

    // ── ALL_SCHEMA_012: Unique numeric IDs ──────────────────────────────

    [Fact]
    public void Validate_DuplicateEventId_ReturnsALL_SCHEMA_012()
    {
        var doc = CreateMinimalDoc(events:
        [
            CreateEvent("test.event1", 1001),
            CreateEvent("test.event2", 1001),
        ]);

        var result = _validator.Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.DuplicateEventId);
    }

    // ── ALL_SCHEMA_013: Meter name valid ────────────────────────────────

    [Theory]
    [InlineData("123invalid")]
    [InlineData("has spaces")]
    [InlineData(".starts.with.dot")]
    [InlineData("ends.with.dot.")]
    public void Validate_InvalidMeterName_ReturnsALL_SCHEMA_013(string meterName)
    {
        var doc = CreateMinimalDoc(meterName: meterName);

        var result = _validator.Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidMeterName);
    }

    [Theory]
    [InlineData("MyCompany.MyService")]
    [InlineData("Valid_Name")]
    [InlineData("A.B.C")]
    public void Validate_ValidMeterName_NoMeterError(string meterName)
    {
        var doc = CreateMinimalDoc(meterName: meterName);

        var result = _validator.Validate(doc);

        Assert.DoesNotContain(result.Errors, e => e.Code == ErrorCodes.InvalidMeterName);
    }

    // ── ALL_SCHEMA_014: Sensitivity validity ────────────────────────────

    [Fact]
    public void Validate_InvalidSensitivity_ReturnsALL_SCHEMA_014()
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
                message: "User {userId}"
                fields:
                  userId:
                    type: string
                    required: true
                    sensitivity: secret
            """;

        var result = ParseAndValidate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidSensitivity);
    }

    [Theory]
    [InlineData("public")]
    [InlineData("internal")]
    [InlineData("pii")]
    [InlineData("credential")]
    public void Validate_ValidSensitivity_NoSensitivityError(string sensitivity)
    {
        var yaml = $$"""
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              test.event:
                id: 1
                severity: INFO
                message: "User {userId}"
                fields:
                  userId:
                    type: string
                    required: true
                    sensitivity: {{sensitivity}}
            """;

        var result = ParseAndValidate(yaml);

        Assert.DoesNotContain(result.Errors, e => e.Code == ErrorCodes.InvalidSensitivity);
    }

    // ── ALL_SCHEMA_015: Max length validity ─────────────────────────────

    [Fact]
    public void Validate_InvalidMaxLength_Zero_ReturnsALL_SCHEMA_015()
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
                  path:
                    type: string
                    required: true
                    maxLength: 0
            """;

        var result = ParseAndValidate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidMaxLength);
    }

    [Fact]
    public void Validate_InvalidMaxLength_Negative_ReturnsALL_SCHEMA_015()
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
                  path:
                    type: string
                    required: true
                    maxLength: -5
            """;

        var result = ParseAndValidate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidMaxLength);
    }

    [Fact]
    public void Validate_InvalidMaxLength_NonNumeric_ReturnsALL_SCHEMA_015()
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
                  path:
                    type: string
                    required: true
                    maxLength: abc
            """;

        var result = ParseAndValidate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidMaxLength);
    }

    [Fact]
    public void Validate_ValidMaxLength_NoError()
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
                  path:
                    type: string
                    required: true
                    maxLength: 256
            """;

        var result = ParseAndValidate(yaml);

        Assert.DoesNotContain(result.Errors, e => e.Code == ErrorCodes.InvalidMaxLength);
    }

    // ── ALL_SCHEMA_016: File size limit ─────────────────────────────────

    [Fact]
    public void Validate_FileSizeExceedsLimit_ReturnsALL_SCHEMA_016()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            """;

        var result = _parser.Parse(yaml, SchemaParser.MaxFileSizeBytes + 1);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.FileSizeExceeded);
    }

    // ── ALL_SCHEMA_017: Event count limit ───────────────────────────────

    [Fact]
    public void Validate_EventCountExceedsLimit_ReturnsALL_SCHEMA_017()
    {
        var events = new List<EventDefinition>();
        for (int i = 0; i < 501; i++)
        {
            events.Add(CreateEvent($"test.event{i}", i + 1));
        }

        var doc = CreateMinimalDoc(events: events);
        var result = _validator.Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.EventCountExceeded);
    }

    [Fact]
    public void Validate_EventCountAtLimit_NoError()
    {
        var events = new List<EventDefinition>();
        for (int i = 0; i < 500; i++)
        {
            events.Add(CreateEvent($"test.event{i}", i + 1));
        }

        var doc = CreateMinimalDoc(events: events);
        var result = _validator.Validate(doc);

        Assert.DoesNotContain(result.Errors, e => e.Code == ErrorCodes.EventCountExceeded);
    }

    // ── ALL_SCHEMA_018: Field count limit ───────────────────────────────

    [Fact]
    public void Validate_FieldCountExceedsLimit_ReturnsALL_SCHEMA_018()
    {
        var fields = new List<FieldDefinition>();
        for (int i = 0; i < 51; i++)
        {
            fields.Add(new FieldDefinition { Name = $"field{i}", Type = FieldType.String });
        }

        var doc = CreateMinimalDoc(events:
        [
            new EventDefinition
            {
                Name = "test.event",
                Id = 1,
                Severity = Severity.Info,
                Message = "Test",
                Fields = fields
            }
        ]);

        var result = _validator.Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.FieldCountExceeded);
    }

    [Fact]
    public void Validate_FieldCountAtLimit_NoError()
    {
        var fields = new List<FieldDefinition>();
        for (int i = 0; i < 50; i++)
        {
            fields.Add(new FieldDefinition { Name = $"field{i}", Type = FieldType.String });
        }

        var doc = CreateMinimalDoc(events:
        [
            new EventDefinition
            {
                Name = "test.event",
                Id = 1,
                Severity = Severity.Info,
                Message = "Test",
                Fields = fields
            }
        ]);

        var result = _validator.Validate(doc);

        Assert.DoesNotContain(result.Errors, e => e.Code == ErrorCodes.FieldCountExceeded);
    }

    // ── Valid complete schema ────────────────────────────────────────────

    [Fact]
    public void Validate_CompleteValidSchema_PassesAllRules()
    {
        var yaml = """
            schema:
              name: "MyService"
              version: "1.0.0"
              namespace: "MyCompany.MyService"
              description: "Test service"
              meterName: "MyCompany.MyService"
            fields:
              userId:
                type: string
                description: "User ID"
                sensitivity: pii
                index: true
              durationMs:
                type: double
                description: "Duration"
                unit: "ms"
            enums:
              HealthStatus:
                description: "Health"
                values:
                  - Healthy
                  - Unhealthy
            events:
              http.request.received:
                id: 1001
                severity: INFO
                message: "HTTP {method} {path} received"
                fields:
                  method:
                    type: string
                    required: true
                  path:
                    type: string
                    required: true
                    maxLength: 256
                metrics:
                  http.request.count:
                    type: counter
                    unit: "requests"
                tags:
                  - api
              app.health.changed:
                id: 4001
                severity: WARN
                message: "Health changed to {status}"
                fields:
                  status:
                    ref: HealthStatus
                    required: true
            """;

        var result = ParseAndValidate(yaml);

        Assert.True(result.IsValid, $"Errors: {string.Join(", ", result.Errors)}");
    }

    // ── Cross-document merge validation ─────────────────────────────────

    [Fact]
    public void Validate_CrossDocumentRefResolution_ResolvesBetweenDocuments()
    {
        var doc1 = new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = "Service1",
                Version = "1.0.0",
                Namespace = "Test.Service1"
            },
            Fields =
            [
                new FieldDefinition { Name = "httpMethod", Type = FieldType.String }
            ]
        };

        var doc2 = new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = "Service2",
                Version = "1.0.0",
                Namespace = "Test.Service2"
            },
            Events =
            [
                new EventDefinition
                {
                    Name = "http.request",
                    Id = 1,
                    Severity = Severity.Info,
                    Message = "Request {method}",
                    Fields =
                    [
                        new FieldDefinition { Name = "method", Ref = "httpMethod", Required = true }
                    ]
                }
            ]
        };

        var result = _validator.Validate([doc1, doc2]);

        Assert.True(result.IsValid, $"Errors: {string.Join(", ", result.Errors)}");
    }

    // ── Multiple errors at once ─────────────────────────────────────────

    [Fact]
    public void Validate_MultipleErrors_ReturnsAllErrors()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "not-semver"
              namespace: "Test.Namespace"
            enums:
              EmptyEnum:
                description: "Empty"
                values: []
            events:
              INVALID_NAME:
                id: 1
                severity: INFO
                message: "Missing {placeholder}"
                fields:
                  field1:
                    type: string
            """;

        var result = ParseAndValidate(yaml);

        Assert.False(result.IsValid);
        // Should have: invalid semver, empty enum, invalid event name, message placeholder mismatch
        Assert.True(result.Errors.Count >= 3, $"Expected ≥3 errors, got {result.Errors.Count}: {string.Join(", ", result.Errors)}");
    }

    // ── Test helpers ────────────────────────────────────────────────────

    private static SchemaDocument CreateMinimalDoc(
        string version = "1.0.0",
        string? meterName = null,
        List<EventDefinition>? events = null,
        List<EnumDefinition>? enums = null,
        List<FieldDefinition>? fields = null)
    {
        return new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = "TestService",
                Version = version,
                Namespace = "Test.Namespace",
                MeterName = meterName
            },
            Events = events ?? [],
            Enums = enums ?? [],
            Fields = fields ?? []
        };
    }

    private static EventDefinition CreateEvent(string name, int id, List<FieldDefinition>? fields = null)
    {
        return new EventDefinition
        {
            Name = name,
            Id = id,
            Severity = Severity.Info,
            Message = "Test event",
            Fields = fields ?? []
        };
    }
}
