using All.Schema.Models;
using All.Schema.Packaging;
using All.Schema.Validation;

namespace All.Schema.Tests;

/// <summary>
/// Tests for <see cref="SchemaPackageResolver"/> — discovering .all.yaml files
/// from NuGet package content directories and resolving package imports.
/// </summary>
public sealed class SchemaPackageResolverTests : IDisposable
{
    private readonly string _tempDir;

    public SchemaPackageResolverTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"schema-pkg-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    // ── IsPackageImport ──────────────────────────────────────────────

    [Theory]
    [InlineData("package:MyCompany.Events/common.all.yaml", true)]
    [InlineData("package:MyLib/schemas/shared.all.yaml", true)]
    [InlineData("shared/common.all.yaml", false)]
    [InlineData("common.all.yaml", false)]
    [InlineData("", false)]
    [InlineData("package:", false)]
    public void IsPackageImport_ClassifiesImportPaths(string importPath, bool expected)
    {
        Assert.Equal(expected, SchemaPackageResolver.IsPackageImport(importPath));
    }

    // ── Constructor validation ───────────────────────────────────────

    [Fact]
    public void Constructor_NullDirectories_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new SchemaPackageResolver(null!));
    }

    [Fact]
    public void Constructor_EmptyDirectories_Succeeds()
    {
        var resolver = new SchemaPackageResolver([]);
        Assert.Empty(resolver.DiscoverSchemas());
    }

    // ── DiscoverSchemas ──────────────────────────────────────────────

    [Fact]
    public void DiscoverSchemas_FindsAllYamlFilesInDirectories()
    {
        // Simulate NuGet package content: packageName/version/contentFiles/any/any/schemas/
        var pkgDir = CreatePackageSchemaDir("MyCompany.Events.Common", "1.0.0");
        File.WriteAllText(Path.Combine(pkgDir, "common.all.yaml"), MinimalYaml("Common"));
        File.WriteAllText(Path.Combine(pkgDir, "http.all.yaml"), MinimalYaml("Http"));

        var resolver = new SchemaPackageResolver([pkgDir]);
        var schemas = resolver.DiscoverSchemas();

        Assert.Equal(2, schemas.Count);
        Assert.Contains(schemas, s => s.SchemaFileName == "common.all.yaml");
        Assert.Contains(schemas, s => s.SchemaFileName == "http.all.yaml");
    }

    [Fact]
    public void DiscoverSchemas_MultipleDirectories_FindsAll()
    {
        var dir1 = CreatePackageSchemaDir("Package.A", "1.0.0");
        var dir2 = CreatePackageSchemaDir("Package.B", "2.0.0");
        File.WriteAllText(Path.Combine(dir1, "a.all.yaml"), MinimalYaml("A"));
        File.WriteAllText(Path.Combine(dir2, "b.all.yaml"), MinimalYaml("B"));

        var resolver = new SchemaPackageResolver([dir1, dir2]);
        var schemas = resolver.DiscoverSchemas();

        Assert.Equal(2, schemas.Count);
    }

    [Fact]
    public void DiscoverSchemas_IgnoresNonYamlFiles()
    {
        var dir = CreatePackageSchemaDir("MyPkg", "1.0.0");
        File.WriteAllText(Path.Combine(dir, "schema.all.yaml"), MinimalYaml("Schema"));
        File.WriteAllText(Path.Combine(dir, "readme.txt"), "Not a schema");
        File.WriteAllText(Path.Combine(dir, "config.yaml"), "not: all-schema");

        var resolver = new SchemaPackageResolver([dir]);
        var schemas = resolver.DiscoverSchemas();

        Assert.Single(schemas);
        Assert.Equal("schema.all.yaml", schemas[0].SchemaFileName);
    }

    [Fact]
    public void DiscoverSchemas_NonexistentDirectory_ReturnsEmpty()
    {
        var resolver = new SchemaPackageResolver(["/nonexistent/path"]);
        var schemas = resolver.DiscoverSchemas();

        Assert.Empty(schemas);
    }

    // ── Resolve ──────────────────────────────────────────────────────

    [Fact]
    public void Resolve_ValidPackageImport_ReturnsFilePath()
    {
        var dir = CreatePackageSchemaDir("MyCompany.Events", "1.0.0");
        var schemaPath = Path.Combine(dir, "common.all.yaml");
        File.WriteAllText(schemaPath, MinimalYaml("Common"));

        var resolver = new SchemaPackageResolver([dir]);
        var result = resolver.Resolve("package:MyCompany.Events/common.all.yaml");

        Assert.NotNull(result);
        Assert.Equal(Path.GetFullPath(schemaPath), Path.GetFullPath(result));
    }

    [Fact]
    public void Resolve_PackageNotFound_ReturnsNull()
    {
        var resolver = new SchemaPackageResolver([]);
        var result = resolver.Resolve("package:NonExistent/schema.all.yaml");

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_SchemaFileNotInDirectory_ReturnsNull()
    {
        var dir = CreatePackageSchemaDir("MyPkg", "1.0.0");
        File.WriteAllText(Path.Combine(dir, "other.all.yaml"), MinimalYaml("Other"));

        var resolver = new SchemaPackageResolver([dir]);
        var result = resolver.Resolve("package:MyPkg/missing.all.yaml");

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_InvalidPackageImport_ReturnsNull()
    {
        var resolver = new SchemaPackageResolver([]);
        var result = resolver.Resolve("not-a-package-import.all.yaml");

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_PathTraversalAttempt_ReturnsNull()
    {
        var dir = CreatePackageSchemaDir("MyPkg", "1.0.0");
        File.WriteAllText(Path.Combine(dir, "safe.all.yaml"), MinimalYaml("Safe"));

        var resolver = new SchemaPackageResolver([dir]);
        var result = resolver.Resolve("package:MyPkg/../../etc/passwd");

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_EmptyPackageImport_ReturnsNull()
    {
        var resolver = new SchemaPackageResolver([]);
        var result = resolver.Resolve("package:");

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_MultipleDirectories_MatchesCorrectOne()
    {
        var dir1 = CreatePackageSchemaDir("Pkg.A", "1.0.0");
        var dir2 = CreatePackageSchemaDir("Pkg.B", "1.0.0");
        File.WriteAllText(Path.Combine(dir1, "a.all.yaml"), MinimalYaml("A"));
        File.WriteAllText(Path.Combine(dir2, "b.all.yaml"), MinimalYaml("B"));

        var resolver = new SchemaPackageResolver([dir1, dir2]);

        var result = resolver.Resolve("package:Pkg.B/b.all.yaml");
        Assert.NotNull(result);
        Assert.Contains("b.all.yaml", result);
    }

    // ── PackageSchemaSource properties ───────────────────────────────

    [Fact]
    public void DiscoverSchemas_ReturnsCorrectSourceProperties()
    {
        var dir = CreatePackageSchemaDir("MyCompany.Events.Common", "1.0.0");
        var schemaPath = Path.Combine(dir, "common.all.yaml");
        File.WriteAllText(schemaPath, MinimalYaml("Common"));

        var resolver = new SchemaPackageResolver([dir]);
        var source = resolver.DiscoverSchemas().Single();

        Assert.Equal("common.all.yaml", source.SchemaFileName);
        Assert.Equal(Path.GetFullPath(schemaPath), Path.GetFullPath(source.FullPath));
        Assert.Equal(dir, source.SourceDirectory);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private string CreatePackageSchemaDir(string packageName, string version)
    {
        var dir = Path.Combine(_tempDir, packageName, version, "schemas");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string MinimalYaml(string name) => $$"""
        schema:
          name: "{{name}}"
          version: "1.0.0"
          namespace: "Test.{{name}}"
        """;
}
