using All.Schema.Models;
using All.Schema.Parsing;
using All.Schema.Validation;

namespace All.Schema.Tests;

/// <summary>
/// Tests for schema merging — combining multiple schema files into one.
/// </summary>
public class SchemaMergerTests
{
    private readonly SchemaParser _parser = new();
    private readonly SchemaValidator _validator = new();
    private readonly SchemaMerger _merger;

    public SchemaMergerTests()
    {
        _merger = new SchemaMerger(_parser, _validator);
    }

    [Fact]
    public void Merge_SingleDocument_ReturnsMergedDocument()
    {
        var doc = new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = "TestService",
                Version = "1.0.0",
                Namespace = "Test.Namespace"
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

        var result = _merger.Merge([doc]);

        Assert.True(result.IsSuccess, $"Errors: {string.Join(", ", result.Errors)}");
        Assert.NotNull(result.Document);
        Assert.Single(result.Document.Events);
    }

    [Fact]
    public void Merge_MultipleDocuments_CombinesEvents()
    {
        var doc1 = new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = "Service1",
                Version = "1.0.0",
                Namespace = "Test.Service1"
            },
            Events =
            [
                new EventDefinition
                {
                    Name = "test.event1",
                    Id = 1,
                    Severity = Severity.Info,
                    Message = "Test 1"
                }
            ]
        };

        var doc2 = new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = "Service2",
                Version = "1.0.0",
                Namespace = "Test.Service2"
            },
            Events =
            [
                new EventDefinition
                {
                    Name = "test.event2",
                    Id = 2,
                    Severity = Severity.Debug,
                    Message = "Test 2"
                }
            ]
        };

        var result = _merger.Merge([doc1, doc2]);

        Assert.True(result.IsSuccess, $"Errors: {string.Join(", ", result.Errors)}");
        Assert.Equal(2, result.Document!.Events.Count);
    }

    [Fact]
    public void Merge_MultipleDocuments_CombinesFieldsAndEnums()
    {
        var doc1 = new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = "Service1",
                Version = "1.0.0",
                Namespace = "Test.Service1"
            },
            Fields =
            [
                new FieldDefinition { Name = "userId", Type = FieldType.String }
            ],
            Enums =
            [
                new EnumDefinition { Name = "Status", Values = ["Active", "Inactive"] }
            ]
        };

        var doc2 = new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = "Service2",
                Version = "1.0.0",
                Namespace = "Test.Service2"
            },
            Fields =
            [
                new FieldDefinition { Name = "orderId", Type = FieldType.String }
            ]
        };

        var result = _merger.Merge([doc1, doc2]);

        Assert.True(result.IsSuccess, $"Errors: {string.Join(", ", result.Errors)}");
        Assert.Equal(2, result.Document!.Fields.Count);
        Assert.Single(result.Document.Enums);
    }

    [Fact]
    public void Merge_DuplicateEventNames_ReportsValidationError()
    {
        var doc1 = new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = "Service1",
                Version = "1.0.0",
                Namespace = "Test.Service1"
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

        var doc2 = new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = "Service2",
                Version = "1.0.0",
                Namespace = "Test.Service2"
            },
            Events =
            [
                new EventDefinition
                {
                    Name = "test.event",
                    Id = 2,
                    Severity = Severity.Debug,
                    Message = "Test"
                }
            ]
        };

        var result = _merger.Merge([doc1, doc2]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.DuplicateEventName);
    }

    [Fact]
    public void Merge_EmptyDocumentList_ReturnsError()
    {
        var result = _merger.Merge([]);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void MergeFromFile_ValidFile_ParsesAndReturns()
    {
        // Create a temp file with valid YAML
        var tempDir = Path.Combine(Path.GetTempPath(), $"schema-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, "test.all.yaml");
            File.WriteAllText(filePath, """
                schema:
                  name: "TestService"
                  version: "1.0.0"
                  namespace: "Test.Namespace"
                events:
                  test.event:
                    id: 1
                    severity: INFO
                    message: "Test"
                """);

            var result = _merger.MergeFromFile(filePath);

            Assert.NotNull(result.Document);
            Assert.Single(result.Document.Events);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void MergeFromFile_WithImports_ResolvesImportedSchemas()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"schema-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var mainPath = Path.Combine(tempDir, "main.all.yaml");
            var importedPath = Path.Combine(tempDir, "shared.all.yaml");

            File.WriteAllText(importedPath, """
                schema:
                  name: "Shared"
                  version: "1.0.0"
                  namespace: "Test.Shared"
                fields:
                  userId:
                    type: string
                    description: "User identifier"
                """);

            File.WriteAllText(mainPath, """
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

            var result = _merger.MergeFromFile(mainPath);

            Assert.NotNull(result.Document);
            Assert.Single(result.Document.Events);
            Assert.Single(result.Document.Fields);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void MergeFromFile_CircularImport_DoesNotInfiniteLoop()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"schema-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var fileA = Path.Combine(tempDir, "a.all.yaml");
            var fileB = Path.Combine(tempDir, "b.all.yaml");

            File.WriteAllText(fileA, """
                schema:
                  name: "A"
                  version: "1.0.0"
                  namespace: "Test.A"
                imports:
                  - "b.all.yaml"
                events:
                  test.a:
                    id: 1
                    severity: INFO
                    message: "From A"
                """);

            File.WriteAllText(fileB, """
                schema:
                  name: "B"
                  version: "1.0.0"
                  namespace: "Test.B"
                imports:
                  - "a.all.yaml"
                events:
                  test.b:
                    id: 2
                    severity: INFO
                    message: "From B"
                """);

            // Should not hang — circular imports are detected via visited set
            var result = _merger.MergeFromFile(fileA);

            Assert.NotNull(result.Document);
            Assert.Equal(2, result.Document.Events.Count);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void MergeFromFile_FileNotFound_ReturnsError()
    {
        var result = _merger.MergeFromFile("/nonexistent/path/test.all.yaml");

        Assert.False(result.IsSuccess);
    }
}
