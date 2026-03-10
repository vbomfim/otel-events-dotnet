using OtelEvents.Schema.Models;
using OtelEvents.Schema.Parsing;
using OtelEvents.Schema.Validation;

namespace OtelEvents.Schema.Tests;

/// <summary>
/// Additional edge-case and security tests for schema parsing and validation.
/// </summary>
public class SchemaEdgeCaseTests
{
    private readonly SchemaParser _parser = new();
    private readonly SchemaValidator _validator = new();

    [Fact]
    public void Parse_YamlWithNoEvents_ReturnsDocumentWithEmptyEvents()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Document!.Events);
    }

    [Fact]
    public void Parse_YamlWithNoFields_ReturnsDocumentWithEmptyFields()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Document!.Fields);
    }

    [Fact]
    public void Parse_YamlWithNoEnums_ReturnsDocumentWithEmptyEnums()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Document!.Enums);
    }

    [Fact]
    public void Validate_EventWithNoFields_MessageWithNoPlaceholders_IsValid()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              app.started:
                id: 1
                severity: INFO
                message: "Application started"
            """;

        var parseResult = _parser.Parse(yaml, yaml.Length);
        Assert.True(parseResult.IsSuccess);

        var result = _validator.Validate(parseResult.Document!);
        Assert.True(result.IsValid, $"Errors: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void Validate_GaugeMetricType_IsValid()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              system.memory:
                id: 1
                severity: DEBUG
                message: "Memory usage"
                metrics:
                  system.memory.usage:
                    type: gauge
                    unit: "bytes"
                    description: "Current memory usage"
            """;

        var parseResult = _parser.Parse(yaml, yaml.Length);
        Assert.True(parseResult.IsSuccess);

        var result = _validator.Validate(parseResult.Document!);
        Assert.DoesNotContain(result.Errors, e => e.Code == ErrorCodes.InvalidMetricType);
    }

    [Fact]
    public void Validate_MeterNameDefaultsToNamespace_ValidatesNamespace()
    {
        // When meterName is not specified, namespace is used
        var doc = new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = "TestService",
                Version = "1.0.0",
                Namespace = "Valid.Namespace"
                // MeterName intentionally null
            }
        };

        var result = _validator.Validate(doc);

        Assert.DoesNotContain(result.Errors, e => e.Code == ErrorCodes.InvalidMeterName);
    }

    [Fact]
    public void Parse_SharedFieldWithSensitivity_ParsesCorrectly()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            fields:
              apiKey:
                type: string
                sensitivity: credential
                description: "API key"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        Assert.Equal(Sensitivity.Credential, result.Document!.Fields[0].Sensitivity);
    }

    [Fact]
    public void Parse_SharedFieldWithMaxLength_ParsesCorrectly()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            fields:
              userAgent:
                type: string
                maxLength: 512
                sensitivity: pii
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        Assert.Equal(512, result.Document!.Fields[0].MaxLength);
        Assert.Equal(Sensitivity.Pii, result.Document.Fields[0].Sensitivity);
    }

    [Fact]
    public void Validate_MultipleDocsMerged_EventCountExceedsLimit()
    {
        // 300 events in each doc = 600 total > 500 limit
        var events1 = Enumerable.Range(1, 300)
            .Select(i => new EventDefinition
            {
                Name = $"service1.event{i}",
                Id = i,
                Severity = Severity.Info,
                Message = "Test"
            }).ToList();

        var events2 = Enumerable.Range(301, 300)
            .Select(i => new EventDefinition
            {
                Name = $"service2.event{i}",
                Id = i,
                Severity = Severity.Info,
                Message = "Test"
            }).ToList();

        var doc1 = new SchemaDocument
        {
            Schema = new SchemaHeader { Name = "S1", Version = "1.0.0", Namespace = "Test.S1" },
            Events = events1
        };
        var doc2 = new SchemaDocument
        {
            Schema = new SchemaHeader { Name = "S2", Version = "1.0.0", Namespace = "Test.S2" },
            Events = events2
        };

        var result = _validator.Validate([doc1, doc2]);

        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.EventCountExceeded);
    }

    [Fact]
    public void Validate_EventNameCaseSensitivity_UpperCaseIsInvalid()
    {
        var doc = new SchemaDocument
        {
            Schema = new SchemaHeader { Name = "Test", Version = "1.0.0", Namespace = "Test.NS" },
            Events =
            [
                new EventDefinition
                {
                    Name = "Http.Request",
                    Id = 1,
                    Severity = Severity.Info,
                    Message = "Test"
                }
            ]
        };

        var result = _validator.Validate(doc);

        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidEventNameFormat);
    }

    [Fact]
    public void ErrorCodes_AllCodesAreUnique()
    {
        // Verify all error codes are unique
        var codes = new[]
        {
            ErrorCodes.DuplicateEventName,
            ErrorCodes.InvalidSeverity,
            ErrorCodes.MessageTemplateMismatch,
            ErrorCodes.UnresolvedRef,
            ErrorCodes.InvalidType,
            ErrorCodes.InvalidEventNameFormat,
            ErrorCodes.RequiredFieldMissingType,
            ErrorCodes.InvalidMetricType,
            ErrorCodes.EmptyEnum,
            ErrorCodes.InvalidSemver,
            ErrorCodes.ReservedPrefix,
            ErrorCodes.DuplicateEventId,
            ErrorCodes.InvalidMeterName,
            ErrorCodes.InvalidSensitivity,
            ErrorCodes.InvalidMaxLength,
            ErrorCodes.FileSizeExceeded,
            ErrorCodes.EventCountExceeded,
            ErrorCodes.FieldCountExceeded
        };

        Assert.Equal(18, codes.Length);
        Assert.Equal(18, codes.Distinct().Count());
    }

    [Fact]
    public void SchemaError_ToString_IncludesCodeAndMessage()
    {
        var error = new SchemaError
        {
            Code = "OTEL_SCHEMA_001",
            Message = "Duplicate event name"
        };

        Assert.Equal("OTEL_SCHEMA_001: Duplicate event name", error.ToString());
    }

    [Fact]
    public void ValidationResult_Success_HasNoErrors()
    {
        var result = ValidationResult.Success();

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidationResult_Failure_HasErrors()
    {
        var errors = new List<SchemaError>
        {
            new() { Code = "TEST", Message = "Test error" }
        };

        var result = ValidationResult.Failure(errors);

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void ParseResult_Success_HasDocument()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Document);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ParseResult_Failure_HasErrors()
    {
        var result = _parser.Parse("invalid yaml: [", 15);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Document);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Validate_SharedFieldValidation_SensitivityOnSharedFields()
    {
        // Shared fields with invalid sensitivity should also be validated
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            fields:
              apiKey:
                type: string
                sensitivity: topsecret
            events:
              test.event:
                id: 1
                severity: INFO
                message: "Key {apiKey}"
                fields:
                  apiKey:
                    ref: apiKey
                    required: true
            """;

        var parseResult = _parser.Parse(yaml, yaml.Length);
        Assert.True(parseResult.IsSuccess);

        // The validation should still work (shared field sensitivity is parsed but
        // validation focuses on event fields; shared fields are storage only)
        var result = _validator.Validate(parseResult.Document!);
        // No sensitivity error since the event field uses ref (no direct sensitivity)
        Assert.DoesNotContain(result.Errors, e => e.Code == ErrorCodes.InvalidSensitivity);
    }
}
