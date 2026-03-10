using OtelEvents.Cli.Commands;

namespace OtelEvents.Cli.Tests;

/// <summary>
/// Tests for the generate command — generates C# source files from .otel.yaml schemas.
/// Exit code 0 = success, 1 = errors.
/// </summary>
public sealed class GenerateCommandTests : IDisposable
{
    private readonly StringWriter _stdout = new();
    private readonly StringWriter _stderr = new();
    private readonly List<string> _tempPaths = [];

    // ═══════════════════════════════════════════════════════════════
    // 1. SUCCESSFUL GENERATION
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Execute_ValidSchema_ReturnsZeroAndWritesFiles()
    {
        var path = CreateTempYaml(ValidateCommandTests.ValidSchemaYaml);
        var outputDir = CreateTempDir();

        var exitCode = GenerateCommand.Execute(path, outputDir, _stdout, _stderr);

        Assert.Equal(0, exitCode);
        var generatedFiles = Directory.GetFiles(outputDir, "*.cs");
        Assert.NotEmpty(generatedFiles);
    }

    [Fact]
    public void Execute_ValidSchema_ShowsGeneratedFileNames()
    {
        var path = CreateTempYaml(ValidateCommandTests.ValidSchemaYaml);
        var outputDir = CreateTempDir();

        GenerateCommand.Execute(path, outputDir, _stdout, _stderr);

        var stdoutOutput = _stdout.ToString();
        Assert.Contains(".cs", stdoutOutput);
    }

    [Fact]
    public void Execute_ValidSchema_CreatesOutputDirectoryIfMissing()
    {
        var path = CreateTempYaml(ValidateCommandTests.ValidSchemaYaml);
        var outputDir = Path.Combine(Path.GetTempPath(), $"all-gen-{Guid.NewGuid()}");
        _tempPaths.Add(outputDir);

        var exitCode = GenerateCommand.Execute(path, outputDir, _stdout, _stderr);

        Assert.Equal(0, exitCode);
        Assert.True(Directory.Exists(outputDir));
    }

    [Fact]
    public void Execute_ValidSchema_GeneratedFilesContainNamespace()
    {
        var path = CreateTempYaml(ValidateCommandTests.ValidSchemaYaml);
        var outputDir = CreateTempDir();

        GenerateCommand.Execute(path, outputDir, _stdout, _stderr);

        var generatedFiles = Directory.GetFiles(outputDir, "*.cs");
        Assert.NotEmpty(generatedFiles);
        var content = File.ReadAllText(generatedFiles[0]);
        Assert.Contains("namespace", content);
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. ERROR CASES
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Execute_InvalidSchema_ReturnsOneAndShowsErrors()
    {
        var yaml = """
            schema:
              name: Test
              version: "1.0.0"
              namespace: Test.Events
              meterName: test.meter
            events:
              INVALID_NAME:
                id: 1
                severity: INFO
                message: "Test"
            """;
        var path = CreateTempYaml(yaml);
        var outputDir = CreateTempDir();

        var exitCode = GenerateCommand.Execute(path, outputDir, _stdout, _stderr);

        Assert.Equal(1, exitCode);
        Assert.Contains("ALL_SCHEMA_", _stderr.ToString());
    }

    [Fact]
    public void Execute_NonExistentFile_ReturnsOneAndShowsError()
    {
        var outputDir = CreateTempDir();

        var exitCode = GenerateCommand.Execute("/nonexistent/path.yaml", outputDir, _stdout, _stderr);

        Assert.Equal(1, exitCode);
        Assert.Contains("not found", _stderr.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_InvalidSchema_DoesNotWriteFiles()
    {
        var yaml = "not: valid: yaml: {{{";
        var path = CreateTempYaml(yaml);
        var outputDir = CreateTempDir();

        GenerateCommand.Execute(path, outputDir, _stdout, _stderr);

        var generatedFiles = Directory.GetFiles(outputDir, "*.cs");
        Assert.Empty(generatedFiles);
    }

    // ═══════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════

    private string CreateTempYaml(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.otel.yaml");
        File.WriteAllText(path, content);
        _tempPaths.Add(path);
        return path;
    }

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"all-gen-{Guid.NewGuid()}");
        Directory.CreateDirectory(dir);
        _tempPaths.Add(dir);
        return dir;
    }

    public void Dispose()
    {
        _stdout.Dispose();
        _stderr.Dispose();
        foreach (var path in _tempPaths)
        {
            if (File.Exists(path))
                File.Delete(path);
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
    }
}
