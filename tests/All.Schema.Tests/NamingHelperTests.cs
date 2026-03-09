using All.Schema.CodeGen;

namespace All.Schema.Tests;

/// <summary>
/// Tests for NamingHelper — validates name conversion from YAML to C# identifiers.
/// </summary>
public class NamingHelperTests
{
    // ── PascalCase ─────────────────────────────────────────────────

    [Theory]
    [InlineData("order.placed", "OrderPlaced")]
    [InlineData("http.request.completed", "HttpRequestCompleted")]
    [InlineData("simple", "Simple")]
    [InlineData("order_status", "OrderStatus")]
    [InlineData("http-method", "HttpMethod")]
    [InlineData("orderId", "OrderId")]
    [InlineData("already.Pascal.Case", "AlreadyPascalCase")]
    public void ToPascalCase_ConvertsCorrectly(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.ToPascalCase(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ToPascalCase_EmptyOrNull_ReturnsAsIs(string? input)
    {
        Assert.Equal(input, NamingHelper.ToPascalCase(input!));
    }

    // ── camelCase ──────────────────────────────────────────────────

    [Theory]
    [InlineData("order.placed", "orderPlaced")]
    [InlineData("OrderId", "orderId")]
    [InlineData("userId", "userId")]
    [InlineData("http_method", "httpMethod")]
    public void ToCamelCase_ConvertsCorrectly(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.ToCamelCase(input));
    }

    // ── Method Name ────────────────────────────────────────────────

    [Theory]
    [InlineData("order.placed", "OrderPlaced")]
    [InlineData("http.request.completed", "HttpRequestCompleted")]
    public void ToMethodName_ConvertsEventNameToMethodName(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.ToMethodName(input));
    }

    // ── Metric Field Name ──────────────────────────────────────────

    [Theory]
    [InlineData("order.placed.count", "s_orderPlacedCount")]
    [InlineData("http.request.duration", "s_httpRequestDuration")]
    public void ToMetricFieldName_GeneratesPrivateStaticName(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.ToMetricFieldName(input));
    }

    // ── GetLastSegment ─────────────────────────────────────────────

    [Theory]
    [InlineData("order.placed.amount", "amount")]
    [InlineData("count", "count")]
    [InlineData("a.b.c", "c")]
    public void GetLastSegment_ReturnsLastPart(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.GetLastSegment(input));
    }

    // ── SanitizeIdentifier ─────────────────────────────────────────

    [Theory]
    [InlineData("validName", "validName")]
    [InlineData("123invalid", "_23invalid")]
    [InlineData("has spaces", "has_spaces")]
    [InlineData("", "_")]
    public void SanitizeIdentifier_MakesValidCSharpIdentifier(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.SanitizeIdentifier(input));
    }
}
