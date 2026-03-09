using All.Schema.Documentation;
using All.Schema.Models;

namespace All.Schema.Tests.Documentation;

/// <summary>
/// Tests for SchemaDocumentationGenerator — validates generated Markdown
/// documentation from schema definitions including events, fields, metrics,
/// enums, table of contents, and formatting.
/// </summary>
public class SchemaDocumentationGeneratorTests
{
    private readonly SchemaDocumentationGenerator _generator = new();

    // ── Helper: minimal valid SchemaDocument ───────────────────────

    private static SchemaDocument CreateMinimalSchema(
        string name = "TestService",
        string ns = "Test.Events",
        string version = "1.0.0",
        string? description = null) => new()
    {
        Schema = new SchemaHeader
        {
            Name = name,
            Version = version,
            Namespace = ns,
            Description = description
        }
    };

    private static SchemaDocument CreateSchemaWithEvent(
        EventDefinition evt,
        string name = "TestService",
        string ns = "Test.Events",
        string? description = null) => new()
    {
        Schema = new SchemaHeader
        {
            Name = name,
            Version = "1.0.0",
            Namespace = ns,
            Description = description
        },
        Events = [evt]
    };

    // ═══════════════════════════════════════════════════════════════
    // 1. BASIC STRUCTURE — Title, Version, Description
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateMarkdown_EmptySchema_ContainsTitleWithSchemaName()
    {
        var doc = CreateMinimalSchema(name: "OrderService");

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("# OrderService", markdown);
    }

    [Fact]
    public void GenerateMarkdown_EmptySchema_ContainsVersionBadge()
    {
        var doc = CreateMinimalSchema(version: "2.1.0");

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("2.1.0", markdown);
    }

    [Fact]
    public void GenerateMarkdown_WithDescription_ContainsDescription()
    {
        var doc = CreateMinimalSchema(description: "Schema for order processing events");

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("Schema for order processing events", markdown);
    }

    [Fact]
    public void GenerateMarkdown_EmptySchema_ContainsNamespace()
    {
        var doc = CreateMinimalSchema(ns: "MyCompany.Events");

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("MyCompany.Events", markdown);
    }

    [Fact]
    public void GenerateMarkdown_EmptySchema_NoEventsSection()
    {
        var doc = CreateMinimalSchema();

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.DoesNotContain("## Events", markdown);
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. TABLE OF CONTENTS
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateMarkdown_WithEvents_ContainsTableOfContents()
    {
        var doc = new SchemaDocument
        {
            Schema = new SchemaHeader { Name = "Test", Version = "1.0.0", Namespace = "Test.Events" },
            Events =
            [
                new EventDefinition { Name = "order.placed", Id = 1, Severity = Severity.Info, Message = "Order placed" },
                new EventDefinition { Name = "order.cancelled", Id = 2, Severity = Severity.Warn, Message = "Order cancelled" }
            ]
        };

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("## Table of Contents", markdown);
        Assert.Contains("[order.placed]", markdown);
        Assert.Contains("[order.cancelled]", markdown);
    }

    [Fact]
    public void GenerateMarkdown_WithEvents_TableOfContentsHasAnchors()
    {
        var doc = CreateSchemaWithEvent(new EventDefinition
        {
            Name = "http.request.received",
            Id = 1,
            Severity = Severity.Info,
            Message = "Request received"
        });

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("(#httprequestreceived)", markdown);
    }

    [Fact]
    public void GenerateMarkdown_WithEnums_TableOfContentsIncludesEnums()
    {
        var doc = new SchemaDocument
        {
            Schema = new SchemaHeader { Name = "Test", Version = "1.0.0", Namespace = "Test.Events" },
            Events = [new EventDefinition { Name = "e1", Id = 1, Severity = Severity.Info, Message = "msg" }],
            Enums = [new EnumDefinition { Name = "Status", Values = ["Active", "Inactive"] }]
        };

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("[Enum Definitions]", markdown);
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. EVENTS SECTION
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateMarkdown_Event_ContainsEventHeading()
    {
        var doc = CreateSchemaWithEvent(new EventDefinition
        {
            Name = "order.placed",
            Id = 1001,
            Severity = Severity.Info,
            Message = "Order {orderId} placed"
        });

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("### order.placed", markdown);
    }

    [Fact]
    public void GenerateMarkdown_Event_ContainsDescription()
    {
        var doc = CreateSchemaWithEvent(new EventDefinition
        {
            Name = "order.placed",
            Id = 1001,
            Severity = Severity.Info,
            Message = "Order placed",
            Description = "Emitted when a new order is successfully placed"
        });

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("Emitted when a new order is successfully placed", markdown);
    }

    [Fact]
    public void GenerateMarkdown_Event_ContainsSeverity()
    {
        var doc = CreateSchemaWithEvent(new EventDefinition
        {
            Name = "order.failed",
            Id = 1001,
            Severity = Severity.Error,
            Message = "Order failed"
        });

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("Error", markdown);
    }

    [Fact]
    public void GenerateMarkdown_Event_ContainsEventId()
    {
        var doc = CreateSchemaWithEvent(new EventDefinition
        {
            Name = "order.placed",
            Id = 1001,
            Severity = Severity.Info,
            Message = "Order placed"
        });

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("1001", markdown);
    }

    [Fact]
    public void GenerateMarkdown_Event_ContainsMessageTemplate()
    {
        var doc = CreateSchemaWithEvent(new EventDefinition
        {
            Name = "order.placed",
            Id = 1001,
            Severity = Severity.Info,
            Message = "Order {orderId} placed successfully"
        });

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("Order {orderId} placed successfully", markdown);
    }

    [Fact]
    public void GenerateMarkdown_EventWithException_IndicatesExceptionSupport()
    {
        var doc = CreateSchemaWithEvent(new EventDefinition
        {
            Name = "order.failed",
            Id = 1002,
            Severity = Severity.Error,
            Message = "Order failed",
            Exception = true
        });

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("Yes", markdown);
    }

    // ═══════════════════════════════════════════════════════════════
    // 4. FIELDS TABLE
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateMarkdown_EventWithFields_ContainsFieldsTable()
    {
        var doc = CreateSchemaWithEvent(new EventDefinition
        {
            Name = "order.placed",
            Id = 1001,
            Severity = Severity.Info,
            Message = "Order {orderId} placed",
            Fields =
            [
                new FieldDefinition { Name = "orderId", Type = FieldType.String, Required = true },
                new FieldDefinition { Name = "amount", Type = FieldType.Double, Required = false }
            ]
        });

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("| Name | Type | Required | Sensitivity |", markdown);
        Assert.Contains("orderId", markdown);
        Assert.Contains("string", markdown);
        Assert.Contains("amount", markdown);
        Assert.Contains("double", markdown);
    }

    [Fact]
    public void GenerateMarkdown_FieldRequired_ShowsYes()
    {
        var doc = CreateSchemaWithEvent(new EventDefinition
        {
            Name = "e1",
            Id = 1,
            Severity = Severity.Info,
            Message = "msg",
            Fields = [new FieldDefinition { Name = "f1", Type = FieldType.String, Required = true }]
        });

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("✓", markdown);
    }

    [Fact]
    public void GenerateMarkdown_FieldNotRequired_ShowsNo()
    {
        var doc = CreateSchemaWithEvent(new EventDefinition
        {
            Name = "e1",
            Id = 1,
            Severity = Severity.Info,
            Message = "msg",
            Fields = [new FieldDefinition { Name = "f1", Type = FieldType.String, Required = false }]
        });

        var markdown = _generator.GenerateMarkdown(doc);

        // The field row should NOT have ✓ for required
        var lines = markdown.Split('\n');
        var fieldRow = lines.First(l => l.Contains("| f1"));
        Assert.DoesNotContain("✓", fieldRow);
    }

    [Fact]
    public void GenerateMarkdown_FieldWithSensitivity_ShowsSensitivity()
    {
        var doc = CreateSchemaWithEvent(new EventDefinition
        {
            Name = "e1",
            Id = 1,
            Severity = Severity.Info,
            Message = "msg",
            Fields = [new FieldDefinition { Name = "email", Type = FieldType.String, Sensitivity = Sensitivity.Pii }]
        });

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("⚠️ Pii", markdown);
    }

    [Fact]
    public void GenerateMarkdown_FieldWithPublicSensitivity_ShowsPublic()
    {
        var doc = CreateSchemaWithEvent(new EventDefinition
        {
            Name = "e1",
            Id = 1,
            Severity = Severity.Info,
            Message = "msg",
            Fields = [new FieldDefinition { Name = "status", Type = FieldType.String, Sensitivity = Sensitivity.Public }]
        });

        var markdown = _generator.GenerateMarkdown(doc);

        var lines = markdown.Split('\n');
        var fieldRow = lines.First(l => l.Contains("| status"));
        Assert.Contains("Public", fieldRow);
    }

    [Fact]
    public void GenerateMarkdown_FieldWithDescription_IncludesDescription()
    {
        var doc = CreateSchemaWithEvent(new EventDefinition
        {
            Name = "e1",
            Id = 1,
            Severity = Severity.Info,
            Message = "msg",
            Fields = [new FieldDefinition { Name = "orderId", Type = FieldType.String, Description = "Unique order identifier" }]
        });

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("Unique order identifier", markdown);
    }

    [Fact]
    public void GenerateMarkdown_FieldWithUnit_IncludesUnit()
    {
        var doc = CreateSchemaWithEvent(new EventDefinition
        {
            Name = "e1",
            Id = 1,
            Severity = Severity.Info,
            Message = "msg",
            Fields = [new FieldDefinition { Name = "duration", Type = FieldType.Double, Unit = "ms" }]
        });

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("ms", markdown);
    }

    [Fact]
    public void GenerateMarkdown_EventWithNoFields_NoFieldsTable()
    {
        var doc = CreateSchemaWithEvent(new EventDefinition
        {
            Name = "e1",
            Id = 1,
            Severity = Severity.Info,
            Message = "msg"
        });

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.DoesNotContain("| Name | Type | Required | Sensitivity |", markdown);
    }

    [Fact]
    public void GenerateMarkdown_FieldWithEnumRef_ShowsEnumRef()
    {
        var doc = CreateSchemaWithEvent(new EventDefinition
        {
            Name = "e1",
            Id = 1,
            Severity = Severity.Info,
            Message = "msg",
            Fields = [new FieldDefinition { Name = "status", Type = FieldType.Enum, Ref = "OrderStatus" }]
        });

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("OrderStatus", markdown);
    }

    // ═══════════════════════════════════════════════════════════════
    // 5. METRICS TABLE
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateMarkdown_EventWithMetrics_ContainsMetricsTable()
    {
        var doc = CreateSchemaWithEvent(new EventDefinition
        {
            Name = "order.placed",
            Id = 1001,
            Severity = Severity.Info,
            Message = "Order placed",
            Metrics =
            [
                new MetricDefinition { Name = "order.placed.count", Type = MetricType.Counter, Unit = "orders" },
                new MetricDefinition { Name = "order.amount", Type = MetricType.Histogram, Unit = "USD" }
            ]
        });

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("| Name | Type | Unit |", markdown);
        Assert.Contains("order.placed.count", markdown);
        Assert.Contains("Counter", markdown);
        Assert.Contains("order.amount", markdown);
        Assert.Contains("Histogram", markdown);
    }

    [Fact]
    public void GenerateMarkdown_MetricWithLabels_ShowsLabels()
    {
        var doc = CreateSchemaWithEvent(new EventDefinition
        {
            Name = "e1",
            Id = 1,
            Severity = Severity.Info,
            Message = "msg",
            Metrics =
            [
                new MetricDefinition
                {
                    Name = "m1",
                    Type = MetricType.Counter,
                    Labels = ["method", "statusCode"]
                }
            ]
        });

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("method", markdown);
        Assert.Contains("statusCode", markdown);
    }

    [Fact]
    public void GenerateMarkdown_MetricWithDescription_ShowsDescription()
    {
        var doc = CreateSchemaWithEvent(new EventDefinition
        {
            Name = "e1",
            Id = 1,
            Severity = Severity.Info,
            Message = "msg",
            Metrics =
            [
                new MetricDefinition
                {
                    Name = "m1",
                    Type = MetricType.Counter,
                    Description = "Total orders placed"
                }
            ]
        });

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("Total orders placed", markdown);
    }

    [Fact]
    public void GenerateMarkdown_EventWithNoMetrics_NoMetricsTable()
    {
        var doc = CreateSchemaWithEvent(new EventDefinition
        {
            Name = "e1",
            Id = 1,
            Severity = Severity.Info,
            Message = "msg"
        });

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.DoesNotContain("| Name | Type | Unit |", markdown);
    }

    // ═══════════════════════════════════════════════════════════════
    // 6. TAGS
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateMarkdown_EventWithTags_ContainsTags()
    {
        var doc = CreateSchemaWithEvent(new EventDefinition
        {
            Name = "e1",
            Id = 1,
            Severity = Severity.Info,
            Message = "msg",
            Tags = ["http", "aspnetcore", "request"]
        });

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("`http`", markdown);
        Assert.Contains("`aspnetcore`", markdown);
        Assert.Contains("`request`", markdown);
    }

    [Fact]
    public void GenerateMarkdown_EventWithNoTags_NoTagsSection()
    {
        var doc = CreateSchemaWithEvent(new EventDefinition
        {
            Name = "e1",
            Id = 1,
            Severity = Severity.Info,
            Message = "msg"
        });

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.DoesNotContain("**Tags:**", markdown);
    }

    // ═══════════════════════════════════════════════════════════════
    // 7. ENUM DEFINITIONS
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateMarkdown_WithEnums_ContainsEnumSection()
    {
        var doc = new SchemaDocument
        {
            Schema = new SchemaHeader { Name = "Test", Version = "1.0.0", Namespace = "Test.Events" },
            Enums =
            [
                new EnumDefinition
                {
                    Name = "OrderStatus",
                    Description = "Possible order states",
                    Values = ["Pending", "Confirmed", "Shipped", "Delivered", "Cancelled"]
                }
            ]
        };

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("## Enum Definitions", markdown);
        Assert.Contains("### OrderStatus", markdown);
        Assert.Contains("Possible order states", markdown);
        Assert.Contains("Pending", markdown);
        Assert.Contains("Confirmed", markdown);
        Assert.Contains("Shipped", markdown);
        Assert.Contains("Delivered", markdown);
        Assert.Contains("Cancelled", markdown);
    }

    [Fact]
    public void GenerateMarkdown_WithMultipleEnums_ContainsAllEnums()
    {
        var doc = new SchemaDocument
        {
            Schema = new SchemaHeader { Name = "Test", Version = "1.0.0", Namespace = "Test.Events" },
            Enums =
            [
                new EnumDefinition { Name = "Status", Values = ["Active", "Inactive"] },
                new EnumDefinition { Name = "Priority", Values = ["Low", "Medium", "High"] }
            ]
        };

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("### Status", markdown);
        Assert.Contains("### Priority", markdown);
    }

    [Fact]
    public void GenerateMarkdown_NoEnums_NoEnumSection()
    {
        var doc = CreateMinimalSchema();

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.DoesNotContain("## Enum Definitions", markdown);
    }

    // ═══════════════════════════════════════════════════════════════
    // 8. SHARED FIELDS
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateMarkdown_WithSharedFields_ContainsSharedFieldsSection()
    {
        var doc = new SchemaDocument
        {
            Schema = new SchemaHeader { Name = "Test", Version = "1.0.0", Namespace = "Test.Events" },
            Fields =
            [
                new FieldDefinition { Name = "requestId", Type = FieldType.String, Description = "Unique request identifier" },
                new FieldDefinition { Name = "durationMs", Type = FieldType.Double, Unit = "ms", Description = "Duration in ms" }
            ]
        };

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("## Shared Fields", markdown);
        Assert.Contains("requestId", markdown);
        Assert.Contains("durationMs", markdown);
    }

    [Fact]
    public void GenerateMarkdown_NoSharedFields_NoSharedFieldsSection()
    {
        var doc = CreateMinimalSchema();

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.DoesNotContain("## Shared Fields", markdown);
    }

    // ═══════════════════════════════════════════════════════════════
    // 9. COMPLETE DOCUMENT — Integration test with real-world schema
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateMarkdown_CompleteSchema_ContainsAllSections()
    {
        var doc = new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = "OrderService",
                Version = "1.0.0",
                Namespace = "OrderService.Events",
                Description = "Order processing events"
            },
            Fields =
            [
                new FieldDefinition { Name = "orderId", Type = FieldType.String, Description = "Unique order identifier" }
            ],
            Enums =
            [
                new EnumDefinition
                {
                    Name = "OrderStatus",
                    Description = "Order lifecycle states",
                    Values = ["Pending", "Confirmed", "Shipped"]
                }
            ],
            Events =
            [
                new EventDefinition
                {
                    Name = "order.placed",
                    Id = 1001,
                    Severity = Severity.Info,
                    Description = "A new order was placed",
                    Message = "Order {orderId} placed for {amount}",
                    Fields =
                    [
                        new FieldDefinition { Name = "orderId", Type = FieldType.String, Required = true },
                        new FieldDefinition { Name = "amount", Type = FieldType.Double, Required = true, Unit = "USD" },
                        new FieldDefinition { Name = "customerEmail", Type = FieldType.String, Sensitivity = Sensitivity.Pii }
                    ],
                    Metrics =
                    [
                        new MetricDefinition { Name = "order.placed.count", Type = MetricType.Counter, Unit = "orders", Description = "Total orders placed" },
                        new MetricDefinition { Name = "order.amount", Type = MetricType.Histogram, Unit = "USD", Labels = ["currency"] }
                    ],
                    Tags = ["order", "commerce"]
                },
                new EventDefinition
                {
                    Name = "order.failed",
                    Id = 1002,
                    Severity = Severity.Error,
                    Description = "An order failed to process",
                    Message = "Order {orderId} failed: {reason}",
                    Exception = true,
                    Fields =
                    [
                        new FieldDefinition { Name = "orderId", Type = FieldType.String, Required = true },
                        new FieldDefinition { Name = "reason", Type = FieldType.String, Required = true }
                    ],
                    Tags = ["order", "error"]
                }
            ]
        };

        var markdown = _generator.GenerateMarkdown(doc);

        // Header
        Assert.Contains("# OrderService", markdown);
        Assert.Contains("1.0.0", markdown);
        Assert.Contains("Order processing events", markdown);

        // Table of Contents
        Assert.Contains("## Table of Contents", markdown);

        // Events
        Assert.Contains("## Events", markdown);
        Assert.Contains("### order.placed", markdown);
        Assert.Contains("### order.failed", markdown);

        // Fields
        Assert.Contains("| Name | Type | Required | Sensitivity |", markdown);

        // Metrics
        Assert.Contains("| Name | Type | Unit |", markdown);

        // Tags
        Assert.Contains("`order`", markdown);
        Assert.Contains("`commerce`", markdown);

        // Enums
        Assert.Contains("## Enum Definitions", markdown);
        Assert.Contains("### OrderStatus", markdown);

        // Shared Fields
        Assert.Contains("## Shared Fields", markdown);
    }

    [Fact]
    public void GenerateMarkdown_MultipleEvents_EventsAppearInOrder()
    {
        var doc = new SchemaDocument
        {
            Schema = new SchemaHeader { Name = "Test", Version = "1.0.0", Namespace = "Test.Events" },
            Events =
            [
                new EventDefinition { Name = "alpha.event", Id = 1, Severity = Severity.Info, Message = "Alpha" },
                new EventDefinition { Name = "beta.event", Id = 2, Severity = Severity.Info, Message = "Beta" },
                new EventDefinition { Name = "gamma.event", Id = 3, Severity = Severity.Info, Message = "Gamma" }
            ]
        };

        var markdown = _generator.GenerateMarkdown(doc);

        var alphaIndex = markdown.IndexOf("### alpha.event", StringComparison.Ordinal);
        var betaIndex = markdown.IndexOf("### beta.event", StringComparison.Ordinal);
        var gammaIndex = markdown.IndexOf("### gamma.event", StringComparison.Ordinal);

        Assert.True(alphaIndex < betaIndex);
        Assert.True(betaIndex < gammaIndex);
    }

    // ═══════════════════════════════════════════════════════════════
    // 10. EDGE CASES
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateMarkdown_FieldTypeEnum_WithRef_ShowsEnumLink()
    {
        var doc = new SchemaDocument
        {
            Schema = new SchemaHeader { Name = "Test", Version = "1.0.0", Namespace = "Test.Events" },
            Enums = [new EnumDefinition { Name = "HealthStatus", Values = ["Healthy", "Degraded", "Unhealthy"] }],
            Events =
            [
                new EventDefinition
                {
                    Name = "health.changed",
                    Id = 1,
                    Severity = Severity.Warn,
                    Message = "Health changed to {status}",
                    Fields = [new FieldDefinition { Name = "status", Type = FieldType.Enum, Ref = "HealthStatus", Required = true }]
                }
            ]
        };

        var markdown = _generator.GenerateMarkdown(doc);

        // Should reference the enum type
        Assert.Contains("enum (HealthStatus)", markdown);
    }

    [Fact]
    public void GenerateMarkdown_AllFieldTypes_MapsCorrectly()
    {
        var fields = new List<FieldDefinition>
        {
            new() { Name = "f1", Type = FieldType.String },
            new() { Name = "f2", Type = FieldType.Int },
            new() { Name = "f3", Type = FieldType.Long },
            new() { Name = "f4", Type = FieldType.Double },
            new() { Name = "f5", Type = FieldType.Bool },
            new() { Name = "f6", Type = FieldType.DateTime },
            new() { Name = "f7", Type = FieldType.Duration },
            new() { Name = "f8", Type = FieldType.Guid },
            new() { Name = "f9", Type = FieldType.StringArray },
            new() { Name = "f10", Type = FieldType.IntArray },
            new() { Name = "f11", Type = FieldType.Map }
        };

        var doc = CreateSchemaWithEvent(new EventDefinition
        {
            Name = "e1",
            Id = 1,
            Severity = Severity.Info,
            Message = "msg",
            Fields = fields
        });

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("| f1 ", markdown);
        Assert.Contains("string", markdown);
        Assert.Contains("int", markdown);
        Assert.Contains("long", markdown);
        Assert.Contains("double", markdown);
        Assert.Contains("bool", markdown);
        Assert.Contains("datetime", markdown);
        Assert.Contains("duration", markdown);
        Assert.Contains("guid", markdown);
        Assert.Contains("string[]", markdown);
        Assert.Contains("int[]", markdown);
        Assert.Contains("map", markdown);
    }

    [Fact]
    public void GenerateMarkdown_AllSeverityLevels_DisplayCorrectly()
    {
        var doc = new SchemaDocument
        {
            Schema = new SchemaHeader { Name = "Test", Version = "1.0.0", Namespace = "Test.Events" },
            Events =
            [
                new EventDefinition { Name = "e.trace", Id = 1, Severity = Severity.Trace, Message = "Trace" },
                new EventDefinition { Name = "e.debug", Id = 2, Severity = Severity.Debug, Message = "Debug" },
                new EventDefinition { Name = "e.info", Id = 3, Severity = Severity.Info, Message = "Info" },
                new EventDefinition { Name = "e.warn", Id = 4, Severity = Severity.Warn, Message = "Warn" },
                new EventDefinition { Name = "e.error", Id = 5, Severity = Severity.Error, Message = "Error" },
                new EventDefinition { Name = "e.fatal", Id = 6, Severity = Severity.Fatal, Message = "Fatal" }
            ]
        };

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("Trace", markdown);
        Assert.Contains("Debug", markdown);
        Assert.Contains("Info", markdown);
        Assert.Contains("Warn", markdown);
        Assert.Contains("Error", markdown);
        Assert.Contains("Fatal", markdown);
    }

    [Fact]
    public void GenerateMarkdown_AllSensitivityLevels_DisplayWithCorrectIndicators()
    {
        var doc = CreateSchemaWithEvent(new EventDefinition
        {
            Name = "e1",
            Id = 1,
            Severity = Severity.Info,
            Message = "msg",
            Fields =
            [
                new FieldDefinition { Name = "f1", Type = FieldType.String, Sensitivity = Sensitivity.Public },
                new FieldDefinition { Name = "f2", Type = FieldType.String, Sensitivity = Sensitivity.Internal },
                new FieldDefinition { Name = "f3", Type = FieldType.String, Sensitivity = Sensitivity.Pii },
                new FieldDefinition { Name = "f4", Type = FieldType.String, Sensitivity = Sensitivity.Credential }
            ]
        });

        var markdown = _generator.GenerateMarkdown(doc);

        // Non-Public sensitivity levels get warning indicators
        Assert.Contains("⚠️ Internal", markdown);
        Assert.Contains("⚠️ Pii", markdown);
        Assert.Contains("🔒 Credential", markdown);
    }

    [Fact]
    public void GenerateMarkdown_MetricWithBuckets_ShowsBuckets()
    {
        var doc = CreateSchemaWithEvent(new EventDefinition
        {
            Name = "e1",
            Id = 1,
            Severity = Severity.Info,
            Message = "msg",
            Metrics =
            [
                new MetricDefinition
                {
                    Name = "m1",
                    Type = MetricType.Histogram,
                    Buckets = [5, 10, 25, 50, 100, 250, 500, 1000]
                }
            ]
        });

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("5", markdown);
        Assert.Contains("1000", markdown);
    }

    [Fact]
    public void GenerateMarkdown_GeneratedTimestamp_ContainsGeneratedNotice()
    {
        var doc = CreateMinimalSchema();

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Contains("Generated by ALL Schema Documentation Generator", markdown);
    }

    [Fact]
    public void GenerateMarkdown_ReturnsNonEmptyString()
    {
        var doc = CreateMinimalSchema();

        var markdown = _generator.GenerateMarkdown(doc);

        Assert.NotNull(markdown);
        Assert.NotEmpty(markdown);
    }

    // ═══════════════════════════════════════════════════════════════
    // 11. GeneratedFile OUTPUT (matches CodeGenerator pattern)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateDocumentation_ReturnsGeneratedFileWithMarkdownExtension()
    {
        var doc = CreateMinimalSchema(name: "OrderService");

        var file = _generator.GenerateDocumentation(doc);

        Assert.Equal("OrderService.md", file.FileName);
        Assert.NotEmpty(file.Content);
    }

    [Fact]
    public void GenerateDocumentation_ContentMatchesGenerateMarkdown()
    {
        var doc = CreateMinimalSchema(name: "OrderService");

        var file = _generator.GenerateDocumentation(doc);
        var markdown = _generator.GenerateMarkdown(doc);

        Assert.Equal(markdown, file.Content);
    }
}
