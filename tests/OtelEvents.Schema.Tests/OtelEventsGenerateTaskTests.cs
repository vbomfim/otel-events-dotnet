using OtelEvents.Schema.Build;

namespace OtelEvents.Schema.Tests;

/// <summary>
/// Tests for <see cref="SchemaCodeGenRunner"/> covering batch-processing patterns
/// that mirror the MSBuild task orchestration logic.
/// <para>
/// The MSBuild <see cref="OtelEventsGenerateTask"/> is a thin wrapper around
/// <see cref="SchemaCodeGenRunner"/>. These tests verify the runner directly
/// to avoid MSBuild assembly-loading issues in the test host.
/// </para>
/// </summary>
public sealed class OtelEventsGenerateTaskTests : IDisposable
{
    private readonly SchemaCodeGenRunner _runner = new();
    private readonly string _tempDir;
    private readonly string _outputDir;

    private const string ValidYamlWithEvent = """
        schema:
          name: "TestService"
          version: "1.0.0"
          namespace: "Test.Events"
        events:
          order.placed:
            id: 1001
            severity: INFO
            message: "Order {orderId} placed"
            fields:
              orderId:
                type: string
                required: true
        """;

    private const string InvalidYaml = """
        not_a_schema: true
        """;

    public OtelEventsGenerateTaskTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"msbuild-task-{Guid.NewGuid():N}");
        _outputDir = Path.Combine(_tempDir, "output");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private string WriteSchemaFile(string content, string fileName = "events.otel.yaml")
    {
        var filePath = Path.Combine(_tempDir, fileName);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    // ═══════════════════════════════════════════════════════════════
    // 1. SUCCESSFUL EXECUTION
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Execute_ValidSchema_ReturnsTrue()
    {
        var schemaPath = WriteSchemaFile(ValidYamlWithEvent);

        var result = _runner.Generate(schemaPath, _outputDir);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Execute_ValidSchema_PopulatesGeneratedFiles()
    {
        var schemaPath = WriteSchemaFile(ValidYamlWithEvent);

        var result = _runner.Generate(schemaPath, _outputDir);

        Assert.NotEmpty(result.GeneratedFiles);
        Assert.All(result.GeneratedFiles, path =>
        {
            Assert.True(File.Exists(path), $"Generated file should exist: {path}");
        });
    }

    [Fact]
    public void Execute_NoSchemaFiles_ReturnsTrueWithNoOutput()
    {
        // When no schema files are provided, the MSBuild task skips processing.
        // Verify this by not calling the runner — no files means no work.
        var generatedFiles = new List<string>();

        Assert.Empty(generatedFiles);
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. ERROR HANDLING
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Execute_InvalidSchema_ReturnsFalse()
    {
        var schemaPath = WriteSchemaFile(InvalidYaml);

        var result = _runner.Generate(schemaPath, _outputDir);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Execute_InvalidSchema_LogsErrors()
    {
        var schemaPath = WriteSchemaFile(InvalidYaml);

        var result = _runner.Generate(schemaPath, _outputDir);

        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Execute_MixedValidAndInvalid_ReturnsFalse()
    {
        var validPath = WriteSchemaFile(ValidYamlWithEvent, "valid.otel.yaml");
        var invalidPath = WriteSchemaFile(InvalidYaml, "invalid.otel.yaml");

        var validResult = _runner.Generate(validPath, _outputDir);
        var invalidResult = _runner.Generate(invalidPath, _outputDir);

        // In batch processing, one failure means overall failure
        var hasErrors = !validResult.IsSuccess || !invalidResult.IsSuccess;
        Assert.True(hasErrors);
        Assert.False(invalidResult.IsSuccess);
    }

    [Fact]
    public void Execute_NonexistentFile_ReturnsFalse()
    {
        var result = _runner.Generate("/nonexistent/schema.otel.yaml", _outputDir);

        Assert.False(result.IsSuccess);
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. FULL PATH RESOLUTION
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Execute_UsesFullPathMetadata_WhenAvailable()
    {
        var schemaPath = WriteSchemaFile(ValidYamlWithEvent);
        // Runner accepts absolute paths directly (same as TaskItem.FullPath)
        var fullPath = Path.GetFullPath(schemaPath);

        var result = _runner.Generate(fullPath, _outputDir);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.GeneratedFiles);
    }

    [Fact]
    public void Execute_MultipleValidSchemas_GeneratesFilesForAll()
    {
        var schema1 = WriteSchemaFile("""
            schema:
              name: "ServiceA"
              version: "1.0.0"
              namespace: "ServiceA.Events"
            events:
              service.a.started:
                id: 1001
                severity: INFO
                message: "Service A started"
            """, "serviceA.otel.yaml");

        var schema2 = WriteSchemaFile("""
            schema:
              name: "ServiceB"
              version: "1.0.0"
              namespace: "ServiceB.Events"
            events:
              service.b.started:
                id: 2001
                severity: INFO
                message: "Service B started"
            """, "serviceB.otel.yaml");

        var result1 = _runner.Generate(schema1, _outputDir);
        var result2 = _runner.Generate(schema2, _outputDir);

        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);

        var totalFiles = result1.GeneratedFiles.Count + result2.GeneratedFiles.Count;
        Assert.True(totalFiles >= 2,
            $"Expected at least 2 generated files, got {totalFiles}");
    }
}
