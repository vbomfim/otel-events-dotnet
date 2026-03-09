using All.Cli.Commands;

namespace All.Cli.Tests;

/// <summary>
/// Tests for the diff command — compares two schema versions and reports changes.
/// Exit code 0 = no breaking changes, 1 = error, 2 = breaking changes detected.
/// </summary>
public sealed class DiffCommandTests : IDisposable
{
    private readonly StringWriter _stdout = new();
    private readonly StringWriter _stderr = new();
    private readonly List<string> _tempFiles = [];

    // ═══════════════════════════════════════════════════════════════
    // 1. IDENTICAL SCHEMAS
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Execute_IdenticalSchemas_ReturnsZeroAndShowsNoChanges()
    {
        var oldPath = CreateTempYaml(BaseSchemaYaml);
        var newPath = CreateTempYaml(BaseSchemaYaml);

        var exitCode = DiffCommand.Execute(oldPath, newPath, _stdout, _stderr);

        Assert.Equal(0, exitCode);
        Assert.Contains("no changes", _stdout.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. NON-BREAKING CHANGES
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Execute_EventAdded_ReturnsZeroAndShowsChange()
    {
        var oldPath = CreateTempYaml(BaseSchemaYaml);
        var newYaml = """
            schema:
              name: Test
              version: "1.1.0"
              namespace: Test.Events
              meterName: test.meter
            events:
              test.event:
                id: 1
                severity: INFO
                message: "Test {field1}"
                fields:
                  field1:
                    type: string
              test.new.event:
                id: 2
                severity: INFO
                message: "New event"
            """;
        var newPath = CreateTempYaml(newYaml);

        var exitCode = DiffCommand.Execute(oldPath, newPath, _stdout, _stderr);

        Assert.Equal(0, exitCode);
        var stdoutOutput = _stdout.ToString();
        Assert.Contains("test.new.event", stdoutOutput);
        Assert.Contains("added", stdoutOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_FieldAdded_ReturnsZeroAndShowsChange()
    {
        var oldPath = CreateTempYaml(BaseSchemaYaml);
        var newYaml = """
            schema:
              name: Test
              version: "1.1.0"
              namespace: Test.Events
              meterName: test.meter
            events:
              test.event:
                id: 1
                severity: INFO
                message: "Test {field1} {field2}"
                fields:
                  field1:
                    type: string
                  field2:
                    type: int
            """;
        var newPath = CreateTempYaml(newYaml);

        var exitCode = DiffCommand.Execute(oldPath, newPath, _stdout, _stderr);

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("BREAKING", _stdout.ToString());
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. BREAKING CHANGES
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Execute_EventRemoved_ReturnsTwoAndShowsBreaking()
    {
        var oldYaml = """
            schema:
              name: Test
              version: "1.0.0"
              namespace: Test.Events
              meterName: test.meter
            events:
              test.event:
                id: 1
                severity: INFO
                message: "Test {field1}"
                fields:
                  field1:
                    type: string
              test.removed:
                id: 2
                severity: INFO
                message: "Will be removed"
            """;
        var oldPath = CreateTempYaml(oldYaml);
        var newPath = CreateTempYaml(BaseSchemaYaml);

        var exitCode = DiffCommand.Execute(oldPath, newPath, _stdout, _stderr);

        Assert.Equal(2, exitCode);
        var stdoutOutput = _stdout.ToString();
        Assert.Contains("BREAKING", stdoutOutput);
        Assert.Contains("test.removed", stdoutOutput);
    }

    [Fact]
    public void Execute_FieldTypeChanged_ReturnsTwoAndShowsBreaking()
    {
        var oldPath = CreateTempYaml(BaseSchemaYaml);
        var newYaml = """
            schema:
              name: Test
              version: "2.0.0"
              namespace: Test.Events
              meterName: test.meter
            events:
              test.event:
                id: 1
                severity: INFO
                message: "Test {field1}"
                fields:
                  field1:
                    type: int
            """;
        var newPath = CreateTempYaml(newYaml);

        var exitCode = DiffCommand.Execute(oldPath, newPath, _stdout, _stderr);

        Assert.Equal(2, exitCode);
        var stdoutOutput = _stdout.ToString();
        Assert.Contains("BREAKING", stdoutOutput);
    }

    [Fact]
    public void Execute_BreakingChanges_ShowsBreakingCount()
    {
        var oldYaml = """
            schema:
              name: Test
              version: "1.0.0"
              namespace: Test.Events
              meterName: test.meter
            events:
              test.event:
                id: 1
                severity: INFO
                message: "Test {field1}"
                fields:
                  field1:
                    type: string
              test.removed:
                id: 2
                severity: INFO
                message: "Will be removed"
            """;
        var oldPath = CreateTempYaml(oldYaml);
        var newPath = CreateTempYaml(BaseSchemaYaml);

        DiffCommand.Execute(oldPath, newPath, _stdout, _stderr);

        var stdoutOutput = _stdout.ToString();
        Assert.Contains("breaking", stdoutOutput, StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════
    // 4. FILE NOT FOUND
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Execute_NonExistentOldFile_ReturnsOneAndShowsError()
    {
        var newPath = CreateTempYaml(BaseSchemaYaml);

        var exitCode = DiffCommand.Execute("/nonexistent/old.yaml", newPath, _stdout, _stderr);

        Assert.Equal(1, exitCode);
        Assert.Contains("not found", _stderr.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_NonExistentNewFile_ReturnsOneAndShowsError()
    {
        var oldPath = CreateTempYaml(BaseSchemaYaml);

        var exitCode = DiffCommand.Execute(oldPath, "/nonexistent/new.yaml", _stdout, _stderr);

        Assert.Equal(1, exitCode);
        Assert.Contains("not found", _stderr.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_InvalidOldSchema_ReturnsOneAndShowsErrors()
    {
        var oldPath = CreateTempYaml("not: valid: yaml: {{{");
        var newPath = CreateTempYaml(BaseSchemaYaml);

        var exitCode = DiffCommand.Execute(oldPath, newPath, _stdout, _stderr);

        Assert.Equal(1, exitCode);
        Assert.NotEqual("", _stderr.ToString());
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
    /// Base schema used as the "old" version in diff tests.
    /// </summary>
    private const string BaseSchemaYaml = """
        schema:
          name: Test
          version: "1.0.0"
          namespace: Test.Events
          meterName: test.meter
        events:
          test.event:
            id: 1
            severity: INFO
            message: "Test {field1}"
            fields:
              field1:
                type: string
        """;
}
