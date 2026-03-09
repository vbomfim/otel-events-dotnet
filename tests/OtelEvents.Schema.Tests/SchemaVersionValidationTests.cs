using OtelEvents.Schema.Models;
using OtelEvents.Schema.Parsing;
using OtelEvents.Schema.Validation;

namespace OtelEvents.Schema.Tests;

/// <summary>
/// Tests for schema version compatibility validation (ALL_SCHEMA_025).
/// When merging multiple schemas, all must share the same major version.
/// </summary>
public class SchemaVersionValidationTests
{
    private readonly SchemaValidator _validator = new();

    // ── ALL_SCHEMA_025: Schema version compatibility ────────────────────

    [Fact]
    public void Validate_SameMajorVersion_NoError()
    {
        var doc1 = CreateDoc("1.0.0");
        var doc2 = CreateDoc("1.2.0");
        var doc3 = CreateDoc("1.5.3");

        var result = _validator.Validate([doc1, doc2, doc3]);

        Assert.DoesNotContain(result.Errors, e => e.Code == ErrorCodes.IncompatibleSchemaVersion);
    }

    [Fact]
    public void Validate_DifferentMajorVersions_ReturnsALL_SCHEMA_025()
    {
        var doc1 = CreateDoc("1.0.0");
        var doc2 = CreateDoc("2.0.0");

        var result = _validator.Validate([doc1, doc2]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.IncompatibleSchemaVersion);
    }

    [Fact]
    public void Validate_DifferentMinorVersions_NoError()
    {
        var doc1 = CreateDoc("2.0.0");
        var doc2 = CreateDoc("2.1.0");

        var result = _validator.Validate([doc1, doc2]);

        Assert.DoesNotContain(result.Errors, e => e.Code == ErrorCodes.IncompatibleSchemaVersion);
    }

    [Fact]
    public void Validate_DifferentPatchVersions_NoError()
    {
        var doc1 = CreateDoc("1.0.0");
        var doc2 = CreateDoc("1.0.5");

        var result = _validator.Validate([doc1, doc2]);

        Assert.DoesNotContain(result.Errors, e => e.Code == ErrorCodes.IncompatibleSchemaVersion);
    }

    [Fact]
    public void Validate_SingleDocument_NoVersionCompatibilityError()
    {
        var doc = CreateDoc("3.0.0");

        var result = _validator.Validate([doc]);

        Assert.DoesNotContain(result.Errors, e => e.Code == ErrorCodes.IncompatibleSchemaVersion);
    }

    [Fact]
    public void Validate_ThreeDocsWithMixedMajorVersions_ReportsError()
    {
        var doc1 = CreateDoc("1.0.0");
        var doc2 = CreateDoc("1.1.0");
        var doc3 = CreateDoc("2.0.0");

        var result = _validator.Validate([doc1, doc2, doc3]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.IncompatibleSchemaVersion);
    }

    [Fact]
    public void Validate_PreReleaseVersions_SameMajor_NoError()
    {
        var doc1 = CreateDoc("1.0.0-alpha");
        var doc2 = CreateDoc("1.0.0-beta");

        var result = _validator.Validate([doc1, doc2]);

        Assert.DoesNotContain(result.Errors, e => e.Code == ErrorCodes.IncompatibleSchemaVersion);
    }

    [Fact]
    public void Validate_VersionErrorMessage_IncludesVersionInfo()
    {
        var doc1 = CreateDoc("1.0.0");
        var doc2 = CreateDoc("2.0.0");

        var result = _validator.Validate([doc1, doc2]);

        var error = result.Errors.First(e => e.Code == ErrorCodes.IncompatibleSchemaVersion);
        Assert.Contains("2.0.0", error.Message);
    }

    // ── Merger integration ──────────────────────────────────────────────

    [Fact]
    public void Merge_IncompatibleVersions_ReportsError()
    {
        var parser = new SchemaParser();
        var merger = new SchemaMerger(parser, _validator);

        var doc1 = CreateDoc("1.0.0");
        var doc2 = CreateDoc("2.0.0");

        var result = merger.Merge([doc1, doc2]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.IncompatibleSchemaVersion);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static SchemaDocument CreateDoc(string version) => new()
    {
        Schema = new SchemaHeader
        {
            Name = "TestService",
            Version = version,
            Namespace = "Test.Namespace"
        }
    };
}
