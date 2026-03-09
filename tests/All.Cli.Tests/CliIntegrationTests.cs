using System.CommandLine;
using All.Cli.Commands;

namespace All.Cli.Tests;

/// <summary>
/// Integration tests verifying that System.CommandLine wiring produces
/// correct exit codes when invoked with argument strings.
/// </summary>
public sealed class CliIntegrationTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    [Fact]
    public void Validate_WithValidFile_ReturnsZero()
    {
        var path = CreateTempYaml(ValidateCommandTests.ValidSchemaYaml);
        var root = Program.BuildRootCommand();

        var exitCode = root.Parse(["validate", path]).Invoke();

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Validate_WithInvalidFile_ReturnsOne()
    {
        var root = Program.BuildRootCommand();

        var exitCode = root.Parse(["validate", "/nonexistent.yaml"]).Invoke();

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Generate_WithValidFile_ReturnsZero()
    {
        var path = CreateTempYaml(ValidateCommandTests.ValidSchemaYaml);
        var outputDir = Path.Combine(Path.GetTempPath(), $"all-gen-{Guid.NewGuid()}");
        _tempFiles.Add(outputDir);
        var root = Program.BuildRootCommand();

        var exitCode = root.Parse(["generate", path, "-o", outputDir]).Invoke();

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Diff_WithIdenticalFiles_ReturnsZero()
    {
        var path1 = CreateTempYaml(ValidateCommandTests.ValidSchemaYaml);
        var path2 = CreateTempYaml(ValidateCommandTests.ValidSchemaYaml);
        var root = Program.BuildRootCommand();

        var exitCode = root.Parse(["diff", path1, path2]).Invoke();

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Diff_WithBreakingChanges_ReturnsTwo()
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
                message: "Removed"
            """;
        var oldPath = CreateTempYaml(oldYaml);
        var newPath = CreateTempYaml(ValidateCommandTests.ValidSchemaYaml);
        var root = Program.BuildRootCommand();

        var exitCode = root.Parse(["diff", oldPath, newPath]).Invoke();

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public void NoArguments_ShowsHelpAndReturnsOne()
    {
        var root = Program.BuildRootCommand();

        // System.CommandLine returns 1 when required command is not provided
        var exitCode = root.Parse([]).Invoke();

        Assert.Equal(1, exitCode);
    }

    private string CreateTempYaml(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.all.yaml");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var path in _tempFiles)
        {
            if (File.Exists(path))
                File.Delete(path);
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
    }
}
