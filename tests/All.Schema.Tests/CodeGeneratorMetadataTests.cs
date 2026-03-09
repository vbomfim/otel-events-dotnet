using All.Schema.CodeGen;
using All.Schema.Models;

namespace All.Schema.Tests;

/// <summary>
/// Tests for generated schema metadata class with version constant.
/// The code generator must emit a {SchemaName}Metadata class with SchemaVersion.
/// </summary>
public class CodeGeneratorMetadataTests
{
    private readonly CodeGenerator _generator = new();

    // ── Metadata class generation ───────────────────────────────────────

    [Fact]
    public void GenerateFromSchema_WithEvents_EmitsMetadataClass()
    {
        var doc = CreateSchema("1.2.0");

        var files = _generator.GenerateFromSchema(doc);
        var eventsFile = files.First(f => f.FileName.Contains("Events"));

        Assert.Contains("public static class TestServiceMetadata", eventsFile.Content);
    }

    [Fact]
    public void GenerateFromSchema_MetadataClass_ContainsSchemaVersionConstant()
    {
        var doc = CreateSchema("1.2.0");

        var files = _generator.GenerateFromSchema(doc);
        var eventsFile = files.First(f => f.FileName.Contains("Events"));

        Assert.Contains("public const string SchemaVersion = \"1.2.0\";", eventsFile.Content);
    }

    [Fact]
    public void GenerateFromSchema_MetadataVersion_MatchesSchemaHeader()
    {
        var doc = CreateSchema("3.5.1");

        var files = _generator.GenerateFromSchema(doc);
        var eventsFile = files.First(f => f.FileName.Contains("Events"));

        Assert.Contains("public const string SchemaVersion = \"3.5.1\";", eventsFile.Content);
    }

    [Fact]
    public void GenerateFromSchema_MetadataClass_HasXmlDocComment()
    {
        var doc = CreateSchema("1.0.0");

        var files = _generator.GenerateFromSchema(doc);
        var eventsFile = files.First(f => f.FileName.Contains("Events"));

        Assert.Contains("/// <summary>", eventsFile.Content);
        Assert.Contains("Metadata", eventsFile.Content);
    }

    [Fact]
    public void GenerateFromSchema_MetadataVersion_EscapesSpecialChars()
    {
        // Pre-release versions with special characters
        var doc = CreateSchema("1.0.0-alpha.1");

        var files = _generator.GenerateFromSchema(doc);
        var eventsFile = files.First(f => f.FileName.Contains("Events"));

        Assert.Contains("public const string SchemaVersion = \"1.0.0-alpha.1\";", eventsFile.Content);
    }

    [Fact]
    public void GenerateFromSchema_MetadataClass_ContainsSchemaName()
    {
        var doc = CreateSchema("1.0.0", name: "OrderService");

        var files = _generator.GenerateFromSchema(doc);
        var eventsFile = files.First(f => f.FileName.Contains("Events"));

        Assert.Contains("public const string SchemaName = \"OrderService\";", eventsFile.Content);
    }

    [Fact]
    public void GenerateFromSchema_EmptySchema_DoesNotEmitMetadata()
    {
        // No events = no events file = no metadata
        var doc = new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = "EmptyService",
                Version = "1.0.0",
                Namespace = "Test.Events"
            }
        };

        var files = _generator.GenerateFromSchema(doc);

        Assert.DoesNotContain(files, f => f.Content.Contains("Metadata"));
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static SchemaDocument CreateSchema(string version, string name = "TestService") => new()
    {
        Schema = new SchemaHeader
        {
            Name = name,
            Version = version,
            Namespace = "Test.Events"
        },
        Events =
        [
            new EventDefinition
            {
                Name = "test.event",
                Id = 1,
                Severity = Severity.Info,
                Message = "Test event"
            }
        ]
    };
}
