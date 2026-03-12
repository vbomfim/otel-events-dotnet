using OtelEvents.Schema.CodeGen;
using OtelEvents.Schema.Models;
using OtelEvents.Schema.Parsing;
using OtelEvents.Schema.Validation;

namespace OtelEvents.Schema.Tests;

/// <summary>
/// Tests for Schema v3 features:
///   - PascalCase event names (OTEL_SCHEMA_006 update)
///   - String event code with prefix
///   - Generated event constants class
///   - Per-event prefix override
///   - Backward compatibility with dot.namespaced names
/// </summary>
public class SchemaV3Tests
{
    private readonly SchemaParser _parser = new();
    private readonly SchemaValidator _validator = new();
    private readonly CodeGenerator _generator = new();

    // ═══════════════════════════════════════════════════════════════
    // 1. PASCALCASE EVENT NAMES — Parser & Validator
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Parse_PascalCaseEventName_ReturnsSuccess()
    {
        var yaml = """
            schema:
              name: "Orders"
              version: "3.0.0"
              namespace: "Test.Events"
            events:
              OrderPlaced:
                id: 1000
                severity: INFO
                message: "Order {orderId} placed"
                fields:
                  - orderId
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Document!.Events);
        Assert.Equal("OrderPlaced", result.Document.Events[0].Name);
    }

    [Theory]
    [InlineData("OrderPlaced")]
    [InlineData("HttpRequestReceived")]
    [InlineData("A")]
    [InlineData("OrderPlaced2")]
    [InlineData("X1Y2Z3")]
    public void Validate_PascalCaseEventName_NoError(string eventName)
    {
        var doc = CreateMinimalDoc(events:
        [
            CreateEvent(eventName, 1),
        ]);

        var result = _validator.Validate(doc);

        Assert.DoesNotContain(result.Errors, e => e.Code == ErrorCodes.InvalidEventNameFormat);
    }

    [Theory]
    [InlineData("http.request")]
    [InlineData("order.placed")]
    [InlineData("db.query.executed")]
    public void Validate_DotNamespacedEventName_StillValid(string eventName)
    {
        var doc = CreateMinimalDoc(events:
        [
            CreateEvent(eventName, 1),
        ]);

        var result = _validator.Validate(doc);

        Assert.DoesNotContain(result.Errors, e => e.Code == ErrorCodes.InvalidEventNameFormat);
    }

    [Theory]
    [InlineData("lowerCase")]
    [InlineData("camelCase")]
    [InlineData("snake_case")]
    [InlineData("has spaces")]
    [InlineData("kebab-case")]
    [InlineData("123StartsWithNumber")]
    public void Validate_InvalidEventNameFormat_ReturnsError(string eventName)
    {
        var doc = CreateMinimalDoc(events:
        [
            CreateEvent(eventName, 1),
        ]);

        var result = _validator.Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidEventNameFormat);
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. PREFIX PARSING — Schema header & per-event
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Parse_PrefixInSchemaHeader_ParsesCorrectly()
    {
        var yaml = """
            schema:
              name: "Orders"
              version: "3.0.0"
              namespace: "Test.Events"
              prefix: ORDER
            events:
              OrderPlaced:
                id: 1000
                severity: INFO
                message: "Order placed"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        Assert.Equal("ORDER", result.Document!.Schema.Prefix);
    }

    [Fact]
    public void Parse_NoPrefixInHeader_PrefixIsNull()
    {
        var yaml = """
            schema:
              name: "Orders"
              version: "3.0.0"
              namespace: "Test.Events"
            events:
              OrderPlaced:
                id: 1000
                severity: INFO
                message: "Order placed"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Document!.Schema.Prefix);
    }

    [Fact]
    public void Parse_PerEventPrefixOverride_ParsesCorrectly()
    {
        var yaml = """
            schema:
              name: "Orders"
              version: "3.0.0"
              namespace: "Test.Events"
              prefix: ORDER
            events:
              PaymentProcessed:
                id: 2000
                severity: INFO
                message: "Payment processed"
                prefix: PAY
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        Assert.Equal("PAY", result.Document!.Events[0].Prefix);
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. EVENT CODE COMPUTATION
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ComputeEventCode_WithPrefix_ReturnsPrefixDashId()
    {
        var evt = CreateEvent("OrderPlaced", 1000);

        var code = CodeGenerator.ComputeEventCode(evt, "ORDER");

        Assert.Equal("ORDER-1000", code);
    }

    [Fact]
    public void ComputeEventCode_NoPrefix_ReturnsIdOnly()
    {
        var evt = CreateEvent("OrderPlaced", 1000);

        var code = CodeGenerator.ComputeEventCode(evt, null);

        Assert.Equal("1000", code);
    }

    [Fact]
    public void ComputeEventCode_PerEventPrefixOverridesSchema()
    {
        var evt = new EventDefinition
        {
            Name = "PaymentProcessed",
            Id = 2000,
            Severity = Severity.Info,
            Message = "Payment processed",
            Prefix = "PAY"
        };

        var code = CodeGenerator.ComputeEventCode(evt, "ORDER");

        Assert.Equal("PAY-2000", code);
    }

    [Fact]
    public void ComputeEventCode_PerEventPrefixWithoutSchemaPrefix()
    {
        var evt = new EventDefinition
        {
            Name = "PaymentProcessed",
            Id = 2000,
            Severity = Severity.Info,
            Message = "Payment processed",
            Prefix = "PAY"
        };

        var code = CodeGenerator.ComputeEventCode(evt, null);

        Assert.Equal("PAY-2000", code);
    }

    // ═══════════════════════════════════════════════════════════════
    // 4. CODE GENERATOR — Event code in LoggerMessage
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Generate_LoggerMessage_EventNameIsEventCode_WithPrefix()
    {
        var doc = CreateSchemaWithPrefix("ORDER", new EventDefinition
        {
            Name = "OrderPlaced",
            Id = 1000,
            Severity = Severity.Info,
            Message = "Order placed"
        });

        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        Assert.Contains("EventName = \"ORDER-1000\"", content);
    }

    [Fact]
    public void Generate_LoggerMessage_EventNameIsNumericId_NoPrefix()
    {
        var doc = CreateSchemaWithEvent(new EventDefinition
        {
            Name = "OrderPlaced",
            Id = 1000,
            Severity = Severity.Info,
            Message = "Order placed"
        });

        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        Assert.Contains("EventName = \"1000\"", content);
    }

    [Fact]
    public void Generate_LoggerMessage_PerEventPrefix_OverridesSchemaPrefix()
    {
        var doc = new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = "TestService",
                Version = "1.0.0",
                Namespace = "Test.Events",
                Prefix = "ORDER"
            },
            Events =
            [
                new EventDefinition
                {
                    Name = "PaymentProcessed",
                    Id = 2000,
                    Severity = Severity.Info,
                    Message = "Payment processed",
                    Prefix = "PAY"
                }
            ]
        };

        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        Assert.Contains("EventName = \"PAY-2000\"", content);
    }

    // ═══════════════════════════════════════════════════════════════
    // 5. CODE GENERATOR — Constants class
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Generate_ConstantsClass_WithPrefix()
    {
        var doc = CreateSchemaWithPrefix("ORDER", new EventDefinition
        {
            Name = "OrderPlaced",
            Id = 1000,
            Severity = Severity.Info,
            Message = "Order placed"
        });

        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        Assert.Contains("public static class OrderPlaced", content);
        Assert.Contains("public const string Code = \"ORDER-1000\";", content);
        Assert.Contains("public const int NumericId = 1000;", content);
        Assert.Contains("public const string EventName = \"OrderPlaced\";", content);
    }

    [Fact]
    public void Generate_ConstantsClass_WithoutPrefix()
    {
        var doc = CreateSchemaWithEvent(new EventDefinition
        {
            Name = "OrderPlaced",
            Id = 500,
            Severity = Severity.Info,
            Message = "Order placed"
        });

        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        Assert.Contains("public static class OrderPlaced", content);
        Assert.Contains("public const string Code = \"500\";", content);
        Assert.Contains("public const int NumericId = 500;", content);
        Assert.Contains("public const string EventName = \"OrderPlaced\";", content);
    }

    [Fact]
    public void Generate_ConstantsClass_DotNamespacedEvent_UsesMethodNameAsClassName()
    {
        var doc = CreateSchemaWithPrefix("SVC", new EventDefinition
        {
            Name = "order.placed",
            Id = 100,
            Severity = Severity.Info,
            Message = "Order placed"
        });

        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        // dot-namespaced "order.placed" → PascalCase class "OrderPlaced"
        Assert.Contains("public static class OrderPlaced", content);
        Assert.Contains("public const string Code = \"SVC-100\";", content);
        Assert.Contains("public const string EventName = \"OrderPlaced\";", content);
    }

    [Fact]
    public void Generate_ConstantsClass_MultipleEvents_GeneratesMultipleClasses()
    {
        var doc = new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = "TestService",
                Version = "1.0.0",
                Namespace = "Test.Events",
                Prefix = "ORD"
            },
            Events =
            [
                new EventDefinition
                {
                    Name = "OrderPlaced",
                    Id = 1000,
                    Severity = Severity.Info,
                    Message = "Order placed"
                },
                new EventDefinition
                {
                    Name = "OrderFailed",
                    Id = 2000,
                    Severity = Severity.Error,
                    Message = "Order failed"
                }
            ]
        };

        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        Assert.Contains("public static class OrderPlaced", content);
        Assert.Contains("public const string Code = \"ORD-1000\";", content);

        Assert.Contains("public static class OrderFailed", content);
        Assert.Contains("public const string Code = \"ORD-2000\";", content);
    }

    [Fact]
    public void Generate_ConstantsClass_PerEventPrefixOverride()
    {
        var doc = new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = "TestService",
                Version = "1.0.0",
                Namespace = "Test.Events",
                Prefix = "ORDER"
            },
            Events =
            [
                new EventDefinition
                {
                    Name = "OrderPlaced",
                    Id = 1000,
                    Severity = Severity.Info,
                    Message = "Order placed"
                },
                new EventDefinition
                {
                    Name = "PaymentProcessed",
                    Id = 2000,
                    Severity = Severity.Info,
                    Message = "Payment processed",
                    Prefix = "PAY"
                }
            ]
        };

        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        Assert.Contains("public const string Code = \"ORDER-1000\";", content);
        Assert.Contains("public const string Code = \"PAY-2000\";", content);
    }

    // ═══════════════════════════════════════════════════════════════
    // 6. END-TO-END: Parse → Validate → Generate
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void EndToEnd_PascalCaseWithPrefix_ProducesCorrectOutput()
    {
        var yaml = """
            schema:
              name: "Orders"
              version: "3.0.0"
              namespace: "Test.Events"
              prefix: ORDER
            events:
              OrderPlaced:
                id: 1000
                severity: INFO
                message: "Order {orderId} placed"
                fields:
                  - orderId
              OrderFailed:
                id: 2000
                severity: ERROR
                message: "Order {orderId} failed: {reason}"
                fields:
                  - orderId
                  - reason
            """;

        // Parse
        var parseResult = _parser.Parse(yaml, yaml.Length);
        Assert.True(parseResult.IsSuccess);

        // Validate
        var validationResult = _validator.Validate(parseResult.Document!);
        Assert.True(validationResult.IsValid, string.Join(", ", validationResult.Errors));

        // Generate
        var files = _generator.GenerateFromSchema(parseResult.Document!);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        // Verify constants
        Assert.Contains("public static class OrderPlaced", content);
        Assert.Contains("public const string Code = \"ORDER-1000\";", content);

        Assert.Contains("public static class OrderFailed", content);
        Assert.Contains("public const string Code = \"ORDER-2000\";", content);

        // Verify LoggerMessage uses event code as EventName
        Assert.Contains("EventName = \"ORDER-1000\"", content);
        Assert.Contains("EventName = \"ORDER-2000\"", content);
    }

    [Fact]
    public void EndToEnd_NoPrefix_CodeIsJustNumericId()
    {
        var yaml = """
            schema:
              name: "Orders"
              version: "3.0.0"
              namespace: "Test.Events"
            events:
              OrderPlaced:
                id: 1000
                severity: INFO
                message: "Order placed"
            """;

        var parseResult = _parser.Parse(yaml, yaml.Length);
        Assert.True(parseResult.IsSuccess);

        var validationResult = _validator.Validate(parseResult.Document!);
        Assert.True(validationResult.IsValid, string.Join(", ", validationResult.Errors));

        var files = _generator.GenerateFromSchema(parseResult.Document!);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        Assert.Contains("public const string Code = \"1000\";", content);
        Assert.Contains("EventName = \"1000\"", content);
    }

    [Fact]
    public void EndToEnd_DotNamespacedBackwardCompat_StillWorks()
    {
        var yaml = """
            schema:
              name: "Orders"
              version: "3.0.0"
              namespace: "Test.Events"
              prefix: SVC
            events:
              order.placed:
                id: 100
                severity: INFO
                message: "Order {orderId} placed"
                fields:
                  - orderId
            """;

        var parseResult = _parser.Parse(yaml, yaml.Length);
        Assert.True(parseResult.IsSuccess);

        var validationResult = _validator.Validate(parseResult.Document!);
        Assert.True(validationResult.IsValid, string.Join(", ", validationResult.Errors));

        var files = _generator.GenerateFromSchema(parseResult.Document!);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        // Dot-namespaced names still generate PascalCase method names
        Assert.Contains("public static class OrderPlaced", content);
        Assert.Contains("public const string Code = \"SVC-100\";", content);
        Assert.Contains("EventName = \"SVC-100\"", content);
    }

    [Fact]
    public void EndToEnd_TransactionLifecycle_WithPascalCase()
    {
        var yaml = """
            schema:
              name: "Orders"
              version: "3.0.0"
              namespace: "Test.Events"
              prefix: ORDER
            events:
              OrderPlaced:
                id: 1000
                type: start
                severity: INFO
                message: "Order {orderId} placed"
                fields:
                  - orderId
              OrderCompleted:
                id: 1001
                type: success
                parent: OrderPlaced
                severity: INFO
                message: "Order {orderId} completed"
                fields:
                  - orderId
              OrderFailed:
                id: 2000
                type: failure
                parent: OrderPlaced
                severity: ERROR
                message: "Order {orderId} failed"
                exception: true
                fields:
                  - orderId
            """;

        var parseResult = _parser.Parse(yaml, yaml.Length);
        Assert.True(parseResult.IsSuccess);

        var validationResult = _validator.Validate(parseResult.Document!);
        Assert.True(validationResult.IsValid, string.Join(", ", validationResult.Errors));

        var files = _generator.GenerateFromSchema(parseResult.Document!);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        // Start event should generate Begin method
        Assert.Contains("BeginOrderPlaced", content);
        // Success/failure should reference parent by PascalCase name
        Assert.Contains("OtelEventsTransactionScope.TryComplete(\"OrderPlaced\"", content);
        Assert.Contains("OtelEventsTransactionScope.TryFail(\"OrderPlaced\"", content);
    }

    // ═══════════════════════════════════════════════════════════════
    // 7. CONSTANTS CLASS HAS XML DOC COMMENTS
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Generate_ConstantsClass_HasXmlDocComments()
    {
        var doc = CreateSchemaWithPrefix("ORDER", new EventDefinition
        {
            Name = "OrderPlaced",
            Id = 1000,
            Severity = Severity.Info,
            Message = "Order placed"
        });

        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        Assert.Contains("/// <summary>", content);
        Assert.Contains("Constants for the OrderPlaced event", content);
    }

    // ═══════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════

    private static SchemaDocument CreateMinimalDoc(
        string version = "1.0.0",
        List<EventDefinition>? events = null)
    {
        return new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = "TestService",
                Version = version,
                Namespace = "Test.Namespace",
            },
            Events = events ?? []
        };
    }

    private static SchemaDocument CreateSchemaWithEvent(
        EventDefinition evt,
        string name = "TestService",
        string ns = "Test.Events") => new()
    {
        Schema = new SchemaHeader
        {
            Name = name,
            Version = "1.0.0",
            Namespace = ns
        },
        Events = [evt]
    };

    private static SchemaDocument CreateSchemaWithPrefix(
        string prefix,
        EventDefinition evt,
        string name = "TestService",
        string ns = "Test.Events") => new()
    {
        Schema = new SchemaHeader
        {
            Name = name,
            Version = "1.0.0",
            Namespace = ns,
            Prefix = prefix
        },
        Events = [evt]
    };

    private static EventDefinition CreateEvent(string name, int id)
    {
        return new EventDefinition
        {
            Name = name,
            Id = id,
            Severity = Severity.Info,
            Message = "Test event"
        };
    }
}
