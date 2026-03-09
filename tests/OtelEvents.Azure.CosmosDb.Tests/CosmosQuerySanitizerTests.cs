namespace OtelEvents.Azure.CosmosDb.Tests;

/// <summary>
/// Tests for <see cref="CosmosQuerySanitizer"/> — replaces string literals
/// with ? placeholders to prevent PII leakage in logged query text.
/// </summary>
public sealed class CosmosQuerySanitizerTests
{
    // ─── String literal replacement ─────────────────────────────────

    [Fact]
    public void Sanitize_ReplacesSimpleStringLiteral_WithPlaceholder()
    {
        var query = "SELECT * FROM c WHERE c.name = 'John'";
        var result = CosmosQuerySanitizer.Sanitize(query);
        Assert.Equal("SELECT * FROM c WHERE c.name = ?", result);
    }

    [Fact]
    public void Sanitize_ReplacesMultipleStringLiterals()
    {
        var query = "SELECT * FROM c WHERE c.name = 'John' AND c.city = 'Seattle'";
        var result = CosmosQuerySanitizer.Sanitize(query);
        Assert.Equal("SELECT * FROM c WHERE c.name = ? AND c.city = ?", result);
    }

    [Fact]
    public void Sanitize_ReplacesStringWithEscapedQuotes()
    {
        var query = @"SELECT * FROM c WHERE c.name = 'O\'Brien'";
        var result = CosmosQuerySanitizer.Sanitize(query);
        Assert.Equal("SELECT * FROM c WHERE c.name = ?", result);
    }

    [Fact]
    public void Sanitize_ReplacesEmptyStringLiteral()
    {
        var query = "SELECT * FROM c WHERE c.name = ''";
        var result = CosmosQuerySanitizer.Sanitize(query);
        Assert.Equal("SELECT * FROM c WHERE c.name = ?", result);
    }

    [Fact]
    public void Sanitize_ReplacesStringWithSpaces()
    {
        var query = "SELECT * FROM c WHERE c.address = '123 Main Street'";
        var result = CosmosQuerySanitizer.Sanitize(query);
        Assert.Equal("SELECT * FROM c WHERE c.address = ?", result);
    }

    // ─── Preserves non-literal content ──────────────────────────────

    [Fact]
    public void Sanitize_PreservesQueryWithNoLiterals()
    {
        var query = "SELECT * FROM c WHERE c.isActive = true";
        var result = CosmosQuerySanitizer.Sanitize(query);
        Assert.Equal("SELECT * FROM c WHERE c.isActive = true", result);
    }

    [Fact]
    public void Sanitize_PreservesFunctionCalls()
    {
        var query = "SELECT VALUE COUNT(1) FROM c WHERE c.status = 'active'";
        var result = CosmosQuerySanitizer.Sanitize(query);
        Assert.Equal("SELECT VALUE COUNT(1) FROM c WHERE c.status = ?", result);
    }

    [Fact]
    public void Sanitize_PreservesFieldNames()
    {
        var query = "SELECT c.firstName, c.lastName FROM c WHERE c.email = 'user@test.com'";
        var result = CosmosQuerySanitizer.Sanitize(query);
        Assert.Equal("SELECT c.firstName, c.lastName FROM c WHERE c.email = ?", result);
    }

    // ─── Null and empty handling ────────────────────────────────────

    [Fact]
    public void Sanitize_NullInput_ReturnsEmpty()
    {
        var result = CosmosQuerySanitizer.Sanitize(null);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Sanitize_EmptyInput_ReturnsEmpty()
    {
        var result = CosmosQuerySanitizer.Sanitize(string.Empty);
        Assert.Equal(string.Empty, result);
    }

    // ─── Max length truncation ──────────────────────────────────────

    [Fact]
    public void Sanitize_TruncatesLongQuery_ToMaxLength()
    {
        var query = new string('A', 3000);
        var result = CosmosQuerySanitizer.Sanitize(query);
        Assert.Equal(CosmosQuerySanitizer.DefaultMaxLength, result.Length);
    }

    [Fact]
    public void Sanitize_CustomMaxLength_Respected()
    {
        var query = new string('A', 200);
        var result = CosmosQuerySanitizer.Sanitize(query, maxLength: 100);
        Assert.Equal(100, result.Length);
    }

    [Fact]
    public void Sanitize_ShortQuery_NotTruncated()
    {
        var query = "SELECT * FROM c";
        var result = CosmosQuerySanitizer.Sanitize(query);
        Assert.Equal("SELECT * FROM c", result);
    }

    // ─── Complex queries ────────────────────────────────────────────

    [Fact]
    public void Sanitize_InClauseWithMultipleStrings()
    {
        var query = "SELECT * FROM c WHERE c.status IN ('active', 'pending', 'review')";
        var result = CosmosQuerySanitizer.Sanitize(query);
        Assert.Equal("SELECT * FROM c WHERE c.status IN (?, ?, ?)", result);
    }

    [Fact]
    public void Sanitize_JoinWithStringLiterals()
    {
        var query = "SELECT * FROM c JOIN d IN c.items WHERE d.type = 'premium' AND c.region = 'US'";
        var result = CosmosQuerySanitizer.Sanitize(query);
        Assert.Equal("SELECT * FROM c JOIN d IN c.items WHERE d.type = ? AND c.region = ?", result);
    }

    [Fact]
    public void Sanitize_ParameterizedQuery_NoChange()
    {
        var query = "SELECT * FROM c WHERE c.id = @id AND c.pk = @pk";
        var result = CosmosQuerySanitizer.Sanitize(query);
        Assert.Equal("SELECT * FROM c WHERE c.id = @id AND c.pk = @pk", result);
    }
}
