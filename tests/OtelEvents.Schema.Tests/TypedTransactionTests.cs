using OtelEvents.Schema.Models;
using OtelEvents.Schema.Parsing;
using OtelEvents.Schema.Validation;

namespace OtelEvents.Schema.Tests;

/// <summary>
/// Tests for typed transaction features: EventType/ParentEvent parsing,
/// validation rules OTEL_SCHEMA_028–031, and code generation for start/success/failure events.
/// </summary>
public class TypedTransactionTests
{
    private readonly SchemaParser _parser = new();
    private readonly SchemaValidator _validator = new();

    // ═══════════════════════════════════════════════════════════════
    // SCHEMA PARSING — type/parent fields
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Parse_EventWithTypeStart_ParsesEventTypeCorrectly()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              new.order:
                id: 1001
                severity: INFO
                type: start
                message: "New order {orderId}"
                fields:
                  orderId:
                    type: string
                    required: true
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        var evt = result.Document!.Events[0];
        Assert.Equal(EventType.Start, evt.EventType);
        Assert.Equal("start", evt.RawEventType);
        Assert.Null(evt.ParentEvent);
    }

    [Fact]
    public void Parse_EventWithTypeSuccessAndParent_ParsesBothFields()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              new.order:
                id: 1001
                severity: INFO
                type: start
                message: "New order {orderId}"
                fields:
                  orderId:
                    type: string
                    required: true
              order.shipped:
                id: 1002
                severity: INFO
                type: success
                parent: new.order
                message: "Order {orderId} shipped"
                fields:
                  orderId:
                    type: string
                    required: true
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        var successEvt = result.Document!.Events[1];
        Assert.Equal(EventType.Success, successEvt.EventType);
        Assert.Equal("new.order", successEvt.ParentEvent);
    }

    [Fact]
    public void Parse_EventWithTypeFailureAndParent_ParsesBothFields()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              new.order:
                id: 1001
                severity: INFO
                type: start
                message: "New order {orderId}"
                fields:
                  orderId:
                    type: string
                    required: true
              order.payment.declined:
                id: 1003
                severity: ERROR
                type: failure
                parent: new.order
                message: "Order {orderId} payment declined"
                fields:
                  orderId:
                    type: string
                    required: true
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        var failureEvt = result.Document!.Events[1];
        Assert.Equal(EventType.Failure, failureEvt.EventType);
        Assert.Equal("new.order", failureEvt.ParentEvent);
    }

    [Fact]
    public void Parse_EventWithNoType_DefaultsToEventType()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              order.updated:
                id: 1001
                severity: INFO
                message: "Order updated"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        var evt = result.Document!.Events[0];
        Assert.Equal(EventType.Event, evt.EventType);
        Assert.Null(evt.RawEventType);
        Assert.Null(evt.ParentEvent);
    }

    [Fact]
    public void Parse_EventWithExplicitTypeEvent_ParsesCorrectly()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              order.note.added:
                id: 1001
                severity: INFO
                type: event
                message: "Note added"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        Assert.Equal(EventType.Event, result.Document!.Events[0].EventType);
        Assert.Equal("event", result.Document!.Events[0].RawEventType);
    }

    [Fact]
    public void Parse_EventWithInvalidType_ParsesWithRawType()
    {
        // Invalid type should parse but be caught by validation
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              order.updated:
                id: 1001
                severity: INFO
                type: invalid_type
                message: "Order updated"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        Assert.Equal("invalid_type", result.Document!.Events[0].RawEventType);
    }

    // ═══════════════════════════════════════════════════════════════
    // VALIDATION — OTEL_SCHEMA_028: Invalid event type
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Validate_InvalidEventType_ReturnsOTEL_SCHEMA_028()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              order.updated:
                id: 1001
                severity: INFO
                type: BOGUS
                message: "Order updated"
            """;

        var result = ParseAndValidate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidEventType);
        Assert.Contains(result.Errors, e => e.Message.Contains("BOGUS"));
    }

    [Theory]
    [InlineData("start")]
    [InlineData("success")]
    [InlineData("failure")]
    [InlineData("event")]
    public void Validate_ValidEventTypes_NoError(string eventType)
    {
        // For success/failure, need a parent and a start event
        var yaml = eventType switch
        {
            "start" => $"""
                schema:
                  name: "TestService"
                  version: "1.0.0"
                  namespace: "Test.Namespace"
                events:
                  test.event:
                    id: 1
                    severity: INFO
                    type: {eventType}
                    message: "Test"
                """,
            "success" or "failure" => $"""
                schema:
                  name: "TestService"
                  version: "1.0.0"
                  namespace: "Test.Namespace"
                events:
                  test.start:
                    id: 1
                    severity: INFO
                    type: start
                    message: "Start"
                  test.outcome:
                    id: 2
                    severity: INFO
                    type: {eventType}
                    parent: test.start
                    message: "Outcome"
                """,
            _ => $"""
                schema:
                  name: "TestService"
                  version: "1.0.0"
                  namespace: "Test.Namespace"
                events:
                  test.event:
                    id: 1
                    severity: INFO
                    type: {eventType}
                    message: "Test"
                """
        };

        var result = ParseAndValidate(yaml);

        Assert.True(result.IsValid, $"Errors: {string.Join(", ", result.Errors)}");
    }

    // ═══════════════════════════════════════════════════════════════
    // VALIDATION — OTEL_SCHEMA_029: Success/failure must have parent
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Validate_SuccessWithoutParent_ReturnsOTEL_SCHEMA_029()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              order.shipped:
                id: 1001
                severity: INFO
                type: success
                message: "Order shipped"
            """;

        var result = ParseAndValidate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.MissingParentEvent);
    }

    [Fact]
    public void Validate_FailureWithoutParent_ReturnsOTEL_SCHEMA_029()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              order.failed:
                id: 1001
                severity: ERROR
                type: failure
                message: "Order failed"
            """;

        var result = ParseAndValidate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.MissingParentEvent);
    }

    // ═══════════════════════════════════════════════════════════════
    // VALIDATION — OTEL_SCHEMA_030: Parent must reference valid start event
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Validate_ParentReferencesNonExistentEvent_ReturnsOTEL_SCHEMA_030()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              order.shipped:
                id: 1001
                severity: INFO
                type: success
                parent: non.existent.event
                message: "Order shipped"
            """;

        var result = ParseAndValidate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidParentEvent);
    }

    [Fact]
    public void Validate_ParentReferencesNonStartEvent_ReturnsOTEL_SCHEMA_030()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              order.updated:
                id: 1001
                severity: INFO
                message: "Order updated"
              order.shipped:
                id: 1002
                severity: INFO
                type: success
                parent: order.updated
                message: "Order shipped"
            """;

        var result = ParseAndValidate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidParentEvent);
    }

    // ═══════════════════════════════════════════════════════════════
    // VALIDATION — OTEL_SCHEMA_031: Start events must not have parent
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Validate_StartEventWithParent_ReturnsOTEL_SCHEMA_031()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              outer.start:
                id: 1001
                severity: INFO
                type: start
                message: "Outer start"
              inner.start:
                id: 1002
                severity: INFO
                type: start
                parent: outer.start
                message: "Inner start with parent"
            """;

        var result = ParseAndValidate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.StartEventWithParent);
    }

    // ═══════════════════════════════════════════════════════════════
    // VALIDATION — Full transaction lifecycle passes
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Validate_CompleteTransactionLifecycle_NoErrors()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              new.order:
                id: 1001
                severity: INFO
                type: start
                message: "New order {orderId}"
                fields:
                  orderId:
                    type: string
                    required: true
              order.shipped:
                id: 1002
                severity: INFO
                type: success
                parent: new.order
                message: "Order {orderId} shipped"
                fields:
                  orderId:
                    type: string
                    required: true
              order.payment.declined:
                id: 1003
                severity: ERROR
                type: failure
                parent: new.order
                message: "Order {orderId} declined"
                fields:
                  orderId:
                    type: string
                    required: true
            """;

        var result = ParseAndValidate(yaml);

        Assert.True(result.IsValid, $"Errors: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void Validate_MultipleStartEventsWithDifferentOutcomes_NoErrors()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              new.order:
                id: 1001
                severity: INFO
                type: start
                message: "New order"
              new.payment:
                id: 1002
                severity: INFO
                type: start
                message: "New payment"
              order.completed:
                id: 1003
                severity: INFO
                type: success
                parent: new.order
                message: "Order completed"
              payment.confirmed:
                id: 1004
                severity: INFO
                type: success
                parent: new.payment
                message: "Payment confirmed"
            """;

        var result = ParseAndValidate(yaml);

        Assert.True(result.IsValid, $"Errors: {string.Join(", ", result.Errors)}");
    }

    // ═══════════════════════════════════════════════════════════════
    // CODE GENERATION — Start events generate Begin methods
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void CodeGen_StartEvent_GeneratesBeginMethodReturningTransactionScope()
    {
        var doc = CreateSchemaWithTransactionEvents();
        var generator = new CodeGen.CodeGenerator();

        var files = generator.GenerateFromSchema(doc);
        var eventsFile = files.First(f => f.FileName.Contains("Events"));

        // Should have Begin method, not Emit method
        Assert.Contains("public static OtelEventsTransactionScope BeginNewOrder(", eventsFile.Content);
        Assert.DoesNotContain("EmitNewOrder", eventsFile.Content);
    }

    [Fact]
    public void CodeGen_StartEvent_GeneratesTransactionScopeReturn()
    {
        var doc = CreateSchemaWithTransactionEvents();
        var generator = new CodeGen.CodeGenerator();

        var files = generator.GenerateFromSchema(doc);
        var eventsFile = files.First(f => f.FileName.Contains("Events"));

        Assert.Contains("return OtelEventsTransactionScope.Begin(\"new.order\")", eventsFile.Content);
    }

    [Fact]
    public void CodeGen_StartEvent_EmitsUsingOtelEventsCausality()
    {
        var doc = CreateSchemaWithTransactionEvents();
        var generator = new CodeGen.CodeGenerator();

        var files = generator.GenerateFromSchema(doc);
        var eventsFile = files.First(f => f.FileName.Contains("Events"));

        Assert.Contains("using OtelEvents.Causality;", eventsFile.Content);
    }

    // ═══════════════════════════════════════════════════════════════
    // CODE GENERATION — Success events generate TryComplete calls
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void CodeGen_SuccessEvent_GeneratesTryCompleteCall()
    {
        var doc = CreateSchemaWithTransactionEvents();
        var generator = new CodeGen.CodeGenerator();

        var files = generator.GenerateFromSchema(doc);
        var eventsFile = files.First(f => f.FileName.Contains("Events"));

        Assert.Contains("OtelEventsTransactionScope.TryComplete(\"new.order\", \"order.shipped\")", eventsFile.Content);
    }

    [Fact]
    public void CodeGen_SuccessEvent_GeneratesStandardEmitMethod()
    {
        var doc = CreateSchemaWithTransactionEvents();
        var generator = new CodeGen.CodeGenerator();

        var files = generator.GenerateFromSchema(doc);
        var eventsFile = files.First(f => f.FileName.Contains("Events"));

        Assert.Contains("public static void EmitOrderShipped(", eventsFile.Content);
    }

    // ═══════════════════════════════════════════════════════════════
    // CODE GENERATION — Failure events generate TryFail calls
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void CodeGen_FailureEvent_GeneratesTryFailCall()
    {
        var doc = CreateSchemaWithTransactionEvents();
        var generator = new CodeGen.CodeGenerator();

        var files = generator.GenerateFromSchema(doc);
        var eventsFile = files.First(f => f.FileName.Contains("Events"));

        Assert.Contains("OtelEventsTransactionScope.TryFail(\"new.order\", \"order.payment.declined\")", eventsFile.Content);
    }

    [Fact]
    public void CodeGen_FailureEvent_GeneratesStandardEmitMethod()
    {
        var doc = CreateSchemaWithTransactionEvents();
        var generator = new CodeGen.CodeGenerator();

        var files = generator.GenerateFromSchema(doc);
        var eventsFile = files.First(f => f.FileName.Contains("Events"));

        Assert.Contains("public static void EmitOrderPaymentDeclined(", eventsFile.Content);
    }

    // ═══════════════════════════════════════════════════════════════
    // CODE GENERATION — Default events unchanged
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void CodeGen_DefaultEvent_GeneratesStandardEmitMethodWithoutTransactionCode()
    {
        var doc = CreateSchemaWithEvent(new EventDefinition
        {
            Name = "order.updated",
            Id = 1001,
            Severity = Severity.Info,
            Message = "Order updated"
        });
        var generator = new CodeGen.CodeGenerator();

        var files = generator.GenerateFromSchema(doc);
        var eventsFile = files.First(f => f.FileName.Contains("Events"));

        Assert.Contains("public static void EmitOrderUpdated(", eventsFile.Content);
        Assert.DoesNotContain("OtelEventsTransactionScope", eventsFile.Content);
        Assert.DoesNotContain("using OtelEvents.Causality;", eventsFile.Content);
    }

    [Fact]
    public void CodeGen_StartEventWithFields_IncludesFieldParameters()
    {
        var doc = CreateSchemaWithTransactionEvents();
        var generator = new CodeGen.CodeGenerator();

        var files = generator.GenerateFromSchema(doc);
        var eventsFile = files.First(f => f.FileName.Contains("Events"));

        // The BeginNewOrder method should include the orderId parameter
        Assert.Contains("BeginNewOrder(this ILogger logger, string orderId", eventsFile.Content);
    }

    [Fact]
    public void CodeGen_StartEvent_CallsLoggerMessageMethod()
    {
        var doc = CreateSchemaWithTransactionEvents();
        var generator = new CodeGen.CodeGenerator();

        var files = generator.GenerateFromSchema(doc);
        var eventsFile = files.First(f => f.FileName.Contains("Events"));

        // The generated method should log via LoggerExtensions before returning scope
        Assert.Contains("TestServiceLoggerExtensions.NewOrder(logger", eventsFile.Content);
    }

    // ═══════════════════════════════════════════════════════════════
    // CODE GENERATION — Start event with metrics
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void CodeGen_StartEventWithMetrics_IncludesMetricRecording()
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
                    Name = "new.order",
                    Id = 1001,
                    Severity = Severity.Info,
                    Message = "New order {orderId}",
                    EventType = EventType.Start,
                    Fields =
                    [
                        new FieldDefinition { Name = "orderId", Required = true }
                    ],
                    Metrics =
                    [
                        new MetricDefinition { Name = "new.order.count", Type = MetricType.Counter, RawType = "counter" }
                    ]
                }
            ]
        };

        var generator = new CodeGen.CodeGenerator();
        var files = generator.GenerateFromSchema(doc);
        var eventsFile = files.First(f => f.FileName.Contains("Events"));

        // Should have both the metric recording and the transaction scope
        Assert.Contains(".Add(1", eventsFile.Content);
        Assert.Contains("OtelEventsTransactionScope.Begin(", eventsFile.Content);
    }

    // ═══════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════

    private ValidationResult ParseAndValidate(string yaml)
    {
        var parseResult = _parser.Parse(yaml, yaml.Length);
        Assert.True(parseResult.IsSuccess, $"Parse failed: {string.Join(", ", parseResult.Errors)}");
        return _validator.Validate(parseResult.Document!);
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

    private static SchemaDocument CreateSchemaWithTransactionEvents() => new()
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
                Name = "new.order",
                Id = 1001,
                Severity = Severity.Info,
                Message = "New order {orderId}",
                EventType = EventType.Start,
                Fields =
                [
                    new FieldDefinition { Name = "orderId", Required = true }
                ]
            },
            new EventDefinition
            {
                Name = "order.shipped",
                Id = 1002,
                Severity = Severity.Info,
                Message = "Order {orderId} shipped",
                EventType = EventType.Success,
                ParentEvent = "new.order",
                Fields =
                [
                    new FieldDefinition { Name = "orderId", Required = true }
                ]
            },
            new EventDefinition
            {
                Name = "order.payment.declined",
                Id = 1003,
                Severity = Severity.Error,
                Message = "Order {orderId} payment declined",
                EventType = EventType.Failure,
                ParentEvent = "new.order",
                Fields =
                [
                    new FieldDefinition { Name = "orderId", Required = true }
                ]
            }
        ]
    };
}
