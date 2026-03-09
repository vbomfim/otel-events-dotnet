using OtelEvents.Cli.Commands;

namespace OtelEvents.Cli.Tests;

/// <summary>
/// Tests for the validate command — parses and validates .all.yaml schema files.
/// Exit code 0 = valid, 1 = errors found.
/// </summary>
public sealed class ValidateCommandTests : IDisposable
{
    private readonly StringWriter _stdout = new();
    private readonly StringWriter _stderr = new();
    private readonly List<string> _tempFiles = [];

    // ═══════════════════════════════════════════════════════════════
    // 1. VALID SCHEMAS
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Execute_ValidSchemaFile_ReturnsZeroAndShowsSuccess()
    {
        var path = CreateTempYaml(ValidSchemaYaml);

        var exitCode = ValidateCommand.Execute(path, _stdout, _stderr);

        Assert.Equal(0, exitCode);
        Assert.Contains("valid", _stdout.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("", _stderr.ToString());
    }

    [Fact]
    public void Execute_ValidSchemaFile_DisplaysSchemaName()
    {
        var path = CreateTempYaml(ValidSchemaYaml);

        ValidateCommand.Execute(path, _stdout, _stderr);

        Assert.Contains("Test", _stdout.ToString());
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. PARSE ERRORS
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Execute_InvalidYaml_ReturnsOneAndShowsErrors()
    {
        var path = CreateTempYaml("not: valid: yaml: {{{");

        var exitCode = ValidateCommand.Execute(path, _stdout, _stderr);

        Assert.Equal(1, exitCode);
        Assert.NotEqual("", _stderr.ToString());
    }

    [Fact]
    public void Execute_MissingSchemaSection_ReturnsOneAndShowsErrors()
    {
        var yaml = """
            events:
              test.event:
                id: 1
                severity: INFO
                message: "Test"
            """;
        var path = CreateTempYaml(yaml);

        var exitCode = ValidateCommand.Execute(path, _stdout, _stderr);

        Assert.Equal(1, exitCode);
        Assert.NotEqual("", _stderr.ToString());
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. VALIDATION ERRORS
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Execute_SchemaWithValidationErrors_ReturnsOneAndShowsErrors()
    {
        // Invalid event name format (uppercase not allowed)
        var yaml = """
            schema:
              name: Test
              version: "1.0.0"
              namespace: Test.Events
              meterName: test.meter
            events:
              INVALID_EVENT_NAME:
                id: 1
                severity: INFO
                message: "Test"
            """;
        var path = CreateTempYaml(yaml);

        var exitCode = ValidateCommand.Execute(path, _stdout, _stderr);

        Assert.Equal(1, exitCode);
        var stderrOutput = _stderr.ToString();
        Assert.Contains("ALL_SCHEMA_", stderrOutput);
    }

    [Fact]
    public void Execute_DuplicateEventIds_ReturnsOneAndShowsErrorCode()
    {
        var yaml = """
            schema:
              name: Test
              version: "1.0.0"
              namespace: Test.Events
              meterName: test.meter
            events:
              test.first:
                id: 1
                severity: INFO
                message: "First"
              test.second:
                id: 1
                severity: INFO
                message: "Second"
            """;
        var path = CreateTempYaml(yaml);

        var exitCode = ValidateCommand.Execute(path, _stdout, _stderr);

        Assert.Equal(1, exitCode);
        Assert.Contains("ALL_SCHEMA_012", _stderr.ToString());
    }

    // ═══════════════════════════════════════════════════════════════
    // 4. FILE NOT FOUND
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Execute_NonExistentFile_ReturnsOneAndShowsError()
    {
        var exitCode = ValidateCommand.Execute("/nonexistent/path.all.yaml", _stdout, _stderr);

        Assert.Equal(1, exitCode);
        Assert.Contains("not found", _stderr.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════
    // 5. ERROR COUNT
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Execute_MultipleValidationErrors_ShowsErrorCount()
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
                severity: INVALID_SEVERITY
                message: "Test {missing}"
            """;
        var path = CreateTempYaml(yaml);

        ValidateCommand.Execute(path, _stdout, _stderr);

        var stderrOutput = _stderr.ToString();
        Assert.Contains("error", stderrOutput, StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════

    private string CreateTempYaml(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.all.yaml");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        _stdout.Dispose();
        _stderr.Dispose();
        foreach (var file in _tempFiles)
        {
            if (File.Exists(file))
                File.Delete(file);
        }
    }

    /// <summary>
    /// Minimal valid .all.yaml schema used across tests.
    /// </summary>
    internal const string ValidSchemaYaml = """
        schema:
          name: Test
          version: "1.0.0"
          namespace: Test.Events
          meterName: test.meter
        events:
          test.event:
            id: 1
            severity: INFO
            message: "Test event {field1}"
            fields:
              field1:
                type: string
        """;
}
