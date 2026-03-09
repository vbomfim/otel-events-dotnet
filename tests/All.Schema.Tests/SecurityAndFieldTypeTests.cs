using All.Schema.Models;
using All.Schema.Parsing;
using All.Schema.Validation;

namespace All.Schema.Tests;

/// <summary>
/// Security and edge-case tests for YAML alias/anchor rejection, tag directives,
/// duplicate enum values, and array/map field types.
/// </summary>
public class SecurityAndFieldTypeTests
{
    private readonly SchemaParser _parser = new();
    private readonly SchemaValidator _validator = new();

    // ── YAML Alias/Anchor Rejection (HIGH severity) ─────────────────────

    [Fact]
    public void Parse_YamlWithAliasAndAnchor_RejectsOrIgnores()
    {
        // YAML bomb pattern: anchor (&) defines a value, alias (*) references it
        var yaml = """
            anchor_value: &anchor
              - "lol"
              - "lol"
            alias_value: *anchor
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.YamlAliasRejected);
    }

    [Fact]
    public void Parse_YamlWithAnchorOnly_Rejects()
    {
        // Even anchors alone should be rejected (they signal intent to use aliases)
        var yaml = """
            schema: &schema_anchor
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.YamlAliasRejected);
    }

    [Fact]
    public void Parse_YamlBomb_Rejects()
    {
        // Classic YAML bomb — exponential expansion through nested aliases
        var yaml = """
            a: &a ["lol","lol","lol","lol","lol","lol","lol","lol","lol"]
            b: &b [*a,*a,*a,*a,*a,*a,*a,*a,*a]
            c: &c [*b,*b,*b,*b,*b,*b,*b,*b,*b]
            schema:
              name: "Bomb"
              version: "1.0.0"
              namespace: "Test.Bomb"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.YamlAliasRejected);
    }

    [Fact]
    public void Parse_ValidYamlWithoutAliases_Succeeds()
    {
        // Confirm that valid YAML without anchors/aliases still works
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              test.event:
                id: 1
                severity: INFO
                message: "Test event"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Document);
    }

    // ── YAML Tag Directive ──────────────────────────────────────────────

    [Fact]
    public void Parse_YamlWithTagDirective_RejectsOrIgnores()
    {
        // YAML tag directives should not affect parsing behavior
        // The parser should either reject or safely ignore them
        var yaml = """
            %TAG !custom! tag:example.com,2025:
            ---
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        // Tag directive is safely handled — either parsed successfully
        // (tags ignored since we only look at scalar values) or rejected
        if (result.IsSuccess)
        {
            // Tags were ignored — parser still extracted correct values
            Assert.Equal("TestService", result.Document!.Schema.Name);
        }
        else
        {
            // Tags were rejected — also acceptable
            Assert.NotEmpty(result.Errors);
        }
    }

    [Fact]
    public void Parse_YamlWithNodeTag_IgnoresTag()
    {
        // Node-level tags (!!str, !!int, etc.) should be ignored
        var yaml = """
            schema:
              name: !!str "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        // YamlDotNet parses the scalar value regardless of tag
        Assert.True(result.IsSuccess);
        Assert.Equal("TestService", result.Document!.Schema.Name);
    }

    // ── Duplicate Enum Values ───────────────────────────────────────────

    [Fact]
    public void Validate_DuplicateEnumValues_ReturnsError()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            enums:
              HttpMethod:
                description: "HTTP methods"
                values:
                  - GET
                  - POST
                  - GET
                  - DELETE
            """;

        var parseResult = _parser.Parse(yaml, yaml.Length);
        Assert.True(parseResult.IsSuccess);

        var result = _validator.Validate(parseResult.Document!);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Code == ErrorCodes.DuplicateEnumValue &&
            e.Message.Contains("GET"));
    }

    [Fact]
    public void Validate_UniqueEnumValues_NoError()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            enums:
              HttpMethod:
                description: "HTTP methods"
                values:
                  - GET
                  - POST
                  - PUT
                  - DELETE
            """;

        var parseResult = _parser.Parse(yaml, yaml.Length);
        Assert.True(parseResult.IsSuccess);

        var result = _validator.Validate(parseResult.Document!);

        Assert.DoesNotContain(result.Errors, e => e.Code == ErrorCodes.DuplicateEnumValue);
    }

    // ── Array Field Types ───────────────────────────────────────────────

    [Fact]
    public void Parse_ArrayFieldType_ParsesCorrectly()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              test.tags:
                id: 1
                severity: INFO
                message: "Tagged event with {tags} and {scores}"
                fields:
                  tags:
                    type: "string[]"
                    description: "Tags for categorization"
                  scores:
                    type: "int[]"
                    description: "Numeric scores"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        var fields = result.Document!.Events[0].Fields;

        var tagsField = fields.First(f => f.Name == "tags");
        Assert.Equal(FieldType.StringArray, tagsField.Type);

        var scoresField = fields.First(f => f.Name == "scores");
        Assert.Equal(FieldType.IntArray, scoresField.Type);
    }

    [Fact]
    public void Parse_ArrayFieldTypeShorthand_ParsesCorrectly()
    {
        // Test the shorthand syntax (scalar value as type directly)
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            fields:
              tags: "string[]"
              ids: "int[]"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        var fields = result.Document!.Fields;

        Assert.Equal(FieldType.StringArray, fields.First(f => f.Name == "tags").Type);
        Assert.Equal(FieldType.IntArray, fields.First(f => f.Name == "ids").Type);
    }

    // ── Map Field Type ──────────────────────────────────────────────────

    [Fact]
    public void Parse_MapFieldType_ParsesCorrectly()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            events:
              test.metadata:
                id: 1
                severity: INFO
                message: "Event with {metadata}"
                fields:
                  metadata:
                    type: map
                    description: "Key-value metadata"
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        var field = result.Document!.Events[0].Fields.First(f => f.Name == "metadata");
        Assert.Equal(FieldType.Map, field.Type);
    }

    [Fact]
    public void Parse_MapFieldTypeShorthand_ParsesCorrectly()
    {
        var yaml = """
            schema:
              name: "TestService"
              version: "1.0.0"
              namespace: "Test.Namespace"
            fields:
              headers: map
            """;

        var result = _parser.Parse(yaml, yaml.Length);

        Assert.True(result.IsSuccess);
        Assert.Equal(FieldType.Map, result.Document!.Fields.First(f => f.Name == "headers").Type);
    }
}
