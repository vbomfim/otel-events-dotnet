using OtelEvents.Schema.CodeGen;
using OtelEvents.Schema.Models;
using OtelEvents.Schema.Parsing;
using OtelEvents.Schema.Validation;

namespace OtelEvents.Schema.Tests;

/// <summary>
/// Security-focused tests for code generation and schema validation.
/// Covers: code injection via enum values, namespace injection, schema name injection,
/// string literal escaping, XML comment escaping, import path traversal,
/// and locale-dependent parsing.
/// </summary>
public class CodeGeneratorSecurityTests
{
    private readonly CodeGenerator _generator = new();
    private readonly SchemaParser _parser = new();
    private readonly SchemaValidator _validator = new();

    // ═══════════════════════════════════════════════════════════════
    // 1. ENUM VALUE SANITIZATION — malicious enum values must be
    //    rejected by validation or sanitized before code generation
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("Valid_Value")]
    [InlineData("Active")]
    [InlineData("_underscore")]
    [InlineData("A1")]
    public void Validate_ValidEnumValues_NoError(string value)
    {
        var doc = CreateDocWithEnum("Status", [value]);

        var result = _validator.Validate(doc);

        Assert.DoesNotContain(result.Errors, e => e.Code == ErrorCodes.InvalidEnumValue);
    }

    [Theory]
    [InlineData("invalid value")]    // space
    [InlineData("123StartDigit")]    // starts with digit
    [InlineData("val;ue")]           // semicolon
    [InlineData("value}")]           // brace
    [InlineData("a\nb")]             // newline
    [InlineData("val\"ue")]          // quote
    [InlineData("")]                 // empty
    public void Validate_InvalidEnumValue_ReturnsOTEL_SCHEMA_019(string value)
    {
        var doc = CreateDocWithEnum("Status", [value]);

        var result = _validator.Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidEnumValue);
    }

    [Fact]
    public void GenerateFromSchema_EnumValues_AreSanitizedInGeneratedCode()
    {
        // Even if validation is bypassed, CodeGenerator must sanitize
        var doc = new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = "TestService",
                Version = "1.0.0",
                Namespace = "Test.Events"
            },
            Enums =
            [
                new EnumDefinition
                {
                    Name = "Status",
                    Values = ["Active", "In-Progress", "has space"]
                }
            ]
        };

        var files = _generator.GenerateFromSchema(doc);
        var enumFile = files.First(f => f.FileName.Contains("Status"));
        var code = enumFile.Content;

        // Sanitized identifiers used as C# enum members
        Assert.Contains("In_Progress,", code);
        Assert.Contains("has_space,", code);
        // Original values preserved in string literals (for ToStringFast mapping)
        Assert.Contains("In_Progress => \"In-Progress\"", code);
        Assert.Contains("has_space => \"has space\"", code);
        // Raw values must NOT appear as bare identifiers (enum members)
        Assert.DoesNotContain("    In-Progress,", code);
        Assert.DoesNotContain("    has space,", code);
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. NAMESPACE VALIDATION — namespace injection must be blocked
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("Valid.Namespace")]
    [InlineData("Company.Product.Events")]
    [InlineData("_N")]
    public void Validate_ValidNamespace_NoError(string ns)
    {
        var doc = CreateMinimalDoc(ns: ns);

        var result = _validator.Validate(doc);

        Assert.DoesNotContain(result.Errors, e => e.Code == ErrorCodes.InvalidNamespace);
    }

    [Theory]
    [InlineData("Invalid;Namespace")]       // semicolon injection
    [InlineData("Ns { }; class Evil {")]    // code injection
    [InlineData("123.Invalid")]             // starts with digit
    [InlineData("")]                        // empty
    [InlineData("Has Space.Bad")]           // space
    public void Validate_InvalidNamespace_ReturnsOTEL_SCHEMA_020(string ns)
    {
        var doc = CreateMinimalDoc(ns: ns);

        var result = _validator.Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidNamespace);
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. SCHEMA NAME VALIDATION — class name injection must be blocked
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("ValidName")]
    [InlineData("MyService")]
    [InlineData("_Service1")]
    public void Validate_ValidSchemaName_NoError(string name)
    {
        var doc = CreateMinimalDoc(name: name);

        var result = _validator.Validate(doc);

        Assert.DoesNotContain(result.Errors, e => e.Code == ErrorCodes.InvalidSchemaName);
    }

    [Theory]
    [InlineData("Invalid Name")]             // space
    [InlineData("Bad;Name")]                 // semicolon
    [InlineData("123Name")]                  // starts with digit
    [InlineData("Name{Evil}")]               // braces
    public void Validate_InvalidSchemaName_ReturnsOTEL_SCHEMA_021(string name)
    {
        var doc = CreateMinimalDoc(name: name);

        var result = _validator.Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidSchemaName);
    }

    // ═══════════════════════════════════════════════════════════════
    // 4. STRING LITERAL ESCAPING — meter name, metric name, version,
    //    unit must be escaped when interpolated into string literals
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateFromSchema_MeterNameWithQuotes_IsEscaped()
    {
        var doc = CreateSchemaWithEventAndMeter(meterName: "meter\"injection");

        var files = _generator.GenerateFromSchema(doc);
        var eventsFile = files.First(f => f.FileName.EndsWith("Events.g.cs"));
        var code = eventsFile.Content;

        // The raw quote must be escaped
        Assert.DoesNotContain("\"meter\"injection\"", code);
        Assert.Contains("meter\\\"injection", code);
    }

    [Fact]
    public void GenerateFromSchema_MetricNameWithSpecialChars_IsEscaped()
    {
        var doc = new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = "TestService",
                Version = "1.0.0",
                Namespace = "Test.Events"
            },
            Events =
            [
                new EventDefinition
                {
                    Name = "test.event",
                    Id = 1,
                    Severity = Severity.Info,
                    Message = "Test",
                    Metrics =
                    [
                        new MetricDefinition
                        {
                            Name = "metric\"name",
                            Type = MetricType.Counter,
                            Unit = "unit\"evil",
                            Description = "desc with \"quotes\""
                        }
                    ]
                }
            ]
        };

        var files = _generator.GenerateFromSchema(doc);
        var eventsFile = files.First(f => f.FileName.EndsWith("Events.g.cs"));
        var code = eventsFile.Content;

        // Metric name in string literal must be escaped
        Assert.Contains("metric\\\"name", code);
        // Unit in string literal must be escaped
        Assert.Contains("unit\\\"evil", code);
    }

    [Fact]
    public void GenerateFromSchema_VersionWithSpecialChars_IsEscaped()
    {
        var doc = CreateSchemaWithEventAndMeter(version: "1.0.0\"; // injected");

        var files = _generator.GenerateFromSchema(doc);
        var eventsFile = files.First(f => f.FileName.EndsWith("Events.g.cs"));
        var code = eventsFile.Content;

        // The quote in the version must be escaped to prevent breaking out of the string literal
        // The escaped form should contain \" not a raw " that would terminate the string
        Assert.Contains("1.0.0\\\"; // injected", code);
    }

    // ═══════════════════════════════════════════════════════════════
    // 5. ESCAPE STRING COMPLETENESS — must handle \n, \r, \t, \0
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateFromSchema_MessageWithNewlines_IsEscaped()
    {
        var doc = new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = "TestService",
                Version = "1.0.0",
                Namespace = "Test.Events"
            },
            Events =
            [
                new EventDefinition
                {
                    Name = "test.event",
                    Id = 1,
                    Severity = Severity.Info,
                    Message = "Line1\nLine2\rLine3\tTabbed\0Null"
                }
            ]
        };

        var files = _generator.GenerateFromSchema(doc);
        var eventsFile = files.First(f => f.FileName.EndsWith("Events.g.cs"));
        var code = eventsFile.Content;

        // Must contain escaped versions, not raw control characters
        Assert.Contains("\\n", code);
        Assert.Contains("\\r", code);
        Assert.Contains("\\t", code);
        Assert.Contains("\\0", code);
        // Raw control chars should not appear
        Assert.DoesNotContain("\n" + "Line2", code); // actual newline between tokens
    }

    // ═══════════════════════════════════════════════════════════════
    // 6. XML DOC COMMENT ESCAPING — <, >, & in descriptions
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateFromSchema_EnumDescriptionWithXmlChars_IsEscaped()
    {
        var doc = new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = "TestService",
                Version = "1.0.0",
                Namespace = "Test.Events"
            },
            Enums =
            [
                new EnumDefinition
                {
                    Name = "Status",
                    Description = "Status <active> & \"ready\"",
                    Values = ["Active", "Inactive"]
                }
            ]
        };

        var files = _generator.GenerateFromSchema(doc);
        var enumFile = files.First(f => f.FileName.Contains("Status"));
        var code = enumFile.Content;

        // XML special chars must be encoded
        Assert.Contains("&lt;", code);
        Assert.Contains("&gt;", code);
        Assert.Contains("&amp;", code);
        // Raw XML-breaking chars should not be in the summary tag
        Assert.DoesNotContain("<active>", code);
    }

    // ═══════════════════════════════════════════════════════════════
    // 7. IMPORT PATH TRAVERSAL — resolved path must stay within
    //    the project directory
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void MergeFromFile_ImportPathTraversal_ReturnsError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"schema-security-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        // Create a file outside the project directory
        var outsideDir = Path.Combine(Path.GetTempPath(), $"schema-outside-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outsideDir);

        try
        {
            var outsideFile = Path.Combine(outsideDir, "evil.otel.yaml");
            File.WriteAllText(outsideFile, """
                schema:
                  name: "Evil"
                  version: "1.0.0"
                  namespace: "Evil.Namespace"
                """);

            var mainFile = Path.Combine(tempDir, "main.otel.yaml");
            File.WriteAllText(mainFile, $"""
                schema:
                  name: "Main"
                  version: "1.0.0"
                  namespace: "Main.Namespace"
                imports:
                  - "../{Path.GetFileName(outsideDir)}/evil.otel.yaml"
                """);

            var merger = new SchemaMerger(_parser, _validator);
            var result = merger.MergeFromFile(mainFile);

            Assert.False(result.IsSuccess);
            Assert.Contains(result.Errors, e => e.Code == ErrorCodes.ImportPathTraversal);
        }
        finally
        {
            Directory.Delete(tempDir, true);
            Directory.Delete(outsideDir, true);
        }
    }

    [Fact]
    public void MergeFromFile_ImportWithinSameDirectory_Succeeds()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"schema-security-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var sharedFile = Path.Combine(tempDir, "shared.otel.yaml");
            File.WriteAllText(sharedFile, """
                schema:
                  name: "Shared"
                  version: "1.0.0"
                  namespace: "Test.Shared"
                fields:
                  userId:
                    type: string
                """);

            var mainFile = Path.Combine(tempDir, "main.otel.yaml");
            File.WriteAllText(mainFile, """
                schema:
                  name: "Main"
                  version: "1.0.0"
                  namespace: "Test.Main"
                imports:
                  - "shared.otel.yaml"
                events:
                  test.event:
                    id: 1
                    severity: INFO
                    message: "Test"
                """);

            var merger = new SchemaMerger(_parser, _validator);
            var result = merger.MergeFromFile(mainFile);

            Assert.True(result.IsSuccess, $"Errors: {string.Join(", ", result.Errors)}");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void MergeFromFile_ImportInSubdirectory_Succeeds()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"schema-security-{Guid.NewGuid():N}");
        var subDir = Path.Combine(tempDir, "sub");
        Directory.CreateDirectory(subDir);
        try
        {
            var sharedFile = Path.Combine(subDir, "shared.otel.yaml");
            File.WriteAllText(sharedFile, """
                schema:
                  name: "Shared"
                  version: "1.0.0"
                  namespace: "Test.Shared"
                fields:
                  userId:
                    type: string
                """);

            var mainFile = Path.Combine(tempDir, "main.otel.yaml");
            File.WriteAllText(mainFile, """
                schema:
                  name: "Main"
                  version: "1.0.0"
                  namespace: "Test.Main"
                imports:
                  - "sub/shared.otel.yaml"
                events:
                  test.event:
                    id: 1
                    severity: INFO
                    message: "Test"
                """);

            var merger = new SchemaMerger(_parser, _validator);
            var result = merger.MergeFromFile(mainFile);

            Assert.True(result.IsSuccess, $"Errors: {string.Join(", ", result.Errors)}");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 8. LOCALE-INDEPENDENT PARSING — double.Parse must use
    //    InvariantCulture for metric bucket boundaries
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Parse_MetricBuckets_ParsesWithInvariantCulture()
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
                  test.duration:
                    type: histogram
                    unit: ms
                    description: "Duration"
                    buckets:
                      - 0.5
                      - 1.0
                      - 2.5
                      - 10.0
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess, $"Parse failed: {string.Join(", ", result.Errors)}");
        var metric = result.Document!.Events[0].Metrics[0];
        Assert.NotNull(metric.Buckets);
        Assert.Equal(4, metric.Buckets!.Count);
        Assert.Equal(0.5, metric.Buckets[0]);
        Assert.Equal(2.5, metric.Buckets[2]);
    }

    // ═══════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════

    private static SchemaDocument CreateDocWithEnum(string enumName, List<string> values)
    {
        return new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = "TestService",
                Version = "1.0.0",
                Namespace = "Test.Events"
            },
            Enums =
            [
                new EnumDefinition
                {
                    Name = enumName,
                    Values = values
                }
            ]
        };
    }

    private static SchemaDocument CreateMinimalDoc(
        string name = "TestService",
        string ns = "Test.Events",
        string version = "1.0.0")
    {
        return new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = name,
                Version = version,
                Namespace = ns
            }
        };
    }

    private static SchemaDocument CreateSchemaWithEventAndMeter(
        string meterName = "Test.Meter",
        string version = "1.0.0")
    {
        return new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = "TestService",
                Version = version,
                Namespace = "Test.Events",
                MeterName = meterName
            },
            Events =
            [
                new EventDefinition
                {
                    Name = "test.event",
                    Id = 1,
                    Severity = Severity.Info,
                    Message = "Test",
                    Metrics =
                    [
                        new MetricDefinition
                        {
                            Name = "test.count",
                            Type = MetricType.Counter
                        }
                    ]
                }
            ]
        };
    }
}
