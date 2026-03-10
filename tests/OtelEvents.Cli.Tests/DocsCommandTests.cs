using OtelEvents.Cli;

namespace OtelEvents.Cli.Tests;

/// <summary>
/// Tests for the DocsCommand — validates that the CLI docs subcommand
/// correctly generates Markdown documentation from YAML schema files.
/// </summary>
public sealed class DocsCommandTests : IDisposable
{
    private readonly string _tempDir;

    public DocsCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"all-cli-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private string CreateSchemaFile(string content, string fileName = "test.otel.yaml")
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private const string MinimalSchema = """
        schema:
          name: TestService
          version: "1.0.0"
          namespace: Test.Events

        events:
          test.event:
            id: 1001
            severity: INFO
            message: "Test event {field1}"
            fields:
              field1:
                type: string
                required: true
        """;

    // ═══════════════════════════════════════════════════════════════
    // 1. SUCCESS CASES
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Execute_ValidSchema_GeneratesOutputFile()
    {
        var schemaPath = CreateSchemaFile(MinimalSchema);
        var outputPath = Path.Combine(_tempDir, "output.md");

        var exitCode = DocsCommand.Execute(schemaPath, outputPath);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public void Execute_ValidSchema_OutputContainsSchemaName()
    {
        var schemaPath = CreateSchemaFile(MinimalSchema);
        var outputPath = Path.Combine(_tempDir, "output.md");

        DocsCommand.Execute(schemaPath, outputPath);

        var content = File.ReadAllText(outputPath);
        Assert.Contains("# TestService", content);
    }

    [Fact]
    public void Execute_ValidSchema_OutputContainsEvents()
    {
        var schemaPath = CreateSchemaFile(MinimalSchema);
        var outputPath = Path.Combine(_tempDir, "output.md");

        DocsCommand.Execute(schemaPath, outputPath);

        var content = File.ReadAllText(outputPath);
        Assert.Contains("### test.event", content);
        Assert.Contains("field1", content);
    }

    [Fact]
    public void Execute_SchemaWithEnums_OutputContainsEnums()
    {
        var schema = """
            schema:
              name: TestService
              version: "1.0.0"
              namespace: Test.Events

            enums:
              Status:
                description: "Order status"
                values:
                  - Active
                  - Inactive

            events:
              test.event:
                id: 1001
                severity: INFO
                message: "Status changed"
            """;

        var schemaPath = CreateSchemaFile(schema);
        var outputPath = Path.Combine(_tempDir, "output.md");

        DocsCommand.Execute(schemaPath, outputPath);

        var content = File.ReadAllText(outputPath);
        Assert.Contains("## Enum Definitions", content);
        Assert.Contains("### Status", content);
        Assert.Contains("Active", content);
        Assert.Contains("Inactive", content);
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. STDOUT FALLBACK
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Execute_NoOutputPath_ReturnsSuccessCode()
    {
        var schemaPath = CreateSchemaFile(MinimalSchema);

        var exitCode = DocsCommand.Execute(schemaPath, outputPath: null);

        Assert.Equal(0, exitCode);
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. ERROR CASES
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Execute_FileNotFound_ReturnsErrorCode()
    {
        var exitCode = DocsCommand.Execute("/nonexistent/path/schema.yaml", outputPath: null);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Execute_InvalidYaml_ReturnsErrorCode()
    {
        var invalidSchema = """
            this is not valid yaml: [unclosed
            """;

        var schemaPath = CreateSchemaFile(invalidSchema);

        var exitCode = DocsCommand.Execute(schemaPath, outputPath: null);

        Assert.Equal(1, exitCode);
    }

    // ═══════════════════════════════════════════════════════════════
    // 4. COMPLEX SCHEMA
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Execute_CompleteSchema_GeneratesFullDocumentation()
    {
        var schema = """
            schema:
              name: OrderService
              version: "1.0.0"
              namespace: Order.Events
              description: "Order processing events"

            fields:
              orderId:
                type: string
                description: "Unique order identifier"

            enums:
              OrderStatus:
                description: "Order lifecycle states"
                values:
                  - Pending
                  - Confirmed
                  - Shipped

            events:
              order.placed:
                id: 1001
                severity: INFO
                description: "A new order was placed"
                message: "Order {orderId} placed"
                fields:
                  orderId:
                    ref: orderId
                    required: true
                  amount:
                    type: double
                    required: true
                    unit: "USD"
                metrics:
                  order.placed.count:
                    type: counter
                    unit: "orders"
                    description: "Total orders placed"
                tags:
                  - order
                  - commerce
            """;

        var schemaPath = CreateSchemaFile(schema);
        var outputPath = Path.Combine(_tempDir, "output.md");

        var exitCode = DocsCommand.Execute(schemaPath, outputPath);

        Assert.Equal(0, exitCode);

        var content = File.ReadAllText(outputPath);
        Assert.Contains("# OrderService", content);
        Assert.Contains("Order processing events", content);
        Assert.Contains("## Events", content);
        Assert.Contains("### order.placed", content);
        Assert.Contains("## Enum Definitions", content);
        Assert.Contains("## Shared Fields", content);
        Assert.Contains("`order`", content);
        Assert.Contains("`commerce`", content);
    }
}
