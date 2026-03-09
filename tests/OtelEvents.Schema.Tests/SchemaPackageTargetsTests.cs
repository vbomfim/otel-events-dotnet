using OtelEvents.Schema.Packaging;

namespace OtelEvents.Schema.Tests;

/// <summary>
/// Tests for <see cref="SchemaPackageTargets"/> — MSBuild integration helpers
/// that generate NuGet package content metadata for .all.yaml files.
/// </summary>
public sealed class SchemaPackageTargetsTests : IDisposable
{
    private readonly string _tempDir;

    public SchemaPackageTargetsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"schema-targets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    // ── FindSchemaFiles ──────────────────────────────────────────────

    [Fact]
    public void FindSchemaFiles_ReturnsAllYamlFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir, "events.all.yaml"), "schema:");
        File.WriteAllText(Path.Combine(_tempDir, "shared.all.yaml"), "schema:");
        File.WriteAllText(Path.Combine(_tempDir, "readme.md"), "not a schema");

        var files = SchemaPackageTargets.FindSchemaFiles(_tempDir);

        Assert.Equal(2, files.Count);
        Assert.All(files, f => Assert.EndsWith(".all.yaml", f));
    }

    [Fact]
    public void FindSchemaFiles_SearchesSubdirectories()
    {
        var subDir = Path.Combine(_tempDir, "schemas");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "nested.all.yaml"), "schema:");

        var files = SchemaPackageTargets.FindSchemaFiles(_tempDir);

        Assert.Single(files);
    }

    [Fact]
    public void FindSchemaFiles_ExcludesBinAndObj()
    {
        var binDir = Path.Combine(_tempDir, "bin");
        var objDir = Path.Combine(_tempDir, "obj");
        Directory.CreateDirectory(binDir);
        Directory.CreateDirectory(objDir);
        File.WriteAllText(Path.Combine(binDir, "output.all.yaml"), "schema:");
        File.WriteAllText(Path.Combine(objDir, "temp.all.yaml"), "schema:");
        File.WriteAllText(Path.Combine(_tempDir, "real.all.yaml"), "schema:");

        var files = SchemaPackageTargets.FindSchemaFiles(_tempDir);

        Assert.Single(files);
        Assert.Contains("real.all.yaml", files[0]);
    }

    [Fact]
    public void FindSchemaFiles_EmptyDirectory_ReturnsEmpty()
    {
        var files = SchemaPackageTargets.FindSchemaFiles(_tempDir);

        Assert.Empty(files);
    }

    [Fact]
    public void FindSchemaFiles_NonexistentDirectory_ReturnsEmpty()
    {
        var files = SchemaPackageTargets.FindSchemaFiles("/nonexistent/path");

        Assert.Empty(files);
    }

    // ── GetPackagePath ───────────────────────────────────────────────

    [Fact]
    public void GetPackagePath_ReturnsContentFilesPath()
    {
        var path = SchemaPackageTargets.GetPackagePath("events.all.yaml");

        Assert.Equal("contentFiles/any/any/schemas/events.all.yaml", path);
    }

    [Fact]
    public void GetPackagePath_HandlesNestedPaths()
    {
        var path = SchemaPackageTargets.GetPackagePath("sub/events.all.yaml");

        // Only the filename should be used — flatten to avoid path issues
        Assert.Equal("contentFiles/any/any/schemas/events.all.yaml", path);
    }

    // ── GeneratePackageMetadata ──────────────────────────────────────

    [Fact]
    public void GeneratePackageMetadata_CreatesCorrectEntries()
    {
        File.WriteAllText(Path.Combine(_tempDir, "events.all.yaml"), "schema:");
        File.WriteAllText(Path.Combine(_tempDir, "shared.all.yaml"), "schema:");

        var metadata = SchemaPackageTargets.GeneratePackageMetadata(_tempDir);

        Assert.Equal(2, metadata.Count);
        Assert.All(metadata, m =>
        {
            Assert.True(File.Exists(m.SourcePath));
            Assert.StartsWith("contentFiles/any/any/schemas/", m.PackagePath);
            Assert.Equal("Content", m.BuildAction);
            Assert.True(m.CopyToOutput);
        });
    }

    [Fact]
    public void GeneratePackageMetadata_EmptyDirectory_ReturnsEmpty()
    {
        var metadata = SchemaPackageTargets.GeneratePackageMetadata(_tempDir);

        Assert.Empty(metadata);
    }
}
