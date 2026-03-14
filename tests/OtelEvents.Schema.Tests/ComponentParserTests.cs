using OtelEvents.Schema.Documentation;
using OtelEvents.Schema.Models;
using OtelEvents.Schema.Parsing;
using OtelEvents.Schema.Validation;

namespace OtelEvents.Schema.Tests;

/// <summary>
/// Tests for the components: YAML block — parsing, validation, merging, and documentation.
/// Covers OTEL_SCHEMA_032 through OTEL_SCHEMA_039 rules.
/// </summary>
public class ComponentParserTests
{
    private readonly SchemaParser _parser = new();
    private readonly SchemaValidator _validator = new();
    private readonly SchemaDocumentationGenerator _docGenerator = new();

    private const string MinimalSchemaPrefix = """
        schema:
          name: "TestService"
          version: "1.0.0"
          namespace: "Test.Namespace"
        """;

    // ═══════════════════════════════════════════════════════════════
    // 1. PARSING — Valid components block
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Parse_ComponentsWithAllFields_ParsesCorrectly()
    {
        var yaml = $$"""
            {{MinimalSchemaPrefix}}
            components:
              orders-db:
                window: 300s
                healthyAbove: 0.95
                degradedAbove: 0.7
                minimumSignals: 10
                cooldown: 30s
                responseTime:
                  percentile: 0.95
                  degradedAfter: 200ms
                  unhealthyAfter: 2000ms
                signals:
                  - event: "http.request.failed"
                    match: { httpRoute: "/api/orders/*" }
                  - event: "http.request.completed"
                    match: { httpRoute: "/api/orders/*" }
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess, $"Parse failed: {string.Join(", ", result.Errors)}");
        Assert.Single(result.Document!.Components);

        var component = result.Document.Components[0];
        Assert.Equal("orders-db", component.Name);
        Assert.Equal(300, component.WindowSeconds);
        Assert.Equal(0.95, component.HealthyAbove);
        Assert.Equal(0.7, component.DegradedAbove);
        Assert.Equal(10, component.MinimumSignals);
        Assert.Equal(30, component.CooldownSeconds);

        Assert.NotNull(component.ResponseTime);
        Assert.Equal(0.95, component.ResponseTime!.Percentile);
        Assert.Equal(200, component.ResponseTime.DegradedAfterMs);
        Assert.Equal(2000, component.ResponseTime.UnhealthyAfterMs);

        Assert.Equal(2, component.Signals.Count);
        Assert.Equal("http.request.failed", component.Signals[0].Event);
        Assert.Equal("/api/orders/*", component.Signals[0].Match["httpRoute"]);
        Assert.Equal("http.request.completed", component.Signals[1].Event);
    }

    [Fact]
    public void Parse_ComponentsWithMinimalFields_OnlySignals()
    {
        var yaml = $$"""
            {{MinimalSchemaPrefix}}
            components:
              simple-check:
                signals:
                  - event: "health.check.completed"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess, $"Parse failed: {string.Join(", ", result.Errors)}");
        Assert.Single(result.Document!.Components);

        var component = result.Document.Components[0];
        Assert.Equal("simple-check", component.Name);
        Assert.Equal(0, component.WindowSeconds);
        Assert.Equal(0, component.HealthyAbove);
        Assert.Equal(0, component.DegradedAbove);
        Assert.Equal(0, component.MinimumSignals);
        Assert.Null(component.ResponseTime);
        Assert.Single(component.Signals);
        Assert.Equal("health.check.completed", component.Signals[0].Event);
        Assert.Empty(component.Signals[0].Match);
    }

    [Fact]
    public void Parse_MultipleComponents_ParsesAll()
    {
        var yaml = $$"""
            {{MinimalSchemaPrefix}}
            components:
              orders-db:
                window: 300s
                healthyAbove: 0.95
                degradedAbove: 0.7
                minimumSignals: 10
                signals:
                  - event: "order.created"
              payments-api:
                window: 60s
                healthyAbove: 0.99
                degradedAbove: 0.9
                minimumSignals: 5
                signals:
                  - event: "payment.processed"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess, $"Parse failed: {string.Join(", ", result.Errors)}");
        Assert.Equal(2, result.Document!.Components.Count);
        Assert.Equal("orders-db", result.Document.Components[0].Name);
        Assert.Equal("payments-api", result.Document.Components[1].Name);
    }

    [Fact]
    public void Parse_SchemaWithoutComponents_ReturnsEmptyList()
    {
        var result = _parser.Parse(MinimalSchemaPrefix, MinimalSchemaPrefix.Length);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Document!.Components);
    }

    [Fact]
    public void Parse_ComponentsWithNoSignals_ReturnsEmptySignalsList()
    {
        var yaml = $$"""
            {{MinimalSchemaPrefix}}
            components:
              empty-component:
                window: 60s
                healthyAbove: 0.9
                degradedAbove: 0.5
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess, $"Parse failed: {string.Join(", ", result.Errors)}");
        Assert.Single(result.Document!.Components);
        Assert.Empty(result.Document.Components[0].Signals);
    }

    [Fact]
    public void Parse_ComponentSignalWithMultipleMatchFilters_ParsesAll()
    {
        var yaml = $$"""
            {{MinimalSchemaPrefix}}
            components:
              api-health:
                signals:
                  - event: "http.request.completed"
                    match:
                      httpRoute: "/api/*"
                      httpMethod: "GET"
                      statusCode: "200"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess, $"Parse failed: {string.Join(", ", result.Errors)}");
        var signal = result.Document!.Components[0].Signals[0];
        Assert.Equal(3, signal.Match.Count);
        Assert.Equal("/api/*", signal.Match["httpRoute"]);
        Assert.Equal("GET", signal.Match["httpMethod"]);
        Assert.Equal("200", signal.Match["statusCode"]);
    }

    [Fact]
    public void Parse_ComponentWindowInMilliseconds_ParsesCorrectly()
    {
        var yaml = $$"""
            {{MinimalSchemaPrefix}}
            components:
              fast-check:
                window: 5000ms
                signals:
                  - event: "fast.event"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess, $"Parse failed: {string.Join(", ", result.Errors)}");
        Assert.Equal(5, result.Document!.Components[0].WindowSeconds);
    }

    [Fact]
    public void Parse_ComponentResponseTimeInSeconds_ConvertsToMs()
    {
        var yaml = $$"""
            {{MinimalSchemaPrefix}}
            components:
              rt-check:
                responseTime:
                  percentile: 0.99
                  degradedAfter: 1s
                  unhealthyAfter: 5s
                signals:
                  - event: "test.event"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess, $"Parse failed: {string.Join(", ", result.Errors)}");
        var rt = result.Document!.Components[0].ResponseTime!;
        Assert.Equal(1000, rt.DegradedAfterMs);
        Assert.Equal(5000, rt.UnhealthyAfterMs);
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. VALIDATION — Component rules
    // ═══════════════════════════════════════════════════════════════

    private ValidationResult ParseAndValidate(string yaml)
    {
        var parseResult = _parser.Parse(yaml, yaml.Length);
        Assert.True(parseResult.IsSuccess, $"Parse failed: {string.Join(", ", parseResult.Errors)}");
        return _validator.Validate(parseResult.Document!);
    }

    [Fact]
    public void Validate_ValidComponent_NoErrors()
    {
        var yaml = $$"""
            {{MinimalSchemaPrefix}}
            components:
              orders-db:
                window: 300s
                healthyAbove: 0.95
                degradedAbove: 0.7
                minimumSignals: 10
                cooldown: 30s
                signals:
                  - event: "http.request.completed"
                    match: { httpRoute: "/api/orders/*" }
            """;

        var result = ParseAndValidate(yaml);

        Assert.True(result.IsValid, $"Errors: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void Validate_ComponentNameWithUpperCase_ReturnsError()
    {
        var yaml = $$"""
            {{MinimalSchemaPrefix}}
            components:
              OrdersDb:
                signals:
                  - event: "test.event"
            """;

        var result = ParseAndValidate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidComponentNameFormat);
    }

    [Fact]
    public void Validate_ComponentNameWithUnderscores_ReturnsError()
    {
        var yaml = $$"""
            {{MinimalSchemaPrefix}}
            components:
              orders_db:
                signals:
                  - event: "test.event"
            """;

        var result = ParseAndValidate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidComponentNameFormat);
    }

    [Fact]
    public void Validate_HealthyAboveOutOfRange_ReturnsError()
    {
        var yaml = $$"""
            {{MinimalSchemaPrefix}}
            components:
              bad-threshold:
                healthyAbove: 1.5
                degradedAbove: 0.7
                signals:
                  - event: "test.event"
            """;

        var result = ParseAndValidate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidComponentThreshold);
    }

    [Fact]
    public void Validate_DegradedAboveOutOfRange_ReturnsError()
    {
        var yaml = $$"""
            {{MinimalSchemaPrefix}}
            components:
              bad-threshold:
                healthyAbove: 0.95
                degradedAbove: -0.1
                signals:
                  - event: "test.event"
            """;

        var result = ParseAndValidate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidComponentThreshold);
    }

    [Fact]
    public void Validate_HealthyAboveNotGreaterThanDegradedAbove_ReturnsError()
    {
        var yaml = $$"""
            {{MinimalSchemaPrefix}}
            components:
              bad-order:
                healthyAbove: 0.7
                degradedAbove: 0.95
                signals:
                  - event: "test.event"
            """;

        var result = ParseAndValidate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidThresholdOrder);
    }

    [Fact]
    public void Validate_HealthyAboveEqualsDegradedAbove_ReturnsError()
    {
        var yaml = $$"""
            {{MinimalSchemaPrefix}}
            components:
              equal-thresholds:
                healthyAbove: 0.8
                degradedAbove: 0.8
                signals:
                  - event: "test.event"
            """;

        var result = ParseAndValidate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidThresholdOrder);
    }

    [Fact]
    public void Validate_NegativeWindow_ReturnsError()
    {
        var yaml = $$"""
            {{MinimalSchemaPrefix}}
            components:
              bad-window:
                window: -10s
                signals:
                  - event: "test.event"
            """;

        var result = ParseAndValidate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidComponentWindow);
    }

    [Fact]
    public void Validate_EmptySignalEventName_ReturnsError()
    {
        var yaml = $$"""
            {{MinimalSchemaPrefix}}
            components:
              bad-signal:
                signals:
                  - event: ""
            """;

        var result = ParseAndValidate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidSignalEventName);
    }

    [Fact]
    public void Validate_DuplicateComponentNames_AcrossDocuments_ReturnsError()
    {
        var doc1 = CreateDocWithComponent("orders-db");
        var doc2 = CreateDocWithComponent("orders-db");

        var result = _validator.Validate([doc1, doc2]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.DuplicateComponentName);
    }

    [Fact]
    public void Validate_UniqueComponentNames_AcrossDocuments_NoError()
    {
        var doc1 = CreateDocWithComponent("orders-db");
        var doc2 = CreateDocWithComponent("payments-api");

        var result = _validator.Validate([doc1, doc2]);

        // Filter only component-related errors
        var componentErrors = result.Errors.Where(e =>
            e.Code == ErrorCodes.DuplicateComponentName).ToList();
        Assert.Empty(componentErrors);
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. MERGER — Components from multiple files
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Merge_ComponentsFromMultipleDocuments_CombinesAll()
    {
        var merger = new SchemaMerger(_parser, _validator);

        var doc1 = CreateDocWithComponent("orders-db");
        var doc2 = CreateDocWithComponent("payments-api");

        var result = merger.Merge([doc1, doc2]);

        Assert.True(result.IsSuccess, $"Errors: {string.Join(", ", result.Errors)}");
        Assert.Equal(2, result.Document!.Components.Count);
        Assert.Contains(result.Document.Components, c => c.Name == "orders-db");
        Assert.Contains(result.Document.Components, c => c.Name == "payments-api");
    }

    [Fact]
    public void Merge_DuplicateComponentNames_ReportsError()
    {
        var merger = new SchemaMerger(_parser, _validator);

        var doc1 = CreateDocWithComponent("orders-db");
        var doc2 = CreateDocWithComponent("orders-db");

        var result = merger.Merge([doc1, doc2]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.DuplicateComponentName);
    }

    [Fact]
    public void Merge_SingleDocumentWithComponents_PreservesComponents()
    {
        var merger = new SchemaMerger(_parser, _validator);

        var doc = CreateDocWithComponent("orders-db");

        var result = merger.Merge([doc]);

        Assert.True(result.IsSuccess, $"Errors: {string.Join(", ", result.Errors)}");
        Assert.Single(result.Document!.Components);
        Assert.Equal("orders-db", result.Document.Components[0].Name);
    }

    // ═══════════════════════════════════════════════════════════════
    // 4. DOCUMENTATION — Components section in markdown
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateMarkdown_WithComponents_ContainsComponentsSection()
    {
        var doc = CreateDocWithComponent("orders-db");

        var markdown = _docGenerator.GenerateMarkdown(doc);

        Assert.Contains("## Components", markdown);
        Assert.Contains("### orders-db", markdown);
    }

    [Fact]
    public void GenerateMarkdown_WithComponents_ContainsSignalsTable()
    {
        var doc = CreateDocWithFullComponent();

        var markdown = _docGenerator.GenerateMarkdown(doc);

        Assert.Contains("#### Signals", markdown);
        Assert.Contains("http.request.completed", markdown);
        Assert.Contains("`httpRoute`", markdown);
    }

    [Fact]
    public void GenerateMarkdown_WithResponseTime_ContainsResponseTimeSection()
    {
        var doc = CreateDocWithFullComponent();

        var markdown = _docGenerator.GenerateMarkdown(doc);

        Assert.Contains("#### Response Time", markdown);
        Assert.Contains("0.95", markdown);
        Assert.Contains("200ms", markdown);
        Assert.Contains("2000ms", markdown);
    }

    [Fact]
    public void GenerateMarkdown_ComponentsInTableOfContents_ContainsComponentsLink()
    {
        var doc = CreateDocWithComponent("orders-db");

        var markdown = _docGenerator.GenerateMarkdown(doc);

        Assert.Contains("- [Components](#components)", markdown);
        Assert.Contains("orders-db", markdown);
    }

    [Fact]
    public void GenerateMarkdown_NoComponents_NoComponentsSection()
    {
        var doc = new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = "TestService",
                Version = "1.0.0",
                Namespace = "Test.Namespace"
            }
        };

        var markdown = _docGenerator.GenerateMarkdown(doc);

        Assert.DoesNotContain("## Components", markdown);
    }

    // ═══════════════════════════════════════════════════════════════
    // 5. ROUND-TRIP — Parse → Validate → Document
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void RoundTrip_ParseValidateDocument_FullComponent()
    {
        var yaml = $$"""
            {{MinimalSchemaPrefix}}
            components:
              orders-db:
                window: 300s
                healthyAbove: 0.95
                degradedAbove: 0.7
                minimumSignals: 10
                cooldown: 30s
                responseTime:
                  percentile: 0.95
                  degradedAfter: 200ms
                  unhealthyAfter: 2000ms
                signals:
                  - event: "http.request.completed"
                    match: { httpRoute: "/api/orders/*" }
                  - event: "http.request.failed"
                    match: { httpRoute: "/api/orders/*" }
            """;

        // Step 1: Parse
        var parseResult = _parser.Parse(yaml, yaml.Length);
        Assert.True(parseResult.IsSuccess, $"Parse failed: {string.Join(", ", parseResult.Errors)}");

        // Step 2: Validate
        var validateResult = _validator.Validate(parseResult.Document!);
        Assert.True(validateResult.IsValid, $"Validation failed: {string.Join(", ", validateResult.Errors)}");

        // Step 3: Document
        var markdown = _docGenerator.GenerateMarkdown(parseResult.Document!);
        Assert.Contains("## Components", markdown);
        Assert.Contains("### orders-db", markdown);
        Assert.Contains("300s", markdown);
        Assert.Contains("http.request.completed", markdown);
        Assert.Contains("http.request.failed", markdown);
    }

    [Fact]
    public void RoundTrip_ParseMergeValidateDocument_MultipleFiles()
    {
        var yaml1 = $$"""
            {{MinimalSchemaPrefix}}
            components:
              orders-db:
                window: 300s
                healthyAbove: 0.95
                degradedAbove: 0.7
                minimumSignals: 10
                signals:
                  - event: "order.created"
            """;

        var yaml2 = $$"""
            {{MinimalSchemaPrefix}}
            components:
              payments-api:
                window: 60s
                healthyAbove: 0.99
                degradedAbove: 0.9
                minimumSignals: 5
                signals:
                  - event: "payment.processed"
            """;

        var result1 = _parser.Parse(yaml1, yaml1.Length);
        var result2 = _parser.Parse(yaml2, yaml2.Length);
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);

        var merger = new SchemaMerger(_parser, _validator);
        var mergeResult = merger.Merge([result1.Document!, result2.Document!]);

        Assert.True(mergeResult.IsSuccess, $"Merge failed: {string.Join(", ", mergeResult.Errors)}");
        Assert.Equal(2, mergeResult.Document!.Components.Count);

        var markdown = _docGenerator.GenerateMarkdown(mergeResult.Document);
        Assert.Contains("### orders-db", markdown);
        Assert.Contains("### payments-api", markdown);
    }

    // ═══════════════════════════════════════════════════════════════
    // 6. EDGE CASES
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Parse_ComponentsWithEventsAndEnums_ParsesAllBlocks()
    {
        var yaml = $$"""
            {{MinimalSchemaPrefix}}
            enums:
              Status:
                values:
                  - Active
                  - Inactive
            events:
              order.created:
                id: 1
                severity: INFO
                message: "Order created"
            components:
              orders-db:
                window: 300s
                healthyAbove: 0.95
                degradedAbove: 0.7
                signals:
                  - event: "order.created"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess, $"Parse failed: {string.Join(", ", result.Errors)}");
        Assert.Single(result.Document!.Enums);
        Assert.Single(result.Document.Events);
        Assert.Single(result.Document.Components);
    }

    [Fact]
    public void Validate_ComponentWithZeroThresholds_NoThresholdErrors()
    {
        // When thresholds are 0 (not set), skip the ordering check
        var yaml = $$"""
            {{MinimalSchemaPrefix}}
            components:
              no-thresholds:
                window: 60s
                signals:
                  - event: "test.event"
            """;

        var result = ParseAndValidate(yaml);

        Assert.DoesNotContain(result.Errors, e => e.Code == ErrorCodes.InvalidThresholdOrder);
        Assert.DoesNotContain(result.Errors, e => e.Code == ErrorCodes.InvalidComponentThreshold);
    }

    // ═══════════════════════════════════════════════════════════════
    // Test helpers
    // ═══════════════════════════════════════════════════════════════

    private static SchemaDocument CreateDocWithComponent(string componentName)
    {
        return new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = "TestService",
                Version = "1.0.0",
                Namespace = "Test.Namespace"
            },
            Components =
            [
                new ComponentDefinition
                {
                    Name = componentName,
                    WindowSeconds = 300,
                    HealthyAbove = 0.95,
                    DegradedAbove = 0.7,
                    MinimumSignals = 10,
                    Signals =
                    [
                        new SignalMapping
                        {
                            Event = "http.request.completed",
                            Match = new Dictionary<string, string> { ["httpRoute"] = "/api/*" }
                        }
                    ]
                }
            ]
        };
    }

    private static SchemaDocument CreateDocWithFullComponent()
    {
        return new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = "TestService",
                Version = "1.0.0",
                Namespace = "Test.Namespace"
            },
            Components =
            [
                new ComponentDefinition
                {
                    Name = "orders-db",
                    WindowSeconds = 300,
                    HealthyAbove = 0.95,
                    DegradedAbove = 0.7,
                    MinimumSignals = 10,
                    CooldownSeconds = 30,
                    ResponseTime = new ResponseTimeConfig
                    {
                        Percentile = 0.95,
                        DegradedAfterMs = 200,
                        UnhealthyAfterMs = 2000
                    },
                    Signals =
                    [
                        new SignalMapping
                        {
                            Event = "http.request.completed",
                            Match = new Dictionary<string, string> { ["httpRoute"] = "/api/orders/*" }
                        }
                    ]
                }
            ]
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // Finding 2: Malformed YAML values — parse sentinel & validation
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("minimumSignals: abc")]
    [InlineData("minimumSignals: true")]
    [InlineData("minimumSignals: 1.5")]
    public void Parse_MalformedMinimumSignals_ReturnsSentinel(string malformedField)
    {
        var yaml = $$"""
            {{MinimalSchemaPrefix}}
            components:
              orders-db:
                {{malformedField}}
                signals:
                  - event: "http.request.completed"
            """;

        var result = _parser.Parse(yaml, yaml.Length);
        Assert.True(result.IsSuccess);

        var component = result.Document!.Components[0];
        Assert.Equal(-1, component.MinimumSignals);
    }

    [Theory]
    [InlineData("healthyAbove: abc")]
    [InlineData("degradedAbove: not_a_number")]
    public void Parse_MalformedThreshold_ReturnsSentinel(string malformedField)
    {
        ArgumentNullException.ThrowIfNull(malformedField);

        var yaml = $$"""
            {{MinimalSchemaPrefix}}
            components:
              orders-db:
                {{malformedField}}
                signals:
                  - event: "http.request.completed"
            """;

        var result = _parser.Parse(yaml, yaml.Length);
        Assert.True(result.IsSuccess);

        var component = result.Document!.Components[0];
        if (malformedField.StartsWith("healthyAbove", StringComparison.Ordinal))
            Assert.Equal(-1, component.HealthyAbove);
        else
            Assert.Equal(-1, component.DegradedAbove);
    }

    [Theory]
    [InlineData("window: abc")]
    [InlineData("window: nots")]
    [InlineData("cooldown: xyz")]
    public void Parse_MalformedDuration_ReturnsSentinel(string malformedField)
    {
        ArgumentNullException.ThrowIfNull(malformedField);

        var yaml = $$"""
            {{MinimalSchemaPrefix}}
            components:
              orders-db:
                {{malformedField}}
                signals:
                  - event: "http.request.completed"
            """;

        var result = _parser.Parse(yaml, yaml.Length);
        Assert.True(result.IsSuccess);

        var component = result.Document!.Components[0];
        if (malformedField.StartsWith("window", StringComparison.Ordinal))
            Assert.True(component.WindowSeconds < 0, "Malformed window should return -1 sentinel");
        else
            Assert.True(component.CooldownSeconds < 0, "Malformed cooldown should return -1 sentinel");
    }

    [Fact]
    public void Validate_MalformedMinimumSignals_ReportsMalformedValueError()
    {
        var yaml = $$"""
            {{MinimalSchemaPrefix}}
            components:
              orders-db:
                minimumSignals: abc
                signals:
                  - event: "http.request.completed"
            """;

        var result = _parser.Parse(yaml, yaml.Length);
        Assert.True(result.IsSuccess);

        var validation = _validator.Validate(result.Document!);
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors,
            e => e.Code == ErrorCodes.MalformedValue && e.Message.Contains("minimumSignals"));
    }

    [Fact]
    public void Validate_MalformedHealthyAbove_ReportsMalformedValueError()
    {
        var yaml = $$"""
            {{MinimalSchemaPrefix}}
            components:
              orders-db:
                healthyAbove: xyz
                signals:
                  - event: "http.request.completed"
            """;

        var result = _parser.Parse(yaml, yaml.Length);
        Assert.True(result.IsSuccess);

        var validation = _validator.Validate(result.Document!);
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors,
            e => e.Code == ErrorCodes.MalformedValue && e.Message.Contains("healthyAbove"));
    }

    [Fact]
    public void Validate_MalformedWindow_ReportsMalformedValueError()
    {
        var yaml = $$"""
            {{MinimalSchemaPrefix}}
            components:
              orders-db:
                window: notaduration
                signals:
                  - event: "http.request.completed"
            """;

        var result = _parser.Parse(yaml, yaml.Length);
        Assert.True(result.IsSuccess);

        var validation = _validator.Validate(result.Document!);
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors,
            e => e.Code == ErrorCodes.MalformedValue && e.Message.Contains("window"));
    }

    // ═══════════════════════════════════════════════════════════════
    // Finding 3: Negative minimumSignals validation (was dead code)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Validate_NegativeMinimumSignals_ReportsError()
    {
        // Simulate a component with negative minimumSignals (sentinel from malformed parse)
        var doc = CreateDocWithComponent(new ComponentDefinition
        {
            Name = "test-component",
            MinimumSignals = -1,
            Signals = [new SignalMapping { Event = "http.request.completed" }]
        });

        var validation = _validator.Validate(doc);
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors,
            e => e.Code == ErrorCodes.InvalidMinimumSignals ||
                 e.Code == ErrorCodes.MalformedValue);
    }

    private static SchemaDocument CreateDocWithComponent(ComponentDefinition component) => new()
    {
        Schema = new SchemaHeader
        {
            Name = "Test",
            Version = "1.0.0",
            Namespace = "Test.Ns"
        },
        Components = [component]
    };
}
