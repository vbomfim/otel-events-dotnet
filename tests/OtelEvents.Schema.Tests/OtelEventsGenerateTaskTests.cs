using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using OtelEvents.Schema.Build;

namespace OtelEvents.Schema.Tests;

/// <summary>
/// Tests for <see cref="OtelEventsGenerateTask"/> — the MSBuild task wrapper
/// that integrates schema code generation into the build pipeline.
/// </summary>
public sealed class OtelEventsGenerateTaskTests : IDisposable
{
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

    private static OtelEventsGenerateTask CreateTask(
        ITaskItem[] schemaFiles,
        string outputDirectory)
    {
        return new OtelEventsGenerateTask
        {
            SchemaFiles = schemaFiles,
            OutputDirectory = outputDirectory,
            BuildEngine = new StubBuildEngine()
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // 1. SUCCESSFUL EXECUTION
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Execute_ValidSchema_ReturnsTrue()
    {
        var schemaPath = WriteSchemaFile(ValidYamlWithEvent);
        var task = CreateTask([new TaskItem(schemaPath)], _outputDir);

        var result = task.Execute();

        Assert.True(result);
    }

    [Fact]
    public void Execute_ValidSchema_PopulatesGeneratedFiles()
    {
        var schemaPath = WriteSchemaFile(ValidYamlWithEvent);
        var task = CreateTask([new TaskItem(schemaPath)], _outputDir);

        task.Execute();

        Assert.NotEmpty(task.GeneratedFiles);
        Assert.All(task.GeneratedFiles, item =>
        {
            Assert.True(File.Exists(item.ItemSpec), $"Generated file should exist: {item.ItemSpec}");
        });
    }

    [Fact]
    public void Execute_NoSchemaFiles_ReturnsTrueWithNoOutput()
    {
        var task = CreateTask([], _outputDir);

        var result = task.Execute();

        Assert.True(result);
        Assert.Empty(task.GeneratedFiles);
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. ERROR HANDLING
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Execute_InvalidSchema_ReturnsFalse()
    {
        var schemaPath = WriteSchemaFile(InvalidYaml);
        var task = CreateTask([new TaskItem(schemaPath)], _outputDir);

        var result = task.Execute();

        Assert.False(result);
    }

    [Fact]
    public void Execute_InvalidSchema_LogsErrors()
    {
        var schemaPath = WriteSchemaFile(InvalidYaml);
        var engine = new StubBuildEngine();
        var task = new OtelEventsGenerateTask
        {
            SchemaFiles = [new TaskItem(schemaPath)],
            OutputDirectory = _outputDir,
            BuildEngine = engine
        };

        task.Execute();

        Assert.NotEmpty(engine.Errors);
    }

    [Fact]
    public void Execute_MixedValidAndInvalid_ReturnsFalse()
    {
        var validPath = WriteSchemaFile(ValidYamlWithEvent, "valid.otel.yaml");
        var invalidPath = WriteSchemaFile(InvalidYaml, "invalid.otel.yaml");
        var task = CreateTask(
            [new TaskItem(validPath), new TaskItem(invalidPath)],
            _outputDir);

        var result = task.Execute();

        Assert.False(result);
    }

    [Fact]
    public void Execute_NonexistentFile_ReturnsFalse()
    {
        var task = CreateTask(
            [new TaskItem("/nonexistent/schema.otel.yaml")],
            _outputDir);

        var result = task.Execute();

        Assert.False(result);
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. TASK ITEM METADATA
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Execute_UsesFullPathMetadata_WhenAvailable()
    {
        var schemaPath = WriteSchemaFile(ValidYamlWithEvent);
        var taskItem = new TaskItem(schemaPath);
        // FullPath metadata is auto-populated by TaskItem for absolute paths
        var task = CreateTask([taskItem], _outputDir);

        var result = task.Execute();

        Assert.True(result);
        Assert.NotEmpty(task.GeneratedFiles);
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

        var task = CreateTask(
            [new TaskItem(schema1), new TaskItem(schema2)],
            _outputDir);

        var result = task.Execute();

        Assert.True(result);
        Assert.True(task.GeneratedFiles.Length >= 2,
            $"Expected at least 2 generated files, got {task.GeneratedFiles.Length}");
    }

    // ═══════════════════════════════════════════════════════════════
    // STUB BUILD ENGINE (minimal MSBuild host for testing)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Minimal <see cref="IBuildEngine"/> implementation for unit testing MSBuild tasks.
    /// Captures logged errors and messages for assertion.
    /// </summary>
    private sealed class StubBuildEngine : IBuildEngine
    {
        public List<string> Errors { get; } = [];
        public List<string> Messages { get; } = [];

        public bool ContinueOnError => false;
        public int LineNumberOfTaskNode => 0;
        public int ColumnNumberOfTaskNode => 0;
        public string ProjectFileOfTaskNode => "test.csproj";

        public void LogErrorEvent(BuildErrorEventArgs e) =>
            Errors.Add(e.Message ?? string.Empty);

        public void LogWarningEvent(BuildWarningEventArgs e) { }

        public void LogMessageEvent(BuildMessageEventArgs e) =>
            Messages.Add(e.Message ?? string.Empty);

        public void LogCustomEvent(CustomBuildEventArgs e) { }

        public bool BuildProjectFile(string projectFileName, string[] targetNames,
            System.Collections.IDictionary globalProperties, System.Collections.IDictionary targetOutputs) => true;
    }
}
