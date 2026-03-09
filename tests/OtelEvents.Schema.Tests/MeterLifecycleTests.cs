using OtelEvents.Schema.CodeGen;
using OtelEvents.Schema.Models;
using OtelEvents.Schema.Parsing;
using OtelEvents.Schema.Validation;

namespace OtelEvents.Schema.Tests;

/// <summary>
/// Tests for IMeterFactory-based meter creation (Issue #20).
/// Covers schema parsing, code generation for both static and DI meter lifecycle modes.
/// </summary>
public class MeterLifecycleTests
{
    private readonly CodeGenerator _generator = new();
    private readonly SchemaParser _parser = new();

    // ═══════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════

    private static SchemaDocument CreateSchemaWithMetrics(
        MeterLifecycle lifecycle = MeterLifecycle.Static,
        string name = "TestService",
        string ns = "Test.Events",
        string? meterName = null) => new()
    {
        Schema = new SchemaHeader
        {
            Name = name,
            Version = "1.0.0",
            Namespace = ns,
            MeterName = meterName,
            MeterLifecycle = lifecycle
        },
        Events =
        [
            new EventDefinition
            {
                Name = "order.placed",
                Id = 1001,
                Severity = Severity.Info,
                Message = "Order placed",
                Metrics =
                [
                    new MetricDefinition
                    {
                        Name = "order.placed.count",
                        Type = MetricType.Counter,
                        Unit = "orders",
                        Description = "Total orders placed"
                    },
                    new MetricDefinition
                    {
                        Name = "order.placed.amount",
                        Type = MetricType.Histogram,
                        Unit = "USD",
                        Description = "Order amount"
                    }
                ]
            }
        ]
    };

    private string GetEventsContent(SchemaDocument doc)
    {
        var files = _generator.GenerateFromSchema(doc);
        return files.First(f => f.FileName.Contains("Events")).Content;
    }

    // ═══════════════════════════════════════════════════════════════
    // 1. SCHEMA PARSER — meterLifecycle field
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Parse_MeterLifecycleOmitted_DefaultsToStatic()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        Assert.Equal(MeterLifecycle.Static, result.Document!.Schema.MeterLifecycle);
    }

    [Fact]
    public void Parse_MeterLifecycleStatic_ParsesCorrectly()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
              meterLifecycle: static
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        Assert.Equal(MeterLifecycle.Static, result.Document!.Schema.MeterLifecycle);
    }

    [Fact]
    public void Parse_MeterLifecycleDI_ParsesCorrectly()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
              meterLifecycle: di
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        Assert.Equal(MeterLifecycle.DI, result.Document!.Schema.MeterLifecycle);
    }

    [Fact]
    public void Parse_MeterLifecycleDI_CaseInsensitive()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
              meterLifecycle: DI
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        Assert.Equal(MeterLifecycle.DI, result.Document!.Schema.MeterLifecycle);
    }

    [Fact]
    public void Parse_MeterLifecycleInvalidValue_ReturnsError()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
              meterLifecycle: singleton
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidMeterLifecycle);
    }

    [Fact]
    public void Parse_MeterLifecycleWithAllHeaderFields_ParsesCorrectly()
    {
        var yaml = """
            schema:
              name: "MyService"
              version: "2.0.0"
              namespace: "MyCompany.MyService"
              description: "My service events"
              meterName: "MyCompany.MyService.Meters"
              meterLifecycle: di
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        var header = result.Document!.Schema;
        Assert.Equal("MyService", header.Name);
        Assert.Equal("2.0.0", header.Version);
        Assert.Equal("MyCompany.MyService", header.Namespace);
        Assert.Equal("My service events", header.Description);
        Assert.Equal("MyCompany.MyService.Meters", header.MeterName);
        Assert.Equal(MeterLifecycle.DI, header.MeterLifecycle);
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. STATIC MODE — existing behavior preserved
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Generate_StaticMode_GeneratesStaticMetricsClass()
    {
        var doc = CreateSchemaWithMetrics(MeterLifecycle.Static);

        var content = GetEventsContent(doc);

        Assert.Contains("public static class TestServiceMetrics", content);
    }

    [Fact]
    public void Generate_StaticMode_GeneratesStaticMeter()
    {
        var doc = CreateSchemaWithMetrics(MeterLifecycle.Static, meterName: "myapp.events");

        var content = GetEventsContent(doc);

        Assert.Contains("private static readonly Meter s_meter = new Meter(\"myapp.events\"", content);
    }

    [Fact]
    public void Generate_StaticMode_GeneratesStaticMetricFields()
    {
        var doc = CreateSchemaWithMetrics(MeterLifecycle.Static);

        var content = GetEventsContent(doc);

        Assert.Contains("public static readonly Counter<long> s_orderPlacedCount", content);
        Assert.Contains("public static readonly Histogram<double> s_orderPlacedAmount", content);
    }

    [Fact]
    public void Generate_StaticMode_DoesNotGenerateIDisposable()
    {
        var doc = CreateSchemaWithMetrics(MeterLifecycle.Static);

        var content = GetEventsContent(doc);

        Assert.DoesNotContain("IDisposable", content);
        Assert.DoesNotContain("Dispose()", content);
    }

    [Fact]
    public void Generate_StaticMode_DoesNotGenerateServiceCollectionExtension()
    {
        var doc = CreateSchemaWithMetrics(MeterLifecycle.Static);

        var files = _generator.GenerateFromSchema(doc);

        Assert.DoesNotContain(files, f => f.FileName.Contains("ServiceCollection"));
        // Also verify no AddXxxMetrics method in any file
        foreach (var file in files)
        {
            Assert.DoesNotContain("IServiceCollection", file.Content);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. DI MODE — IMeterFactory injection
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Generate_DIMode_GeneratesSealedInstanceClass()
    {
        var doc = CreateSchemaWithMetrics(MeterLifecycle.DI);

        var content = GetEventsContent(doc);

        Assert.Contains("public sealed class TestServiceMetrics", content);
        Assert.DoesNotContain("public static class TestServiceMetrics", content);
    }

    [Fact]
    public void Generate_DIMode_ImplementsIDisposable()
    {
        var doc = CreateSchemaWithMetrics(MeterLifecycle.DI);

        var content = GetEventsContent(doc);

        Assert.Contains("IDisposable", content);
        Assert.Contains("public void Dispose()", content);
        Assert.Contains("_meter?.Dispose()", content);
    }

    [Fact]
    public void Generate_DIMode_HasIMeterFactoryConstructor()
    {
        var doc = CreateSchemaWithMetrics(MeterLifecycle.DI);

        var content = GetEventsContent(doc);

        Assert.Contains("IMeterFactory meterFactory", content);
        Assert.Contains("public TestServiceMetrics(IMeterFactory meterFactory)", content);
    }

    [Fact]
    public void Generate_DIMode_CreatesMeterViaFactory()
    {
        var doc = CreateSchemaWithMetrics(MeterLifecycle.DI, meterName: "myapp.events");

        var content = GetEventsContent(doc);

        Assert.Contains("meterFactory.Create(\"myapp.events\")", content);
        Assert.DoesNotContain("new Meter(", content);
    }

    [Fact]
    public void Generate_DIMode_UsesMeterNameFromSchema()
    {
        var doc = CreateSchemaWithMetrics(MeterLifecycle.DI, meterName: "custom.meter.name");

        var content = GetEventsContent(doc);

        Assert.Contains("meterFactory.Create(\"custom.meter.name\")", content);
    }

    [Fact]
    public void Generate_DIMode_DefaultsMeterNameToNamespace()
    {
        var doc = CreateSchemaWithMetrics(MeterLifecycle.DI, ns: "MyCompany.Orders");

        var content = GetEventsContent(doc);

        Assert.Contains("meterFactory.Create(\"MyCompany.Orders\")", content);
    }

    [Fact]
    public void Generate_DIMode_GeneratesInstanceMeterField()
    {
        var doc = CreateSchemaWithMetrics(MeterLifecycle.DI);

        var content = GetEventsContent(doc);

        Assert.Contains("private readonly Meter _meter;", content);
        Assert.DoesNotContain("private static readonly Meter s_meter", content);
    }

    [Fact]
    public void Generate_DIMode_GeneratesPublicProperties()
    {
        var doc = CreateSchemaWithMetrics(MeterLifecycle.DI);

        var content = GetEventsContent(doc);

        Assert.Contains("public Counter<long> OrderPlacedCount { get; }", content);
        Assert.Contains("public Histogram<double> OrderPlacedAmount { get; }", content);
    }

    [Fact]
    public void Generate_DIMode_CreatesInstrumentsInConstructor()
    {
        var doc = CreateSchemaWithMetrics(MeterLifecycle.DI);

        var content = GetEventsContent(doc);

        Assert.Contains("_meter.CreateCounter<long>(\"order.placed.count\"", content);
        Assert.Contains("_meter.CreateHistogram<double>(\"order.placed.amount\"", content);
    }

    [Fact]
    public void Generate_DIMode_InstrumentsHaveUnitAndDescription()
    {
        var doc = CreateSchemaWithMetrics(MeterLifecycle.DI);

        var content = GetEventsContent(doc);

        Assert.Contains("\"orders\"", content);
        Assert.Contains("\"Total orders placed\"", content);
        Assert.Contains("\"USD\"", content);
        Assert.Contains("\"Order amount\"", content);
    }

    // ═══════════════════════════════════════════════════════════════
    // 4. DI MODE — Service registration extension
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Generate_DIMode_GeneratesServiceCollectionExtensionFile()
    {
        var doc = CreateSchemaWithMetrics(MeterLifecycle.DI);

        var files = _generator.GenerateFromSchema(doc);

        Assert.Contains(files, f => f.FileName == "TestServiceMetricsServiceCollectionExtensions.g.cs");
    }

    [Fact]
    public void Generate_DIMode_ExtensionMethodHasCorrectSignature()
    {
        var doc = CreateSchemaWithMetrics(MeterLifecycle.DI);

        var files = _generator.GenerateFromSchema(doc);
        var extFile = files.First(f => f.FileName.Contains("ServiceCollection"));

        Assert.Contains("public static IServiceCollection AddTestServiceMetrics(this IServiceCollection services)", extFile.Content);
    }

    [Fact]
    public void Generate_DIMode_ExtensionMethodRegistersSingleton()
    {
        var doc = CreateSchemaWithMetrics(MeterLifecycle.DI);

        var files = _generator.GenerateFromSchema(doc);
        var extFile = files.First(f => f.FileName.Contains("ServiceCollection"));

        Assert.Contains("services.AddSingleton<TestServiceMetrics>()", extFile.Content);
    }

    [Fact]
    public void Generate_DIMode_ExtensionFileUsesCorrectNamespace()
    {
        var doc = CreateSchemaWithMetrics(MeterLifecycle.DI, ns: "MyCompany.Orders");

        var files = _generator.GenerateFromSchema(doc);
        var extFile = files.First(f => f.FileName.Contains("ServiceCollection"));

        Assert.Contains("namespace MyCompany.Orders;", extFile.Content);
    }

    [Fact]
    public void Generate_DIMode_ExtensionFileHasAutoGeneratedHeader()
    {
        var doc = CreateSchemaWithMetrics(MeterLifecycle.DI);

        var files = _generator.GenerateFromSchema(doc);
        var extFile = files.First(f => f.FileName.Contains("ServiceCollection"));

        Assert.Contains("<auto-generated>", extFile.Content);
        Assert.Contains("#nullable enable", extFile.Content);
    }

    [Fact]
    public void Generate_DIMode_ExtensionFileHasRequiredUsings()
    {
        var doc = CreateSchemaWithMetrics(MeterLifecycle.DI);

        var files = _generator.GenerateFromSchema(doc);
        var extFile = files.First(f => f.FileName.Contains("ServiceCollection"));

        Assert.Contains("using Microsoft.Extensions.DependencyInjection;", extFile.Content);
    }

    // ═══════════════════════════════════════════════════════════════
    // 5. DI MODE — Emit methods reference instance metrics
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Generate_DIMode_EventExtensionsUsesInstanceMetrics()
    {
        var doc = CreateSchemaWithMetrics(MeterLifecycle.DI);

        var content = GetEventsContent(doc);

        // Emit method should accept TestServiceMetrics as a parameter
        Assert.Contains("TestServiceMetrics metrics", content);
    }

    [Fact]
    public void Generate_DIMode_EmitMethodRecordsViaInstanceProperties()
    {
        var doc = CreateSchemaWithMetrics(MeterLifecycle.DI);

        var content = GetEventsContent(doc);

        Assert.Contains("metrics.OrderPlacedCount.Add(1", content);
    }

    // ═══════════════════════════════════════════════════════════════
    // 6. EDGE CASES
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Generate_DIMode_NoMetrics_DoesNotGenerateMetricsClass()
    {
        var doc = new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = "TestService",
                Version = "1.0.0",
                Namespace = "Test.Events",
                MeterLifecycle = MeterLifecycle.DI
            },
            Events =
            [
                new EventDefinition
                {
                    Name = "simple.event",
                    Id = 1000,
                    Severity = Severity.Info,
                    Message = "Simple event"
                }
            ]
        };

        var files = _generator.GenerateFromSchema(doc);
        var content = files.First(f => f.FileName.Contains("Events")).Content;

        Assert.DoesNotContain("TestServiceMetrics", content);
        Assert.DoesNotContain("IMeterFactory", content);
        Assert.DoesNotContain(files, f => f.FileName.Contains("ServiceCollection"));
    }

    [Fact]
    public void Generate_DIMode_CustomSchemaName_UsesCorrectClassName()
    {
        var doc = CreateSchemaWithMetrics(MeterLifecycle.DI, name: "OrderService");

        var content = GetEventsContent(doc);

        Assert.Contains("public sealed class OrderServiceMetrics", content);
        Assert.Contains("public OrderServiceMetrics(IMeterFactory meterFactory)", content);
    }

    [Fact]
    public void Generate_DIMode_CustomSchemaName_ExtensionMethodUsesCorrectName()
    {
        var doc = CreateSchemaWithMetrics(MeterLifecycle.DI, name: "OrderService");

        var files = _generator.GenerateFromSchema(doc);
        var extFile = files.First(f => f.FileName.Contains("ServiceCollection"));

        Assert.Contains("AddOrderServiceMetrics", extFile.Content);
        Assert.Contains("AddSingleton<OrderServiceMetrics>", extFile.Content);
    }

    [Theory]
    [InlineData("static")]
    [InlineData("Static")]
    [InlineData("STATIC")]
    public void Parse_MeterLifecycleStatic_AllCasings_ParsesCorrectly(string value)
    {
        var yaml = $"""
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
              meterLifecycle: {value}
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        Assert.Equal(MeterLifecycle.Static, result.Document!.Schema.MeterLifecycle);
    }

    [Theory]
    [InlineData("di")]
    [InlineData("Di")]
    [InlineData("DI")]
    public void Parse_MeterLifecycleDI_AllCasings_ParsesCorrectly(string value)
    {
        var yaml = $"""
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
              meterLifecycle: {value}
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        Assert.Equal(MeterLifecycle.DI, result.Document!.Schema.MeterLifecycle);
    }

    [Fact]
    public void Generate_DIMode_IncludesDiagnosticsMetricsUsing()
    {
        var doc = CreateSchemaWithMetrics(MeterLifecycle.DI);

        var content = GetEventsContent(doc);

        Assert.Contains("using System.Diagnostics.Metrics;", content);
    }
}
