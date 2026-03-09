using OtelEvents.Schema.Models;
using OtelEvents.Schema.Packaging;
using OtelEvents.Schema.Parsing;
using OtelEvents.Schema.Validation;

namespace OtelEvents.Schema.Tests;

/// <summary>
/// Tests for cross-package schema resolution — verifying that SchemaMerger
/// resolves imports from NuGet packages via SchemaPackageResolver.
/// </summary>
public sealed class SchemaMergerPackageTests : IDisposable
{
    private readonly SchemaParser _parser = new();
    private readonly SchemaValidator _validator = new();
    private readonly string _tempDir;

    public SchemaMergerPackageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"schema-merge-pkg-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    // ── Package import resolution ────────────────────────────────────

    [Fact]
    public void MergeFromFile_WithPackageImport_ResolvesFromPackage()
    {
        // Arrange: simulate a NuGet package with a shared schema
        var pkgDir = CreatePackageDir("MyCompany.Events.Shared");
        WriteSchema(pkgDir, "shared.all.yaml", """
            schema:
              name: "Shared"
              version: "1.0.0"
              namespace: "MyCompany.Events.Shared"
            fields:
              correlationId:
                type: string
                description: "Correlation ID for request tracing"
            enums:
              Status:
                values: [Success, Failure, Pending]
            """);

        // Arrange: a consumer schema that imports from the package
        var consumerDir = Path.Combine(_tempDir, "consumer");
        Directory.CreateDirectory(consumerDir);
        WriteSchema(consumerDir, "service.all.yaml", """
            schema:
              name: "OrderService"
              version: "1.0.0"
              namespace: "MyCompany.OrderService"
            imports:
              - "package:MyCompany.Events.Shared/shared.all.yaml"
            events:
              order.placed:
                id: 1
                severity: INFO
                message: "Order placed with correlation {correlationId}"
                fields:
                  correlationId:
                    ref: correlationId
                    required: true
            """);

        var resolver = new SchemaPackageResolver([pkgDir]);
        var merger = new SchemaMerger(_parser, _validator, resolver);

        // Act
        var result = merger.MergeFromFile(Path.Combine(consumerDir, "service.all.yaml"));

        // Assert
        Assert.True(result.IsSuccess, $"Errors: {string.Join(", ", result.Errors)}");
        Assert.NotNull(result.Document);
        Assert.Single(result.Document.Events);
        Assert.Single(result.Document.Fields); // correlationId from shared package
        Assert.Single(result.Document.Enums);  // Status from shared package
    }

    [Fact]
    public void MergeFromFile_PackageImportNotFound_ReturnsError()
    {
        var consumerDir = Path.Combine(_tempDir, "consumer");
        Directory.CreateDirectory(consumerDir);
        WriteSchema(consumerDir, "service.all.yaml", """
            schema:
              name: "Service"
              version: "1.0.0"
              namespace: "Test.Service"
            imports:
              - "package:NonExistent.Package/shared.all.yaml"
            events:
              test.event:
                id: 1
                severity: INFO
                message: "Test"
            """);

        var resolver = new SchemaPackageResolver([]);
        var merger = new SchemaMerger(_parser, _validator, resolver);

        var result = merger.MergeFromFile(Path.Combine(consumerDir, "service.all.yaml"));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.PackageSchemaNotFound);
    }

    [Fact]
    public void MergeFromFile_MixedLocalAndPackageImports_ResolvesAll()
    {
        // Local shared schema
        var localDir = Path.Combine(_tempDir, "local");
        Directory.CreateDirectory(localDir);
        WriteSchema(localDir, "local-shared.all.yaml", """
            schema:
              name: "LocalShared"
              version: "1.0.0"
              namespace: "Test.LocalShared"
            fields:
              tenantId:
                type: string
                description: "Tenant identifier"
            """);

        // Package shared schema
        var pkgDir = CreatePackageDir("Pkg.Common");
        WriteSchema(pkgDir, "common.all.yaml", """
            schema:
              name: "Common"
              version: "1.0.0"
              namespace: "Pkg.Common"
            fields:
              requestId:
                type: string
                description: "Request identifier"
            """);

        // Consumer that imports both
        WriteSchema(localDir, "main.all.yaml", """
            schema:
              name: "MainService"
              version: "1.0.0"
              namespace: "Test.MainService"
            imports:
              - "local-shared.all.yaml"
              - "package:Pkg.Common/common.all.yaml"
            events:
              test.event:
                id: 1
                severity: INFO
                message: "Test with {tenantId} and {requestId}"
                fields:
                  tenantId:
                    ref: tenantId
                    required: true
                  requestId:
                    ref: requestId
                    required: true
            """);

        var resolver = new SchemaPackageResolver([pkgDir]);
        var merger = new SchemaMerger(_parser, _validator, resolver);

        var result = merger.MergeFromFile(Path.Combine(localDir, "main.all.yaml"));

        Assert.True(result.IsSuccess, $"Errors: {string.Join(", ", result.Errors)}");
        Assert.NotNull(result.Document);
        Assert.Single(result.Document.Events);
        Assert.Equal(2, result.Document.Fields.Count); // tenantId + requestId
    }

    [Fact]
    public void MergeFromFile_MultiplePackageImports_ResolvesAll()
    {
        var pkgDir1 = CreatePackageDir("Pkg.Auth");
        WriteSchema(pkgDir1, "auth.all.yaml", """
            schema:
              name: "Auth"
              version: "1.0.0"
              namespace: "Pkg.Auth"
            fields:
              userId:
                type: string
                description: "User ID"
            """);

        var pkgDir2 = CreatePackageDir("Pkg.Http");
        WriteSchema(pkgDir2, "http.all.yaml", """
            schema:
              name: "Http"
              version: "1.0.0"
              namespace: "Pkg.Http"
            fields:
              statusCode:
                type: int
                description: "HTTP status code"
            """);

        var consumerDir = Path.Combine(_tempDir, "consumer");
        Directory.CreateDirectory(consumerDir);
        WriteSchema(consumerDir, "api.all.yaml", """
            schema:
              name: "ApiService"
              version: "1.0.0"
              namespace: "Test.Api"
            imports:
              - "package:Pkg.Auth/auth.all.yaml"
              - "package:Pkg.Http/http.all.yaml"
            events:
              api.request:
                id: 1
                severity: INFO
                message: "API request by {userId} returned {statusCode}"
                fields:
                  userId:
                    ref: userId
                    required: true
                  statusCode:
                    ref: statusCode
                    required: true
            """);

        var resolver = new SchemaPackageResolver([pkgDir1, pkgDir2]);
        var merger = new SchemaMerger(_parser, _validator, resolver);

        var result = merger.MergeFromFile(Path.Combine(consumerDir, "api.all.yaml"));

        Assert.True(result.IsSuccess, $"Errors: {string.Join(", ", result.Errors)}");
        Assert.Equal(2, result.Document!.Fields.Count);
    }

    [Fact]
    public void MergeFromFile_PackageSchemaWithEvents_MergesEventsAcrossPackages()
    {
        var pkgDir = CreatePackageDir("Pkg.BaseEvents");
        WriteSchema(pkgDir, "base.all.yaml", """
            schema:
              name: "BaseEvents"
              version: "1.0.0"
              namespace: "Pkg.BaseEvents"
            events:
              base.startup:
                id: 9000
                severity: INFO
                message: "Service started"
            """);

        var consumerDir = Path.Combine(_tempDir, "consumer");
        Directory.CreateDirectory(consumerDir);
        WriteSchema(consumerDir, "service.all.yaml", """
            schema:
              name: "MyService"
              version: "1.0.0"
              namespace: "Test.MyService"
            imports:
              - "package:Pkg.BaseEvents/base.all.yaml"
            events:
              service.ready:
                id: 1
                severity: INFO
                message: "Service ready"
            """);

        var resolver = new SchemaPackageResolver([pkgDir]);
        var merger = new SchemaMerger(_parser, _validator, resolver);

        var result = merger.MergeFromFile(Path.Combine(consumerDir, "service.all.yaml"));

        Assert.True(result.IsSuccess, $"Errors: {string.Join(", ", result.Errors)}");
        Assert.Equal(2, result.Document!.Events.Count);
    }

    // ── Cross-package validation ─────────────────────────────────────

    [Fact]
    public void MergeFromFile_DuplicateEventIdAcrossPackages_ReportsError()
    {
        var pkgDir = CreatePackageDir("Pkg.Events");
        WriteSchema(pkgDir, "pkg.all.yaml", """
            schema:
              name: "PkgEvents"
              version: "1.0.0"
              namespace: "Pkg.Events"
            events:
              pkg.event:
                id: 1
                severity: INFO
                message: "Package event"
            """);

        var consumerDir = Path.Combine(_tempDir, "consumer");
        Directory.CreateDirectory(consumerDir);
        WriteSchema(consumerDir, "service.all.yaml", """
            schema:
              name: "Service"
              version: "1.0.0"
              namespace: "Test.Service"
            imports:
              - "package:Pkg.Events/pkg.all.yaml"
            events:
              service.event:
                id: 1
                severity: INFO
                message: "Conflicting ID"
            """);

        var resolver = new SchemaPackageResolver([pkgDir]);
        var merger = new SchemaMerger(_parser, _validator, resolver);

        var result = merger.MergeFromFile(Path.Combine(consumerDir, "service.all.yaml"));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.DuplicateEventId);
    }

    [Fact]
    public void MergeFromFile_DuplicateEventNameAcrossPackages_ReportsError()
    {
        var pkgDir = CreatePackageDir("Pkg.Events");
        WriteSchema(pkgDir, "pkg.all.yaml", """
            schema:
              name: "PkgEvents"
              version: "1.0.0"
              namespace: "Pkg.Events"
            events:
              test.event:
                id: 100
                severity: INFO
                message: "Package event"
            """);

        var consumerDir = Path.Combine(_tempDir, "consumer");
        Directory.CreateDirectory(consumerDir);
        WriteSchema(consumerDir, "service.all.yaml", """
            schema:
              name: "Service"
              version: "1.0.0"
              namespace: "Test.Service"
            imports:
              - "package:Pkg.Events/pkg.all.yaml"
            events:
              test.event:
                id: 200
                severity: INFO
                message: "Duplicate name"
            """);

        var resolver = new SchemaPackageResolver([pkgDir]);
        var merger = new SchemaMerger(_parser, _validator, resolver);

        var result = merger.MergeFromFile(Path.Combine(consumerDir, "service.all.yaml"));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.DuplicateEventName);
    }

    [Fact]
    public void MergeFromFile_IncompatibleVersionAcrossPackages_ReportsError()
    {
        var pkgDir = CreatePackageDir("Pkg.V2");
        WriteSchema(pkgDir, "v2.all.yaml", """
            schema:
              name: "V2Package"
              version: "2.0.0"
              namespace: "Pkg.V2"
            fields:
              data:
                type: string
            """);

        var consumerDir = Path.Combine(_tempDir, "consumer");
        Directory.CreateDirectory(consumerDir);
        WriteSchema(consumerDir, "service.all.yaml", """
            schema:
              name: "Service"
              version: "1.0.0"
              namespace: "Test.Service"
            imports:
              - "package:Pkg.V2/v2.all.yaml"
            events:
              test.event:
                id: 1
                severity: INFO
                message: "Test"
            """);

        var resolver = new SchemaPackageResolver([pkgDir]);
        var merger = new SchemaMerger(_parser, _validator, resolver);

        var result = merger.MergeFromFile(Path.Combine(consumerDir, "service.all.yaml"));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.IncompatibleSchemaVersion);
    }

    // ── Backward compatibility ───────────────────────────────────────

    [Fact]
    public void MergeFromFile_WithoutResolver_LocalImportsStillWork()
    {
        // Original SchemaMerger (no resolver) should continue to work
        var merger = new SchemaMerger(_parser, _validator);

        var dir = Path.Combine(_tempDir, "local-only");
        Directory.CreateDirectory(dir);
        WriteSchema(dir, "shared.all.yaml", """
            schema:
              name: "Shared"
              version: "1.0.0"
              namespace: "Test.Shared"
            fields:
              userId:
                type: string
            """);

        WriteSchema(dir, "main.all.yaml", """
            schema:
              name: "Main"
              version: "1.0.0"
              namespace: "Test.Main"
            imports:
              - "shared.all.yaml"
            events:
              user.login:
                id: 1
                severity: INFO
                message: "User {userId} logged in"
                fields:
                  userId:
                    ref: userId
                    required: true
            """);

        var result = merger.MergeFromFile(Path.Combine(dir, "main.all.yaml"));

        Assert.True(result.IsSuccess, $"Errors: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void MergeFromFile_WithoutResolver_PackageImportReturnsError()
    {
        // Without a resolver, package imports should fail gracefully
        var merger = new SchemaMerger(_parser, _validator);

        var dir = Path.Combine(_tempDir, "no-resolver");
        Directory.CreateDirectory(dir);
        WriteSchema(dir, "main.all.yaml", """
            schema:
              name: "Main"
              version: "1.0.0"
              namespace: "Test.Main"
            imports:
              - "package:SomePkg/schema.all.yaml"
            events:
              test.event:
                id: 1
                severity: INFO
                message: "Test"
            """);

        var result = merger.MergeFromFile(Path.Combine(dir, "main.all.yaml"));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.PackageSchemaNotFound);
    }

    [Fact]
    public void Merge_InMemoryDocuments_StillWorks()
    {
        // The Merge(documents) overload should remain unchanged
        var merger = new SchemaMerger(_parser, _validator);

        var doc = new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = "Test",
                Version = "1.0.0",
                Namespace = "Test.Ns"
            },
            Events =
            [
                new EventDefinition
                {
                    Name = "test.event",
                    Id = 1,
                    Severity = Severity.Info,
                    Message = "Test"
                }
            ]
        };

        var result = merger.Merge([doc]);

        Assert.True(result.IsSuccess, $"Errors: {string.Join(", ", result.Errors)}");
    }

    // ── Circular imports with packages ───────────────────────────────

    [Fact]
    public void MergeFromFile_PackageImportedTwice_DeduplicatesViaVisited()
    {
        var pkgDir = CreatePackageDir("Pkg.Shared");
        WriteSchema(pkgDir, "shared.all.yaml", """
            schema:
              name: "Shared"
              version: "1.0.0"
              namespace: "Pkg.Shared"
            fields:
              commonField:
                type: string
            """);

        var consumerDir = Path.Combine(_tempDir, "consumer");
        Directory.CreateDirectory(consumerDir);

        // Two local schemas both import the same package schema
        WriteSchema(consumerDir, "a.all.yaml", """
            schema:
              name: "A"
              version: "1.0.0"
              namespace: "Test.A"
            imports:
              - "package:Pkg.Shared/shared.all.yaml"
            """);

        WriteSchema(consumerDir, "main.all.yaml", """
            schema:
              name: "Main"
              version: "1.0.0"
              namespace: "Test.Main"
            imports:
              - "a.all.yaml"
              - "package:Pkg.Shared/shared.all.yaml"
            events:
              test.event:
                id: 1
                severity: INFO
                message: "Test with {commonField}"
                fields:
                  commonField:
                    ref: commonField
                    required: true
            """);

        var resolver = new SchemaPackageResolver([pkgDir]);
        var merger = new SchemaMerger(_parser, _validator, resolver);

        var result = merger.MergeFromFile(Path.Combine(consumerDir, "main.all.yaml"));

        Assert.True(result.IsSuccess, $"Errors: {string.Join(", ", result.Errors)}");
        // Should only have ONE commonField (deduplication via visited set)
        Assert.Single(result.Document!.Fields);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private string CreatePackageDir(string packageName)
    {
        var dir = Path.Combine(_tempDir, "packages", packageName, "1.0.0", "schemas");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void WriteSchema(string dir, string fileName, string yaml)
    {
        File.WriteAllText(Path.Combine(dir, fileName), yaml);
    }
}
